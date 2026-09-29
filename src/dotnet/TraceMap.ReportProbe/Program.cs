using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using TraceMap.Reporting;

// Replay bounded report composition without altering the retained run. The
// optional writer replay creates and removes a private scratch folder in TEMP.
// Never print input, exception messages, symbols, paths, or stack traces.
if (args.Length is not (2 or 3) || args.Length == 3 && args[2] != "--writer")
{
    Console.WriteLine("probe=invalid-arguments");
    return 2;
}

var stage = "input";
try
{
    var configPath = Path.GetFullPath(args[0]);
    var reportPath = Path.GetFullPath(args[1]);
    var indexPath = Path.Combine(reportPath, "combined.sqlite");
    var selectionPath = Path.Combine(reportPath, "selected-pages.local.txt");
    if (File.GetAttributes(configPath).HasFlag(FileAttributes.ReparsePoint) ||
        new FileInfo(configPath).Length is < 1 or > 4_194_304 ||
        !File.Exists(indexPath) || File.GetAttributes(indexPath).HasFlag(FileAttributes.ReparsePoint))
        throw new InvalidDataException("PROBE_INPUT_INVALID");
    using var config = JsonDocument.Parse(File.ReadAllBytes(configPath));
    var root = config.RootElement;
    var budgets = root.GetProperty("budgets");
    var reports = budgets.TryGetProperty("reports", out var reportBudgets) && reportBudgets.ValueKind == JsonValueKind.Object
        ? reportBudgets : default;
    var selected = root.GetProperty("pageMode").GetString() == "selected";
    if (selected && (!File.Exists(selectionPath) || File.GetAttributes(selectionPath).HasFlag(FileAttributes.ReparsePoint)))
        throw new InvalidDataException("PROBE_SELECTION_INVALID");
    if (selected && new FileInfo(selectionPath).Length > 4_194_304)
        throw new InvalidDataException("PROBE_SELECTION_LIMIT");
    var indexLimit = Long(budgets, "maxRetainedArtifactBytes", 17_179_869_184);
    if (new FileInfo(indexPath).Length > indexLimit) throw new InvalidDataException("PROBE_INDEX_LIMIT");
    var maxWork = checked((int)Long(budgets, "graphMaxWork", 2_000_000));
    var maxDepth = Int(budgets, "graphMaxDepth", 20);
    var maxPaths = Int(budgets, "graphMaxPaths", 256);
    var maxFacts = Int(reports, "maxInputFacts", 250_000);
    var maxEdges = Int(reports, "maxInputEdges", 250_000);
    var maxText = Int(reports, "maxInputTextBytes", 128 * 1024 * 1024);
    var maxFrontier = Int(reports, "maxFrontier", 10_000);
    var maxGraphStorage = Long(reports, "maxGraphStorageBytes", 512L * 1024 * 1024);

    stage = "packet";
    var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(indexPath, reportPath,
        MaxSurfaces: Int(reports, "maxSurfaces", 1_000),
        MaxEventChains: Int(reports, "maxEventChains", 1_000),
        MaxGaps: Int(reports, "maxGaps", 10_000),
        MaxDepth: maxDepth, MaxPaths: maxPaths,
        MaxInputFacts: maxFacts, MaxInputEdges: maxEdges, MaxInputTextBytes: maxText,
        SurfaceListPath: selected ? selectionPath : null,
        MaxTraversalWork: maxWork, MaxFrontier: maxFrontier)
        { MaxGraphStorageBytes = maxGraphStorage, LiteralSurfaceListPaths = true });
    Console.WriteLine("probe.packet=passed");

    stage = "retained-source";
    await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
    { DataSource = indexPath, Mode = SqliteOpenMode.ReadOnly, Cache = SqliteCacheMode.Private }.ToString());
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "select source_index_id, scan_id, commit_sha from index_sources where label = 'retained'";
    await using var reader = await command.ExecuteReaderAsync();
    if (!await reader.ReadAsync()) throw new InvalidDataException("PROBE_RETAINED_SOURCE_UNAVAILABLE");
    var sourceIndexId = reader.GetString(0);
    var scanId = reader.GetString(1);
    var commitSha = reader.GetString(2);
    if (await reader.ReadAsync()) throw new InvalidDataException("PROBE_RETAINED_SOURCE_AMBIGUOUS");

    stage = "paths";
    var allRoots = packet.EventChains.Where(chain => !string.IsNullOrWhiteSpace(chain.HandlerSymbol))
        .Select(chain => new CombinedPathSymbolRoot(sourceIndexId, scanId, commitSha, chain.HandlerSymbol!))
        .Distinct().OrderBy(root => root.SymbolId, StringComparer.Ordinal).ToArray();
    var roots = allRoots.Take(Int(reports, "maxCompiledRoots", 1_000)).ToArray();
    var paths = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(new(indexPath, reportPath,
        ToSurface: "database-api", IncludeLegacyRoots: true, MaxDepth: maxDepth,
        MaxPaths: maxPaths, MaxFrontier: maxFrontier) { MaxTraversalWork = maxWork },
        roots, combinedIndex: true, new(maxFacts, maxEdges, maxText)
        { MaxGraphStorageBytes = maxGraphStorage });
    Console.WriteLine("probe.paths=passed");

    stage = "index-hash";
    await using var file = File.OpenRead(indexPath);
    var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(file));
    Console.WriteLine("probe.indexHash=passed");

    stage = "grouped-projection";
    var selectionBytes = selected ? new FileInfo(selectionPath).Length : 0;
    var projection = new GroupedCompiledPathLimits(
        Long(reports, "maxProjectionInputBytes", 256L * 1024 * 1024),
        Long(reports, "maxOutputBytes", 512L * 1024 * 1024) - selectionBytes,
        Math.Max(1, maxPaths), Int(reports, "maxProjectionRecords", 500_000),
        Int(reports, "maxProjectionReferences", 2_000_000));
    var grouped = GroupedCompiledPathHandoffBuilder.Create(paths, hash, projection);
    Console.WriteLine("probe.groupedProjection=passed");
    stage = "grouped-restore";
    _ = GroupedCompiledPathHandoffBuilder.Restore(grouped, projection);
    Console.WriteLine("probe.groupedRestore=passed");
    if (args.Length == 3)
    {
        stage = "writer";
        var scratchLabel = "tracemap-report-probe-" + Guid.NewGuid().ToString("N");
        var scratchRoot = Path.Combine(Path.GetTempPath(), scratchLabel);
        try
        {
            await GroupedCompiledPathReportWriter.WriteAsync(grouped, Path.Combine(scratchRoot, "compiled"), projection);
            Console.WriteLine("probe.writer=passed");
        }
        finally
        {
            try
            {
                if (Directory.Exists(scratchRoot)) Directory.Delete(scratchRoot, recursive: true);
                Console.WriteLine("probe.scratchCleanup=passed");
            }
            catch
            {
                stage = "scratch-cleanup";
                Console.WriteLine($"probe.scratchCleanup=failed;label={scratchLabel}");
                throw new IOException("PROBE_SCRATCH_CLEANUP_FAILED");
            }
        }
        Console.WriteLine("probe.result=writer-passed;later-native-validation-not-tested;no-scan");
    }
    else Console.WriteLine("probe.result=writer-or-later-not-tested;read-only;no-scan");
    return 0;
}
catch (Exception exception)
{
    Console.WriteLine($"probe.failureStage={stage};exceptionType={SafeType(exception)};code={SafeCode(exception)};traceMapFrame={SafeFrame(exception)};no-private-content");
    return 1;
}

static int Int(JsonElement item, string name, int fallback) =>
    item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
        ? value.GetInt32() : fallback;
static long Long(JsonElement item, string name, long fallback) =>
    item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
        ? value.GetInt64() : fallback;
static string SafeType(Exception exception) => exception switch
{
    InvalidDataException => "InvalidData", SqliteException => "Sqlite", JsonException => "Json",
    OutOfMemoryException => "OutOfMemory", IOException => "IO", OverflowException => "Overflow",
    ArgumentException => "Argument", InvalidOperationException => "InvalidOperation",
    OperationCanceledException => "Cancelled", _ => "Other"
};
static string SafeCode(Exception exception) => exception is InvalidDataException &&
    Regex.IsMatch(exception.Message, "^WEBFORMS_(GROUPED_HANDOFF|GROUPED_REPORT|NATIVE_REPORT)_[A-Z_]{1,80}$",
        RegexOptions.CultureInvariant) ? exception.Message : "unavailable";
static string SafeFrame(Exception exception)
{
    foreach (var frame in new StackTrace(exception).GetFrames() ?? [])
    {
        var method = frame.GetMethod();
        var type = method?.DeclaringType;
        if (type?.Assembly.GetName().Name is not { } assembly ||
            !assembly.StartsWith("TraceMap.", StringComparison.Ordinal)) continue;
        // Only code-owned type/method identifiers, never file names, arguments or exception text.
        var name = type.FullName + "." + method!.Name;
        if (name.Length <= 180)
            return string.Concat(name.Select(character => char.IsAsciiLetterOrDigit(character) ||
                character is '.' or '_' or '+' or '`' ? character : '_'));
    }
    return "unavailable";
}
