using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TraceMap.Reporting;

public sealed record EvidenceQueryRecipeCatalog(
    string SchemaVersion,
    string Generator,
    string RuleId,
    IReadOnlyList<EvidenceQueryRecipe> Recipes,
    IReadOnlyList<string> Limitations);

public sealed record EvidenceQueryRecipe(
    string RecipeId,
    string Title,
    string Purpose,
    string RuleId,
    string EvidenceTier,
    IReadOnlyList<string> SupportedInputKinds,
    IReadOnlyList<EvidenceQueryParameter> Parameters,
    IReadOnlyList<string> ResultFields,
    IReadOnlyDictionary<string, string> SqlByInputKind,
    IReadOnlyList<string> EvidenceRequirements,
    IReadOnlyList<string> Limitations);

public sealed record EvidenceQueryParameter(
    string Name,
    string Type,
    IReadOnlyList<string> RequiredForInputKinds,
    string Description);

public sealed record EvidenceDocRetrievalHint(
    string RecipeId,
    string InputKind,
    string RuleId,
    string EvidenceTier,
    string Reason,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<string> SupportingIds);

public static class EvidenceDocsQueryRecipes
{
    public const string SchemaVersion = "tracemap-evidence-query-recipes.v1";
    public const string RecipeRuleId = "docs-export.query-recipe.v1";
    public const string HintRuleId = "docs-export.retrieval-hint.v1";
    private static readonly Regex MutationPattern = new(@"\b(insert|update|delete|merge|replace|drop|alter|create|attach|detach|vacuum|reindex|pragma|transaction|commit|rollback)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
    private static readonly Regex TablePattern = new(@"\b(?:from|join)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
    private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "facts", "combined_facts", "call_edges", "combined_call_edges"
    };

    public static EvidenceQueryRecipeCatalog Build()
    {
        var recipes = new[]
        {
            Recipe("fact-by-id", "Exact evidence fact", "Retrieve one cited fact by stable evidence ID.",
                [Text("fact_id", "Stable fact or combined-fact ID."), Limit()],
                SingleFacts("where fact_id = $fact_id"), CombinedFacts("where combined_fact_id = $fact_id or original_fact_id = $fact_id"),
                ["An exact ID match does not prove that related evidence is complete."]),
            Recipe("facts-by-file-span", "Evidence overlapping a file span", "Retrieve retained facts whose spans overlap a cited repository-relative span.",
                [Text("file_path", "Repository-relative file path."), Integer("start_line", "Inclusive starting line."), Integer("end_line", "Inclusive ending line."), Limit()],
                SingleFacts("where file_path = $file_path and end_line >= $start_line and start_line <= $end_line"),
                CombinedFacts("where file_path = $file_path and end_line >= $start_line and start_line <= $end_line"),
                ["Span overlap is static location evidence and does not prove runtime execution or semantic relationship."]),
            Recipe("facts-by-symbol", "Evidence mentioning a symbol", "Retrieve facts that retain an exact source, target, or contract symbol.",
                [Text("symbol", "Exact retained symbol or contract identity."), Limit()],
                SingleFacts("where source_symbol = $symbol or target_symbol = $symbol or contract_element = $symbol"),
                CombinedFacts("where source_symbol = $symbol or target_symbol = $symbol or contract_element = $symbol"),
                ["Exact retained symbol equality can miss aliases, dynamic dispatch, generated code, and unavailable semantic bindings."]),
            Recipe("calls-from-handler", "Calls retained from a handler", "Retrieve static call edges whose caller equals a retained handler symbol.",
                [Text("handler_symbol", "Exact retained handler symbol."), Limit()],
                SingleCalls("where caller_symbol = $handler_symbol"), CombinedCalls("where caller_symbol = $handler_symbol"),
                ["Retained call edges do not prove runtime reachability, branch feasibility, or complete transitive closure."]),
            Recipe("callers-of-callee", "Reverse callers of a callee", "Retrieve static call edges whose callee equals a retained target symbol.",
                [Text("callee_symbol", "Exact retained callee symbol."), Limit()],
                SingleCalls("where callee_symbol = $callee_symbol"), CombinedCalls("where callee_symbol = $callee_symbol"),
                ["Reverse caller evidence is coverage-relative and does not prove runtime invocation or absence of other callers."]),
            Recipe("webforms-surface-facts", "Facts associated with a Web Forms surface", "Retrieve facts whose safe packet identity metadata names a Web Forms surface.",
                [Text("surface_id", "Stable Web Forms surface ID."), Limit()],
                SingleFacts("where json_extract(properties_json, '$.surfaceIdentity') = $surface_id"),
                CombinedFacts("where json_extract(properties_json, '$.surfaceIdentity') = $surface_id"),
                ["Only facts retaining the supported surfaceIdentity metadata key are returned."]),
            Recipe("database-evidence-by-handler", "Database-shaped evidence retained for a handler", "Retrieve database and query-shaped facts whose retained source symbol equals a handler.",
                [Text("handler_symbol", "Exact retained handler or method symbol."), Limit()],
                SingleFacts("where source_symbol = $handler_symbol and fact_type in ('SqlCommandDetected','ObjectCreated','MethodInvoked','CallEdge','ArgumentPassed','PropertyAccessed','DatabaseOperationCandidate','SqlTextUsed','QueryPatternDetected','DapperCallDetected')"),
                CombinedFacts("where source_symbol = $handler_symbol and fact_type in ('SqlCommandDetected','ObjectCreated','MethodInvoked','CallEdge','ArgumentPassed','PropertyAccessed','DatabaseOperationCandidate','SqlTextUsed','QueryPatternDetected','DapperCallDetected')"),
                ["Database-shaped static evidence does not prove SQL execution, live schema existence, stored-procedure selection, or production use."]),
            Recipe("stored-procedure-candidate-context", "Stored-procedure candidate context", "Retrieve command construction, command-type, invocation, and argument facts retained for a method that may contain a stored-procedure-shaped operation.",
                [Text("method_symbol", "Exact retained method symbol containing the candidate operation."), Limit()],
                SingleFacts("where source_symbol = $method_symbol and fact_type in ('SqlCommandDetected','ObjectCreated','MethodInvoked','CallEdge','ArgumentPassed','PropertyAccessed','DatabaseOperationCandidate')"),
                CombinedFacts("where source_symbol = $method_symbol and fact_type in ('SqlCommandDetected','ObjectCreated','MethodInvoked','CallEdge','ArgumentPassed','PropertyAccessed','DatabaseOperationCandidate')"),
                ["Co-occurring command, property, invocation, and argument facts do not by themselves establish object identity, stored-procedure assignment, parameter completeness, execution, or success."]),
            Recipe("boundary-supporting-facts", "Evidence supporting a downstream boundary", "Retrieve the retained terminal evidence row cited by a downstream boundary.",
                [Text("terminal_evidence_id", "Exact retained terminal evidence ID cited by the boundary."), Limit()],
                SingleFacts("where fact_id = $terminal_evidence_id"),
                CombinedFacts("where combined_fact_id = $terminal_evidence_id or original_fact_id = $terminal_evidence_id"),
                ["A terminal evidence match does not reconstruct ordering or prove a runtime call path; packet supporting IDs remain authoritative."]),
            Recipe("gap-neighborhood", "Evidence near a gap span", "Retrieve retained facts overlapping a gap's repository-relative source span.",
                [Text("file_path", "Repository-relative file path."), Integer("start_line", "Inclusive gap starting line."), Integer("end_line", "Inclusive gap ending line."), Limit()],
                SingleFacts("where file_path = $file_path and end_line >= $start_line and start_line <= $end_line"),
                CombinedFacts("where file_path = $file_path and end_line >= $start_line and start_line <= $end_line"),
                ["Nearby evidence is review context only and does not close, explain, or negate the gap."])
        }.OrderBy(recipe => recipe.RecipeId, StringComparer.Ordinal).ToArray();

        var catalog = new EvidenceQueryRecipeCatalog(
            SchemaVersion,
            EvidenceDocsExporter.GeneratorName,
            RecipeRuleId,
            recipes,
            [
                "Recipes query only retained static TraceMap evidence and do not add findings or prove runtime behavior.",
                "Callers must preserve rule IDs, evidence tiers, commit identity, file spans, coverage context, and explicit gaps when interpreting results.",
                "The catalog contains no application SQL, source snippets, private prompts, business intent, target architecture, or code-generation instructions."
            ]);
        Validate(catalog);
        return catalog;
    }

    public static void Validate(EvidenceQueryRecipeCatalog catalog)
    {
        if (catalog.SchemaVersion != SchemaVersion || catalog.Recipes.Count == 0
            || catalog.Recipes.Select(recipe => recipe.RecipeId).Distinct(StringComparer.Ordinal).Count() != catalog.Recipes.Count)
        {
            throw new InvalidOperationException("EvidenceQueryRecipeCatalogInvalid");
        }

        foreach (var recipe in catalog.Recipes)
        {
            if (recipe.RuleId != RecipeRuleId || recipe.EvidenceTier != "Tier2Structural"
                || recipe.SupportedInputKinds.Count != 2
                || recipe.SqlByInputKind.Count != 2
                || !recipe.SupportedInputKinds.Contains("single-index", StringComparer.Ordinal)
                || !recipe.SupportedInputKinds.Contains("combined-index", StringComparer.Ordinal)
                || !recipe.SqlByInputKind.ContainsKey("single-index")
                || !recipe.SqlByInputKind.ContainsKey("combined-index")
                || recipe.Parameters.Select(parameter => parameter.Name).Distinct(StringComparer.Ordinal).Count() != recipe.Parameters.Count
                || recipe.Parameters.Any(parameter => parameter.RequiredForInputKinds.Count == 0
                    || parameter.RequiredForInputKinds.Any(kind => !recipe.SupportedInputKinds.Contains(kind, StringComparer.Ordinal)))
                || recipe.Parameters.SingleOrDefault(parameter => parameter.Name == "limit") is not { Type: "integer" } limitParameter
                || !recipe.SupportedInputKinds.All(kind => limitParameter.RequiredForInputKinds.Contains(kind, StringComparer.Ordinal)))
            {
                throw new InvalidOperationException("EvidenceQueryRecipeContractInvalid");
            }

            foreach (var (inputKind, sql) in recipe.SqlByInputKind)
            {
                var normalized = sql.Trim();
                if (!recipe.SupportedInputKinds.Contains(inputKind, StringComparer.Ordinal)
                    || !normalized.StartsWith("select ", StringComparison.OrdinalIgnoreCase)
                    || normalized.Count(character => character == ';') != 1
                    || !normalized.EndsWith(';')
                    || MutationPattern.IsMatch(normalized)
                    || !normalized.Contains("limit $limit", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("EvidenceQueryRecipeSqlUnsafe");
                }

                var tables = TablePattern.Matches(normalized).Select(match => match.Groups[1].Value).ToArray();
                if (tables.Length == 0 || tables.Any(table => !AllowedTables.Contains(table)))
                {
                    throw new InvalidOperationException("EvidenceQueryRecipeSqlTableUnsupported");
                }

                foreach (var parameter in recipe.Parameters.Where(parameter => parameter.RequiredForInputKinds.Contains(inputKind, StringComparer.Ordinal)))
                {
                    if (!normalized.Contains($"${parameter.Name}", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("EvidenceQueryRecipeParameterUnused");
                    }
                }
            }
        }
    }

    public static string RenderJson(EvidenceQueryRecipeCatalog catalog)
        => JsonSerializer.Serialize(catalog, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }) + "\n";

    public static string RenderMarkdown(EvidenceQueryRecipeCatalog catalog)
    {
        var builder = new StringBuilder()
            .AppendLine("---")
            .AppendLine("tracemap_generated: true")
            .AppendLine($"tracemap_export_schema: {EvidenceDocsExporter.SchemaVersion}")
            .AppendLine($"tracemap_generator: {EvidenceDocsExporter.GeneratorName}")
            .AppendLine("tracemap_content_sha256: ")
            .AppendLine("summary_kind: query-recipes")
            .AppendLine("claim_level: hidden")
            .AppendLine("source_labels:")
            .AppendLine("---")
            .AppendLine()
            .AppendLine("# TraceMap Evidence Query Recipes")
            .AppendLine()
            .AppendLine("These deterministic read-only recipes query TraceMap-owned evidence tables. They do not query an application database and do not add conclusions.")
            .AppendLine();
        foreach (var recipe in catalog.Recipes)
        {
            builder.AppendLine($"## `{recipe.RecipeId}` — {recipe.Title}")
                .AppendLine()
                .AppendLine(recipe.Purpose)
                .AppendLine()
                .AppendLine($"Rule: `{recipe.RuleId}`. Evidence tier: `{recipe.EvidenceTier}`.")
                .AppendLine()
                .AppendLine("Parameters:");
            foreach (var parameter in recipe.Parameters)
            {
                builder.AppendLine($"- `{parameter.Name}` (`{parameter.Type}`, required for `{string.Join("`, `", parameter.RequiredForInputKinds.OrderBy(value => value, StringComparer.Ordinal))}`): {parameter.Description}");
            }
            foreach (var inputKind in recipe.SupportedInputKinds.OrderBy(value => value, StringComparer.Ordinal))
            {
                builder.AppendLine().AppendLine($"### {inputKind}").AppendLine().AppendLine("```sql")
                    .AppendLine(recipe.SqlByInputKind[inputKind].Trim()).AppendLine("```");
            }
            builder.AppendLine().AppendLine("Limitations:");
            foreach (var limitation in recipe.Limitations) builder.AppendLine($"- {limitation}");
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static EvidenceQueryRecipe Recipe(string id, string title, string purpose, IReadOnlyList<EvidenceQueryParameter> parameters, string singleSql, string combinedSql, IReadOnlyList<string> limitations)
        => new(
            id,
            title,
            purpose,
            RecipeRuleId,
            "Tier2Structural",
            ["combined-index", "single-index"],
            parameters.Concat([SourceIndex()]).OrderBy(parameter => parameter.Name, StringComparer.Ordinal).ToArray(),
            ["evidence_id", "source_index_id", "commit_sha", "fact_type", "rule_id", "evidence_tier", "source_symbol", "target_symbol", "contract_element", "file_path", "start_line", "end_line"],
            new SortedDictionary<string, string>(StringComparer.Ordinal) { ["combined-index"] = combinedSql, ["single-index"] = singleSql },
            ["Treat every returned row as static evidence carrying its own rule, tier, commit, and source span.", "Preserve empty results as coverage-relative no-match outcomes rather than absence findings."],
            limitations);

    private static EvidenceQueryParameter Text(string name, string description) => new(name, "string", ["combined-index", "single-index"], description);
    private static EvidenceQueryParameter Integer(string name, string description) => new(name, "integer", ["combined-index", "single-index"], description);
    private static EvidenceQueryParameter Limit() => new("limit", "integer", ["combined-index", "single-index"], "Maximum rows; callers should use a positive bounded value.");
    private static EvidenceQueryParameter SourceIndex() => new("source_index_id", "string", ["combined-index"], "Owning combined-index source ID retained by the evidence chunk.");

    private static string SingleFacts(string where) => $"""
        select fact_id as evidence_id, null as source_index_id, commit_sha, fact_type, rule_id, evidence_tier,
               source_symbol, target_symbol, contract_element, file_path, start_line, end_line
        from facts
        {where}
        order by file_path, start_line, fact_id
        limit $limit;
        """;

    private static string CombinedFacts(string where) => $"""
        select combined_fact_id as evidence_id, source_index_id, commit_sha, fact_type, rule_id, evidence_tier,
               source_symbol, target_symbol, contract_element, file_path, start_line, end_line
        from combined_facts
        {ParenthesizedWhere(where)}
          and source_index_id = $source_index_id
        order by source_index_id, file_path, start_line, combined_fact_id
        limit $limit;
        """;

    private static string SingleCalls(string where) => $"""
        select fact_id as evidence_id, null as source_index_id, commit_sha, 'CallEdge' as fact_type, rule_id, evidence_tier,
               caller_symbol as source_symbol, callee_symbol as target_symbol, call_kind as contract_element, file_path, start_line, end_line
        from call_edges
        {where}
        order by file_path, start_line, fact_id
        limit $limit;
        """;

    private static string CombinedCalls(string where) => $"""
        select combined_fact_id as evidence_id, source_index_id, commit_sha, 'CallEdge' as fact_type, rule_id, evidence_tier,
               caller_symbol as source_symbol, callee_symbol as target_symbol, call_kind as contract_element, file_path, start_line, end_line
        from combined_call_edges
        {ParenthesizedWhere(where)}
          and source_index_id = $source_index_id
        order by source_index_id, file_path, start_line, combined_fact_id
        limit $limit;
        """;

    private static string ParenthesizedWhere(string where)
    {
        const string prefix = "where ";
        if (!where.StartsWith(prefix, StringComparison.Ordinal) || where.Length == prefix.Length)
            throw new InvalidOperationException("EvidenceQueryRecipeWhereInvalid");
        return $"where ({where[prefix.Length..]})";
    }
}

public static partial class EvidenceDocsExporter
{
    private const int MaxRetrievalHintsPerChunk = 8;

    private static IReadOnlyList<EvidenceDocChunk> AddRetrievalHints(IReadOnlyList<EvidenceDocChunk> chunks)
    {
        var withHints = chunks.Select(chunk => WithRetrievalHints(chunk, GenericRetrievalHints(chunk))).ToArray();
        ValidateRetrievalHints(withHints);
        return withHints;
    }

    private static EvidenceDocChunk WithRetrievalHints(EvidenceDocChunk chunk, IEnumerable<EvidenceDocRetrievalHint> hints)
    {
        var merged = chunk.RetrievalHints.Concat(hints)
            .Select(hint => ScopeHint(chunk, hint))
            .OfType<EvidenceDocRetrievalHint>()
            .GroupBy(HintIdentity, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(hint => hint.RecipeId, StringComparer.Ordinal)
            .ThenBy(HintIdentity, StringComparer.Ordinal)
            .Take(MaxRetrievalHintsPerChunk)
            .ToArray();
        return chunk with { RetrievalHints = merged };
    }

    private static IReadOnlyList<EvidenceDocRetrievalHint> GenericRetrievalHints(EvidenceDocChunk chunk)
    {
        var hints = new List<EvidenceDocRetrievalHint>();
        foreach (var factId in chunk.Citations.SelectMany(citation => citation.SupportingFactIds)
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal)
                     .Take(2))
        {
            hints.Add(Hint(
                "fact-by-id",
                "Retrieve the exact cited evidence row and preserve its rule, tier, commit, and span.",
                [("fact_id", factId), ("limit", "1")],
                [factId]));
        }

        foreach (var citation in chunk.Citations
                     .Where(citation => citation.FilePath is not null && citation.StartLine is not null && citation.EndLine is not null)
                     .GroupBy(citation => $"{citation.FilePath}|{citation.StartLine}|{citation.EndLine}", StringComparer.Ordinal)
                     .Select(group => group.First())
                     .OrderBy(citation => citation.FilePath, StringComparer.Ordinal)
                     .ThenBy(citation => citation.StartLine)
                     .Take(2))
        {
            hints.Add(Hint(
                "facts-by-file-span",
                "Retrieve other retained evidence overlapping this cited source span.",
                [("file_path", citation.FilePath!), ("start_line", citation.StartLine!.Value.ToString()), ("end_line", citation.EndLine!.Value.ToString()), ("limit", "100")],
                [citation.CitationId]));
        }

        return hints;
    }

    private static EvidenceDocRetrievalHint Hint(
        string recipeId,
        string reason,
        IReadOnlyList<(string Name, string Value)> parameters,
        IReadOnlyList<string> supportingIds)
        => new(
            recipeId,
            string.Empty,
            EvidenceDocsQueryRecipes.HintRuleId,
            "Tier2Structural",
            reason,
            new SortedDictionary<string, string>(parameters.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal), StringComparer.Ordinal),
            supportingIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());

    private static string HintIdentity(EvidenceDocRetrievalHint hint)
        => $"{hint.InputKind}|{hint.RecipeId}|{string.Join('|', hint.Parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"))}";

    private static EvidenceDocRetrievalHint? ScopeHint(EvidenceDocChunk chunk, EvidenceDocRetrievalHint hint)
    {
        var sources = chunk.SourceRefs.DistinctBy(source => source.SourceId).ToArray();
        if (sources.Length != 1)
        {
            return null;
        }

        var inputKind = sources[0].SourceScope switch
        {
            "single-source" => "single-index",
            "combined-source" => "combined-index",
            _ => null
        };
        if (inputKind is null)
        {
            return null;
        }

        var parameters = new SortedDictionary<string, string>(
            hint.Parameters.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
        if (inputKind == "combined-index")
        {
            parameters["source_index_id"] = sources[0].SourceId;
        }
        else
        {
            parameters.Remove("source_index_id");
            foreach (var name in new[] { "fact_id", "terminal_evidence_id" })
            {
                if (parameters.TryGetValue(name, out var value) && value.StartsWith("single:", StringComparison.Ordinal))
                {
                    parameters[name] = value["single:".Length..];
                }
            }
        }

        return hint with { InputKind = inputKind, Parameters = parameters };
    }

    private static void ValidateRetrievalHints(IReadOnlyList<EvidenceDocChunk> chunks)
    {
        var recipes = EvidenceDocsQueryRecipes.Build().Recipes.ToDictionary(recipe => recipe.RecipeId, StringComparer.Ordinal);
        foreach (var hint in chunks.SelectMany(chunk => chunk.RetrievalHints))
        {
            if (hint.RuleId != EvidenceDocsQueryRecipes.HintRuleId
                || hint.EvidenceTier != "Tier2Structural"
                || !recipes.TryGetValue(hint.RecipeId, out var recipe)
                || !recipe.SupportedInputKinds.Contains(hint.InputKind, StringComparer.Ordinal)
                || hint.SupportingIds.Count == 0)
            {
                throw new InvalidOperationException("EvidenceDocRetrievalHintInvalid");
            }

            var expectedParameters = recipe.Parameters
                .Where(parameter => parameter.RequiredForInputKinds.Contains(hint.InputKind, StringComparer.Ordinal))
                .Select(parameter => parameter.Name)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var actualParameters = hint.Parameters.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!expectedParameters.SequenceEqual(actualParameters, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("EvidenceDocRetrievalHintParameterInvalid");
            }

            foreach (var parameter in recipe.Parameters.Where(parameter => parameter.RequiredForInputKinds.Contains(hint.InputKind, StringComparer.Ordinal)))
            {
                var value = hint.Parameters[parameter.Name];
                if (string.IsNullOrWhiteSpace(value)
                    || parameter.Type == "integer" && (!int.TryParse(value, out var parsed) || parsed < 1)
                    || parameter.Name == "limit" && int.Parse(value) > 1000)
                {
                    throw new InvalidOperationException("EvidenceDocRetrievalHintValueInvalid");
                }
            }
        }
    }
}
