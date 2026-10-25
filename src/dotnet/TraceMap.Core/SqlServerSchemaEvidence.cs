using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TraceMap.Core;

public sealed record SqlServerInputProvenance(string GeneratorSha256, string BoundedInputSha256);
public sealed record SqlServerObjectIdentity(string Server, string Database, string Schema, string Procedure);
public sealed record SqlServerDeclarationParameter(string Name, string SqlType, int? Size, string Units,
    string Direction, bool HasDefault, int? Precision, int? Scale, string? Collation, int Line);
public sealed record SqlServerProcedure(SqlServerObjectIdentity Identity, string FilePath, int StartLine, int EndLine,
    IReadOnlyList<SqlServerDeclarationParameter> Parameters);
public sealed record SqlServerDeclarations(string Version, string Repo, string Revision, string Origin, string IdentifierPolicy,
    IReadOnlyList<SqlServerProcedure> Procedures, SqlServerInputProvenance? Provenance = null);
public sealed record SqlServerSchemaExport(string Version, string GeneratorSha256, string BoundedInputSha256,
    string ExtractorVersion, SqlServerDeclarations Declarations, string RuleId, string Tier);
public sealed record SqlServerCallBinding(string FactId, string CommandText, SqlServerObjectIdentity Identity, string SourceEvidenceSha256);
public sealed record SqlServerBindings(string Version, string Repo, string Revision, IReadOnlyList<SqlServerCallBinding> Calls, SqlServerInputProvenance? Provenance = null);
public sealed record SqlServerComparison(string FactId, string RuleId, string Tier, string Status, IReadOnlyList<string> Reasons,
    string SourcePath, int SourceStartLine, int SourceEndLine, string Revision, string SourceExtractor,
    string SourceExtractorVersion, string SourceRuleId, string SourceTier, SqlServerProcedure? Declaration, SqlServerCallState? CallState, SqlServerCallBinding? Binding);
public sealed record SqlServerComparisonReport(string Version, string GeneratorSha256, string BoundedInputSha256,
    string SchemaGeneratorSha256, string SchemaInputSha256, string SchemaExtractorVersion, string SchemaRuleId, string SchemaTier, string Repo, string Revision, string Coverage,
    string Verdict, IReadOnlyList<SqlServerComparison> Comparisons, IReadOnlyList<string> Limitations);

/// <summary>Offline synthetic declarations only. Integrity checks do not authenticate evidence.</summary>
public static class SqlServerSchemaEvidence
{
    public const string ExtractorVersion = "sqlserver-synthetic-declarations/1";
    public const string RuleId = "database.sqlserver.parameter-comparison.v1";
    public const int MaxBytes = 4 * 1024 * 1024;
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 32
    };
    private static readonly Lazy<string> Generator = new(() => Hash(File.ReadAllBytes(typeof(SqlServerSchemaEvidence).Assembly.Location)));
    public static string GeneratorSha256 => Generator.Value;
    public static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    public static string Digest<T>(T value) => Hash(Encoding.UTF8.GetBytes(Json(value)));
    public static T Read<T>(string json)
    {
        Require(Encoding.UTF8.GetByteCount(json) <= MaxBytes, "input-size-limit");
        using var document = JsonDocument.Parse(json, new() { MaxDepth = 32 });
        void Check(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in element.EnumerateObject()) { Require(names.Add(p.Name), "duplicate-json-property"); Check(p.Value); }
            }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Check(item);
        }
        Check(document.RootElement);
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new ArgumentException("null-input");
    }
    private static void Require(bool ok, string reason) { if (!ok) throw new ArgumentException(reason); }
    private static bool Identifier(string? value) => value is { Length: > 0 and <= 128 } && Regex.IsMatch(value, "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
    private static bool Sha(string? value, int length = 64) => value is not null && value.Length == length && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool PathSafe(string? value) => value is { Length: > 0 and <= 512 } && !value.StartsWith('/') && !value.Contains('\\')
        && value.Split('/').All(p => p is not ("" or "." or "..") && p.All(c => char.IsAsciiLetterOrDigit(c) || "._-".Contains(c)));
    private static bool Identity(SqlServerObjectIdentity? i) => i is not null && Identifier(i.Server) && Identifier(i.Database) && Identifier(i.Schema) && Identifier(i.Procedure);
    public static SqlServerSchemaExport Produce(string input)
    {
        var d = Read<SqlServerDeclarations>(input);
        Validate(d);
        // Hash the exact canonical public declaration projection; input whitespace is immaterial.
        return new("sqlserver-schema/1", GeneratorSha256, Digest(d), ExtractorVersion, d, "database.sqlserver.synthetic-schema.v1", EvidenceTiers.Tier2Structural);
    }
    private static void Validate(SqlServerDeclarations d)
    {
        Require(d.Provenance is null || Sha(d.Provenance.GeneratorSha256) && Sha(d.Provenance.BoundedInputSha256), "invalid-input-provenance");
        Require(d.Version == "sqlserver-declarations/1" && d.Origin == "independent-synthetic-declarations"
            && d.IdentifierPolicy == "ordinal-exact-no-aliases", "unsupported-declaration-contract");
        Require(PathSafe(d.Repo) && !d.Repo.Contains('/') && Sha(d.Revision, 40), "invalid-repo-or-revision");
        Require(d.Procedures is { Count: > 0 and <= 256 }, "procedure-limit");
        var identities = new HashSet<SqlServerObjectIdentity>();
        foreach (var p in d.Procedures)
        {
            Require(p is not null && Identity(p.Identity) && identities.Add(p.Identity), "duplicate-or-invalid-object");
            Require(PathSafe(p!.FilePath) && p.StartLine > 0 && p.EndLine >= p.StartLine && p.Parameters is { Count: <= 128 }, "invalid-schema-location-or-parameters");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in p.Parameters!)
            {
                Require(a is not null && a.Name is { Length: > 1 } && a.Name.StartsWith('@') && Identifier(a.Name[1..]) && names.Add(a.Name), "duplicate-or-invalid-parameter");
                Require(a!.SqlType is "Int" or "BigInt" or "Bit" or "NVarChar" or "VarChar" or "Decimal", "unsupported-type");
                Require(a.Direction is "Input" or "InputOutput", "OUTPUT-is-input-output-not-output-only");
                Require(a.Line >= p.StartLine && a.Line <= p.EndLine, "invalid-parameter-location");
                Require(a.SqlType is "NVarChar" or "VarChar" ? a.Size is -1 or > 0 && a.Size <= (a.SqlType == "NVarChar" ? 4000 : 8000)
                    && a.Units == (a.SqlType == "NVarChar" ? "utf16-code-units" : "bytes") : a.Size is null && a.Units == "not-applicable", "invalid-size-or-units");
                Require((a.Precision is null or >= 1 and <= 38) && (a.Scale is null or >= 0 and <= 38)
                    && (a.Precision is null || a.Scale is null || a.Scale <= a.Precision)
                    && (a.Collation is null || Identifier(a.Collation)), "invalid-facets");
            }
        }
    }
    public static SqlServerSchemaExport Admit(string exportJson, string declarationsJson, string repo, string revision)
    {
        var expected = Produce(declarationsJson);
        var actual = Read<SqlServerSchemaExport>(exportJson);
        Require(actual.Version == "sqlserver-schema/1" && actual.ExtractorVersion == ExtractorVersion, "unsupported-export-version");
        Require(Json(actual) == Json(expected), "schema-integrity-or-generator-mismatch");
        Require(actual.Declarations.Repo == repo && actual.Declarations.Revision == revision, "unexpected-input-identity");
        return actual;
    }

    private static object CallProjection(CodeFact f) => new { f.FactId, f.Repo, f.CommitSha, f.RuleId, f.EvidenceTier,
        f.Evidence.FilePath, f.Evidence.StartLine, f.Evidence.EndLine, f.Evidence.ExtractorId, f.Evidence.ExtractorVersion,
        state = f.Properties.TryGetValue("sqlServerParameterState", out var json) ? Read<SqlServerCallState>(json) : null };
    public static string CallDigest(CodeFact fact) => Digest(CallProjection(fact));

    public static SqlServerComparisonReport Compare(SqlServerSchemaExport schema, SqlServerBindings bindings,
        IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        // Do not allow callers of the native library to bypass admission validation.
        schema = Admit(Json(schema), Json(schema.Declarations), repo, revision);
        Require(bindings.Provenance is null || Sha(bindings.Provenance.GeneratorSha256) && Sha(bindings.Provenance.BoundedInputSha256), "invalid-binding-provenance");
        Require(bindings.Version == "sqlserver-bindings/1" && bindings.Repo == repo && bindings.Revision == revision
            && bindings.Calls is { Count: <= 4096 }, "invalid-binding-envelope");
        Require(facts.Count <= 1000000, "fact-limit");
        var calls = facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate).OrderBy(f => f.FactId, StringComparer.Ordinal).ToArray();
        Require(calls.All(f => f.Repo == repo && f.CommitSha == revision && f.Evidence is not null && f.Properties is not null
            && !string.IsNullOrWhiteSpace(f.FactId) && PathSafe(f.Evidence.FilePath) && f.Evidence.StartLine > 0 && f.Evidence.EndLine >= f.Evidence.StartLine), "invalid-call-identity-or-location");
        Require(calls.Select(f => f.FactId).Distinct(StringComparer.Ordinal).Count() == calls.Length, "duplicate-call-identity");
        var map = new Dictionary<string, SqlServerCallBinding>(StringComparer.Ordinal);
        foreach (var b in bindings.Calls!)
        {
            Require(b is not null && Identity(b.Identity) && !string.IsNullOrEmpty(b.FactId)
                && b.CommandText == b.Identity.Schema + "." + b.Identity.Procedure && map.TryAdd(b.FactId, b)
                && calls.Any(f => f.FactId == b.FactId && CallDigest(f) == b.SourceEvidenceSha256), "invalid-ambiguous-stale-or-substituted-binding");
        }
        var rows = new List<SqlServerComparison>();
        foreach (var fact in calls)
        {
            var reasons = new List<string>();
            var state = fact.Properties.TryGetValue("sqlServerParameterState", out var json) ? Read<SqlServerCallState>(json) : null;
            SqlServerProcedure? procedure = null;
            var mismatch = false;
            void Unknown(string reason) => reasons.Add(reason);
            void Mismatch(string reason) { reasons.Add(reason); mismatch = true; }
            if (fact.RuleId != RuleIds.DatabaseOperationCallPattern || fact.EvidenceTier != EvidenceTiers.Tier1Semantic || fact.Evidence.ExtractorVersion is not (ScannerVersions.CSharpSemanticExtractor or ScannerVersions.VisualBasicSemanticExtractor)) Unknown("unsupported-source-extractor-or-tier");
            if (state is null || state.Version != "sqlserver-call/1" || state.Coverage != "bounded") Unknown(state?.Gap ?? "parameter-state-unavailable");
            else if (state.CommandType != "StoredProcedure") Unknown(state.CommandType == "Text" ? "text-command-outside-scope" : "stored-procedure-intent-unavailable");
            if (!map.TryGetValue(fact.FactId, out var binding)) Unknown("binding-missing");
            else
            {
                procedure = schema.Declarations.Procedures.SingleOrDefault(p => p.Identity == binding.Identity);
                if (procedure is null) Unknown("exact-schema-identity-unavailable");
                if (state?.CommandTextHash != FactFactory.Hash(binding.CommandText, 64)) Unknown("command-binding-conflict");
            }
            if (reasons.Count == 0 && procedure is not null && state is not null)
            {
                if (state.Parameters is null || state.Parameters.Count > 128 || state.Parameters.Any(p => p is null || p.Name is null || !Identifier(p.Name.TrimStart('@'))
                    || !p.Name.StartsWith('@') || p.SqlType is not ("Int" or "BigInt" or "Bit" or "NVarChar" or "VarChar" or "Decimal") || p.Direction is not ("Input" or "Output" or "InputOutput" or "ReturnValue") || p.Line < 1 || p.Line > fact.Evidence.EndLine || (p.EndLine ?? p.Line) < p.Line || (p.EndLine ?? p.Line) > fact.Evidence.EndLine
                    || (p.DirectionLine is not null && (p.DirectionLine < p.Line || p.DirectionLine > fact.Evidence.EndLine))
                    || (p.DirectionEndLine is not null && (p.DirectionLine is null || p.DirectionEndLine < p.DirectionLine || p.DirectionEndLine > fact.Evidence.EndLine))
                    || p.SizeUnits != (p.SqlType == "NVarChar" ? "utf16-code-units" : p.SqlType == "VarChar" ? "provider-ansi-size-policy-unknown" : "not-applicable"))
                    || state.Parameters.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != state.Parameters.Count) Unknown("invalid-or-ambiguous-call-parameters");
                else
                {
                    foreach (var p in state.Parameters.Where(p => p.Direction != "ReturnValue"))
                    {
                        var declaration = procedure.Parameters.SingleOrDefault(a => a.Name == p.Name);
                        if (declaration is null)
                        {
                            if (procedure.Parameters.Any(a => string.Equals(a.Name, p.Name, StringComparison.OrdinalIgnoreCase))) Unknown("identifier-case-policy-unknown");
                            else Mismatch("extra-parameter:" + p.Name);
                            continue;
                        }
                        if (declaration.Collation is not null || declaration.Precision is not null || declaration.Scale is not null || declaration.SqlType == "Decimal") Unknown("unsupported-facets:" + p.Name);
                        if (p.SqlType != declaration.SqlType) Mismatch("type:" + p.Name);
                        if (p.Direction != declaration.Direction)
                        {
                            if (declaration.Direction == "InputOutput" && p.Direction == "Output") Unknown("output-call-input-value-unproven:" + p.Name);
                            else Mismatch("direction:" + p.Name);
                        }
                        if (declaration.SqlType == "VarChar") Unknown("varchar-provider-length-policy-unsupported:" + p.Name);
                        else if (declaration.SqlType == "NVarChar" && p.SqlType == "NVarChar")
                        {
                            if (p.Size is null or 0 or < -1) Unknown("source-size-unavailable:" + p.Name);
                            else if (p.Size != declaration.Size) Mismatch("declared-size-difference:" + p.Name);
                        }
                    }
                    foreach (var p in procedure.Parameters.Where(a => !state.Parameters.Any(p => p.Name == a.Name && p.Direction != "ReturnValue")))
                    {
                        if (p.HasDefault) Unknown("omitted-default-parameter-unsupported:" + p.Name);
                        else if (state.Parameters.Any(a => string.Equals(a.Name, p.Name, StringComparison.OrdinalIgnoreCase))) Unknown("identifier-case-policy-unknown");
                        else Mismatch("missing-parameter:" + p.Name);
                    }
                }
            }
            var unknown = reasons.Any(r => !r.StartsWith("type:", StringComparison.Ordinal) && !r.StartsWith("direction:", StringComparison.Ordinal)
                && !r.StartsWith("declared-size-difference:", StringComparison.Ordinal) && !r.StartsWith("extra-parameter:", StringComparison.Ordinal) && !r.StartsWith("missing-parameter:", StringComparison.Ordinal));
            rows.Add(new(fact.FactId, RuleId, unknown ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural,
                unknown ? "unknown" : mismatch ? "mismatch" : "bounded-match", reasons.Order(StringComparer.Ordinal).ToArray(),
                fact.Evidence.FilePath, fact.Evidence.StartLine, fact.Evidence.EndLine, revision, fact.Evidence.ExtractorId, fact.Evidence.ExtractorVersion, fact.RuleId, fact.EvidenceTier, procedure, state, binding));
        }
        // Allowlist projection only: never hash raw SQL, snippets, original facts or private source bytes.
        var projection = new { schema, bindings, calls = calls.Select(CallProjection) };
        return new("sqlserver-comparison/1", GeneratorSha256, Digest(projection), schema.GeneratorSha256, schema.BoundedInputSha256,
            schema.ExtractorVersion, schema.RuleId, schema.Tier, repo, revision, "partial", "no-aggregate-clean-verdict", rows,
            ["Synthetic declarations are not installed schema or deployed server observations.",
            "Exact ordinal identifiers only; no aliases, case/collation equivalence, DNN qualifiers or connection inference.",
            "Declared size differences do not prove runtime truncation or incompatibility; nvarchar uses UTF-16 code units, MAX is -1.",
            "varchar schema length counts bytes; this slice does not resolve the provider ANSI length/encoding policy.",
            "OUTPUT declares input/output capability, never a pure output-only parameter; output-only calls leave input behavior unknown.",
            "ReturnValue is separate from procedure parameters, affected rows and result sets; none is result-column validation.",
            "Reachability, source build/scan gaps and truncation are not cleared by this separate parameter-only report.",
            "Self-consistent hashes prove integrity only, not authenticity, SQL execution, deployment or private acceptance."]);
    }
}
