using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Explicit local disclosure only. Metadata inspection never loads or executes input assemblies.
try
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    var allowed = new HashSet<string> { "--paths", "--assembly", "--expected-sha256", "--out" };
    var consent = false;
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--allow-local-command-text" && !consent) { consent = true; continue; }
        if (!allowed.Contains(args[i]) || i + 1 >= args.Length || !options.TryAdd(args[i], args[++i]))
            throw new InvalidDataException();
    }
    if (!consent || options.Count != 4) throw new InvalidDataException();
    var reportPath = Path.GetFullPath(options["--paths"]);
    var binaryPath = Path.GetFullPath(options["--assembly"]);
    var output = Path.GetFullPath(options["--out"]);
    var expected = options["--expected-sha256"].ToLowerInvariant();
    if (expected.Length != 64 || expected.Any(c => !Uri.IsHexDigit(c)) || File.Exists(output)
        || new FileInfo(reportPath).Length > 64L * 1024 * 1024
        || new FileInfo(binaryPath).Length > 256L * 1024 * 1024) throw new InvalidDataException();
    var reportBytes = File.ReadAllBytes(reportPath);
    var binaryBytes = File.ReadAllBytes(binaryPath);
    if (Hash(binaryBytes) != expected) throw new InvalidDataException();
    using var report = JsonDocument.Parse(reportBytes);
    var paths = report.RootElement.GetProperty("paths");
    if (paths.GetArrayLength() > 256) throw new InvalidDataException();
    var truncation = "unknown (native flag unavailable)";
    if (report.RootElement.TryGetProperty("summary", out var summary)
        && summary.ValueKind == JsonValueKind.Object && summary.TryGetProperty("truncated", out var flag))
        truncation = flag.ValueKind switch { JsonValueKind.True => "true", JsonValueKind.False => "false", _ => truncation };
    var reasons = new SortedSet<string>(StringComparer.Ordinal);
    if (report.RootElement.TryGetProperty("gaps", out var gaps) && gaps.ValueKind == JsonValueKind.Array)
        foreach (var gap in gaps.EnumerateArray())
            if (gap.TryGetProperty("gapKind", out var gapKind) && gapKind.GetString() == "TruncatedByLimit")
                reasons.Add(gap.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(reason.GetString()) ? reason.GetString()! : "reason unavailable");
    var reasonText = reasons.Count > 0 ? string.Join(", ", reasons)
        : truncation == "false" ? "none recorded (not completeness proof)" : "reason unavailable in retained diagnostics";
    var rows = new List<(string Path, string State, string Identity, string Type)>();
    foreach (var path in paths.EnumerateArray())
    {
        var nodes = path.GetProperty("nodes");
        if (nodes.GetArrayLength() == 0) throw new InvalidDataException();
        var terminal = nodes[nodes.GetArrayLength() - 1];
        var state = "unavailable"; var identity = ""; var type = "unavailable";
        if (terminal.TryGetProperty("surfaceKind", out var surface) && surface.GetString() == "database-api"
            && terminal.TryGetProperty("commandBinding", out var binding) && binding.ValueKind == JsonValueKind.Object)
        {
            if (binding.TryGetProperty("commandTextFromPath", out var text) && text.ValueKind == JsonValueKind.Object)
            {
                state = text.GetProperty("state").GetString() ?? "unavailable";
                if (state is "method-local-constant" or "constant-on-encoded-call-path"
                    && text.GetProperty("origin").GetProperty("kind").GetString() == "constant-string-hash")
                    identity = text.GetProperty("origin").GetProperty("identity").GetString() ?? "";
            }
            if (binding.TryGetProperty("commandTypeFromPath", out var kind) && kind.ValueKind == JsonValueKind.Object
                && kind.GetProperty("state").GetString() is "method-local-constant" or "constant-on-encoded-call-path"
                && kind.GetProperty("origin").GetProperty("kind").GetString() == "constant-int32")
                type = kind.GetProperty("origin").GetProperty("identity").GetString() switch { "4" => "StoredProcedure", "1" => "Text", _ => "unavailable" };
        }
        rows.Add((path.GetProperty("pathId").GetString() ?? "unknown", state, identity, type));
    }
    var wanted = rows.Where(r => r.Identity.Length > 0).Select(r => r.Identity).ToHashSet(StringComparer.Ordinal);
    var matches = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
    using var stream = new MemoryStream(binaryBytes, writable: false);
    using var assembly = AssemblyDefinition.ReadAssembly(stream, new ReaderParameters { ReadingMode = ReadingMode.Deferred, ReadSymbols = false });
    var work = 0;
    var pending = new Stack<TypeDefinition>(assembly.MainModule.Types.Reverse());
    while (pending.Count > 0)
    {
        var definition = pending.Pop();
        foreach (var nested in definition.NestedTypes.Reverse()) pending.Push(nested);
        foreach (var method in definition.Methods)
        {
            if (++work > 2_000_000) throw new InvalidDataException();
            if (!method.HasBody) continue;
            foreach (var instruction in method.Body.Instructions)
            {
                if (++work > 2_000_000) throw new InvalidDataException();
                if (instruction.OpCode.Code != Code.Ldstr || instruction.Operand is not string value) continue;
                if (value.Length > 32768) continue;
                var bytes = new byte[value.Length * 2];
                for (var j = 0; j < value.Length; j++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(j * 2, 2), value[j]);
                var key = $"str:{value.Length}:{Hash(bytes)}";
                if (!wanted.Contains(key)) continue;
                if (!matches.TryGetValue(key, out var values)) matches[key] = values = new(StringComparer.Ordinal);
                values.Add(value);
            }
        }
    }
    var lines = new List<string> { "# SQL command literals — PRIVATE LOCAL OUTPUT", "",
        "Rule: sql.local.command-literal-match.v1; Tier3SyntaxOrTextual.", "",
        "Content match only: the report's constant hash/UTF-16 length matched an ldstr operand in the explicitly supplied binary. The expected binary SHA is operator-supplied; this tool does not independently bind the binary to the historical scan or verify source provenance. Preserve the route's original evidence tier and warnings. No execution, SQL parsing, procedure existence, current-build identity, parameter/connection/transaction proof or whole-site completeness is established.", "",
        $"Generator SHA-256: {Hash(File.ReadAllBytes(typeof(Program).Assembly.Location))}",
        $"Cecil SHA-256: {Hash(File.ReadAllBytes(typeof(AssemblyDefinition).Assembly.Location))}",
        $"Bounded path-report input SHA-256: {Hash(reportBytes)}",
        $"Explicit binary input SHA-256: {expected}",
        $"Original query truncated: {truncation}",
        $"Original query truncation reasons: {Cell(reasonText)}",
        "Recovery covers only supplied retained paths. Successful literal recovery never clears original query truncation, proves complete enumeration, or upgrades historical/current-build provenance. Missing reason entries do not mean no truncation. If flag and reason entries conflict, retain both and reconcile the native evidence.",
        "Bounds: 64 MiB path report; 256 paths; 256 MiB binary; two million inspected methods/instructions; 32768 UTF-16 units per literal. Overlength literals are unavailable, not absent. Symbolic/dynamic expressions are not evaluated.", "",
        "| Path | Command type evidence | Text evidence state | Local literal / status |", "| --- | --- | --- | --- |" };
    foreach (var row in rows)
    {
        var value = row.Identity.Length > 0 && matches.TryGetValue(row.Identity, out var values) && values.Count == 1
            ? "hash-matched literal: " + values.Single() : "unavailable: no unique admitted constant match";
        lines.Add($"| {Cell(row.Path)} | {Cell(row.Type)} | {Cell(row.State)} | {Cell(value)} |");
    }
    using var writer = new StreamWriter(new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None));
    foreach (var line in lines) writer.WriteLine(line);
    Console.WriteLine($"Local literal report written; paths={rows.Count}. Do not upload this file.");
    return 0;
}
catch (Exception)
{
    Console.Error.WriteLine("Local literal recovery refused: check explicit consent, exact binary hash, valid bounded inputs and fresh output. No private details printed.");
    return 2;
}

static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static string Cell(string value)
{
    // Preserve UTF-16 units without UTF-8 replacement and prevent active Markdown links/images.
    var escaped = string.Concat(value.Select(c => char.IsSurrogate(c) || c == '\0' ? $"\\u{(int)c:x4}" : c.ToString()));
    return WebUtility.HtmlEncode(escaped).Replace("|", "&#124;", StringComparison.Ordinal)
        .Replace("`", "&#96;", StringComparison.Ordinal).Replace("[", "&#91;", StringComparison.Ordinal)
        .Replace("]", "&#93;", StringComparison.Ordinal).Replace("\r", "&#13;", StringComparison.Ordinal).Replace("\n", "&#10;", StringComparison.Ordinal);
}
