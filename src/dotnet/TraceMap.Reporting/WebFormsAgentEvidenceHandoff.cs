using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace TraceMap.Reporting;

public sealed record WebFormsHandoffEvidenceReference(
    string EvidenceId,
    string Role,
    string? Caller,
    string? Callee,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string RuleId,
    string EvidenceTier);

public sealed record WebFormsHandoffRetrievalHint(
    string RecipeId,
    string InputKind,
    string RuleId,
    string EvidenceTier,
    string Reason,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<string> SupportingIds,
    IReadOnlyList<string> ExpectedResultFields);

public sealed record WebFormsHandoffReadTarget(int Order, string Kind, string Locator, string Reason);

public sealed record WebFormsHandoffCorpusSelector(string Kind, string Value);

public sealed record WebFormsHandoffQuestion(
    string QuestionId,
    string ArtifactFamily,
    string Question,
    IReadOnlyList<string> SupportingIds);

public sealed record WebFormsAgentCaseHandoff(
    string SchemaVersion,
    string Kind,
    string Privacy,
    string RuleId,
    string QueryRecipeSchemaVersion,
    WebFormsAgentHandoffProvenance Provenance,
    WebFormsAgentHandoffSubject Subject,
    IReadOnlyList<WebFormsHandoffReadTarget> ReadFirst,
    IReadOnlyList<WebFormsHandoffEvidenceReference> Evidence,
    IReadOnlyList<WebFormsHandoffCorpusSelector> CorpusSelectors,
    IReadOnlyList<WebFormsHandoffRetrievalHint> RetrievalHints,
    IReadOnlyList<WebFormsHandoffQuestion> ExternalEvidenceQuestions,
    IReadOnlyList<string> Limitations);

public sealed record WebFormsAgentHandoffProvenance(
    string ScanId,
    string CommitSha,
    string InspectionSchemaVersion,
    string InspectionSnapshot,
    string InspectionSha256);

public sealed record WebFormsAgentHandoffSubject(
    string CaseId,
    string Handler,
    IReadOnlyList<string> SurfaceIds,
    IReadOnlyList<string> MethodSymbols,
    IReadOnlyList<string> StoppingSymbols,
    string EvidenceConclusion,
    bool TraversalLimitReached);

/// <summary>
/// Builds private, deterministic discovery metadata over retained Web Forms review evidence.
/// This is navigation metadata, not a scanner finding or modernization conclusion.
/// </summary>
public static class WebFormsAgentEvidenceHandoff
{
    public const string SchemaVersion = "tracemap-agent-evidence-handoff.v1";
    public const string RuleId = "diagnostic.webforms.agent-evidence-handoff.v1";
    private const int MaximumCaseHandoffBytes = 4 * 1024 * 1024;
    private const long MaximumIndexBytes = 16L * 1024 * 1024 * 1024;
    private const long MaximumCorpusJsonLinesBytes = 256L * 1024 * 1024;
    private const int MaximumCorpusLines = 100_000;
    private const int MaximumCorpusLineCharacters = 4 * 1024 * 1024;
    private const int MaximumHintsPerCase = 128;
    private const int MaximumRecommendedChunksPerCase = 32;
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly Regex CaseIdPattern = new("^case-[0-9]{3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SafeChunkTokenPattern = new("^[a-z0-9][a-z0-9.:-]{0,255}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static WebFormsAgentCaseHandoff BuildCase(
        JsonElement inspection,
        JsonElement selectedCase,
        string caseId,
        string privateReportFileName,
        string inspectionSnapshotFileName,
        string inspectionSha256,
        string? setHandoffFileName = null)
    {
        if (!CaseIdPattern.IsMatch(caseId)
            || inspection.GetProperty("schemaVersion").GetString() != "webforms-batch-inspection.v1")
        {
            throw new InvalidDataException("AgentHandoffSchemaMismatch");
        }

        var scanId = RequiredBoundedString(inspection, "scanId");
        var commitSha = RequiredBoundedString(inspection, "commitSha");
        var handler = selectedCase.TryGetProperty("handler", out var handlerValue)
            ? RequiredBoundedString(handlerValue)
            : RequiredBoundedString(selectedCase.GetProperty("handlerLocation"), "callee");
        var surfaces = selectedCase.GetProperty("bindings").EnumerateArray()
            .Select(binding => OptionalBoundedString(binding, "surfaceId"))
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Take(64)
            .ToArray();
        var methods = selectedCase.GetProperty("methods").EnumerateArray()
            .Select(method => RequiredBoundedString(method, "symbol"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Take(256)
            .ToArray();
        var stoppingSymbols = StringArray(selectedCase, "stoppingSymbols", 256);
        if (stoppingSymbols.Count == 0)
            stoppingSymbols = StringArray(selectedCase, "unresolvedOtherLeaves", 256);
        var evidenceConclusion = OptionalBoundedString(selectedCase, "evidenceConclusion") ?? "unavailable-from-older-inspection";
        var traversalLimitReached = selectedCase.TryGetProperty("bounded", out var bounded) && bounded.GetBoolean();
        var evidence = ReadEvidence(selectedCase);
        var selectors = surfaces.Select(value => new WebFormsHandoffCorpusSelector("surface-id", value))
            .Concat([new("handler-symbol", handler)])
            .Concat(evidence.Select(value => new WebFormsHandoffCorpusSelector("supporting-id", value.EvidenceId)))
            .Distinct()
            .OrderBy(value => value.Kind, StringComparer.Ordinal)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .Take(512)
            .ToArray();
        var hints = BuildHints(handler, surfaces, methods, stoppingSymbols, evidence, selectedCase);
        var databaseEvidence = evidence
            .Where(value => IsDatabaseShaped(value.Caller) || IsDatabaseShaped(value.Callee))
            .Select(value => value.EvidenceId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var questions = new List<WebFormsHandoffQuestion>();
        if (stoppingSymbols.Count > 0)
        {
            questions.Add(new(
                "resolve-unavailable-terminal-source",
                "authorized-source-or-definition",
                "Obtain authorized source or declaration metadata for retained stopping symbols whose definitions or outgoing evidence remain unavailable.",
                evidence.Where(value => stoppingSymbols.Contains(value.Callee, StringComparer.Ordinal)).Select(value => value.EvidenceId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));
        }
        if (databaseEvidence.Length > 0)
        {
            questions.Add(new(
                "confirm-application-database-contract",
                "authorized-database-definition-or-schema",
                "Obtain the authorized procedure/query definition, parameter contract, and result-set schema for retained database-shaped boundaries; do not execute against an application database from this handoff.",
                databaseEvidence));
        }
        if (!evidenceConclusion.Contains("no-other-unresolved-leaves", StringComparison.Ordinal))
        {
            questions.Add(new(
                "confirm-expected-business-behavior",
                "authorized-owner-review",
                "Confirm whether the observed UI/control behavior is the complete expected business behavior for this trigger.",
                evidence.Select(value => value.EvidenceId).Take(32).ToArray()));
        }

        var readFirst = new List<WebFormsHandoffReadTarget>
        {
            new(1, "private-report-section", privateReportFileName + "#summary", "Read provenance, coverage, and the retained evidence conclusion first."),
            new(2, "private-report-section", privateReportFileName + "#trigger", "Read the retained event binding and trigger context."),
            new(3, "private-report-section", privateReportFileName + "#call-path", "Follow the retained static call path from the selected handler."),
            new(4, "private-report-section", privateReportFileName + "#evidence", "Inspect cited locations, rules, tiers, and available source context.")
        };
        if (setHandoffFileName is not null)
            readFirst.Add(new(5, "set-handoff", setHandoffFileName, "Use the set handoff to locate the matching TraceMap index and optional evidence corpus."));

        return new(
            SchemaVersion,
            "webforms-private-case-evidence-handoff",
            "PRIVATE: retained paths, symbols, fact IDs, and local artifact links; keep on the authorized work machine.",
            RuleId,
            EvidenceDocsQueryRecipes.SchemaVersion,
            new(scanId, commitSha, "webforms-batch-inspection.v1", inspectionSnapshotFileName, inspectionSha256),
            new(caseId, handler, surfaces, methods, stoppingSymbols, evidenceConclusion, traversalLimitReached),
            readFirst,
            evidence,
            selectors,
            hints,
            questions.OrderBy(value => value.QuestionId, StringComparer.Ordinal).ToArray(),
            [
                "Static retained evidence does not prove runtime execution, order, branch feasibility, successful binding, or complete coverage.",
                "Missing source, calls, query results, or external definitions do not prove absence.",
                "Retrieval hints query only TraceMap-owned evidence; they do not query an application operational database.",
                "This handoff does not infer business intent, produce a BRD, choose a target architecture, or generate migration code."
            ]);
    }

    public static void WriteCase(string path, WebFormsAgentCaseHandoff handoff)
    {
        ValidateCase(handoff);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, handoff, JsonOptions);
        stream.WriteByte((byte)'\n');
    }

    public static IReadOnlyList<string> WriteSet(
        string inspectionPath,
        string outputDirectory,
        string outputPath,
        string? indexPath = null,
        string? evidenceDocsRoot = null)
    {
        var inspectionInfo = new FileInfo(inspectionPath);
        if (!inspectionInfo.Exists || inspectionInfo.Length > 32L * 1024 * 1024)
            throw new InvalidDataException("AgentHandoffInspectionUnavailable");
        var outputRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDirectory));
        var rootPath = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetDirectoryName(rootPath), outputRoot, PathComparison) || Path.GetFileName(rootPath) != "agent-evidence-handoff.json")
            throw new InvalidDataException("AgentHandoffOutputInvalid");
        if (File.Exists(rootPath)) throw new IOException("AgentHandoffOutputExists");

        using var inspectionDocument = JsonDocument.Parse(File.ReadAllText(inspectionInfo.FullName));
        StaticHtmlEvidenceExplorer.RejectDuplicateJsonProperties(inspectionDocument.RootElement);
        var inspection = inspectionDocument.RootElement;
        if (inspection.GetProperty("schemaVersion").GetString() != "webforms-batch-inspection.v1")
            throw new InvalidDataException("AgentHandoffSchemaMismatch");
        var scanId = RequiredBoundedString(inspection, "scanId");
        var commitSha = RequiredBoundedString(inspection, "commitSha");
        var inspectionSha256 = HashFile(inspectionInfo.FullName);

        var handoffFiles = Directory.GetFiles(outputRoot, "case-*.handoff.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (handoffFiles.Length is < 1 or > 64) throw new InvalidDataException("AgentHandoffCaseLimit");
        var cases = handoffFiles.Select(ReadCase).ToArray();
        if (cases.Select(value => value.Subject.CaseId).Distinct(StringComparer.Ordinal).Count() != cases.Length
            || cases.Any(value => value.Provenance.ScanId != scanId
                || value.Provenance.CommitSha != commitSha
                || value.Provenance.InspectionSha256 != inspectionSha256))
        {
            throw new InvalidDataException("AgentHandoffProvenanceMismatch");
        }

        var evidenceStore = ReadEvidenceStore(indexPath, outputRoot, scanId, commitSha);
        var corpus = ReadCorpus(evidenceDocsRoot, outputRoot, scanId, commitSha, cases);
        var caseEntries = cases.OrderBy(value => value.Subject.CaseId, StringComparer.Ordinal).Select(value => new
        {
            caseId = value.Subject.CaseId,
            handler = value.Subject.Handler,
            surfaceIds = value.Subject.SurfaceIds,
            evidenceConclusion = value.Subject.EvidenceConclusion,
            traversalLimitReached = value.Subject.TraversalLimitReached,
            privateReport = value.Subject.CaseId + ".private.html",
            caseHandoff = value.Subject.CaseId + ".handoff.json",
            corpusSelectors = value.CorpusSelectors,
            recommendedChunks = corpus.ChunksByCase.GetValueOrDefault(value.Subject.CaseId) ?? [],
            retrievalHintCount = value.RetrievalHints.Count,
            externalEvidenceQuestionCount = value.ExternalEvidenceQuestions.Count
        }).ToArray();
        var payload = new
        {
            schemaVersion = SchemaVersion,
            kind = "webforms-private-review-set-evidence-handoff",
            privacy = "PRIVATE: retained paths, symbols, fact IDs, and local artifact locators; keep on the authorized work machine.",
            ruleId = RuleId,
            queryRecipeSchemaVersion = EvidenceDocsQueryRecipes.SchemaVersion,
            provenance = new
            {
                scanId,
                commitSha,
                inspectionSchemaVersion = "webforms-batch-inspection.v1",
                inspectionSnapshot = Path.GetFileName(inspectionInfo.FullName),
                inspectionSha256
            },
            evidenceStore,
            evidenceCorpus = corpus.Summary,
            readFirst = new object[]
            {
                new { order = 1, kind = "review-index", locator = "index.html", reason = "Select the retained exception case relevant to the review question." },
                new { order = 2, kind = "review-queue", locator = "index.md", reason = "Read or update human verdict metadata without changing scanner evidence." },
                new { order = 3, kind = "case-handoff", locator = "case-NNN.handoff.json", reason = "Use one case handoff to choose bounded evidence and query recipes." }
            },
            cases = caseEntries,
            consumerLoop = new[]
            {
                "Open the review index and select one case.",
                "Read that case handoff and its ordered local report sections.",
                "Read matching evidence-corpus chunks when a validated corpus is available.",
                "Run only the closed read-only TraceMap recipes named by retrieval hints.",
                "Preserve gaps and request the authorized external artifact named by unanswered evidence questions.",
                "Perform BRD or modernization reasoning only in an authorized downstream workflow."
            },
            limitations = new[]
            {
                "TraceMap index.sqlite is the source of truth for retained static evidence; this handoff is a navigation projection.",
                "Application databases are external evidence sources and are never queried by this handoff.",
                "Missing or empty query results do not prove source, behavior, or runtime absence.",
                "This artifact does not generate a BRD or modernization plan."
            }
        };

        var temporaryPath = rootPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, payload, JsonOptions);
                stream.WriteByte((byte)'\n');
            }
            File.Move(temporaryPath, rootPath);
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }

        return
        [
            "agentEvidenceHandoff=created",
            $"cases={cases.Length}|evidenceStore={evidenceStore.Availability}|evidenceCorpus={corpus.Summary.Availability}|recommendedChunks={corpus.ChunksByCase.Values.Sum(value => value.Count)}",
            "nonClaim=navigation-projection-not-scanner-evidence-or-brd"
        ];
    }

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static IReadOnlyList<WebFormsHandoffEvidenceReference> ReadEvidence(JsonElement selectedCase)
    {
        var values = new List<WebFormsHandoffEvidenceReference>();
        AddWitness(values, selectedCase.GetProperty("handlerLocation"), "handler");
        foreach (var binding in selectedCase.GetProperty("bindings").EnumerateArray())
            AddWitness(values, binding.GetProperty("bindingLocation"), "event-binding");
        foreach (var method in selectedCase.GetProperty("methods").EnumerateArray())
        {
            foreach (var declaration in method.GetProperty("exactDeclarationLocations").EnumerateArray())
                AddWitness(values, declaration, "exact-declaration");
            foreach (var call in method.GetProperty("outgoingCallSites").EnumerateArray())
                AddWitness(values, call, "retained-call");
        }
        if (values.Count > 512) throw new InvalidDataException("AgentHandoffEvidenceLimit");
        return values.Distinct().OrderBy(value => value.FilePath, StringComparer.Ordinal)
            .ThenBy(value => value.StartLine).ThenBy(value => value.Role, StringComparer.Ordinal)
            .ThenBy(value => value.EvidenceId, StringComparer.Ordinal).ToArray();
    }

    private static void AddWitness(List<WebFormsHandoffEvidenceReference> values, JsonElement value, string role)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return;
        var factId = OptionalBoundedString(value, "factId");
        if (factId is null) return;
        values.Add(new(
            factId,
            role,
            OptionalBoundedString(value, "caller"),
            OptionalBoundedString(value, "callee"),
            OptionalBoundedString(value, "filePath"),
            OptionalPositiveInt(value, "startLine"),
            OptionalPositiveInt(value, "endLine"),
            OptionalBoundedString(value, "ruleId") ?? "rule-unavailable",
            OptionalBoundedString(value, "tier") ?? OptionalBoundedString(value, "evidenceTier") ?? "Tier4Unknown"));
    }

    private static IReadOnlyList<WebFormsHandoffRetrievalHint> BuildHints(
        string handler,
        IReadOnlyList<string> surfaces,
        IReadOnlyList<string> methods,
        IReadOnlyList<string> stoppingSymbols,
        IReadOnlyList<WebFormsHandoffEvidenceReference> evidence,
        JsonElement selectedCase)
    {
        var catalog = EvidenceDocsQueryRecipes.Build();
        var byId = catalog.Recipes.ToDictionary(value => value.RecipeId, StringComparer.Ordinal);
        var hints = new List<WebFormsHandoffRetrievalHint>();
        void Add(string recipeId, string reason, IEnumerable<(string Name, string Value)> parameters, IEnumerable<string> supportingIds)
        {
            if (hints.Count >= MaximumHintsPerCase) return;
            if (!byId.TryGetValue(recipeId, out var recipe)) throw new InvalidDataException("AgentHandoffRecipeUnavailable");
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, value) in parameters) values[name] = value;
            values["limit"] = values.GetValueOrDefault("limit", "250");
            var required = recipe.Parameters.Where(value => value.RequiredForInputKinds.Contains("single-index", StringComparer.Ordinal)).Select(value => value.Name).ToArray();
            if (required.Any(value => !values.ContainsKey(value)) || values.Keys.Any(value => recipe.Parameters.All(parameter => parameter.Name != value)))
                throw new InvalidDataException("AgentHandoffRecipeParameterInvalid");
            hints.Add(new(recipeId, "single-index", recipe.RuleId, recipe.EvidenceTier, reason, values,
                supportingIds.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(64).ToArray(),
                recipe.ResultFields));
        }

        Add("calls-from-handler", "Retrieve retained direct calls from the selected handler.", [("handler_symbol", handler)], evidence.Where(value => value.Caller == handler).Select(value => value.EvidenceId));
        foreach (var surface in surfaces.Take(16))
            Add("webforms-surface-facts", "Retrieve retained facts explicitly associated with this surface.", [("surface_id", surface)], [surface]);
        foreach (var method in methods.Take(48))
            Add("database-evidence-by-handler", "Check this retained method for database-shaped static evidence without inferring execution.", [("handler_symbol", method)], evidence.Where(value => value.Caller == method || value.Callee == method).Select(value => value.EvidenceId));
        foreach (var stopping in stoppingSymbols.Take(16))
            Add("callers-of-callee", "Retrieve reverse callers retained for this stopping symbol.", [("callee_symbol", stopping)], evidence.Where(value => value.Callee == stopping).Select(value => value.EvidenceId));
        foreach (var item in evidence.Where(value => value.Role is "handler" or "event-binding").Take(16))
        {
            Add("fact-by-id", "Retrieve the exact retained trigger or handler fact.", [("fact_id", item.EvidenceId)], [item.EvidenceId]);
            if (item.FilePath is not null && item.StartLine is not null && item.EndLine is not null)
                Add("facts-by-file-span", "Retrieve retained evidence overlapping this trigger or handler span.",
                    [("file_path", item.FilePath), ("start_line", item.StartLine.Value.ToString()), ("end_line", item.EndLine.Value.ToString())], [item.EvidenceId]);
        }
        foreach (var method in selectedCase.GetProperty("methods").EnumerateArray())
        {
            var symbol = RequiredBoundedString(method, "symbol");
            var databaseCallIds = method.GetProperty("outgoingCallSites").EnumerateArray()
                .Where(call => IsDatabaseShaped(OptionalBoundedString(call, "callee")))
                .Select(call => OptionalBoundedString(call, "factId"))
                .Where(value => value is not null).Cast<string>().ToArray();
            if (databaseCallIds.Length > 0)
                Add("stored-procedure-candidate-context", "Retrieve command construction, command-type, invocation, and argument evidence around this database-shaped caller.", [("method_symbol", symbol)], databaseCallIds);
        }

        return hints.GroupBy(value => JsonSerializer.Serialize(new { value.RecipeId, value.Parameters }), StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ThenBy(value => string.Join('|', value.Parameters.Select(pair => pair.Key + "=" + pair.Value)), StringComparer.Ordinal)
            .Take(MaximumHintsPerCase)
            .ToArray();
    }

    private static void ValidateCase(WebFormsAgentCaseHandoff handoff)
    {
        if (handoff.SchemaVersion != SchemaVersion || handoff.RuleId != RuleId || !CaseIdPattern.IsMatch(handoff.Subject.CaseId)
            || handoff.Provenance.InspectionSchemaVersion != "webforms-batch-inspection.v1"
            || handoff.Provenance.InspectionSha256.Length != 64 || handoff.Evidence.Count > 512 || handoff.RetrievalHints.Count > MaximumHintsPerCase)
            throw new InvalidDataException("AgentHandoffContractInvalid");
        var catalog = EvidenceDocsQueryRecipes.Build().Recipes.ToDictionary(value => value.RecipeId, StringComparer.Ordinal);
        foreach (var hint in handoff.RetrievalHints)
        {
            var required = catalog.GetValueOrDefault(hint.RecipeId)?.Parameters
                .Where(value => value.RequiredForInputKinds.Contains(hint.InputKind, StringComparer.Ordinal))
                .Select(value => value.Name)
                .ToArray() ?? [];
            if (!catalog.TryGetValue(hint.RecipeId, out var recipe) || hint.RuleId != recipe.RuleId || hint.EvidenceTier != recipe.EvidenceTier
                || hint.InputKind != "single-index" || required.Any(value => !hint.Parameters.ContainsKey(value))
                || hint.Parameters.Keys.Any(value => recipe.Parameters.All(parameter => parameter.Name != value))
                || !hint.Parameters.TryGetValue("limit", out var limitText) || !int.TryParse(limitText, out var limit) || limit is < 1 or > 10_000
                || !hint.ExpectedResultFields.SequenceEqual(recipe.ResultFields, StringComparer.Ordinal))
                throw new InvalidDataException("AgentHandoffRecipeUnavailable");
        }
    }

    private static WebFormsAgentCaseHandoff ReadCase(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > MaximumCaseHandoffBytes) throw new InvalidDataException("AgentHandoffCaseUnavailable");
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        StaticHtmlEvidenceExplorer.RejectDuplicateJsonProperties(document.RootElement);
        var value = JsonSerializer.Deserialize<WebFormsAgentCaseHandoff>(json, JsonOptions)
            ?? throw new InvalidDataException("AgentHandoffCaseUnavailable");
        ValidateCase(value);
        if (Path.GetFileName(path) != value.Subject.CaseId + ".handoff.json") throw new InvalidDataException("AgentHandoffCaseUnavailable");
        return value;
    }

    private static EvidenceStoreSummary ReadEvidenceStore(string? indexPath, string outputRoot, string scanId, string commitSha)
    {
        if (string.IsNullOrWhiteSpace(indexPath))
            return new("not-supplied", "tracemap-index-sqlite", null, scanId, commitSha, null, null, null,
                "Supply -IndexPath or a configured indexPath to enable read-only source-of-truth queries.");
        var info = new FileInfo(Path.GetFullPath(indexPath));
        if (!info.Exists || info.Length is < 1 or > MaximumIndexBytes) throw new InvalidDataException("AgentHandoffIndexUnavailable");
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = info.FullName, Mode = SqliteOpenMode.ReadOnly }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select repo, scanner_version, analysis_level, build_status from scan_manifest where scan_id=$scan and commit_sha=$commit order by repo limit 2;";
        command.Parameters.AddWithValue("$scan", scanId);
        command.Parameters.AddWithValue("$commit", commitSha);
        using var reader = command.ExecuteReader();
        var rows = new List<string[]>();
        while (reader.Read())
        {
            var row = new[] { reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3) };
            if (row.Any(value => value.Length > 4096)) throw new InvalidDataException("AgentHandoffIndexUnavailable");
            rows.Add(row);
        }
        if (rows.Count != 1) throw new InvalidDataException("AgentHandoffIndexProvenanceMismatch");
        var locator = RelativeLocator(outputRoot, info.FullName);
        return new("validated", "tracemap-index-sqlite", locator, scanId, commitSha, rows[0][0], rows[0][1], rows[0][2],
            $"Opened read-only; scan_manifest build status: {rows[0][3]}. Run only recipes from {EvidenceDocsQueryRecipes.SchemaVersion}.");
    }

    private static CorpusProjection ReadCorpus(string? corpusRoot, string outputRoot, string scanId, string commitSha, IReadOnlyList<WebFormsAgentCaseHandoff> cases)
    {
        var empty = cases.ToDictionary(value => value.Subject.CaseId, _ => (IReadOnlyList<RecommendedChunk>)[], StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(corpusRoot))
            return new(new("not-supplied", "tracemap-evidence-docs-export", null, null, 0,
                "Supply -EvidenceDocsRoot to resolve corpus selectors to exact generated chunk IDs."), empty);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(corpusRoot));
        if (!Directory.Exists(root)) throw new InvalidDataException("AgentHandoffCorpusUnavailable");
        var manifestPath = BoundedCorpusFile(root, "manifest.json", 16L * 1024 * 1024);
        var recipesPath = BoundedCorpusFile(root, "query-recipes.json", 4L * 1024 * 1024);
        var chunksPath = BoundedCorpusFile(root, "chunks.jsonl", MaximumCorpusJsonLinesBytes);
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestPath));
        StaticHtmlEvidenceExplorer.RejectDuplicateJsonProperties(manifestDocument.RootElement);
        var manifest = manifestDocument.RootElement;
        var schemaVersion = RequiredBoundedString(manifest, "schemaVersion");
        if (schemaVersion != EvidenceDocsExporter.SchemaVersion)
            throw new InvalidDataException("AgentHandoffCorpusSchemaMismatch");
        var commits = StringArray(manifest, "commitShas", 256);
        var scans = manifest.GetProperty("inputs").EnumerateArray()
            .SelectMany(input => input.GetProperty("sourceRefs").EnumerateArray())
            .Select(source => OptionalBoundedString(source, "scanId"))
            .Where(value => value is not null).Cast<string>().Distinct(StringComparer.Ordinal).ToArray();
        if (!commits.Contains(commitSha, StringComparer.OrdinalIgnoreCase) || !scans.Contains(scanId, StringComparer.Ordinal))
            throw new InvalidDataException("AgentHandoffCorpusProvenanceMismatch");
        using var recipeDocument = JsonDocument.Parse(File.ReadAllText(recipesPath));
        StaticHtmlEvidenceExplorer.RejectDuplicateJsonProperties(recipeDocument.RootElement);
        if (RequiredBoundedString(recipeDocument.RootElement, "schemaVersion") != EvidenceDocsQueryRecipes.SchemaVersion)
            throw new InvalidDataException("AgentHandoffRecipeSchemaMismatch");

        var selectorValues = cases.ToDictionary(value => value.Subject.CaseId,
            value => value.CorpusSelectors.Select(selector => selector.Value).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        var selected = cases.ToDictionary(value => value.Subject.CaseId, _ => new List<RecommendedChunk>(), StringComparer.Ordinal);
        using var reader = new StreamReader(chunksPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lineCount = 0;
        while (reader.ReadLine() is { } line)
        {
            if (++lineCount > MaximumCorpusLines || line.Length > MaximumCorpusLineCharacters)
                throw new InvalidDataException("AgentHandoffCorpusLimit");
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var chunkDocument = JsonDocument.Parse(line);
            StaticHtmlEvidenceExplorer.RejectDuplicateJsonProperties(chunkDocument.RootElement);
            var chunk = chunkDocument.RootElement;
            var chunkId = RequiredBoundedString(chunk, "chunkId");
            var chunkFamily = RequiredBoundedString(chunk, "chunkFamily");
            var chunkType = RequiredBoundedString(chunk, "chunkType");
            if (!SafeChunkTokenPattern.IsMatch(chunkId) || !SafeChunkTokenPattern.IsMatch(chunkFamily) || !SafeChunkTokenPattern.IsMatch(chunkType))
                throw new InvalidDataException("AgentHandoffCorpusSchemaMismatch");
            var values = StringArray(chunk, "supportingIds", 2048).ToHashSet(StringComparer.Ordinal);
            if (chunk.TryGetProperty("retrievalHints", out var hintValues) && hintValues.ValueKind == JsonValueKind.Array)
            {
                foreach (var hint in hintValues.EnumerateArray())
                    if (hint.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Object)
                        foreach (var parameter in parameters.EnumerateObject())
                            if (parameter.Value.ValueKind == JsonValueKind.String && parameter.Value.GetString() is { } value && value.Length <= 4096)
                                values.Add(value);
            }
            foreach (var item in cases)
            {
                if (selected[item.Subject.CaseId].Count >= MaximumRecommendedChunksPerCase || !values.Overlaps(selectorValues[item.Subject.CaseId])) continue;
                selected[item.Subject.CaseId].Add(new(chunkId, chunkFamily, chunkType,
                    $"chunks/{chunkFamily}/{Slug(chunkId)}.md", "Matches a retained case supporting ID or exact retrieval parameter."));
            }
        }
        var projected = selected.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyList<RecommendedChunk>)pair.Value.OrderBy(value => ChunkRank(value.ChunkType)).ThenBy(value => value.ChunkId, StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
        return new(new("validated", "tracemap-evidence-docs-export", RelativeLocator(outputRoot, root), schemaVersion,
            projected.Values.Sum(value => value.Count), "Matched only by retained supporting IDs or exact retrieval-hint parameters; no semantic inference was added."), projected);
    }

    private static string BoundedCorpusFile(string root, string name, long maximumBytes)
    {
        var path = Path.GetFullPath(Path.Combine(root, name));
        if (!string.Equals(Path.GetDirectoryName(path), root, PathComparison)) throw new InvalidDataException("AgentHandoffCorpusUnavailable");
        var info = new FileInfo(path);
        if (!info.Exists || info.Length is < 1 || info.Length > maximumBytes) throw new InvalidDataException("AgentHandoffCorpusUnavailable");
        return path;
    }

    private static string? RelativeLocator(string outputRoot, string target)
    {
        var value = Path.GetRelativePath(outputRoot, target).Replace('\\', '/');
        return !Path.IsPathRooted(value) && value.Length <= 2048 && !value.Contains('\r') && !value.Contains('\n') ? value : null;
    }

    private static string Slug(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.ToLowerInvariant()) builder.Append(char.IsAsciiLetterOrDigit(character) ? character : '-');
        return Regex.Replace(builder.ToString(), "-+", "-").Trim('-');
    }

    private static int ChunkRank(string value) => value switch
    {
        "surface" => 0,
        "event-chain" => 1,
        "downstream-boundary" => 2,
        "gap" => 3,
        _ => 4
    };

    private static bool IsDatabaseShaped(string? value) => value is not null &&
        (value.Contains("System.Data", StringComparison.Ordinal)
         || value.Contains("SqlCommand", StringComparison.Ordinal)
         || value.Contains("DataAdapter", StringComparison.Ordinal)
         || value.Contains("ExecuteReader", StringComparison.Ordinal)
         || value.Contains("ExecuteNonQuery", StringComparison.Ordinal));

    private static string RequiredBoundedString(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) ? RequiredBoundedString(value) : throw new InvalidDataException("AgentHandoffSchemaMismatch");

    private static string RequiredBoundedString(JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return !string.IsNullOrWhiteSpace(text) && text.Length <= 4096 ? text : throw new InvalidDataException("AgentHandoffSchemaMismatch");
    }

    private static string? OptionalBoundedString(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return !string.IsNullOrWhiteSpace(text) && text.Length <= 4096 ? text : null;
    }

    private static int? OptionalPositiveInt(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number > 0 ? number : null;

    private static IReadOnlyList<string> StringArray(JsonElement parent, string property, int maximum)
    {
        if (!parent.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array) return [];
        var values = value.EnumerateArray().Select(OptionalBoundedStringValue).Where(item => item is not null).Cast<string>()
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(maximum + 1).ToArray();
        if (values.Length > maximum) throw new InvalidDataException("AgentHandoffInputLimit");
        return values;
    }

    private static string? OptionalBoundedStringValue(JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return !string.IsNullOrWhiteSpace(text) && text.Length <= 4096 ? text : null;
    }

    private sealed record EvidenceStoreSummary(
        string Availability,
        string Kind,
        string? RelativeLocator,
        string ScanId,
        string CommitSha,
        string? Repository,
        string? ScannerVersion,
        string? AnalysisLevel,
        string Guidance);

    private sealed record CorpusSummary(
        string Availability,
        string Kind,
        string? RelativeLocator,
        string? SchemaVersion,
        int RecommendedChunkCount,
        string Guidance);

    private sealed record RecommendedChunk(string ChunkId, string ChunkFamily, string ChunkType, string Locator, string Reason);

    private sealed record CorpusProjection(CorpusSummary Summary, IReadOnlyDictionary<string, IReadOnlyList<RecommendedChunk>> ChunksByCase);
}
