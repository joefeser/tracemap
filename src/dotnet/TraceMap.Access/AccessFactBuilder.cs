using TraceMap.Core;

namespace TraceMap.Access;

public static class AccessFactBuilder
{
    public const string ScannerVersion = "tracemap-access/0.1.0";
    private const string ExtractorId = "AccessCatalogExtractor";

    public static ScanResult Build(AccessValidatedInput input, AccessDatabaseProjection projection, AccessScanOptions options, AccessLimits? limits = null)
    {
        limits ??= AccessLimits.Default;
        if (!string.Equals(input.DatabaseHash, projection.DatabaseHash, StringComparison.Ordinal))
            throw new AccessScanException("AccessProjectionHashMismatch");

        if (limits.MaxFacts < 1 || limits.MaxGaps < 1) throw new AccessScanException("AccessInvalidLimitConfiguration");
        var gapsTruncated = projection.Gaps.Count > limits.MaxGaps;
        var projectedGaps = projection.Gaps
            .OrderBy(gap => gap.Classification, StringComparer.Ordinal)
            .ThenBy(gap => gap.StableScopeKey, StringComparer.Ordinal)
            .Take(gapsTruncated ? Math.Max(0, limits.MaxGaps - 1) : limits.MaxGaps)
            .ToList();
        if (gapsTruncated)
            projectedGaps.Add(new("AccessGapLimitReached", "database", null));
        var gapNames = projectedGaps.Select(gap => gap.Classification).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var scanId = "access-scan-" + FactFactory.Hash(string.Join('|',
            "access-scan/v1",
            input.RepoName,
            input.CommitSha,
            input.DatabaseRelativePath,
            input.DatabaseHash,
            projection.AccessVersion,
            options.TimeoutSeconds,
            ScannerVersions.AccessExtractor), 32);
        var manifest = new ScanManifest(
            scanId,
            input.RepoName,
            input.RemoteUrl,
            input.Branch,
            input.CommitSha,
            ScannerVersion,
            DateTimeOffset.UtcNow,
            "Level1SemanticAnalysisReduced",
            "FailedOrPartial",
            [],
            [],
            [],
            gapNames,
            ScanRootRelativePath: ".",
            ScanRootPathHash: FactFactory.Hash(input.DatabaseRelativePath, 32),
            GitRootHash: null);

        var facts = new List<CodeFact>();
        var span = Span(input.DatabaseRelativePath);
        var databaseIdentitySeed = AccessSafeValues.DatabaseIdentitySeed(input.RepositoryIdentityHash, input.CommitSha, input.DatabaseRelativePath, input.DatabaseHash);
        var databaseStableKey = AccessSafeValues.DatabaseStableKey(databaseIdentitySeed);

        facts.Add(Create(manifest, FactTypes.FileInventoried, RuleIds.LegacyAccessDatabaseInventory, EvidenceTiers.Tier2Structural, span,
            properties: Props(
                ("fileKind", "MicrosoftAccessDatabase"),
                ("databaseHash", input.DatabaseHash),
                ("databaseExtension", input.DatabaseExtension),
                ("databaseStableKey", databaseStableKey),
                ("sizeBytes", new FileInfo(input.DatabaseFullPath).Length.ToString(System.Globalization.CultureInfo.InvariantCulture)))));
        facts.Add(Create(manifest, FactTypes.LegacyDataMetadataDeclared, RuleIds.LegacyAccessDatabaseInventory, EvidenceTiers.Tier2Structural, span,
            targetSymbol: databaseStableKey,
            properties: Props(
                ("metadataFormat", "microsoft-access"),
                ("modelKind", "access-database"),
                ("descriptorRole", "database-catalog"),
                ("metadataHash", input.DatabaseHash),
                ("stableModelKey", databaseStableKey),
                ("omittedSystemObjectCount", projection.OmittedSystemObjectCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("formCount", projection.UiInventory?.FormCount?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("reportCount", projection.UiInventory?.ReportCount?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("formsReportsCoverage", projection.UiInventory?.Coverage),
                ("vbaModuleCount", projection.VbaInventory?.ModuleCount?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("vbaLoadedModuleCountUnchanged", projection.VbaInventory?.LoadedModuleCountUnchanged is bool loadedModuleCountUnchanged
                    ? loadedModuleCountUnchanged.ToString().ToLowerInvariant()
                    : null),
                ("vbaCoverage", projection.VbaInventory?.Coverage),
                ("coverageLabel", "reduced-static-design"),
                ("limitations", "binary-container-span;no-rows;no-execution;no-runtime-proof"))));

        foreach (var capability in projection.Capabilities.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            facts.Add(Create(manifest, FactTypes.AnalyzerCapabilityDiagnostic, RuleIds.LegacyAccessDatabaseInventory, EvidenceTiers.Tier2Structural, span,
                targetSymbol: databaseStableKey,
                properties: Props(("capability", capability.Name), ("status", capability.Status), ("accessVersion", projection.AccessVersion), ("databaseStableKey", databaseStableKey))));
        }

        foreach (var table in projection.Tables)
        {
            facts.Add(Create(manifest, FactTypes.LegacyDataEntityDeclared, RuleIds.LegacyAccessSchema, EvidenceTiers.Tier2Structural, span,
                targetSymbol: table.Identity.StableKey,
                properties: IdentityProps(table.Identity,
                    ("entityKind", "table"), ("modelKind", "access-table"), ("descriptorRole", "logical-entity"),
                    ("stableModelKey", table.Identity.StableKey), ("databaseStableKey", databaseStableKey), ("coverageLabel", "catalog-observed"))));
            facts.Add(Create(manifest, FactTypes.LegacyDataStorageObjectDeclared, RuleIds.LegacyAccessSchema, EvidenceTiers.Tier2Structural, span,
                targetSymbol: table.Identity.StableKey,
                properties: IdentityProps(table.Identity,
                    ("storageObjectKind", "table"), ("modelKind", "access-table"), ("descriptorRole", "storage-object"),
                    ("stableModelKey", table.Identity.StableKey), ("databaseStableKey", databaseStableKey), ("coverageLabel", "catalog-observed"))));

            foreach (var field in table.Fields)
            {
                facts.Add(Create(manifest, FactTypes.LegacyDataColumnDeclared, RuleIds.LegacyAccessSchema, EvidenceTiers.Tier2Structural, span,
                    sourceSymbol: table.Identity.StableKey,
                    targetSymbol: field.Identity.StableKey,
                    properties: IdentityProps(field.Identity,
                        ("parentStableKey", table.Identity.StableKey), ("stableModelKey", field.Identity.StableKey),
                        ("ordinal", field.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        ("dataTypeFamily", field.TypeFamily), ("declaredSize", field.DeclaredSize.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        ("required", field.Required.ToString().ToLowerInvariant()), ("descriptorRole", "column"))));
            }

            foreach (var index in table.Indexes)
            {
                facts.Add(Create(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyAccessSchema, EvidenceTiers.Tier2Structural, span,
                    sourceSymbol: table.Identity.StableKey,
                    targetSymbol: index.Identity.StableKey,
                    properties: IdentityProps(index.Identity,
                        ("mappingKind", "table-index"), ("sourceStableKey", table.Identity.StableKey),
                        ("indexPrimary", index.Primary.ToString().ToLowerInvariant()), ("indexUnique", index.Unique.ToString().ToLowerInvariant()),
                        ("fieldStableKeys", string.Join(';', index.FieldStableKeys)))));
            }
        }

        foreach (var relationship in projection.Relationships)
        {
            facts.Add(Create(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyAccessSchema, EvidenceTiers.Tier2Structural, span,
                sourceSymbol: relationship.SourceTableStableKey,
                targetSymbol: relationship.TargetTableStableKey,
                properties: IdentityProps(relationship.Identity,
                    ("mappingKind", "declared-relationship"), ("stableModelKey", relationship.Identity.StableKey),
                    ("sourceStableKey", relationship.SourceTableStableKey), ("targetStableKey", relationship.TargetTableStableKey),
                    ("relationshipAttributes", relationship.Attributes.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("fieldPairs", string.Join(';', relationship.Fields.OrderBy(field => field.Ordinal).Select(field => $"{field.SourceFieldStableKey}>{field.TargetFieldStableKey}"))))));
        }

        foreach (var query in projection.Queries)
        {
            facts.Add(Create(manifest, FactTypes.AccessQueryDeclared, RuleIds.LegacyAccessQuery, EvidenceTiers.Tier2Structural, span,
                targetSymbol: query.Identity.StableKey,
                properties: IdentityProps(query.Identity,
                    ("queryKind", query.QueryKind), ("sqlHash", query.SqlHash), ("sqlLength", query.SqlLength.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("referenceCoverage", query.ReferenceCoverage), ("parameterCount", query.Parameters.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("parameterDescriptors", string.Join(';', query.Parameters.OrderBy(parameter => parameter.Ordinal)
                        .Select(parameter => $"{parameter.Ordinal}:{parameter.Identity.StableKey}:{parameter.TypeFamily}"))),
                    ("isPassThrough", query.IsPassThrough.ToString().ToLowerInvariant()),
                    ("stableQueryKey", query.Identity.StableKey))));

            foreach (var dependency in query.Dependencies)
            {
                facts.Add(Create(manifest, FactTypes.AccessQueryDependencyCandidate, RuleIds.LegacyAccessQuery, EvidenceTiers.Tier3SyntaxOrTextual, span,
                    sourceSymbol: query.Identity.StableKey,
                    targetSymbol: dependency.TargetStableKey,
                    properties: Props(("sourceQueryStableKey", query.Identity.StableKey), ("targetStableKey", dependency.TargetStableKey),
                        ("targetKind", dependency.TargetKind), ("coverageLabel", dependency.Coverage), ("limitations", "static-access-sql-shape-only;no-execution"))));
            }
        }

        foreach (var boundary in projection.ExternalLinks)
        {
            facts.Add(Create(manifest, FactTypes.AccessExternalLinkDeclared, RuleIds.LegacyAccessExternalLink, EvidenceTiers.Tier2Structural, span,
                sourceSymbol: boundary.Identity.StableKey,
                properties: Props(("sourceObjectStableKey", boundary.Identity.StableKey), ("boundaryKind", boundary.BoundaryKind),
                    ("sourceKind", boundary.SourceKind), ("externalSourceHash", boundary.SourceHash),
                    ("coverageLabel", "hash-only-boundary"), ("limitations", "no-refresh;no-connectivity-proof;no-execution"))));
        }

        foreach (var surface in projection.UiSurfaces ?? [])
        {
            var surfaceFactType = surface.SurfaceKind == "report" ? FactTypes.AccessReportDeclared : FactTypes.AccessFormDeclared;
            facts.Add(Create(manifest, surfaceFactType, RuleIds.LegacyAccessUiSurface, EvidenceTiers.Tier2Structural, span,
                targetSymbol: surface.Identity.StableKey,
                properties: IdentityProps(surface.Identity,
                    ("surfaceKind", surface.SurfaceKind), ("stableSurfaceKey", surface.Identity.StableKey),
                    ("modulePresence", surface.ModulePresence), ("boundState", surface.BoundState),
                    ("designHash", surface.DesignHash), ("controlCount", surface.Controls.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("controlTypeCounts", ControlTypeCounts(surface.Controls)), ("eventDescriptors", EventDescriptors(surface.Events)),
                    ("coverageLabel", surface.Coverage),
                    ("limitations", "no-render;no-invocation;no-runtime-reachability;binary-container-span"))));
            AddBindingFacts(facts, manifest, span, surface.Bindings);

            foreach (var control in surface.Controls)
            {
                facts.Add(Create(manifest, FactTypes.AccessControlDeclared, RuleIds.LegacyAccessUiSurface, EvidenceTiers.Tier2Structural, span,
                    sourceSymbol: surface.Identity.StableKey,
                    targetSymbol: control.Identity.StableKey,
                    properties: IdentityProps(control.Identity,
                        ("surfaceStableKey", control.SurfaceStableKey), ("stableControlKey", control.Identity.StableKey),
                        ("controlType", control.ControlType), ("ordinal", control.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        ("eventDescriptors", EventDescriptors(control.Events)), ("coverageLabel", "static-design-metadata"),
                        ("limitations", "no-render;no-value-read;no-event-execution"))));
                AddBindingFacts(facts, manifest, span, control.Bindings);
            }
        }

        foreach (var module in projection.VbaModules ?? [])
        {
            var moduleSpan = VbaSpan(input.DatabaseRelativePath, 1, Math.Max(1, module.LineCount));
            facts.Add(Create(manifest, FactTypes.AccessVbaModuleDeclared, RuleIds.LegacyAccessVba, EvidenceTiers.Tier3SyntaxOrTextual, moduleSpan,
                targetSymbol: module.Identity.StableKey,
                properties: IdentityProps(module.Identity,
                    ("moduleStableKey", module.Identity.StableKey), ("moduleKind", module.ModuleKind),
                    ("moduleHash", module.ModuleHash), ("lineCount", module.LineCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("procedureCount", module.Procedures.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("coverageLabel", module.Coverage),
                    ("limitations", "bounded-textual-evidence;no-source-persisted;no-execution;no-runtime-dispatch-proof"))));

            foreach (var procedure in module.Procedures)
            {
                var procedureSpan = VbaSpan(input.DatabaseRelativePath, procedure.StartLine, procedure.EndLine);
                facts.Add(Create(manifest, FactTypes.AccessVbaProcedureDeclared, RuleIds.LegacyAccessVba, EvidenceTiers.Tier3SyntaxOrTextual, procedureSpan,
                    sourceSymbol: module.Identity.StableKey,
                    targetSymbol: procedure.Identity.StableKey,
                    properties: IdentityProps(procedure.Identity,
                        ("moduleStableKey", procedure.ModuleStableKey), ("procedureStableKey", procedure.Identity.StableKey),
                        ("procedureKind", procedure.ProcedureKind),
                        ("callCount", procedure.Calls.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        ("limitations", "declaration-shape-only;no-runtime-dispatch-proof"))));

                foreach (var call in procedure.Calls)
                {
                    var properties = IdentityProps(call.Identity,
                        ("procedureStableKey", call.ProcedureStableKey), ("stableCallKey", call.Identity.StableKey),
                        ("callKind", call.CallKind), ("targetStableKey", call.TargetStableKey),
                        ("targetKind", call.TargetKind), ("expressionHash", call.ExpressionHash),
                        ("expressionLength", call.ExpressionLength > 0 ? call.ExpressionLength.ToString(System.Globalization.CultureInfo.InvariantCulture) : null),
                        ("coverageLabel", call.Coverage),
                        ("limitations", "bounded-static-candidate;no-execution;no-branch-or-runtime-target-proof"));
                    if (call.LiteralTargetIdentity is not null)
                    {
                        properties["literalTargetNameHash"] = call.LiteralTargetIdentity.NameHash;
                        if (call.LiteralTargetIdentity.DisplayName is not null)
                            properties["literalTargetName"] = call.LiteralTargetIdentity.DisplayName;
                    }
                    facts.Add(Create(manifest, FactTypes.AccessNavigationCandidate, RuleIds.LegacyAccessVba, EvidenceTiers.Tier3SyntaxOrTextual,
                        VbaSpan(input.DatabaseRelativePath, call.StartLine, call.EndLine),
                        sourceSymbol: call.ProcedureStableKey,
                        targetSymbol: call.TargetStableKey,
                        properties: properties));
                }
            }
        }

        foreach (var binding in projection.EventBindings ?? [])
        {
            facts.Add(Create(manifest, FactTypes.AccessEventBindingCandidate, RuleIds.LegacyAccessEventBinding, EvidenceTiers.Tier3SyntaxOrTextual, span,
                sourceSymbol: binding.OwnerStableKey,
                targetSymbol: binding.ProcedureStableKey,
                properties: Props(
                    ("ownerStableKey", binding.OwnerStableKey), ("eventRole", binding.EventRole),
                    ("moduleStableKey", binding.ModuleStableKey), ("procedureStableKey", binding.ProcedureStableKey),
                    ("coverageLabel", binding.Coverage),
                    ("limitations", "exact-same-module-static-candidate;no-event-execution-or-runtime-dispatch-proof"))));
        }

        foreach (var gap in projectedGaps)
        {
            facts.Add(Create(manifest, FactTypes.AnalysisGap, gap.RuleId ?? RuleIds.LegacyAccessCoverageGap, EvidenceTiers.Tier4Unknown, span,
                targetSymbol: gap.StableScopeKey,
                properties: Props(("classification", gap.Classification), ("gapKind", "access-design"), ("scopeKind", gap.ScopeKind),
                    ("scopeStableKey", gap.StableScopeKey), ("limitations", "unable-to-prove;not-clean-absence"))));
        }

        facts = facts
            .GroupBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        if (facts.Count > limits.MaxFacts)
        {
            manifest = manifest with { KnownGaps = manifest.KnownGaps.Append("AccessFactLimitReached").Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray() };
            var limitGap = Create(manifest, FactTypes.AnalysisGap, RuleIds.LegacyAccessCoverageGap, EvidenceTiers.Tier4Unknown, span,
                targetSymbol: databaseStableKey,
                properties: Props(("classification", "AccessFactLimitReached"), ("gapKind", "access-design"), ("scopeKind", "database"),
                    ("scopeStableKey", databaseStableKey), ("limitations", "fact-ceiling-reached;remaining-projections-omitted")));
            facts = facts
                .OrderByDescending(fact => fact.FactType is FactTypes.FileInventoried or FactTypes.LegacyDataMetadataDeclared)
                .ThenBy(fact => fact.FactType, StringComparer.Ordinal)
                .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
                .Take(limits.MaxFacts - 1)
                .Append(limitGap)
                .ToList();
        }
        var ordered = facts.OrderBy(fact => fact.FactType, StringComparer.Ordinal).ThenBy(fact => fact.FactId, StringComparer.Ordinal).ToArray();
        return new ScanResult(manifest, ordered, [new FileInventoryItem(input.DatabaseRelativePath, "MicrosoftAccessDatabase", new FileInfo(input.DatabaseFullPath).Length)]);
    }

    private static CodeFact Create(ScanManifest manifest, string type, string rule, string tier, EvidenceSpan span,
        string? sourceSymbol = null, string? targetSymbol = null, IReadOnlyDictionary<string, string>? properties = null) =>
        FactFactory.Create(manifest, type, rule, tier, span, sourceSymbol: sourceSymbol, targetSymbol: targetSymbol, properties: properties);

    private static EvidenceSpan Span(string path) => new(path, 1, 1, null, ExtractorId, ScannerVersions.AccessExtractor);

    private static EvidenceSpan VbaSpan(string path, int startLine, int endLine) =>
        new(path, Math.Max(1, startLine), Math.Max(Math.Max(1, startLine), endLine), null, ExtractorId, ScannerVersions.AccessExtractor);

    private static void AddBindingFacts(List<CodeFact> facts, ScanManifest manifest, EvidenceSpan span, IReadOnlyList<AccessBindingProjection> bindings)
    {
        foreach (var binding in bindings.OrderBy(item => item.Identity.StableKey, StringComparer.Ordinal))
        {
            var targets = binding.TargetStableKeys.Count == 0 ? new string?[] { null } : binding.TargetStableKeys.Cast<string?>().ToArray();
            foreach (var target in targets)
            {
                facts.Add(Create(manifest, FactTypes.AccessBindingDeclared, RuleIds.LegacyAccessBinding,
                    binding.SourceKind is "direct-object" or "direct-field" ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier3SyntaxOrTextual,
                    span,
                    sourceSymbol: binding.OwnerStableKey,
                    targetSymbol: target,
                    properties: IdentityProps(binding.Identity,
                        ("ownerStableKey", binding.OwnerStableKey), ("stableBindingKey", binding.Identity.StableKey),
                        ("bindingKind", binding.BindingKind), ("sourceKind", binding.SourceKind),
                        ("expressionHash", binding.ExpressionHash),
                        ("expressionLength", binding.ExpressionLength > 0 ? binding.ExpressionLength.ToString(System.Globalization.CultureInfo.InvariantCulture) : null),
                        ("targetStableKey", target), ("targetKind", binding.TargetKind), ("coverageLabel", binding.Coverage),
                        ("limitations", "declared-static-binding-only;no-evaluation;no-runtime-target-proof"))));
            }
        }
    }

    private static string ControlTypeCounts(IReadOnlyList<AccessControlProjection> controls) =>
        string.Join(';', controls.GroupBy(item => item.ControlType, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}:{group.Count()}"));

    private static string EventDescriptors(IReadOnlyList<AccessUiEventProjection> events) =>
        string.Join(';', events.OrderBy(item => item.EventRole, StringComparer.Ordinal)
            .Select(item => item.ValueHash is null
                ? $"{item.EventRole}:{item.Category}"
                : $"{item.EventRole}:{item.Category}:{item.ValueLength}:{item.ValueHash}"));

    private static SortedDictionary<string, string> IdentityProps(AccessSafeIdentity identity, params (string Key, string? Value)[] values)
    {
        var properties = Props(values);
        properties["objectNameHash"] = identity.NameHash;
        if (identity.DisplayName is not null) properties["objectName"] = identity.DisplayName;
        return properties;
    }

    private static SortedDictionary<string, string> Props(params (string Key, string? Value)[] values)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values) if (!string.IsNullOrWhiteSpace(value)) properties[key] = value;
        return properties;
    }
}
