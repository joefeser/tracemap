using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace TraceMap.Core;

public static class LegacyBatchDataMovementExtractor
{
    private const string ExtractorId = "LegacyBatchDataMovementExtractor";
    private const int RelatedFactLimit = 50;
    private const string Limitations = "Static candidate evidence only; scheduling, execution, successful data movement, completeness, retries, idempotency, checkpoint effectiveness, transaction outcome, monitoring, target state, and production use are not proven.";

    private static readonly HashSet<string> CSharpKinds = new(StringComparer.Ordinal)
    {
        "CSharp", "WebFormsCodeBehind"
    };

    private static readonly HashSet<string> MessageFactTypes = new(StringComparer.Ordinal)
    {
        FactTypes.MessagePublisherSurface, FactTypes.MessageConsumerSurface, FactTypes.MessageBindingDeclared
    };

    private static readonly HashSet<string> ExternalFactTypes = new(StringComparer.Ordinal)
    {
        FactTypes.HttpCallDetected,
        FactTypes.WcfServiceReferenceMapping,
        FactTypes.AsmxServiceReferenceMapping,
        FactTypes.AsmxClientOperationDeclared,
        FactTypes.RemotingClientActivationDeclared
    };

    private static readonly HashSet<string> FileReadMethods = new(StringComparer.Ordinal)
    {
        "EnumerateFiles", "GetFiles", "OpenRead", "ReadAllBytes", "ReadAllLines", "ReadAllText", "ReadLines"
    };

    private static readonly HashSet<string> FileWriteMethods = new(StringComparer.Ordinal)
    {
        "AppendAllLines", "AppendAllText", "Copy", "Create", "Move", "OpenWrite", "Replace", "WriteAllBytes", "WriteAllLines", "WriteAllText"
    };

    private static readonly HashSet<string> StoredProcedureExecuteMethods = new(StringComparer.Ordinal)
    {
        "ExecuteNonQuery", "ExecuteNonQueryAsync", "ExecuteReader", "ExecuteReaderAsync", "ExecuteScalar", "ExecuteScalarAsync"
    };

    private static readonly HashSet<string> RetryMethods = new(StringComparer.Ordinal)
    {
        "ExecuteWithRetry", "ExecuteWithRetryAsync", "Retry", "RetryAsync", "WaitAndRetry", "WaitAndRetryAsync"
    };

    private static readonly HashSet<string> CheckpointMethods = new(StringComparer.Ordinal)
    {
        "Acknowledge", "AcknowledgeAsync", "Checkpoint", "CheckpointAsync", "CommitOffset", "SaveCheckpoint", "SaveCheckpointAsync"
    };

    private static readonly HashSet<string> TelemetryMethods = new(StringComparer.Ordinal)
    {
        "LogCritical", "LogDebug", "LogError", "LogInformation", "LogTrace", "LogWarning", "Record", "StartActivity", "TrackEvent", "TrackException", "TrackMetric"
    };

    public static IReadOnlyList<CodeFact> Extract(
        string repoPath,
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<CodeFact> existingFacts)
    {
        if (!HasWebFormsInventory(inventory))
        {
            return [];
        }

        var observations = new List<BatchObservation>();
        var gaps = new List<PendingGap>();

        AddExistingSemanticObservations(existingFacts, observations);
        AddMessageObservations(existingFacts, observations);
        AddConfigurationObservations(existingFacts, observations);
        AddSsisObservations(inventory, observations);

        foreach (var item in inventory.Where(item => CSharpKinds.Contains(item.Kind)).OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            ExtractCSharpFile(repoPath, item, existingFacts, observations, gaps);
        }

        var factById = existingFacts
            .GroupBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var projectPathsByFile = existingFacts
            .Where(fact => !string.IsNullOrWhiteSpace(fact.ProjectPath))
            .GroupBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(fact => fact.ProjectPath!).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var relatedFactsByFile = existingFacts
            .Where(fact => fact.FactType == FactTypes.DatabaseOperationCandidate || MessageFactTypes.Contains(fact.FactType) || ExternalFactTypes.Contains(fact.FactType))
            .GroupBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

        var facts = new List<CodeFact>();
        foreach (var observation in observations
                     .OrderBy(item => item.Evidence.FilePath, StringComparer.Ordinal)
                     .ThenBy(item => item.Evidence.StartLine)
                     .ThenBy(item => item.SurfaceKind, StringComparer.Ordinal)
                     .ThenBy(item => item.Mechanism, StringComparer.Ordinal)
                     .ThenBy(item => item.OwnerMember, StringComparer.Ordinal))
        {
            var related = RelatedFacts(observation, relatedFactsByFile.GetValueOrDefault(observation.Evidence.FilePath) ?? []);
            var supporting = observation.SupportingFactIds
                .Concat(related.Select(fact => fact.FactId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .Take(RelatedFactLimit)
                .ToArray();
            var projectPaths = observation.ProjectPath is { Length: > 0 }
                ? [observation.ProjectPath]
                : supporting
                    .Select(id => factById.GetValueOrDefault(id)?.ProjectPath)
                    .Concat(projectPathsByFile.GetValueOrDefault(observation.Evidence.FilePath) ?? [])
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
            var projectPath = projectPaths.Length == 1 ? projectPaths[0] : null;

            var properties = new SortedDictionary<string, string>(observation.Properties, StringComparer.Ordinal)
            {
                ["coverageLabel"] = Coverage(observation.EvidenceTier),
                ["limitations"] = Limitations,
                ["mechanism"] = observation.Mechanism,
                ["operationKind"] = observation.OperationKind,
                ["ownerStatus"] = observation.OwnerMember is null ? "file-level" : "member-declared",
                ["projectResolution"] = projectPaths.Length switch { 0 => "unavailable", 1 => "resolved", _ => "ambiguous" },
                ["surfaceKind"] = observation.SurfaceKind
            };
            AddIfPresent(properties, "ownerMember", observation.OwnerMember);
            AddIfPresent(properties, "ownerType", observation.OwnerType);
            AddIfPresent(properties, "supportingFactIds", supporting.Length == 0 ? null : string.Join(",", supporting));
            AddRelated(properties, "databaseOperationFactIds", related.Where(fact => fact.FactType == FactTypes.DatabaseOperationCandidate));
            AddRelated(properties, "messageBoundaryFactIds", related.Where(fact => MessageFactTypes.Contains(fact.FactType)));
            AddRelated(properties, "externalBoundaryFactIds", related.Where(fact => ExternalFactTypes.Contains(fact.FactType)));

            var evidence = new EvidenceSpan(
                observation.Evidence.FilePath,
                observation.Evidence.StartLine,
                observation.Evidence.EndLine,
                observation.Evidence.SnippetHash,
                ExtractorId,
                ScannerVersions.LegacyBatchDataMovementExtractor);
            var fact = FactFactory.Create(
                manifest,
                FactTypes.LegacyBatchDataMovementDeclared,
                RuleIds.LegacyWebFormsBatchDataMovement,
                observation.EvidenceTier,
                evidence,
                projectPath,
                observation.OwnerMember ?? observation.OwnerType ?? observation.Evidence.FilePath,
                observation.SurfaceKind,
                observation.Mechanism,
                properties);
            facts.Add(fact);

            if (projectPaths.Length == 0)
            {
                facts.Add(CreateGap(manifest, fact, "BatchOwnerProjectUnavailable", "No unique owning project was proven for this batch/data-movement candidate."));
            }
            else if (projectPaths.Length > 1)
            {
                facts.Add(CreateGap(manifest, fact, "AmbiguousBatchOwnerProject", "Multiple owning projects were observed for this source path; TraceMap did not select one."));
            }

            if (related.Count > RelatedFactLimit)
            {
                facts.Add(CreateGap(manifest, fact, "BatchCompositionLimitReached", "Related evidence exceeded the deterministic per-candidate composition limit."));
            }

            foreach (var gap in observation.Gaps)
            {
                facts.Add(CreateGap(manifest, fact, gap.Kind, gap.Message));
            }
        }

        foreach (var gap in gaps)
        {
            facts.Add(CreateGap(manifest, gap));
        }

        if (manifest.BuildStatus != "Succeeded" && observations.Any(observation => observation.EvidenceTier != EvidenceTiers.Tier1Semantic))
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AnalysisGap,
                RuleIds.LegacyWebFormsBatchDataMovement,
                EvidenceTiers.Tier4Unknown,
                Evidence(".", 1, 1),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["coverage"] = "reduced",
                    ["gapKind"] = "ReducedBatchSemanticCoverage",
                    ["message"] = "Batch/data-movement inventory includes syntax or structural fallback because semantic coverage is reduced.",
                    ["limitations"] = Limitations
                }));
        }

        return facts
            .GroupBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddExistingSemanticObservations(IReadOnlyList<CodeFact> existingFacts, List<BatchObservation> observations)
    {
        foreach (var fact in existingFacts.Where(fact => fact.FactType == FactTypes.MethodInvoked && fact.EvidenceTier == EvidenceTiers.Tier1Semantic))
        {
            var target = fact.TargetSymbol ?? fact.Properties.GetValueOrDefault("methodSymbol") ?? string.Empty;
            var method = MethodName(target);
            if (IsSystemIoTarget(target) && (FileReadMethods.Contains(method) || FileWriteMethods.Contains(method)))
            {
                observations.Add(FromFact(
                    fact,
                    "file-data-movement",
                    "compiler-resolved-system-io-call",
                    FileReadMethods.Contains(method) ? "read" : "write",
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["apiClassification"] = method
                    }));
            }
            else if (IsSqlBulkCopyTarget(target) && method is "WriteToServer" or "WriteToServerAsync")
            {
                observations.Add(FromFact(fact, "bulk-copy", "compiler-resolved-sql-bulk-copy", "write"));
            }
        }

        foreach (var fact in existingFacts.Where(fact => fact.FactType == FactTypes.ObjectCreated && fact.EvidenceTier == EvidenceTiers.Tier1Semantic))
        {
            var target = fact.TargetSymbol ?? string.Empty;
            if (target.Contains("System.IO.FileSystemWatcher", StringComparison.Ordinal))
            {
                observations.Add(FromFact(fact, "file-data-movement", "compiler-resolved-file-system-watcher", "watch"));
            }
        }
    }

    private static void AddMessageObservations(IReadOnlyList<CodeFact> existingFacts, List<BatchObservation> observations)
    {
        foreach (var fact in existingFacts.Where(fact => MessageFactTypes.Contains(fact.FactType)))
        {
            var operation = fact.Properties.GetValueOrDefault("operationDirection")
                ?? fact.Properties.GetValueOrDefault("operationKind")
                ?? "declare";
            var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
            AddIfPresent(properties, "messageSurfaceKind", fact.Properties.GetValueOrDefault("surfaceKind"));
            observations.Add(FromFact(fact, "message-data-movement", "existing-message-surface", operation, properties));
        }
    }

    private static void AddConfigurationObservations(IReadOnlyList<CodeFact> existingFacts, List<BatchObservation> observations)
    {
        foreach (var fact in existingFacts.Where(fact => fact.FactType == FactTypes.ConfigKeyDeclared))
        {
            var key = fact.Properties.GetValueOrDefault("keyPath") ?? fact.TargetSymbol;
            var classification = ClassifyIntegrationConfigKey(key);
            if (classification is null)
            {
                continue;
            }

            observations.Add(new(
                fact.Evidence,
                fact.EvidenceTier,
                "configuration-integration",
                "config-key-shape",
                "declare",
                null,
                null,
                fact.ProjectPath,
                [fact.FactId],
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["configurationKind"] = classification,
                    ["configurationKeyHash"] = FactFactory.Hash($"batch-config|{key}", 32)
                },
                []));
        }
    }

    private static void AddSsisObservations(IReadOnlyList<FileInventoryItem> inventory, List<BatchObservation> observations)
    {
        foreach (var item in inventory.Where(item => item.Kind == "SsisPackage").OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            observations.Add(new(
                Evidence(item.RelativePath, 1, 1),
                EvidenceTiers.Tier2Structural,
                "etl-package",
                "ssis-package-file",
                "declare",
                null,
                null,
                null,
                [],
                new SortedDictionary<string, string>(StringComparer.Ordinal),
                []));
        }
    }

    private static void ExtractCSharpFile(
        string repoPath,
        FileInventoryItem item,
        IReadOnlyList<CodeFact> existingFacts,
        List<BatchObservation> observations,
        List<PendingGap> gaps)
    {
        string text;
        try
        {
            text = File.ReadAllText(Path.Combine(repoPath, item.RelativePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            gaps.Add(new(item.RelativePath, 1, "UnreadableBatchSource", "Unable to read an in-scope C# source for batch/data-movement extraction."));
            return;
        }

        CompilationUnitSyntax root;
        try
        {
            root = CSharpSyntaxTree.ParseText(SourceText.From(text), path: item.RelativePath).GetCompilationUnitRoot();
        }
        catch (ArgumentException)
        {
            gaps.Add(new(item.RelativePath, 1, "MalformedBatchSource", "Unable to parse an in-scope C# source for batch/data-movement extraction."));
            return;
        }

        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var ownerType = QualifiedTypeName(type);
            foreach (var baseType in type.BaseList?.Types ?? [])
            {
                var baseName = NormalizeGlobalTypeName(baseType.Type.ToString());
                if (baseName == "System.ServiceProcess.ServiceBase")
                {
                    observations.Add(SyntaxObservation(item.RelativePath, baseType, "windows-service", "service-base-type", "host", null, ownerType));
                }
                else if (baseName == "Quartz.IJob")
                {
                    observations.Add(SyntaxObservation(item.RelativePath, baseType, "scheduled-task", "quartz-job-type", "execute", null, ownerType));
                }
            }
        }

        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var member = method.Identifier.ValueText;
            var ownerType = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault() is { } type ? QualifiedTypeName(type) : null;
            var signals = MemberSignals(method);
            if (member == "Main" && method.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword)))
            {
                observations.Add(SyntaxObservation(item.RelativePath, method.Identifier, "console-job", "static-main-method", "entry", member, ownerType, signals));
            }

            foreach (var attribute in method.AttributeLists.SelectMany(list => list.Attributes))
            {
                var attributeName = SimpleTypeName(attribute.Name.ToString());
                if (attributeName is not ("TimerTrigger" or "TimerTriggerAttribute"))
                {
                    continue;
                }

                var properties = new SortedDictionary<string, string>(signals, StringComparer.Ordinal);
                var localGaps = new List<GapDescriptor>();
                ClassifyScheduleReference(attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression, existingFacts, properties, localGaps);
                var supportingFactIds = properties.GetValueOrDefault("scheduleConfigFactId") is { Length: > 0 } configFactId ? new[] { configFactId } : [];
                observations.Add(SyntaxObservation(item.RelativePath, attribute, "scheduled-task", "timer-trigger-attribute", "trigger", member, ownerType, properties, localGaps, supportingFactIds));
            }

            foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var invocationName = InvocationName(invocation);
                var receiver = InvocationReceiver(invocation);
                if (invocationName == "AddOrUpdate" && NormalizeGlobalTypeName(receiver) == "Hangfire.RecurringJob")
                {
                    var properties = new SortedDictionary<string, string>(signals, StringComparer.Ordinal)
                    {
                        ["scheduleSource"] = invocation.ArgumentList.Arguments.Count == 0 ? "unavailable" : "omitted-static-or-dynamic"
                    };
                    observations.Add(SyntaxObservation(item.RelativePath, invocation, "scheduled-task", "hangfire-recurring-job", "schedule", member, ownerType, properties));
                }

                if (IsExplicitSystemIoReceiver(receiver) && (FileReadMethods.Contains(invocationName) || FileWriteMethods.Contains(invocationName))
                    && !HasSemanticFileObservation(existingFacts, item.RelativePath, invocation))
                {
                    var properties = new SortedDictionary<string, string>(signals, StringComparer.Ordinal)
                    {
                        ["apiClassification"] = invocationName
                    };
                    observations.Add(SyntaxObservation(
                        item.RelativePath,
                        invocation,
                        "file-data-movement",
                        "qualified-system-io-call",
                        FileReadMethods.Contains(invocationName) ? "read" : "write",
                        member,
                        ownerType,
                        properties));
                }
            }

            foreach (var creation in method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var typeName = NormalizeGlobalTypeName(creation.Type.ToString());
                if (typeName == "System.IO.FileSystemWatcher"
                    && !HasSemanticFileWatcherObservation(existingFacts, item.RelativePath, creation))
                {
                    observations.Add(SyntaxObservation(item.RelativePath, creation, "file-data-movement", "qualified-file-system-watcher", "watch", member, ownerType, signals));
                }
                else if (typeName is "System.Timers.Timer" or "System.Threading.Timer")
                {
                    observations.Add(SyntaxObservation(item.RelativePath, creation, "scheduled-task", "qualified-timer-construction", "trigger", member, ownerType, signals));
                }
            }

            AddStoredProcedureObservations(item.RelativePath, method, member, ownerType, signals, observations);
            AddBulkCopySyntaxFallback(item.RelativePath, method, member, ownerType, signals, existingFacts, observations);
        }
    }

    private static void AddStoredProcedureObservations(
        string filePath,
        MethodDeclarationSyntax method,
        string member,
        string? ownerType,
        IReadOnlyDictionary<string, string> signals,
        List<BatchObservation> observations)
    {
        var commandVariables = method.DescendantNodes().OfType<VariableDeclarationSyntax>()
            .Where(declaration => IsSupportedCommandType(declaration.Type.ToString()))
            .SelectMany(declaration => declaration.Variables)
            .Select(variable => variable.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var variable in method.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (variable.Initializer?.Value is ObjectCreationExpressionSyntax creation
                && IsSupportedCommandType(creation.Type.ToString()))
            {
                commandVariables.Add(variable.Identifier.ValueText);
            }
        }
        foreach (var assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is MemberAccessExpressionSyntax left
                && left.Name.Identifier.ValueText == "CommandType"
                && commandVariables.Contains(left.Expression.ToString())
                && assignment.Right.ToString().EndsWith("CommandType.StoredProcedure", StringComparison.Ordinal))
            {
                commandVariables.Add(left.Expression.ToString());
            }
        }

        foreach (var creation in method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (IsSupportedCommandType(creation.Type.ToString())
                && creation.Initializer?.Expressions.OfType<AssignmentExpressionSyntax>().Any(assignment =>
                    assignment.Left.ToString() == "CommandType"
                    && assignment.Right.ToString().EndsWith("CommandType.StoredProcedure", StringComparison.Ordinal)) == true
                && creation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable })
            {
                commandVariables.Add(variable.Identifier.ValueText);
            }
        }

        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!StoredProcedureExecuteMethods.Contains(InvocationName(invocation))
                || invocation.Expression is not MemberAccessExpressionSyntax access
                || !commandVariables.Contains(access.Expression.ToString()))
            {
                continue;
            }

            var properties = CopyProperties(signals);
            properties["loopDeclaration"] = invocation.Ancestors().Any(ancestor => ancestor is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
                ? "present"
                : "not-observed";
            observations.Add(SyntaxObservation(filePath, invocation, "stored-procedure-batch", "command-type-stored-procedure", "execute", member, ownerType, properties));
        }
    }

    private static void AddBulkCopySyntaxFallback(
        string filePath,
        MethodDeclarationSyntax method,
        string member,
        string? ownerType,
        IReadOnlyDictionary<string, string> signals,
        IReadOnlyList<CodeFact> existingFacts,
        List<BatchObservation> observations)
    {
        var variables = method.DescendantNodes().OfType<VariableDeclarationSyntax>()
            .Where(declaration => IsSupportedBulkCopyType(declaration.Type.ToString()))
            .SelectMany(declaration => declaration.Variables)
            .Select(variable => variable.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (InvocationName(invocation) is not ("WriteToServer" or "WriteToServerAsync")
                || invocation.Expression is not MemberAccessExpressionSyntax access
                || !variables.Contains(access.Expression.ToString())
                || HasSemanticBulkCopyObservation(existingFacts, filePath, invocation))
            {
                continue;
            }

            observations.Add(SyntaxObservation(filePath, invocation, "bulk-copy", "declared-sql-bulk-copy-variable", "write", member, ownerType, signals));
        }
    }

    private static SortedDictionary<string, string> MemberSignals(MethodDeclarationSyntax method)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var invocationNames = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(InvocationName).ToHashSet(StringComparer.Ordinal);
        if (invocationNames.Overlaps(RetryMethods)) properties["retryDeclaration"] = "named-call";
        if (method.DescendantNodes().OfType<CatchClauseSyntax>().Any()) properties["errorHandlingDeclaration"] = "catch-clause";
        if (invocationNames.Any(name => name is "BeginTransaction" or "BeginTransactionAsync" or "Commit" or "CommitAsync" or "Rollback" or "RollbackAsync")) properties["transactionDeclaration"] = "named-call";
        if (invocationNames.Overlaps(CheckpointMethods)) properties["checkpointDeclaration"] = "named-call";
        if (invocationNames.Overlaps(TelemetryMethods)) properties["telemetryDeclaration"] = "named-call";
        return properties;
    }

    private static void ClassifyScheduleReference(
        ExpressionSyntax? expression,
        IReadOnlyList<CodeFact> existingFacts,
        SortedDictionary<string, string> properties,
        List<GapDescriptor> gaps)
    {
        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            var value = literal.Token.ValueText;
            if (value.Length > 2 && value[0] == '%' && value[^1] == '%')
            {
                var configKey = value[1..^1];
                var config = existingFacts.FirstOrDefault(fact => fact.FactType == FactTypes.ConfigKeyDeclared
                    && string.Equals(fact.Properties.GetValueOrDefault("keyPath") ?? fact.TargetSymbol, configKey, StringComparison.Ordinal));
                properties["scheduleSource"] = config is null ? "config-reference-unavailable" : "config-reference-matched";
                properties["scheduleReferenceHash"] = FactFactory.Hash($"batch-schedule|{configKey}", 32);
                if (config is not null) properties["scheduleConfigFactId"] = config.FactId;
                else gaps.Add(new("BatchConfigurationReferenceUnavailable", "A timer trigger references a configuration key that was not declared in the scanned snapshot."));
            }
            else
            {
                properties["scheduleSource"] = "literal-omitted";
            }
        }
        else
        {
            properties["scheduleSource"] = "dynamic-or-unavailable";
            gaps.Add(new("DynamicBatchScheduleUnsupported", "A scheduled trigger was observed, but its schedule source was not a bounded literal or configuration reference."));
        }
    }

    private static BatchObservation FromFact(
        CodeFact fact,
        string surfaceKind,
        string mechanism,
        string operationKind,
        SortedDictionary<string, string>? properties = null)
    {
        var ownerMember = fact.Properties.GetValueOrDefault("containingMethod") ?? SimpleMemberName(fact.SourceSymbol);
        var ownerType = fact.Properties.GetValueOrDefault("containingType");
        return new(
            fact.Evidence,
            fact.EvidenceTier,
            surfaceKind,
            mechanism,
            operationKind,
            ownerMember,
            ownerType,
            fact.ProjectPath,
            [fact.FactId],
            properties ?? new SortedDictionary<string, string>(StringComparer.Ordinal),
            []);
    }

    private static BatchObservation SyntaxObservation(
        string filePath,
        SyntaxNodeOrToken node,
        string surfaceKind,
        string mechanism,
        string operationKind,
        string? ownerMember,
        string? ownerType,
        IReadOnlyDictionary<string, string>? properties = null,
        IReadOnlyList<GapDescriptor>? gaps = null,
        IReadOnlyList<string>? supportingFactIds = null) =>
        new(
            Evidence(filePath, node),
            EvidenceTiers.Tier3SyntaxOrTextual,
            surfaceKind,
            mechanism,
            operationKind,
            ownerMember,
            ownerType,
            null,
            supportingFactIds ?? [],
            CopyProperties(properties),
            gaps ?? []);

    private static List<CodeFact> RelatedFacts(BatchObservation observation, IReadOnlyList<CodeFact> candidates)
    {
        if (observation.OwnerMember is null)
        {
            return [];
        }

        return candidates
            .Where(fact => BelongsToMember(fact, observation.OwnerMember, observation.OwnerType))
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToList();
    }

    private static bool BelongsToMember(CodeFact fact, string member, string? ownerType)
    {
        var memberMatches = string.Equals(fact.Properties.GetValueOrDefault("containingMethod"), member, StringComparison.Ordinal)
            || string.Equals(fact.SourceSymbol, member, StringComparison.Ordinal);
        var source = NormalizeGlobalTypeName(fact.SourceSymbol ?? string.Empty);
        memberMatches |= source.Contains($".{member}(", StringComparison.Ordinal)
            || source.EndsWith($".{member}", StringComparison.Ordinal);
        if (!memberMatches)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ownerType))
        {
            return true;
        }

        var normalizedOwner = NormalizeGlobalTypeName(ownerType);
        var containingType = NormalizeGlobalTypeName(fact.Properties.GetValueOrDefault("containingType") ?? string.Empty);
        return containingType == normalizedOwner
            || containingType.EndsWith($".{normalizedOwner}", StringComparison.Ordinal)
            || source.StartsWith($"{normalizedOwner}.{member}", StringComparison.Ordinal)
            || source.Contains($".{normalizedOwner}.{member}", StringComparison.Ordinal);
    }

    private static void AddRelated(SortedDictionary<string, string> properties, string key, IEnumerable<CodeFact> facts)
    {
        var ids = facts.Select(fact => fact.FactId).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).Take(RelatedFactLimit).ToArray();
        if (ids.Length > 0) properties[key] = string.Join(",", ids);
    }

    private static CodeFact CreateGap(ScanManifest manifest, CodeFact supportingFact, string kind, string message) =>
        FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.LegacyWebFormsBatchDataMovement,
            EvidenceTiers.Tier4Unknown,
            Evidence(supportingFact.Evidence.FilePath, supportingFact.Evidence.StartLine, supportingFact.Evidence.EndLine),
            supportingFact.ProjectPath,
            supportingFact.SourceSymbol,
            supportingFact.TargetSymbol,
            kind,
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverage"] = "reduced",
                ["gapKind"] = kind,
                ["limitations"] = Limitations,
                ["message"] = message,
                ["supportingFactIds"] = supportingFact.FactId
            });

    private static CodeFact CreateGap(ScanManifest manifest, PendingGap gap) =>
        FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.LegacyWebFormsBatchDataMovement,
            EvidenceTiers.Tier4Unknown,
            Evidence(gap.FilePath, gap.Line, gap.Line),
            contractElement: gap.Kind,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverage"] = "reduced",
                ["gapKind"] = gap.Kind,
                ["limitations"] = Limitations,
                ["message"] = gap.Message
            });

    private static bool HasWebFormsInventory(IReadOnlyList<FileInventoryItem> inventory) =>
        inventory.Any(item => item.Kind is "WebFormsMarkup" or "WebFormsCodeBehind" or "WebFormsDesigner" or "AspNetApplication" or "AspNetHandler" or "AspNetSiteMap");

    private static bool HasSemanticFileObservation(IReadOnlyList<CodeFact> facts, string filePath, InvocationExpressionSyntax invocation)
    {
        var line = Line(invocation);
        var method = InvocationName(invocation);
        return facts.Any(fact => fact.FactType == FactTypes.MethodInvoked
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.Evidence.FilePath == filePath
            && fact.Evidence.StartLine <= line
            && fact.Evidence.EndLine >= line
            && IsSystemIoTarget(fact.TargetSymbol ?? fact.Properties.GetValueOrDefault("methodSymbol") ?? string.Empty)
            && MethodName(fact.TargetSymbol ?? fact.Properties.GetValueOrDefault("methodSymbol") ?? string.Empty) == method);
    }

    private static bool HasSemanticFileWatcherObservation(IReadOnlyList<CodeFact> facts, string filePath, ObjectCreationExpressionSyntax creation)
    {
        var line = Line(creation);
        return facts.Any(fact => fact.FactType == FactTypes.ObjectCreated
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.Evidence.FilePath == filePath
            && fact.Evidence.StartLine <= line
            && fact.Evidence.EndLine >= line
            && (fact.TargetSymbol ?? string.Empty).Contains("System.IO.FileSystemWatcher", StringComparison.Ordinal));
    }

    private static bool HasSemanticBulkCopyObservation(IReadOnlyList<CodeFact> facts, string filePath, InvocationExpressionSyntax invocation)
    {
        var line = Line(invocation);
        var method = InvocationName(invocation);
        return facts.Any(fact => fact.FactType == FactTypes.MethodInvoked
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.Evidence.FilePath == filePath
            && fact.Evidence.StartLine <= line
            && fact.Evidence.EndLine >= line
            && IsSqlBulkCopyTarget(fact.TargetSymbol ?? fact.Properties.GetValueOrDefault("methodSymbol") ?? string.Empty)
            && MethodName(fact.TargetSymbol ?? fact.Properties.GetValueOrDefault("methodSymbol") ?? string.Empty) == method);
    }

    private static bool IsSystemIoTarget(string target) =>
        target.StartsWith("global::System.IO.File.", StringComparison.Ordinal)
        || target.StartsWith("System.IO.File.", StringComparison.Ordinal)
        || target.StartsWith("global::System.IO.Directory.", StringComparison.Ordinal)
        || target.StartsWith("System.IO.Directory.", StringComparison.Ordinal)
        || target.StartsWith("global::System.IO.FileInfo.", StringComparison.Ordinal)
        || target.StartsWith("System.IO.FileInfo.", StringComparison.Ordinal)
        || target.StartsWith("global::System.IO.DirectoryInfo.", StringComparison.Ordinal)
        || target.StartsWith("System.IO.DirectoryInfo.", StringComparison.Ordinal);

    private static bool IsSqlBulkCopyTarget(string target) =>
        target.Contains("System.Data.SqlClient.SqlBulkCopy.", StringComparison.Ordinal)
        || target.Contains("Microsoft.Data.SqlClient.SqlBulkCopy.", StringComparison.Ordinal);

    private static bool IsExplicitSystemIoReceiver(string receiver) =>
        receiver is "System.IO.File" or "System.IO.Directory" or "global::System.IO.File" or "global::System.IO.Directory";

    private static string MethodName(string symbol)
    {
        var open = symbol.IndexOf('(');
        var head = open >= 0 ? symbol[..open] : symbol;
        var dot = head.LastIndexOf('.');
        return dot >= 0 ? head[(dot + 1)..] : head;
    }

    private static string InvocationName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        _ => string.Empty
    };

    private static string InvocationReceiver(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax access ? access.Expression.ToString() : string.Empty;

    private static string? SimpleMemberName(string? sourceSymbol)
    {
        if (string.IsNullOrWhiteSpace(sourceSymbol)) return null;
        var open = sourceSymbol.IndexOf('(');
        var head = open >= 0 ? sourceSymbol[..open] : sourceSymbol;
        var dot = head.LastIndexOf('.');
        return dot >= 0 ? head[(dot + 1)..] : head;
    }

    private static string SimpleTypeName(string value)
    {
        var normalized = value.EndsWith("Attribute", StringComparison.Ordinal) ? value[..^9] : value;
        var generic = normalized.IndexOf('<');
        if (generic >= 0) normalized = normalized[..generic];
        var dot = normalized.LastIndexOf('.');
        return dot >= 0 ? normalized[(dot + 1)..] : normalized;
    }

    private static string NormalizeGlobalTypeName(string value) =>
        value.StartsWith("global::", StringComparison.Ordinal) ? value[8..] : value;

    private static bool IsSupportedCommandType(string value) => NormalizeGlobalTypeName(value) is
        "System.Data.Common.DbCommand" or "System.Data.SqlClient.SqlCommand" or "Microsoft.Data.SqlClient.SqlCommand" or "Npgsql.NpgsqlCommand";

    private static bool IsSupportedBulkCopyType(string value) => NormalizeGlobalTypeName(value) is
        "System.Data.SqlClient.SqlBulkCopy" or "Microsoft.Data.SqlClient.SqlBulkCopy";

    private static string QualifiedTypeName(TypeDeclarationSyntax type)
    {
        var names = new Stack<string>();
        for (SyntaxNode? current = type; current is not null; current = current.Parent)
        {
            if (current is TypeDeclarationSyntax currentType) names.Push(currentType.Identifier.ValueText);
            else if (current is BaseNamespaceDeclarationSyntax currentNamespace) names.Push(currentNamespace.Name.ToString());
        }
        return string.Join(".", names);
    }

    private static string? ClassifyIntegrationConfigKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var normalized = new string(key.Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ').ToArray());
        var compact = normalized.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (ContainsAny(compact, "servicebus", "rabbitmq", "kafka", "queue", "topic")) return "message-boundary";
        if (ContainsAny(compact, "batchschedule", "cronexpression", "timerschedule")) return "schedule";
        if (ContainsAny(compact, "filedrop", "inbounddirectory", "outbounddirectory", "importpath", "exportpath")) return "file-boundary";
        if (ContainsAny(compact, "ssis", "etlpackage")) return "etl";
        return null;
    }

    private static bool ContainsAny(string value, params string[] candidates) => candidates.Any(candidate => value.Contains(candidate, StringComparison.Ordinal));

    private static void AddIfPresent(SortedDictionary<string, string> properties, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) properties[key] = value;
    }

    private static SortedDictionary<string, string> CopyProperties(IReadOnlyDictionary<string, string>? properties)
    {
        var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in properties ?? new Dictionary<string, string>()) copy[key] = value;
        return copy;
    }

    private static string Coverage(string tier) => tier switch
    {
        EvidenceTiers.Tier1Semantic => "semantic-static",
        EvidenceTiers.Tier2Structural => "structural-static",
        _ => "syntax-static"
    };

    private static EvidenceSpan Evidence(string filePath, SyntaxNodeOrToken node)
    {
        var span = node.SyntaxTree!.GetLineSpan(node.Span);
        return Evidence(filePath, span.StartLinePosition.Line + 1, Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1));
    }

    private static EvidenceSpan Evidence(string filePath, int startLine, int endLine) =>
        new(filePath, startLine, Math.Max(startLine, endLine), null, ExtractorId, ScannerVersions.LegacyBatchDataMovementExtractor);

    private static int Line(SyntaxNode node) => node.SyntaxTree.GetLineSpan(node.Span).StartLinePosition.Line + 1;

    private sealed record BatchObservation(
        EvidenceSpan Evidence,
        string EvidenceTier,
        string SurfaceKind,
        string Mechanism,
        string OperationKind,
        string? OwnerMember,
        string? OwnerType,
        string? ProjectPath,
        IReadOnlyList<string> SupportingFactIds,
        SortedDictionary<string, string> Properties,
        IReadOnlyList<GapDescriptor> Gaps);

    private sealed record GapDescriptor(string Kind, string Message);
    private sealed record PendingGap(string FilePath, int Line, string Kind, string Message);
}
