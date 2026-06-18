namespace TraceMap.Core;

public static class AnalyzerCapabilityDiagnosticExtractor
{
    public const string SchemaVersion = "legacy-dotnet-toolchain-diagnostics.v1";

    public static class Codes
    {
        public const string CSharpSemanticCompilation = nameof(CSharpSemanticCompilation);
        public const string MSBuildProjectLoad = nameof(MSBuildProjectLoad);
        public const string ReferenceAssemblyResolution = nameof(ReferenceAssemblyResolution);
        public const string SyntaxFallbackAvailable = nameof(SyntaxFallbackAvailable);
        public const string LegacyProjectConfigInspection = nameof(LegacyProjectConfigInspection);
        public const string LegacyFrameworkSignalDetected = nameof(LegacyFrameworkSignalDetected);
        public const string LegacyMSBuildToolsetSignalDetected = nameof(LegacyMSBuildToolsetSignalDetected);
        public const string LegacyNuGetRestoreAwareness = nameof(LegacyNuGetRestoreAwareness);
        public const string GeneratedDesignerLinkage = nameof(GeneratedDesignerLinkage);
        public const string LegacyWebStackShape = nameof(LegacyWebStackShape);
        public const string LegacyRemotingShape = nameof(LegacyRemotingShape);
        public const string DownstreamNoEvidenceCoverage = nameof(DownstreamNoEvidenceCoverage);
    }

    public static class States
    {
        public const string Available = "available";
        public const string Reduced = "reduced";
        public const string Unavailable = "unavailable";
        public const string NotRequested = "not-requested";
        public const string Unknown = "unknown";
        public const string NotApplicable = "not-applicable";
    }

    public static class Effects
    {
        public const string FullSemantic = "full-semantic";
        public const string ReducedSemantic = "reduced-semantic";
        public const string SyntaxOnly = "syntax-only";
        public const string StructuralOnly = "structural-only";
        public const string ConfigOnly = "config-only";
        public const string UnknownGap = "unknown-gap";
        public const string Informational = "informational";
    }

    public static class GuidanceCodes
    {
        public const string UseSemanticEvidenceWhenAvailable = nameof(UseSemanticEvidenceWhenAvailable);
        public const string TreatAsReducedCoverage = nameof(TreatAsReducedCoverage);
        public const string UseSyntaxFallbackEvidence = nameof(UseSyntaxFallbackEvidence);
        public const string ReviewProjectConfigSignals = nameof(ReviewProjectConfigSignals);
        public const string RestoreNotAttemptedNoAbsenceClaim = nameof(RestoreNotAttemptedNoAbsenceClaim);
        public const string ReviewSanitizedRestoreFailure = nameof(ReviewSanitizedRestoreFailure);
        public const string ReviewGeneratedDesignTimeCoverage = nameof(ReviewGeneratedDesignTimeCoverage);
        public const string ReviewLegacyToolchainSignals = nameof(ReviewLegacyToolchainSignals);
        public const string ReviewUnknownCapabilityGap = nameof(ReviewUnknownCapabilityGap);
    }

    public static class LimitationCodes
    {
        public const string SemanticStatusDerived = "semantic-status-derived";
        public const string SyntaxFallbackOnly = "syntax-fallback-only";
        public const string ProjectConfigStaticOnly = "project-config-static-only";
        public const string RestoreNotAttempted = "restore-not-attempted";
        public const string RestoreCategoryOnly = "restore-category-only";
        public const string DesignTimeLinkageGap = "design-time-linkage-gap";
        public const string LegacyToolchainStaticSignal = "legacy-toolchain-static-signal";
        public const string UnknownToolchainGap = "unknown-toolchain-gap";
        public const string CoverageContextOnly = "coverage-context-only";
    }

    private const int MaxSupportingIds = 12;

    private static readonly HashSet<string> RestoreFailureCodes = new(StringComparer.Ordinal)
    {
        "NuGetRestoreFailed",
        "CredentialRequired",
        "PackageSourceUnavailable",
        "UnsupportedPackageFormat",
        "PackageVersionUnavailable"
    };

    public static IReadOnlyList<CodeFact> Extract(
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        SemanticExtractionResult semanticResult,
        IReadOnlyList<CodeFact> facts,
        ScanOptions options)
    {
        var diagnostics = new List<CapabilityCandidate>();
        var buildEnvironmentFacts = facts
            .Where(fact => fact.FactType == FactTypes.BuildEnvironmentDiagnostic)
            .OrderBy(SupportFactSortKey, StringComparer.Ordinal)
            .ToArray();
        var analysisGaps = facts
            .Where(fact => fact.FactType == FactTypes.AnalysisGap)
            .OrderBy(SupportFactSortKey, StringComparer.Ordinal)
            .ToArray();
        var buildStatusFacts = facts
            .Where(fact => fact.FactType == FactTypes.BuildStatus)
            .OrderBy(SupportFactSortKey, StringComparer.Ordinal)
            .ToArray();

        var projectScopes = inventory
            .Where(item => item.Kind is "Project" or "NonCSharpProject")
            .Select(item => item.RelativePath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var csharpFiles = inventory.Where(item => FileInventory.IsCSharpKind(item.Kind)).ToArray();
        var hasDotNetScope = projectScopes.Length > 0 || csharpFiles.Length > 0 || buildEnvironmentFacts.Length > 0;

        if (hasDotNetScope)
        {
            diagnostics.Add(SemanticCapability(manifest, semanticResult, projectScopes, csharpFiles, buildStatusFacts, analysisGaps));
            diagnostics.Add(ProjectLoadCapability(manifest, semanticResult, projectScopes, csharpFiles, buildStatusFacts, buildEnvironmentFacts, analysisGaps));
            diagnostics.AddRange(ReferenceAssemblyCapabilities(manifest, semanticResult, buildEnvironmentFacts));
            diagnostics.Add(SyntaxFallbackCapability(manifest, semanticResult, facts, projectScopes, csharpFiles, analysisGaps));
        }

        diagnostics.AddRange(ProjectConfigCapabilities(manifest, buildEnvironmentFacts));
        diagnostics.AddRange(LegacyFrameworkCapabilities(manifest, buildEnvironmentFacts));
        diagnostics.AddRange(LegacyToolsetCapabilities(manifest, buildEnvironmentFacts));
        diagnostics.AddRange(RestoreCapabilities(manifest, buildEnvironmentFacts, options));
        diagnostics.AddRange(GeneratedCapabilities(manifest, buildEnvironmentFacts));
        diagnostics.AddRange(LegacyWebCapabilities(manifest, facts, buildEnvironmentFacts));
        diagnostics.AddRange(LegacyRemotingCapabilities(manifest, facts));

        if (diagnostics.Any(item => item.CapabilityState is States.Reduced or States.Unavailable or States.Unknown))
        {
            diagnostics.Add(WorkspaceCandidate(
                manifest,
                Codes.DownstreamNoEvidenceCoverage,
                "downstream-coverage",
                manifest.BuildStatus == "FailedOrPartial" ? States.Reduced : States.Unknown,
                manifest.BuildStatus == "Succeeded" ? Effects.Informational : Effects.UnknownGap,
                RuleIds.AnalyzerCapabilityDownstreamCoverage,
                EvidenceTiers.Tier4Unknown,
                GuidanceCodes.TreatAsReducedCoverage,
                LimitationCodes.CoverageContextOnly,
                diagnostics
                    .Where(item => item.CapabilityState is States.Reduced or States.Unavailable or States.Unknown)
                    .SelectMany(item => item.SupportingFacts)
                    .Concat(analysisGaps)
                    .Concat(buildStatusFacts)
                    .ToArray()));
        }

        return diagnostics
            .Where(item => ShouldEmit(item, manifest, buildEnvironmentFacts))
            .GroupBy(item => CandidateIdentity(item), StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(item => item.SourceScope, StringComparer.Ordinal)
            .ThenBy(item => item.CapabilityCode, StringComparer.Ordinal)
            .ThenBy(item => item.CapabilityState, StringComparer.Ordinal)
            .ThenBy(item => item.StartLine)
            .ThenBy(item => item.RuleId, StringComparer.Ordinal)
            .Select(item => CreateFact(manifest, item))
            .OrderBy(fact => fact.Properties.GetValueOrDefault("sourceScope"), StringComparer.Ordinal)
            .ThenBy(fact => fact.Properties.GetValueOrDefault("capabilityCode"), StringComparer.Ordinal)
            .ThenBy(fact => fact.Properties.GetValueOrDefault("capabilityState"), StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.RuleId, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool IsClosedCapabilityFact(CodeFact fact)
    {
        return fact.FactType == FactTypes.AnalyzerCapabilityDiagnostic
            && IsClosedCode(fact.Properties.GetValueOrDefault("capabilityCode"))
            && IsClosedKind(fact.Properties.GetValueOrDefault("capabilityKind"))
            && IsClosedState(fact.Properties.GetValueOrDefault("capabilityState"))
            && IsClosedCoverageEffect(fact.Properties.GetValueOrDefault("coverageEffect"))
            && IsClosedGuidanceCode(fact.Properties.GetValueOrDefault("guidanceCode"))
            && IsClosedLimitationCode(fact.Properties.GetValueOrDefault("limitationCode"));
    }

    private static CapabilityCandidate SemanticCapability(
        ScanManifest manifest,
        SemanticExtractionResult semanticResult,
        IReadOnlyList<string> projectScopes,
        IReadOnlyList<FileInventoryItem> csharpFiles,
        IReadOnlyList<CodeFact> buildStatusFacts,
        IReadOnlyList<CodeFact> analysisGaps)
    {
        var state = semanticResult.Attempted
            ? semanticResult.ReducedCoverage ? States.Reduced : States.Available
            : projectScopes.Count > 0 || csharpFiles.Count > 0 ? States.Unknown : States.NotApplicable;
        var effect = state switch
        {
            States.Available => Effects.FullSemantic,
            States.Reduced => Effects.ReducedSemantic,
            States.NotApplicable => Effects.Informational,
            _ => Effects.UnknownGap
        };
        var tier = state == States.Available || state == States.Reduced
            ? EvidenceTiers.Tier2Structural
            : EvidenceTiers.Tier4Unknown;
        return WorkspaceCandidate(
            manifest,
            Codes.CSharpSemanticCompilation,
            "semantic",
            state,
            effect,
            RuleIds.AnalyzerCapabilitySemantic,
            tier,
            state == States.Available ? GuidanceCodes.UseSemanticEvidenceWhenAvailable : GuidanceCodes.TreatAsReducedCoverage,
            state == States.Unknown ? LimitationCodes.UnknownToolchainGap : LimitationCodes.SemanticStatusDerived,
            analysisGaps.Concat(buildStatusFacts).ToArray(),
            strongestSupportingEvidenceTier: StrongestTier(analysisGaps.Concat(buildStatusFacts)));
    }

    private static CapabilityCandidate ProjectLoadCapability(
        ScanManifest manifest,
        SemanticExtractionResult semanticResult,
        IReadOnlyList<string> projectScopes,
        IReadOnlyList<FileInventoryItem> csharpFiles,
        IReadOnlyList<CodeFact> buildStatusFacts,
        IReadOnlyList<CodeFact> buildEnvironmentFacts,
        IReadOnlyList<CodeFact> analysisGaps)
    {
        var workspaceSupport = buildEnvironmentFacts
            .Where(fact => fact.Properties.GetValueOrDefault("diagnosticKind") == BuildEnvironmentDiagnosticExtractor.DiagnosticKindWorkspace)
            .Concat(analysisGaps)
            .Concat(buildStatusFacts)
            .ToArray();
        var state = semanticResult.Attempted
            ? semanticResult.ReducedCoverage ? States.Reduced : States.Available
            : projectScopes.Count > 0 || csharpFiles.Count > 0 ? States.Unknown : States.NotApplicable;
        var effect = state switch
        {
            States.Available => Effects.FullSemantic,
            States.Reduced => Effects.ReducedSemantic,
            States.NotApplicable => Effects.Informational,
            _ => Effects.UnknownGap
        };
        return WorkspaceCandidate(
            manifest,
            Codes.MSBuildProjectLoad,
            "semantic",
            state,
            effect,
            RuleIds.AnalyzerCapabilitySemantic,
            state is States.Available or States.Reduced ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier4Unknown,
            state == States.Available ? GuidanceCodes.UseSemanticEvidenceWhenAvailable : GuidanceCodes.TreatAsReducedCoverage,
            state == States.Unknown ? LimitationCodes.UnknownToolchainGap : LimitationCodes.SemanticStatusDerived,
            workspaceSupport,
            strongestSupportingEvidenceTier: StrongestTier(workspaceSupport));
    }

    private static IEnumerable<CapabilityCandidate> ReferenceAssemblyCapabilities(
        ScanManifest manifest,
        SemanticExtractionResult semanticResult,
        IReadOnlyList<CodeFact> buildEnvironmentFacts)
    {
        var referenceGaps = buildEnvironmentFacts
            .Where(fact => fact.Properties.GetValueOrDefault("diagnosticCode") == "MissingReferenceAssemblies")
            .ToArray();
        if (referenceGaps.Length > 0)
        {
            yield return WorkspaceCandidate(
                manifest,
                Codes.ReferenceAssemblyResolution,
                "semantic",
                States.Unavailable,
                Effects.UnknownGap,
                RuleIds.AnalyzerCapabilitySemantic,
                EvidenceTiers.Tier4Unknown,
                GuidanceCodes.TreatAsReducedCoverage,
                LimitationCodes.UnknownToolchainGap,
                referenceGaps);
        }
        else if (semanticResult.Attempted && !semanticResult.ReducedCoverage)
        {
            yield return WorkspaceCandidate(
                manifest,
                Codes.ReferenceAssemblyResolution,
                "semantic",
                States.Available,
                Effects.FullSemantic,
                RuleIds.AnalyzerCapabilitySemantic,
                EvidenceTiers.Tier2Structural,
                GuidanceCodes.UseSemanticEvidenceWhenAvailable,
                LimitationCodes.SemanticStatusDerived,
                []);
        }
    }

    private static CapabilityCandidate SyntaxFallbackCapability(
        ScanManifest manifest,
        SemanticExtractionResult semanticResult,
        IReadOnlyList<CodeFact> facts,
        IReadOnlyList<string> projectScopes,
        IReadOnlyList<FileInventoryItem> csharpFiles,
        IReadOnlyList<CodeFact> analysisGaps)
    {
        var syntaxSupport = facts
            .Where(fact => fact.RuleId.StartsWith("csharp.syntax.", StringComparison.Ordinal)
                || fact.Evidence.ExtractorVersion == ScannerVersions.CSharpSyntaxExtractor)
            .OrderBy(SupportFactSortKey, StringComparer.Ordinal)
            .Take(MaxSupportingIds)
            .ToArray();
        var state = syntaxSupport.Length > 0
            ? States.Available
            : projectScopes.Count == 0 && csharpFiles.Count == 0 ? States.NotApplicable : States.Unknown;
        var effect = state == States.Available && semanticResult.ReducedCoverage
            ? Effects.SyntaxOnly
            : state == States.Available ? Effects.Informational : Effects.UnknownGap;
        return WorkspaceCandidate(
            manifest,
            Codes.SyntaxFallbackAvailable,
            "syntax-fallback",
            state,
            effect,
            RuleIds.AnalyzerCapabilitySyntaxFallback,
            state == States.Available ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier4Unknown,
            state == States.Available ? GuidanceCodes.UseSyntaxFallbackEvidence : GuidanceCodes.ReviewUnknownCapabilityGap,
            state == States.Available ? LimitationCodes.SyntaxFallbackOnly : LimitationCodes.UnknownToolchainGap,
            syntaxSupport.Concat(analysisGaps).ToArray(),
            strongestSupportingEvidenceTier: StrongestTier(syntaxSupport));
    }

    private static IEnumerable<CapabilityCandidate> ProjectConfigCapabilities(ScanManifest manifest, IReadOnlyList<CodeFact> buildEnvironmentFacts)
    {
        foreach (var group in buildEnvironmentFacts
            .Where(fact => fact.RuleId is RuleIds.BuildEnvironmentProjectFormat
                or RuleIds.BuildEnvironmentTargetFramework
                or RuleIds.BuildEnvironmentToolset)
            .Where(IsLegacyOrReducedProjectConfigSignal)
            .GroupBy(SourceScopeForFact, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var support = group.OrderBy(SupportFactSortKey, StringComparer.Ordinal).ToArray();
            var hasUnknown = support.Any(fact => fact.EvidenceTier == EvidenceTiers.Tier4Unknown);
            var first = support.First();
            yield return CandidateFromSupport(
                manifest,
                Codes.LegacyProjectConfigInspection,
                "project-config",
                hasUnknown ? States.Reduced : States.Available,
                hasUnknown ? Effects.StructuralOnly : Effects.ConfigOnly,
                RuleIds.AnalyzerCapabilityProjectConfig,
                hasUnknown ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural,
                GuidanceCodes.ReviewProjectConfigSignals,
                hasUnknown ? LimitationCodes.UnknownToolchainGap : LimitationCodes.ProjectConfigStaticOnly,
                first,
                support);
        }
    }

    private static IEnumerable<CapabilityCandidate> LegacyFrameworkCapabilities(ScanManifest manifest, IReadOnlyList<CodeFact> buildEnvironmentFacts)
    {
        foreach (var fact in buildEnvironmentFacts
            .Where(fact => fact.RuleId == RuleIds.BuildEnvironmentTargetFramework)
            .Where(IsLegacyOrReducedProjectConfigSignal)
            .OrderBy(SupportFactSortKey, StringComparer.Ordinal))
        {
            var frameworkFamily = FrameworkFamily(fact);
            yield return CandidateFromSupport(
                manifest,
                Codes.LegacyFrameworkSignalDetected,
                "legacy-toolchain",
                States.Available,
                Effects.Informational,
                RuleIds.AnalyzerCapabilityLegacyToolchain,
                EvidenceTiers.Tier2Structural,
                GuidanceCodes.ReviewLegacyToolchainSignals,
                LimitationCodes.LegacyToolchainStaticSignal,
                fact,
                [fact],
                frameworkFamily: frameworkFamily);
        }
    }

    private static IEnumerable<CapabilityCandidate> LegacyToolsetCapabilities(ScanManifest manifest, IReadOnlyList<CodeFact> buildEnvironmentFacts)
    {
        foreach (var group in buildEnvironmentFacts
            .Where(fact => fact.RuleId == RuleIds.BuildEnvironmentToolset
                || (fact.RuleId == RuleIds.BuildEnvironmentProjectFormat
                    && fact.Properties.GetValueOrDefault("diagnosticCode") == "WebApplicationProjectTargets"))
            .GroupBy(SourceScopeForFact, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var support = group.OrderBy(SupportFactSortKey, StringComparer.Ordinal).ToArray();
            var hasUnknown = support.Any(fact => fact.EvidenceTier == EvidenceTiers.Tier4Unknown);
            var first = support.First();
            yield return CandidateFromSupport(
                manifest,
                Codes.LegacyMSBuildToolsetSignalDetected,
                "legacy-toolchain",
                hasUnknown ? States.Unknown : States.Available,
                hasUnknown ? Effects.StructuralOnly : Effects.Informational,
                RuleIds.AnalyzerCapabilityLegacyToolchain,
                hasUnknown ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural,
                GuidanceCodes.ReviewLegacyToolchainSignals,
                hasUnknown ? LimitationCodes.UnknownToolchainGap : LimitationCodes.LegacyToolchainStaticSignal,
                first,
                support);
        }
    }

    private static IEnumerable<CapabilityCandidate> RestoreCapabilities(ScanManifest manifest, IReadOnlyList<CodeFact> buildEnvironmentFacts, ScanOptions options)
    {
        foreach (var group in buildEnvironmentFacts
            .Where(fact => fact.RuleId == RuleIds.BuildEnvironmentRestore)
            .GroupBy(SourceScopeForFact, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var support = group.OrderBy(SupportFactSortKey, StringComparer.Ordinal).ToArray();
            var hasFailure = support.Any(fact => RestoreFailureCodes.Contains(fact.Properties.GetValueOrDefault("diagnosticCode") ?? string.Empty));
            var first = support.First();
            var state = hasFailure ? States.Reduced : options.Restore ? States.Available : States.NotRequested;
            var effect = hasFailure ? Effects.ReducedSemantic : Effects.Informational;
            yield return CandidateFromSupport(
                manifest,
                Codes.LegacyNuGetRestoreAwareness,
                "package-restore",
                state,
                effect,
                RuleIds.AnalyzerCapabilityPackageRestore,
                hasFailure ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier2Structural,
                hasFailure ? GuidanceCodes.ReviewSanitizedRestoreFailure : options.Restore ? GuidanceCodes.ReviewProjectConfigSignals : GuidanceCodes.RestoreNotAttemptedNoAbsenceClaim,
                hasFailure ? LimitationCodes.RestoreCategoryOnly : options.Restore ? LimitationCodes.ProjectConfigStaticOnly : LimitationCodes.RestoreNotAttempted,
                first,
                support);
        }
    }

    private static IEnumerable<CapabilityCandidate> GeneratedCapabilities(ScanManifest manifest, IReadOnlyList<CodeFact> buildEnvironmentFacts)
    {
        foreach (var group in buildEnvironmentFacts
            .Where(fact => fact.RuleId == RuleIds.BuildEnvironmentGeneratedFiles)
            .GroupBy(SourceScopeForFact, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var support = group.OrderBy(SupportFactSortKey, StringComparer.Ordinal).ToArray();
            var first = support.First();
            var hasUnknown = support.Any(fact => fact.EvidenceTier == EvidenceTiers.Tier4Unknown);
            yield return CandidateFromSupport(
                manifest,
                Codes.GeneratedDesignerLinkage,
                "generated-design-time",
                hasUnknown ? States.Unknown : States.Reduced,
                hasUnknown ? Effects.UnknownGap : Effects.SyntaxOnly,
                RuleIds.AnalyzerCapabilityGeneratedDesignTime,
                hasUnknown ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier3SyntaxOrTextual,
                GuidanceCodes.ReviewGeneratedDesignTimeCoverage,
                LimitationCodes.DesignTimeLinkageGap,
                first,
                support);
        }
    }

    private static IEnumerable<CapabilityCandidate> LegacyWebCapabilities(
        ScanManifest manifest,
        IReadOnlyList<CodeFact> facts,
        IReadOnlyList<CodeFact> buildEnvironmentFacts)
    {
        var support = facts
            .Where(fact => fact.FactType.StartsWith("WebForms", StringComparison.Ordinal)
                || fact.FactType.StartsWith("Wcf", StringComparison.Ordinal)
                || fact.FactType.StartsWith("Asmx", StringComparison.Ordinal)
                || fact.FactType.StartsWith("AspNet", StringComparison.Ordinal))
            .Concat(buildEnvironmentFacts.Where(fact => fact.Properties.GetValueOrDefault("diagnosticCode") == "WebApplicationProjectTargets"))
            .GroupBy(SourceScopeForFact, StringComparer.Ordinal);
        foreach (var group in support.OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var rows = group.OrderBy(SupportFactSortKey, StringComparer.Ordinal).ToArray();
            var first = rows.First();
            yield return CandidateFromSupport(
                manifest,
                Codes.LegacyWebStackShape,
                "legacy-toolchain",
                States.Available,
                Effects.StructuralOnly,
                RuleIds.AnalyzerCapabilityLegacyToolchain,
                StrongestTier(rows) == EvidenceTiers.Tier4Unknown ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural,
                GuidanceCodes.ReviewLegacyToolchainSignals,
                LimitationCodes.LegacyToolchainStaticSignal,
                first,
                rows);
        }
    }

    private static IEnumerable<CapabilityCandidate> LegacyRemotingCapabilities(ScanManifest manifest, IReadOnlyList<CodeFact> facts)
    {
        foreach (var group in facts
            .Where(fact => fact.FactType.StartsWith("Remoting", StringComparison.Ordinal))
            .GroupBy(SourceScopeForFact, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var support = group.OrderBy(SupportFactSortKey, StringComparer.Ordinal).ToArray();
            var first = support.First();
            yield return CandidateFromSupport(
                manifest,
                Codes.LegacyRemotingShape,
                "legacy-toolchain",
                States.Available,
                Effects.StructuralOnly,
                RuleIds.AnalyzerCapabilityLegacyToolchain,
                StrongestTier(support) == EvidenceTiers.Tier4Unknown ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural,
                GuidanceCodes.ReviewLegacyToolchainSignals,
                LimitationCodes.LegacyToolchainStaticSignal,
                first,
                support);
        }
    }

    private static CapabilityCandidate WorkspaceCandidate(
        ScanManifest manifest,
        string capabilityCode,
        string capabilityKind,
        string capabilityState,
        string coverageEffect,
        string ruleId,
        string evidenceTier,
        string guidanceCode,
        string limitationCode,
        IReadOnlyList<CodeFact> support,
        string? strongestSupportingEvidenceTier = null)
    {
        return new CapabilityCandidate(
            capabilityCode,
            capabilityKind,
            capabilityState,
            coverageEffect,
            ruleId,
            evidenceTier,
            ".",
            1,
            1,
            null,
            "workspace",
            null,
            null,
            SortSupport(support),
            guidanceCode,
            limitationCode,
            strongestSupportingEvidenceTier);
    }

    private static CapabilityCandidate CandidateFromSupport(
        ScanManifest manifest,
        string capabilityCode,
        string capabilityKind,
        string capabilityState,
        string coverageEffect,
        string ruleId,
        string evidenceTier,
        string guidanceCode,
        string limitationCode,
        CodeFact first,
        IReadOnlyList<CodeFact> support,
        string? frameworkFamily = null)
    {
        return new CapabilityCandidate(
            capabilityCode,
            capabilityKind,
            capabilityState,
            coverageEffect,
            ruleId,
            evidenceTier,
            first.Evidence.FilePath,
            Math.Max(1, first.Evidence.StartLine),
            Math.Max(1, first.Evidence.EndLine),
            first.ProjectPath,
            SourceScopeForFact(first),
            first.Properties.GetValueOrDefault("projectStyle"),
            frameworkFamily,
            SortSupport(support),
            guidanceCode,
            limitationCode,
            StrongestTier(support));
    }

    private static CodeFact CreateFact(ScanManifest manifest, CapabilityCandidate item)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["capabilityCode"] = item.CapabilityCode,
            ["capabilityKind"] = item.CapabilityKind,
            ["capabilityState"] = item.CapabilityState,
            ["coverageEffect"] = item.CoverageEffect,
            ["guidance"] = GuidanceText(item.GuidanceCode),
            ["guidanceCode"] = item.GuidanceCode,
            ["limitation"] = LimitationText(item.LimitationCode),
            ["limitationCode"] = item.LimitationCode,
            ["schemaVersion"] = SchemaVersion,
            ["sourceScope"] = item.SourceScope
        };
        AddIfPresent(properties, "frameworkFamily", item.FrameworkFamily);
        AddIfPresent(properties, "projectStyle", item.ProjectStyle);
        AddIfPresent(properties, "strongestSupportingEvidenceTier", item.StrongestSupportingEvidenceTier);
        if (item.SupportingFacts.Count > 0)
        {
            properties["supportingFactCount"] = item.SupportingFacts.Count.ToString();
            properties["supportingFactIds"] = string.Join(";", item.SupportingFacts.Take(MaxSupportingIds).Select(fact => fact.FactId));
            properties["supportingRuleIds"] = string.Join(";", item.SupportingFacts.Select(fact => fact.RuleId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).Take(MaxSupportingIds));
        }
        else
        {
            properties["supportingFactCount"] = "0";
        }

        return FactFactory.Create(
            manifest,
            FactTypes.AnalyzerCapabilityDiagnostic,
            item.RuleId,
            CapTier(item.CapabilityCode, item.EvidenceTier),
            new EvidenceSpan(
                item.FilePath,
                item.StartLine,
                item.EndLine,
                null,
                "AnalyzerCapabilityDiagnosticExtractor",
                ScannerVersions.AnalyzerCapabilityExtractor),
            projectPath: item.ProjectPath,
            contractElement: item.CapabilityCode,
            properties: properties);
    }

    private static IReadOnlyList<CodeFact> SortSupport(IEnumerable<CodeFact> support)
    {
        return support
            .Where(fact => !string.IsNullOrWhiteSpace(fact.FactId))
            .OrderBy(SupportFactSortKey, StringComparer.Ordinal)
            .GroupBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(MaxSupportingIds)
            .ToArray();
    }

    private static string SupportFactSortKey(CodeFact fact)
    {
        return string.Join("|", fact.RuleId, fact.Evidence.FilePath, fact.Evidence.StartLine.ToString("D8"), fact.FactId);
    }

    private static string SourceScopeForFact(CodeFact fact)
    {
        var scope = fact.ProjectPath ?? fact.Evidence.FilePath;
        return string.IsNullOrWhiteSpace(scope) || scope == "."
            ? "workspace"
            : FileInventory.NormalizeRelativePath(scope);
    }

    private static string StrongestTier(IEnumerable<CodeFact> facts)
    {
        var rank = facts.Select(fact => fact.EvidenceTier).Select(TierRank).DefaultIfEmpty(4).Min();
        return rank switch
        {
            1 => EvidenceTiers.Tier1Semantic,
            2 => EvidenceTiers.Tier2Structural,
            3 => EvidenceTiers.Tier3SyntaxOrTextual,
            _ => EvidenceTiers.Tier4Unknown
        };
    }

    private static int TierRank(string tier)
    {
        return tier switch
        {
            EvidenceTiers.Tier1Semantic => 1,
            EvidenceTiers.Tier2Structural => 2,
            EvidenceTiers.Tier3SyntaxOrTextual => 3,
            _ => 4
        };
    }

    private static string CapTier(string capabilityCode, string tier)
    {
        var rank = TierRank(tier);
        var capRank = capabilityCode switch
        {
            Codes.SyntaxFallbackAvailable => 3,
            Codes.GeneratedDesignerLinkage => 3,
            Codes.DownstreamNoEvidenceCoverage => 4,
            _ => 2
        };
        var capped = Math.Max(rank, capRank);
        return capped switch
        {
            2 => EvidenceTiers.Tier2Structural,
            3 => EvidenceTiers.Tier3SyntaxOrTextual,
            _ => EvidenceTiers.Tier4Unknown
        };
    }

    private static string FrameworkFamily(CodeFact fact)
    {
        var target = fact.Properties.GetValueOrDefault("targetFramework") ?? fact.TargetSymbol ?? string.Empty;
        if (target.StartsWith(".NETFramework", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("net4", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("net3", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("net2", StringComparison.OrdinalIgnoreCase))
        {
            return ".NET Framework";
        }

        if (target.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
        {
            return ".NET Standard";
        }

        if (target.StartsWith("net", StringComparison.OrdinalIgnoreCase)
            && (target.Length == 3 || char.IsDigit(target[3])))
        {
            return ".NET";
        }

        return fact.Properties.GetValueOrDefault("toolFamily") ?? "unknown";
    }

    private static bool IsLegacyOrReducedProjectConfigSignal(CodeFact fact)
    {
        var diagnosticCode = fact.Properties.GetValueOrDefault("diagnosticCode") ?? string.Empty;
        if (diagnosticCode is "SdkStyleTargetFramework")
        {
            return false;
        }

        return diagnosticCode.StartsWith("Legacy", StringComparison.Ordinal)
            || diagnosticCode.StartsWith("Old", StringComparison.Ordinal)
            || diagnosticCode.StartsWith("Unknown", StringComparison.Ordinal)
            || diagnosticCode.StartsWith("Unsupported", StringComparison.Ordinal)
            || diagnosticCode is "NonSdkStyleProject"
                or "VisualStudioVersionDeclared"
                or "ImportedLegacyTargets"
                or "UnknownImportedTargets"
                or "WebApplicationProjectTargets"
                or "MissingReferenceAssemblies"
                or "SdkResolutionFailed"
                or "MSBuildRegistrationFailed"
                or "CompilationCreationFailed"
                or "UncategorizedWorkspaceFailure";
    }

    private static bool ShouldEmit(CapabilityCandidate item, ScanManifest manifest, IReadOnlyList<CodeFact> buildEnvironmentFacts)
    {
        if (item.CapabilityCode is Codes.CSharpSemanticCompilation or Codes.MSBuildProjectLoad or Codes.ReferenceAssemblyResolution or Codes.SyntaxFallbackAvailable)
        {
            return item.CapabilityState != States.NotApplicable;
        }

        return true;
    }

    private static string CandidateIdentity(CapabilityCandidate item)
    {
        return string.Join("|", item.CapabilityCode, item.CapabilityKind, item.CapabilityState, item.CoverageEffect, item.SourceScope, item.FilePath, item.StartLine, item.RuleId);
    }

    private static bool IsClosedCode(string? value)
    {
        return value is Codes.CSharpSemanticCompilation
            or Codes.MSBuildProjectLoad
            or Codes.ReferenceAssemblyResolution
            or Codes.SyntaxFallbackAvailable
            or Codes.LegacyProjectConfigInspection
            or Codes.LegacyFrameworkSignalDetected
            or Codes.LegacyMSBuildToolsetSignalDetected
            or Codes.LegacyNuGetRestoreAwareness
            or Codes.GeneratedDesignerLinkage
            or Codes.LegacyWebStackShape
            or Codes.LegacyRemotingShape
            or Codes.DownstreamNoEvidenceCoverage;
    }

    private static bool IsClosedKind(string? value)
    {
        return value is "semantic" or "syntax-fallback" or "project-config" or "package-restore" or "generated-design-time" or "legacy-toolchain" or "downstream-coverage";
    }

    private static bool IsClosedState(string? value)
    {
        return value is States.Available or States.Reduced or States.Unavailable or States.NotRequested or States.Unknown or States.NotApplicable;
    }

    private static bool IsClosedCoverageEffect(string? value)
    {
        return value is Effects.FullSemantic or Effects.ReducedSemantic or Effects.SyntaxOnly or Effects.StructuralOnly or Effects.ConfigOnly or Effects.UnknownGap or Effects.Informational;
    }

    private static bool IsClosedGuidanceCode(string? value)
    {
        return value is GuidanceCodes.UseSemanticEvidenceWhenAvailable
            or GuidanceCodes.TreatAsReducedCoverage
            or GuidanceCodes.UseSyntaxFallbackEvidence
            or GuidanceCodes.ReviewProjectConfigSignals
            or GuidanceCodes.RestoreNotAttemptedNoAbsenceClaim
            or GuidanceCodes.ReviewSanitizedRestoreFailure
            or GuidanceCodes.ReviewGeneratedDesignTimeCoverage
            or GuidanceCodes.ReviewLegacyToolchainSignals
            or GuidanceCodes.ReviewUnknownCapabilityGap;
    }

    private static bool IsClosedLimitationCode(string? value)
    {
        return value is LimitationCodes.SemanticStatusDerived
            or LimitationCodes.SyntaxFallbackOnly
            or LimitationCodes.ProjectConfigStaticOnly
            or LimitationCodes.RestoreNotAttempted
            or LimitationCodes.RestoreCategoryOnly
            or LimitationCodes.DesignTimeLinkageGap
            or LimitationCodes.LegacyToolchainStaticSignal
            or LimitationCodes.UnknownToolchainGap
            or LimitationCodes.CoverageContextOnly;
    }

    private static string GuidanceText(string guidanceCode)
    {
        return guidanceCode switch
        {
            GuidanceCodes.UseSemanticEvidenceWhenAvailable => "Semantic evidence was available for the selected scope.",
            GuidanceCodes.TreatAsReducedCoverage => "Known gaps reduce confidence; no-evidence rows remain coverage-relative.",
            GuidanceCodes.UseSyntaxFallbackEvidence => "Syntax fallback evidence is available but is not compiler-resolved proof.",
            GuidanceCodes.ReviewProjectConfigSignals => "Project and config metadata explains static analyzer capability only.",
            GuidanceCodes.RestoreNotAttemptedNoAbsenceClaim => "Explicit restore was not requested; package absence was not inferred.",
            GuidanceCodes.ReviewSanitizedRestoreFailure => "Explicit restore failed with a sanitized category.",
            GuidanceCodes.ReviewGeneratedDesignTimeCoverage => "Generated or design-time linkage is incomplete or unknown.",
            GuidanceCodes.ReviewLegacyToolchainSignals => "Legacy framework or toolset signals appear relevant for full semantic analysis.",
            _ => "TraceMap could not classify the capability state precisely."
        };
    }

    private static string LimitationText(string limitationCode)
    {
        return limitationCode switch
        {
            LimitationCodes.SemanticStatusDerived => "Capability status summarizes scan behavior and is not a primary symbol observation.",
            LimitationCodes.SyntaxFallbackOnly => "Evidence is syntax or text fallback and cannot prove compiler-resolved behavior.",
            LimitationCodes.ProjectConfigStaticOnly => "Project and config metadata was inspected without evaluating arbitrary build logic.",
            LimitationCodes.RestoreNotAttempted => "Explicit restore was not requested and package absence is not inferred.",
            LimitationCodes.RestoreCategoryOnly => "Restore failure is represented by sanitized category only.",
            LimitationCodes.DesignTimeLinkageGap => "Generated or design-time artifacts are missing, malformed, unlinked, or unknown.",
            LimitationCodes.LegacyToolchainStaticSignal => "Framework and toolset signals are static guidance, not local installation proof.",
            LimitationCodes.CoverageContextOnly => "Downstream coverage context only; does not prove absence, impact, no-impact, or runtime behavior.",
            _ => "TraceMap could not prove the toolchain capability or cause."
        };
    }

    private static void AddIfPresent(IDictionary<string, string> properties, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            properties[key] = value;
        }
    }

    private sealed record CapabilityCandidate(
        string CapabilityCode,
        string CapabilityKind,
        string CapabilityState,
        string CoverageEffect,
        string RuleId,
        string EvidenceTier,
        string FilePath,
        int StartLine,
        int EndLine,
        string? ProjectPath,
        string SourceScope,
        string? ProjectStyle,
        string? FrameworkFamily,
        IReadOnlyList<CodeFact> SupportingFacts,
        string GuidanceCode,
        string LimitationCode,
        string? StrongestSupportingEvidenceTier);
}
