using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Text;

namespace TraceMap.Core;

public static partial class LegacyWebFormsExtractor
{
    private static readonly Lazy<string> EntryGeneratorSha256 = new(() => Convert.ToHexStringLower(
        SHA256.HashData(GeneratorBinaryIdentity.ReadBytes(typeof(LegacyWebFormsExtractor).Assembly))));
    private sealed class EntryEvidenceIndex(IReadOnlyList<CodeFact> facts)
    {
        internal ILookup<string, CodeFact> ById { get; } = facts.ToLookup(f => f.FactId, StringComparer.Ordinal);
        internal ILookup<string, CodeFact> TypesByPath { get; } = facts.Where(f => f.FactType == FactTypes.TypeDeclared)
            .ToLookup(f => f.Evidence.FilePath, StringComparer.Ordinal);
        internal ILookup<string, CodeFact> PropertiesByOwner { get; } = facts.Where(f => f.FactType == FactTypes.PropertyDeclared)
            .ToLookup(f => f.Properties.GetValueOrDefault("targetContainingSymbolId") ?? "", StringComparer.Ordinal);
        internal ILookup<string, CodeFact> MembersByOwner { get; } = facts.Where(f => f.EvidenceTier == EvidenceTiers.Tier1Semantic
                && f.FactType is FactTypes.FieldDeclared or FactTypes.PropertyDeclared or FactTypes.MethodDeclared or FactTypes.EventDeclared)
            .ToLookup(f => f.Properties.GetValueOrDefault("targetContainingSymbolId") ?? "", StringComparer.Ordinal);
        internal ILookup<string, CodeFact> BasesByType { get; } = facts.Where(f => f.FactType == FactTypes.SymbolRelationship
                && f.EvidenceTier == EvidenceTiers.Tier1Semantic && f.ContractElement == "InheritsFrom")
            .ToLookup(f => f.Properties.GetValueOrDefault("sourceSymbolId") ?? "", StringComparer.Ordinal);
        internal ILookup<string, CodeFact> MethodsById { get; } = facts.Where(f => f.FactType == FactTypes.MethodDeclared)
            .ToLookup(f => f.Properties.GetValueOrDefault("targetSymbolId") ?? "", StringComparer.Ordinal);
        internal IReadOnlyList<CodeFact> Overrides { get; } = facts.Where(f => f.FactType == FactTypes.SymbolRelationship
            && f.RuleId is RuleIds.VisualBasicSemanticSymbolRelationship or RuleIds.CSharpSemanticSymbolRelationship
            && f.ContractElement == "Overrides" && f.EvidenceTier == EvidenceTiers.Tier1Semantic
            && f.Properties.ContainsKey("webFormsFrameworkEntry")).ToArray();
    }

    private static void AddEntryRelationships(string repoPath, ScanManifest manifest, WebFormsPage page,
        CodeFact pageFact, IReadOnlyList<CodeFact> controls, WebFormsContext context,
        EntryEvidenceIndex existing, List<CodeFact> facts)
    {
        var comparison = IsVisualBasicPage(page) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var candidates = new Dictionary<string, (CodeFact Relation, CodeFact? Type)>(
            IsVisualBasicPage(page) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var control in controls)
        {
            var id = control.Properties.GetValueOrDefault("controlId") ?? "";
            if (controls.Count(c => string.Equals(c.Properties.GetValueOrDefault("controlId"), id, comparison)) != 1)
            {
                EntryGap(manifest, control.Evidence, RuleIds.WebFormsEntryContainment, "AmbiguousControlId", [pageFact, control], facts);
                continue;
            }
            var registrations = page.Registrations.Where(r =>
                string.Equals(r.TagPrefix, control.Properties.GetValueOrDefault("controlPrefix"), StringComparison.OrdinalIgnoreCase)
                && (r.RegistrationShape != "src"
                    || string.Equals(r.TagName, control.Properties.GetValueOrDefault("controlType"), StringComparison.OrdinalIgnoreCase))).ToArray();
            if (registrations.Length == 0) continue;
            if (registrations.Length != 1)
            {
                EntryGap(manifest, control.Evidence, RuleIds.WebFormsEntryContainment, "MissingOrAmbiguousRegistration", [pageFact, control], facts);
                continue;
            }
            var compositions = facts.Where(f => f.FactType == FactTypes.WebFormsCompositionDeclared
                && f.SourceSymbol == control.TargetSymbol
                && f.Properties.GetValueOrDefault("relationshipKind") is "UsesRegisteredUserControl" or "UsesRegisteredAssemblyControl" or "UsesRegisteredAppCodeControlCandidate").ToArray();
            if (compositions.Length != 1)
            {
                EntryGap(manifest, control.Evidence, RuleIds.WebFormsEntryContainment, "UnresolvedRegistrationTarget", [pageFact, control], facts);
                continue;
            }
            var composition = compositions[0];
            var supports = new List<CodeFact> { pageFact, control, composition };
            CodeFact? type = null;
            if (control.Properties.GetValueOrDefault("registrationTypeFactId") is { } typeId)
                type = existing.ById[typeId].Take(2).ToArray() is [var uniqueType] ? uniqueType : null;
            else if (control.Properties.GetValueOrDefault("registeredSourcePath") is { } sourcePath)
            {
                var surfaces = context.Pages.Where(p => p.FilePath == sourcePath).ToArray();
                if (surfaces.Length == 1)
                {
                    var targetPage = surfaces[0];
                    supports.Add(CreatePageFact(manifest, targetPage));
                    var types = existing.TypesByPath[targetPage.LinkedCodePath ?? ""].Where(f => f.FactType == FactTypes.TypeDeclared
                        && f.EvidenceTier == EvidenceTiers.Tier1Semantic
                        && string.Equals(WebFormsDeclarationQualifiedName(f), targetPage.PageTypeName, IsVisualBasicPage(targetPage) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                        && f.Evidence.FilePath == targetPage.LinkedCodePath).ToArray();
                    if (types.Length == 1) type = types[0];
                }
            }
            if (type is not null) supports.Add(type);
            var markupControl = page.Controls.Single(c => string.Equals(c.ControlId, id, comparison));
            var relation = EntryFact(manifest, RuleIds.WebFormsEntryContainment, composition.EvidenceTier,
                control.Evidence with { EndLine = markupControl.EndLine }, "control-containment", SurfaceIdentity(page.FilePath), control.TargetSymbol!,
                type is null ? "Control type unavailable; construction and entry execution are unproven."
                    : "Control construction and entry execution are unproven.", supports,
                new Dictionary<string, string> { ["controlId"] = id, ["targetTypeSymbolId"] = type?.Properties.GetValueOrDefault("targetSymbolId") ?? "",
                    ["targetType"] = type?.TargetSymbol ?? "",
                    ["targetFilePath"] = control.Properties.GetValueOrDefault("registeredSourcePath") ?? "", ["surfaceIdentity"] = SurfaceIdentity(page.FilePath) });
            facts.Add(relation);
            candidates[id] = (relation, type);
        }

        var markup = page.MarkupSnapshot;
        if (markup is null)
        {
            EntryGap(manifest, pageFact.Evidence, RuleIds.WebFormsEntryProperty, "MarkupUnavailable", [pageFact], facts);
            return;
        }
        var source = SourceText.From(markup);
        var masked = Regex.Replace(MaskServerComments(markup), @"<!--[\s\S]*?-->", m => new string(' ', m.Length), RegexOptions.None, TimeSpan.FromSeconds(1));
        // No naming-container inference: any template makes instance scope unavailable.
        var nestedScope = Regex.IsMatch(masked, @"<\s*(?:\w+:)?\w*Template\b", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        foreach (Match expression in InlineServerExpressionRegex().Matches(masked))
        {
            var evidence = new EvidenceSpan(page.FilePath, LineAt(source, expression.Index),
                LineAt(source, expression.Index + expression.Length - 1), FactFactory.Hash(expression.Value, 32),
                "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor);
            var parsed = Regex.Match(expression.Groups["body"].Value,
                @"^\s*(?<control>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<property>[A-Za-z_][A-Za-z0-9_]*)\s*$",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (expression.Groups["marker"].Value != "#" || !parsed.Success || nestedScope)
            {
                EntryGap(manifest, evidence, RuleIds.WebFormsEntryProperty, "UnsupportedExpressionOrScope", [pageFact], facts);
                continue;
            }
            if (!candidates.TryGetValue(parsed.Groups["control"].Value, out var candidate) || candidate.Type is null)
            {
                EntryGap(manifest, evidence, RuleIds.WebFormsEntryProperty, "UnresolvedControlType", [pageFact], facts);
                continue;
            }
            var scope = EntryPageScope(page, existing, comparison);
            if (scope is null)
            {
                EntryGap(manifest, evidence, RuleIds.WebFormsEntryProperty, "UnavailablePageScope", [pageFact, candidate.Relation], facts);
                continue;
            }
            var receiverMembers = scope.Value.TypeIds.SelectMany(id => existing.MembersByOwner[id])
                .Where(f => string.Equals(f.Properties.GetValueOrDefault("fieldName") ?? f.Properties.GetValueOrDefault("propertyName")
                    ?? f.Properties.GetValueOrDefault("name") ?? f.ContractElement, parsed.Groups["control"].Value, comparison))
                .OrderBy(f => f.FactId, StringComparer.Ordinal).Take(2).ToArray();
            if (receiverMembers.Length > 1 || receiverMembers.Any(f => f.FactType != FactTypes.FieldDeclared
                    || f.Properties.GetValueOrDefault("fieldType") != candidate.Type.TargetSymbol
                    || f.Properties.GetValueOrDefault("calleeAssemblyName") != candidate.Type.Properties.GetValueOrDefault("targetSymbolAssemblyName")
                    || f.Properties.GetValueOrDefault("calleeAssemblyVersion") != candidate.Type.Properties.GetValueOrDefault("targetSymbolAssemblyVersion")
                    || !f.Properties.ContainsKey("calleeAssemblyPublicKeyToken")
                    || !candidate.Type.Properties.ContainsKey("targetSymbolAssemblyPublicKeyToken")
                    || f.Properties["calleeAssemblyPublicKeyToken"] != candidate.Type.Properties["targetSymbolAssemblyPublicKeyToken"]))
            {
                EntryGap(manifest, evidence, RuleIds.WebFormsEntryProperty, "ConflictingControlReceiver",
                    new[] { pageFact, candidate.Relation, candidate.Type }.Concat(scope.Value.Support).Concat(receiverMembers).ToArray(), facts);
                continue;
            }
            var typeId = candidate.Type.Properties.GetValueOrDefault("targetSymbolId");
            var members = existing.PropertiesByOwner[typeId ?? ""].Where(f => f.FactType == FactTypes.PropertyDeclared
                && f.EvidenceTier == EvidenceTiers.Tier1Semantic && !string.IsNullOrEmpty(typeId)
                && f.Properties.GetValueOrDefault("targetContainingSymbolId") == typeId
                && f.SourceSymbol == candidate.Type.TargetSymbol
                && string.Equals(f.Properties.GetValueOrDefault("propertyName") ?? f.ContractElement, parsed.Groups["property"].Value, comparison)).ToArray();
            if (members.Length != 1)
            {
                EntryGap(manifest, evidence, RuleIds.WebFormsEntryProperty, "MissingOrAmbiguousProperty", [pageFact, candidate.Relation, candidate.Type], facts);
                continue;
            }
            var member = members[0];
            if (member.Properties.GetValueOrDefault("getterAccessibility") != "Public"
                || member.Properties.GetValueOrDefault("getterPubliclyAccessible") != "True"
                || member.Properties.GetValueOrDefault("parameterCount") != "0"
                || member.Properties.GetValueOrDefault("isDefault") != "False")
            {
                EntryGap(manifest, evidence, RuleIds.WebFormsEntryProperty, "UnavailablePublicGetter",
                    [pageFact, candidate.Relation, candidate.Type, member], facts);
                continue;
            }
            facts.Add(EntryFact(manifest, RuleIds.WebFormsEntryProperty, EvidenceTiers.Tier3SyntaxOrTextual,
                evidence, "declarative-property-reference", candidate.Relation.TargetSymbol!, member.TargetSymbol!,
                "Binding evaluation, receiver instance and getter invocation are unproven.",
                new[] { pageFact, candidate.Relation, candidate.Type, member }.Concat(scope.Value.Support).Concat(receiverMembers).ToArray(),
                new Dictionary<string, string> { ["targetSymbolId"] = member.Properties["targetSymbolId"],
                    ["surfaceIdentity"] = SurfaceIdentity(page.FilePath), ["controlId"] = parsed.Groups["control"].Value }));
        }
    }

    private static (IReadOnlySet<string> TypeIds, IReadOnlyList<CodeFact> Support)? EntryPageScope(
        WebFormsPage page, EntryEvidenceIndex index, StringComparison comparison)
    {
        var types = index.TypesByPath[page.LinkedCodePath ?? ""].Where(f => f.EvidenceTier == EvidenceTiers.Tier1Semantic
            && string.Equals(WebFormsDeclarationQualifiedName(f), page.PageTypeName, comparison)).ToArray();
        if (types.Length != 1 || types[0].Properties.GetValueOrDefault("targetSymbolId") is not { Length: > 0 } typeId) return null;
        var support = new List<CodeFact> { types[0] };
        var ids = new HashSet<string>(StringComparer.Ordinal) { typeId };
        while (true)
        {
            var bases = index.BasesByType[typeId].ToArray();
            if (bases.Length == 0) return (ids, support);
            var targets = bases.Select(f => f.Properties.GetValueOrDefault("targetSymbolId")).Distinct(StringComparer.Ordinal).ToArray();
            if (targets.Length != 1 || targets[0] is not { Length: > 0 } next || ids.Count >= 16 || !ids.Add(next)) return null;
            support.AddRange(bases);
            typeId = next;
        }
    }

    private static void AddLifecycleEntryRelationships(ScanManifest manifest, EntryEvidenceIndex existing, List<CodeFact> facts)
    {
        foreach (var witness in existing.Overrides)
        {
            var declarations = existing.MethodsById[witness.Properties.GetValueOrDefault("sourceSymbolId") ?? ""].Where(f => f.FactType == FactTypes.MethodDeclared
                && f.EvidenceTier == EvidenceTiers.Tier1Semantic
                && f.Properties.GetValueOrDefault("targetSymbolId") == witness.Properties.GetValueOrDefault("sourceSymbolId")
                && f.TargetSymbol == witness.SourceSymbol && f.ProjectPath == witness.ProjectPath).ToArray();
            if (declarations.Length != 1) continue;
            facts.Add(EntryFact(manifest, RuleIds.WebFormsEntryLifecycle, EvidenceTiers.Tier2Structural,
                witness.Evidence, "framework-lifecycle-candidate", witness.TargetSymbol!, witness.SourceSymbol!,
                "Framework dispatch, control construction and lifecycle execution are unproven.", [witness, declarations[0]],
                new Dictionary<string, string> { ["targetSymbolId"] = witness.Properties["sourceSymbolId"],
                    ["frameworkSymbolId"] = witness.Properties["targetSymbolId"],
                    ["frameworkAssemblyIdentity"] = witness.Properties["webFormsFrameworkAssemblyIdentity"] }));
        }
    }

    private static void EntryGap(ScanManifest manifest, EvidenceSpan evidence, string rule, string boundary,
        IReadOnlyList<CodeFact> supports, List<CodeFact> facts) => facts.Add(EntryFact(manifest, rule,
            EvidenceTiers.Tier4Unknown, evidence, "unresolved-entry", SurfaceIdentity(evidence.FilePath), "", boundary, supports,
            new Dictionary<string, string> { ["gapKind"] = boundary, ["message"] = "WebForms entry relationship unavailable: " + boundary }, true));

    private static CodeFact EntryFact(ScanManifest manifest, string rule, string tier, EvidenceSpan evidence,
        string kind, string source, string target, string boundary, IReadOnlyList<CodeFact> supports,
        IReadOnlyDictionary<string, string> extra, bool gap = false)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in extra) properties[key] = value;
        properties["relationshipKind"] = kind;
        properties["firstUnresolvedBoundary"] = boundary;
        properties["coverageLabel"] = gap ? "unavailable-webforms-entry" : "partial-static-webforms-entry";
        properties["supportingFactIds"] = string.Join(",", supports.Select(f => f.FactId).Distinct().Order(StringComparer.Ordinal));
        properties["generatorSha256"] = EntryGeneratorSha256.Value;
        properties["boundedInputSha256"] = FactFactory.Hash(JsonSerializer.Serialize(new { evidence, kind, source, target, extra, supports = supports.OrderBy(f => f.FactId, StringComparer.Ordinal) }), 64);
        return FactFactory.Create(manifest, gap ? FactTypes.AnalysisGap : FactTypes.WebFormsEntryRelationship,
            rule, tier, evidence with { ExtractorId = "LegacyWebFormsExtractor", ExtractorVersion = ScannerVersions.LegacyWebFormsExtractor },
            sourceSymbol: source, targetSymbol: target, contractElement: kind, properties: properties);
    }
}
