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
    var reportPaths = new List<string>();
    var allowed = new HashSet<string> { "--assembly", "--expected-sha256", "--out", "--contract-catalog" };
    var consent = false;
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--allow-local-command-text" && !consent) { consent = true; continue; }
        if (args[i] == "--paths" && i + 1 < args.Length) { reportPaths.Add(Path.GetFullPath(args[++i])); continue; }
        if (!allowed.Contains(args[i]) || i + 1 >= args.Length || !options.TryAdd(args[i], args[++i]))
            throw new InvalidDataException();
    }
    if (!consent || !options.ContainsKey("--assembly") || !options.ContainsKey("--expected-sha256") || !options.ContainsKey("--out") || reportPaths.Count is < 1 or > 32
        || reportPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != reportPaths.Count) throw new InvalidDataException();
    var binaryPath = Path.GetFullPath(options["--assembly"]);
    var output = Path.GetFullPath(options["--out"]);
    var expected = options["--expected-sha256"].ToLowerInvariant();
    if (expected.Length != 64 || expected.Any(c => !Uri.IsHexDigit(c)) || File.Exists(output)
        || reportPaths.Sum(path => new FileInfo(path).Length) > 64L * 1024 * 1024
        || new FileInfo(binaryPath).Length > 256L * 1024 * 1024) throw new InvalidDataException();
    var binaryBytes = File.ReadAllBytes(binaryPath);
    if (Hash(binaryBytes) != expected) throw new InvalidDataException();
    var contracts = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    string? catalogHash = null;
    if (options.TryGetValue("--contract-catalog", out var catalogPath))
    {
        if (new FileInfo(catalogPath).Length > 4 * 1024 * 1024) throw new InvalidDataException();
        var bytes = File.ReadAllBytes(catalogPath);
        if (bytes.Length > 4 * 1024 * 1024) throw new InvalidDataException();
        catalogHash = Hash(bytes);
        using var catalog = JsonDocument.Parse(bytes);
        var root = catalog.RootElement;
        if (root.GetProperty("schema").GetString() != "sql-contract-catalog.v1") throw new InvalidDataException();
        var entries = root.GetProperty("procedures");
        if (entries.GetArrayLength() > 1024) throw new InvalidDataException();
        foreach (var entry in entries.EnumerateArray())
        {
            var literal = Required(entry, "commandLiteral");
            var description = "operator-supplied contract candidate; database=" + Required(entry, "databaseLabel")
                + "; procedure=" + Required(entry, "procedure") + "; source=" + Required(entry, "sourceReference");
            var parameters = entry.GetProperty("parameters");
            if (parameters.GetArrayLength() > 256) throw new InvalidDataException();
            foreach (var parameter in parameters.EnumerateArray())
            {
                var direction = Required(parameter, "direction");
                if (direction is not ("Input" or "Output" or "InputOutput" or "ReturnValue" or "Unknown")) throw new InvalidDataException();
                description += "; parameter=" + Required(parameter, "name") + " " + Required(parameter, "type") + " " + direction;
            }
            description += parameters.GetArrayLength() == 0 ? "; no parameters supplied (not proof of zero parameters)" : "";
            if (!contracts.TryGetValue(literal, out var candidates)) contracts[literal] = candidates = new();
            candidates.Add(description);
        }
    }
    var rows = new List<(int Input, string Path, string State, string Identity, string Type, string Caller, string Terminal, string Evidence, string Limits)>();
    var inputLines = new List<string>();
    var inputHashes = new HashSet<string>(StringComparer.Ordinal);
    var ordinal = 0;
    long totalBytes = 0;
    foreach (var reportPath in reportPaths)
    {
    ordinal++;
    var reportBytes = File.ReadAllBytes(reportPath);
    totalBytes += reportBytes.Length;
    if (totalBytes > 64L * 1024 * 1024 || !inputHashes.Add(Hash(reportBytes))) throw new InvalidDataException();
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
    inputLines.AddRange(new[] { $"## Input {ordinal}",
        $"Bounded path-report input SHA-256: {Hash(reportBytes)}",
        $"Original query truncated: {truncation}",
        $"Original query truncation reasons: {Cell(reasonText)}",
        $"Retained path rows: {paths.GetArrayLength()} (not unique procedures or business operations).",
        "Current-build identity: not established by this projection. Native source provenance is retained below, not upgraded.",
        "Native source provenance: " + Cell(report.RootElement.TryGetProperty("sources", out var sources) ? sources.GetRawText() : "unavailable"),
        "Native query: " + Cell(report.RootElement.TryGetProperty("query", out var query) ? query.GetRawText() : "unavailable"),
        "Native coverage: " + Cell(report.RootElement.TryGetProperty("reportCoverage", out var coverage) ? coverage.ToString() : "unavailable"),
        "Native coverage warnings: " + Cell(report.RootElement.TryGetProperty("coverageWarnings", out var warnings) ? warnings.GetRawText() : "unavailable"),
        "Native limitations: " + Cell(report.RootElement.TryGetProperty("limitations", out var limits) ? limits.GetRawText() : "unavailable"), "" });
    if (paths.GetArrayLength() == 0) inputLines.Add("No retained paths for this input; not evidence of no SQL use.");
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
        rows.Add((ordinal, path.GetProperty("pathId").GetString() ?? "unknown", state, identity, type,
            nodes[0].TryGetProperty("symbolId", out var caller) ? caller.ToString() : "unavailable",
            terminal.TryGetProperty("surfaceName", out var name) ? name.ToString() : "unavailable",
            (terminal.TryGetProperty("ruleId", out var rule) ? rule.ToString() : "unavailable") + " / "
            + (terminal.TryGetProperty("evidenceTier", out var tier) ? tier.ToString() : "unavailable"),
            "classification=" + (path.TryGetProperty("classification", out var classification) ? classification.ToString() : "unavailable")
            + "; confidence=" + (path.TryGetProperty("confidence", out var confidence) ? confidence.ToString() : "unavailable")
            + "; notes=" + (path.TryGetProperty("notes", out var notes) ? notes.GetRawText() : "unavailable")
            + "; terminal limitations=" + (terminal.TryGetProperty("limitations", out var terminalLimits) ? terminalLimits.GetRawText() : "unavailable")));
        if (rows.Count > 1024) throw new InvalidDataException();
    }
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
    var lines = new List<string> { "# Consolidated SQL evidence inventory — PRIVATE LOCAL OUTPUT", "",
        "Rule: sql.local.command-literal-match.v1; Tier3SyntaxOrTextual.", "",
        "Content match only: the report's constant hash/UTF-16 length matched an ldstr operand in the explicitly supplied binary. The expected binary SHA is operator-supplied; this tool does not independently bind the binary to the historical scan or verify source provenance. Preserve the route's original evidence tier and warnings. No execution, SQL parsing, procedure existence, current-build identity, parameter/connection/transaction proof or whole-site completeness is established.", "",
        $"Generator SHA-256: {Hash(File.ReadAllBytes(typeof(Program).Assembly.Location))}",
        $"Cecil SHA-256: {Hash(File.ReadAllBytes(typeof(AssemblyDefinition).Assembly.Location))}",
        $"Explicit binary input SHA-256: {expected}",
        "Recovery covers only supplied retained paths. Successful literal recovery never clears original query truncation, proves complete enumeration, or upgrades historical/current-build provenance. Missing reason entries do not mean no truncation. If flag and reason entries conflict, retain both and reconcile the native evidence.",
        "Bounds: 32 reports, 64 MiB aggregate report bytes; 256 paths per report, 1024 total rows; 256 MiB binary; two million inspected methods/instructions; 32768 UTF-16 units per literal. Overlength literals are unavailable, not absent. Symbolic/dynamic expressions are not evaluated.", "" };
    lines.AddRange(inputLines);
    lines.Add("Contract catalog input SHA-256: " + (catalogHash ?? "not supplied"));
    lines.Add("Contract rule: sql.local.contract-catalog-match.v1; Tier3SyntaxOrTextual. Exact ordinal literal matching only for StoredProcedure rows. Operator-supplied metadata is not independently verified schema, connection binding or runtime parameter evidence. Multiple matching entries stay ambiguous. Migration decisions remain undecided.");
    lines.AddRange(new[] { "## Retained SQL rows", "",
        "Rows remain scoped by input ordinal and path ID. No cross-root deduplication or path splicing is performed. Repeated constants do not establish unique procedure counts.", "",
        "| Input / path | Caller | Terminal / rule / tier | Command type evidence | Text evidence state | Local literal / status | Native path qualification | Parameters / connection / transaction | Schema contract / migration decision |",
        "| --- | --- | --- | --- | --- | --- | --- | --- | --- |" });
    foreach (var row in rows)
    {
        var value = row.Identity.Length > 0 && matches.TryGetValue(row.Identity, out var values) && values.Count == 1
            ? "hash-matched literal: " + values.Single() : "unavailable: no unique admitted constant match";
        var contract = "not supplied / undecided";
        if (catalogHash is not null)
        {
            contract = "no eligible exact catalog match / undecided";
            if (row.Type == "StoredProcedure" && matches.TryGetValue(row.Identity, out var literals) && literals.Count == 1
                && contracts.TryGetValue(literals.Single(), out var candidates))
                contract = candidates.Count == 1 ? candidates[0] + "; migration undecided"
                    : $"ambiguous: {candidates.Count} catalog entries; no database selected / undecided";
        }
        lines.Add($"| {row.Input} / {Cell(row.Path)} | {Cell(row.Caller)} | {Cell(row.Terminal)} / {Cell(row.Evidence)} | {Cell(row.Type)} | {Cell(row.State)} | {Cell(value)} | {Cell(row.Limits)} | not established by literal projection; inspect native evidence | {Cell(contract)} |");
    }
    lines.AddRange(new[] { "", "## Per-path human verification handoff", "",
        "Rule: workflow.backend-extraction.retained-report.v1. These are pending review obligations, not new extracted facts or implementation approval. Keep reviewer decisions in the private client work tracker. Each input/path below refers to the evidence row above; repeated paths are not distinct business operations.", "" });
    foreach (var row in rows)
    {
        lines.Add($"### Input {row.Input} / {Cell(row.Path)}");
        lines.Add("- [ ] Verify page/event or other entry point and identity bridge to this caller; preserve historical/current-build and truncation limits.");
        lines.Add(row.Identity.Length == 0
            ? "- [ ] Inspect unresolved command expression at the evidenced callsite; record constant/dynamic/unknown with evidence, without guessing."
            : "- [ ] Verify recovered command/procedure candidate against authorized local schema evidence; a literal or catalog match alone is not verification.");
        lines.Add("- [ ] Map caller arguments to parameter names, types, directions, null/default behavior; document results, return values and affected rows.");
        lines.Add("- [ ] Verify database/connection selection, transactions, validation, authorization and failure behavior.");
        lines.Add("- [ ] Record reviewer and evidence references, retain/replace/retire decision, API acceptance tests and implementation approval for this slice.");
        lines.Add("Reviewer: unassigned. Verification evidence: not supplied. Decision: undecided. Implementation acceptance: pending. Other slices need not be complete before this slice is reviewed.");
        lines.Add("");
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
static string Required(JsonElement element, string name)
{
    var value = element.GetProperty(name).GetString();
    if (string.IsNullOrWhiteSpace(value) || value.Length > 4096) throw new InvalidDataException();
    return value;
}
static string Cell(string value)
{
    // Preserve UTF-16 units without UTF-8 replacement and prevent active Markdown links/images.
    var escaped = string.Concat(value.Select(c => char.IsSurrogate(c) || c == '\0' ? $"\\u{(int)c:x4}" : c.ToString()));
    return WebUtility.HtmlEncode(escaped).Replace("|", "&#124;", StringComparison.Ordinal)
        .Replace("`", "&#96;", StringComparison.Ordinal).Replace("[", "&#91;", StringComparison.Ordinal)
        .Replace("]", "&#93;", StringComparison.Ordinal).Replace("\r", "&#13;", StringComparison.Ordinal).Replace("\n", "&#10;", StringComparison.Ordinal);
}
