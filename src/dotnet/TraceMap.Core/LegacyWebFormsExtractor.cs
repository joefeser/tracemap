using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace TraceMap.Core;

public static partial class LegacyWebFormsExtractor
{
    private static readonly HashSet<string> SupportedEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnClick",
        "OnCommand",
        "OnSelectedIndexChanged",
        "OnTextChanged",
        "OnCheckedChanged",
        "OnRowCommand",
        "OnItemCommand",
        "OnLoad",
        "OnInit"
    };

    private static readonly HashSet<string> UiMemberNames = new(StringComparer.Ordinal)
    {
        "Text",
        "Visible",
        "Enabled",
        "CssClass",
        "Style",
        "DataSource"
    };

    public static IReadOnlyList<CodeFact> Extract(
        string repoPath,
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<CodeFact> existingFacts)
    {
        var context = BuildContext(repoPath, inventory);
        var facts = new List<CodeFact>();
        var designerFactsByPageAndField = new Dictionary<string, CodeFact>(StringComparer.Ordinal);

        foreach (var designer in context.Designers)
        {
            var fact = CreateDesignerFact(manifest, designer);
            facts.Add(fact);
            designerFactsByPageAndField[PageFieldKey(designer.PageTypeName, designer.FieldName)] = fact;
        }

        foreach (var page in context.Pages)
        {
            facts.Add(CreatePageFact(manifest, page));
            foreach (var control in page.Controls)
            {
                var designerFact = designerFactsByPageAndField.GetValueOrDefault(PageFieldKey(page.PageTypeName, control.ControlId));
                facts.Add(CreateControlFact(manifest, page, control, designerFact));
            }

            foreach (var binding in page.Bindings)
            {
                var designerFact = designerFactsByPageAndField.GetValueOrDefault(PageFieldKey(page.PageTypeName, binding.ControlId));
                var bindingFact = CreateEventBindingFact(manifest, page, binding, designerFact);
                facts.Add(bindingFact);
                AddHandlerResolutionFacts(manifest, page, binding, bindingFact, context, existingFacts, facts);
            }

            foreach (var gap in page.Gaps)
            {
                facts.Add(CreateGap(manifest, page.FilePath, gap.Line, gap.GapKind, gap.Message));
            }

            AddAutoWireupFacts(manifest, page, context, existingFacts, facts);
        }

        var allFacts = existingFacts.Concat(facts).ToArray();
        foreach (var resolution in facts.Where(fact => fact.FactType == FactTypes.WebFormsHandlerResolved).ToArray())
        {
            facts.Add(CreateFlowFact(manifest, resolution, allFacts));
            var logicSignal = CreateLogicSignalFact(manifest, resolution, context, allFacts);
            if (logicSignal is not null)
            {
                facts.Add(logicSignal);
            }
        }

        return facts
            .GroupBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    private static WebFormsContext BuildContext(string repoPath, IReadOnlyList<FileInventoryItem> inventory)
    {
        var pages = inventory
            .Where(item => item.Kind == "WebFormsMarkup")
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .Select(item => ParseMarkupFile(repoPath, item))
            .ToArray();
        var codeFiles = inventory
            .Where(item => item.Kind is "WebFormsCodeBehind" or "CSharp")
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .Select(item => ParseCodeFile(repoPath, item.RelativePath))
            .Where(file => file is not null)
            .Select(file => file!)
            .ToArray();
        var designers = inventory
            .Where(item => item.Kind == "WebFormsDesigner")
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .SelectMany(item => ParseDesignerFile(repoPath, item.RelativePath))
            .ToArray();

        return new WebFormsContext(pages, codeFiles, designers);
    }

    private static WebFormsPage ParseMarkupFile(string repoPath, FileInventoryItem file)
    {
        var fullPath = Path.Combine(repoPath, file.RelativePath);
        try
        {
            var text = File.ReadAllText(fullPath);
            var source = SourceText.From(text);
            var directive = DirectiveRegex().Matches(text).Cast<Match>().FirstOrDefault();
            var directiveAttributes = directive is null
                ? new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : ParseAttributes(directive.Groups["attrs"].Value);
            var directiveKind = directive?.Groups["kind"].Value ?? MarkupKind(file.RelativePath);
            var pageTypeName = SafeIdentifier(directiveAttributes.GetValueOrDefault("Inherits"))
                ?? SafeIdentifier(Path.GetFileNameWithoutExtension(file.RelativePath))
                ?? "unknown";
            var codeBehind = SafeMarkupPath(directiveAttributes.GetValueOrDefault("CodeBehind"));
            var codeFile = SafeMarkupPath(directiveAttributes.GetValueOrDefault("CodeFile"));
            var linkedCodePath = ResolveLinkedCodePath(file.RelativePath, codeBehind ?? codeFile);
            var autoEventWireup = ParseAutoEventWireup(directiveAttributes.GetValueOrDefault("AutoEventWireup"));
            var page = new WebFormsPage(
                file.RelativePath,
                directiveKind,
                pageTypeName,
                codeBehind,
                codeFile,
                SafeMarkupPath(directiveAttributes.GetValueOrDefault("MasterPageFile")),
                linkedCodePath,
                autoEventWireup,
                directive is null ? 1 : LineAt(source, directive.Index),
                [],
                [],
                directive is null
                    ? [new WebFormsGap("MalformedWebFormsDirective", "Unable to parse a WebForms page/control/master directive.", 1)]
                    : []);

            var controls = new List<WebFormsControl>();
            var bindings = new List<WebFormsBinding>();
            var gaps = page.Gaps.ToList();
            foreach (Match match in ServerControlRegex().Matches(text).Cast<Match>().OrderBy(match => match.Index))
            {
                var attrs = ParseAttributes(match.Groups["attrs"].Value);
                if (!IsServerControl(attrs))
                {
                    continue;
                }

                var line = LineAt(source, match.Index);
                var controlType = SafeIdentifier(match.Groups["type"].Value) ?? "unknown";
                var controlId = SafeIdentifier(attrs.GetValueOrDefault("ID")) ?? $"{controlType}@{line}";
                controls.Add(new WebFormsControl(controlType, controlId, line, FactFactory.Hash(match.Value, 32)));
                foreach (var (name, value) in attrs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    if (SupportedEvents.Contains(name) && LooksLikeHandlerName(value))
                    {
                        bindings.Add(new WebFormsBinding(controlType, controlId, name, SafeIdentifier(value)!, line, FactFactory.Hash(match.Value, 32)));
                    }
                    else if (name.StartsWith("On", StringComparison.OrdinalIgnoreCase)
                        && !SupportedEvents.Contains(name)
                        && LooksLikeHandlerName(value))
                    {
                        gaps.Add(new WebFormsGap("UnsupportedWebFormsEventAttribute", $"Unsupported WebForms event-like attribute `{name}` requires review.", line));
                    }
                }
            }

            return page with
            {
                Controls = controls.OrderBy(control => control.Line).ThenBy(control => control.ControlId, StringComparer.Ordinal).ThenBy(control => control.ControlType, StringComparer.Ordinal).ToArray(),
                Bindings = bindings.OrderBy(binding => binding.Line).ThenBy(binding => binding.ControlId, StringComparer.Ordinal).ThenBy(binding => binding.EventName, StringComparer.Ordinal).ThenBy(binding => binding.HandlerName, StringComparer.Ordinal).ToArray(),
                Gaps = gaps.OrderBy(gap => gap.Line).ThenBy(gap => gap.GapKind, StringComparer.Ordinal).ToArray()
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new WebFormsPage(file.RelativePath, MarkupKind(file.RelativePath), Path.GetFileNameWithoutExtension(file.RelativePath), null, null, null, ResolveLinkedCodePath(file.RelativePath, null), null, 1, [], [], [new WebFormsGap("UnreadableWebFormsMarkup", "Unable to read WebForms markup for extraction.", 1)]);
        }
    }

    private static WebFormsCodeFile? ParseCodeFile(string repoPath, string relativePath)
    {
        try
        {
            var text = File.ReadAllText(Path.Combine(repoPath, relativePath));
            var tree = CSharpSyntaxTree.ParseText(SourceText.From(text), path: relativePath);
            var root = tree.GetCompilationUnitRoot();
            var methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Select(method => ToMethodInfo(tree, method))
                .OrderBy(method => method.Line)
                .ThenBy(method => method.MethodName, StringComparer.Ordinal)
                .ToArray();
            var subscriptions = root.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(assignment => assignment.IsKind(SyntaxKind.AddAssignmentExpression))
                .Select(assignment => new WebFormsEventSubscription(assignment.Left.ToString(), assignment.Right.ToString()))
                .OrderBy(subscription => subscription.EventName, StringComparer.Ordinal)
                .ThenBy(subscription => subscription.HandlerName, StringComparer.Ordinal)
                .ToArray();
            return new WebFormsCodeFile(relativePath, methods, subscriptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IReadOnlyList<WebFormsDesignerField> ParseDesignerFile(string repoPath, string relativePath)
    {
        try
        {
            var text = File.ReadAllText(Path.Combine(repoPath, relativePath));
            var tree = CSharpSyntaxTree.ParseText(SourceText.From(text), path: relativePath);
            var root = tree.GetCompilationUnitRoot();
            var fields = new List<WebFormsDesignerField>();
            foreach (var variable in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (variable.Parent?.Parent is not FieldDeclarationSyntax fieldDeclaration)
                {
                    continue;
                }

                var containingClass = variable.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (containingClass is null)
                {
                    continue;
                }

                var span = tree.GetLineSpan(variable.Span);
                fields.Add(new WebFormsDesignerField(
                    relativePath,
                    QualifiedClassName(containingClass),
                    variable.Identifier.ValueText,
                    fieldDeclaration.Declaration.Type.ToString(),
                    span.StartLinePosition.Line + 1,
                    Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1)));
            }

            return fields
                .OrderBy(field => field.FilePath, StringComparer.Ordinal)
                .ThenBy(field => field.Line)
                .ThenBy(field => field.FieldName, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static WebFormsMethod ToMethodInfo(SyntaxTree tree, MethodDeclarationSyntax method)
    {
        var span = tree.GetLineSpan(method.Span);
        var containingClass = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        var parameterTypes = method.ParameterList.Parameters
            .Select(parameter => parameter.Type?.ToString() ?? string.Empty)
            .ToArray();
        var hasCommonEventSignature = parameterTypes.Length == 2
            && parameterTypes[0].EndsWith("object", StringComparison.OrdinalIgnoreCase)
            && parameterTypes[1].Contains("EventArgs", StringComparison.Ordinal);
        return new WebFormsMethod(
            tree.FilePath,
            QualifiedClassName(containingClass),
            method.Identifier.ValueText,
            span.StartLinePosition.Line + 1,
            Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1),
            hasCommonEventSignature,
            method);
    }

    private static void AddHandlerResolutionFacts(
        ScanManifest manifest,
        WebFormsPage page,
        WebFormsBinding binding,
        CodeFact bindingFact,
        WebFormsContext context,
        IReadOnlyList<CodeFact> existingFacts,
        List<CodeFact> facts)
    {
        var candidates = CandidateMethods(page, binding.HandlerName, context).ToArray();
        if (candidates.Length == 0)
        {
            facts.Add(CreateGap(manifest, page.FilePath, binding.Line, "MissingWebFormsHandler", $"No linked code-behind method matched handler `{binding.HandlerName}`."));
            return;
        }

        if (candidates.Length > 1)
        {
            facts.Add(CreateGap(manifest, page.FilePath, binding.Line, "AmbiguousWebFormsHandler", $"Multiple linked code-behind methods matched handler `{binding.HandlerName}`; TraceMap did not choose one."));
            return;
        }

        var method = candidates[0];
        facts.Add(CreateHandlerFact(manifest, page, binding, bindingFact, method, existingFacts, isAutoWireup: false));
    }

    private static void AddAutoWireupFacts(
        ScanManifest manifest,
        WebFormsPage page,
        WebFormsContext context,
        IReadOnlyList<CodeFact> existingFacts,
        List<CodeFact> facts)
    {
        foreach (var (handlerName, eventName) in new[] { ("Page_Load", "OnLoad"), ("Page_Init", "OnInit") })
        {
            var candidates = CandidateMethods(page, handlerName, context).ToArray();
            if (candidates.Length == 0)
            {
                continue;
            }

            var hasExplicitSubscription = HasExplicitEventSubscription(page, handlerName, eventName, context);
            if (page.AutoEventWireup != true && !hasExplicitSubscription)
            {
                facts.Add(CreateGap(manifest, page.FilePath, page.DirectiveLine, "AutoEventWireupUnavailable", $"Auto-event-wireup handler `{handlerName}` is visible, but explicit enabled evidence is absent."));
                continue;
            }

            if (candidates.Length > 1)
            {
                facts.Add(CreateGap(manifest, page.FilePath, page.DirectiveLine, "AmbiguousAutoEventWireupHandler", $"Multiple linked code-behind methods matched auto-event-wireup handler `{handlerName}`."));
                continue;
            }

            var syntheticBinding = new WebFormsBinding(page.DirectiveKind, page.PageTypeName, eventName, handlerName, page.DirectiveLine, null);
            var bindingFact = FactFactory.Create(
                manifest,
                FactTypes.WebFormsEventBindingDeclared,
                RuleIds.LegacyWebFormsEventBinding,
                EvidenceTiers.Tier3SyntaxOrTextual,
                new EvidenceSpan(page.FilePath, page.DirectiveLine, page.DirectiveLine, null, "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor),
                targetSymbol: handlerName,
                contractElement: handlerName,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["bindingKind"] = "AutoEventWireup",
                    ["controlId"] = page.PageTypeName,
                    ["controlType"] = page.DirectiveKind,
                    ["eventName"] = eventName,
                    ["handlerName"] = handlerName,
                    ["pageTypeName"] = page.PageTypeName,
                    ["ruleLimitations"] = "Static auto-event-wireup evidence does not prove page lifecycle execution."
            });
            facts.Add(bindingFact);
            facts.Add(CreateHandlerFact(manifest, page, syntheticBinding, bindingFact, candidates[0], existingFacts, isAutoWireup: page.AutoEventWireup == true, hasExplicitSubscription));
        }
    }

    private static IEnumerable<WebFormsMethod> CandidateMethods(WebFormsPage page, string handlerName, WebFormsContext context)
    {
        var linkedCodePath = page.LinkedCodePath;
        return context.CodeFiles
            .Where(file => linkedCodePath is null || file.FilePath.Equals(linkedCodePath, StringComparison.Ordinal))
            .SelectMany(file => file.Methods)
            .Where(method => method.MethodName.Equals(handlerName, StringComparison.Ordinal))
            .Where(method => PageTypeMatches(page.PageTypeName, method.PageTypeName))
            .OrderBy(method => method.FilePath, StringComparer.Ordinal)
            .ThenBy(method => method.Line)
            .ToArray();
    }

    private static CodeFact CreatePageFact(ScanManifest manifest, WebFormsPage page)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["directiveKind"] = page.DirectiveKind,
            ["pageTypeName"] = page.PageTypeName,
            ["ruleLimitations"] = "WebForms file inventory is static evidence and does not prove runtime page activation."
        };
        AddOptional(properties, "codeBehindPath", page.CodeBehindPath);
        AddOptional(properties, "codeFilePath", page.CodeFilePath);
        AddOptional(properties, "linkedCodePath", page.LinkedCodePath);
        AddOptional(properties, "masterPageFile", page.MasterPageFile);
        if (page.AutoEventWireup is not null)
        {
            properties["autoEventWireup"] = page.AutoEventWireup.Value.ToString();
        }

        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsPageDeclared,
            RuleIds.LegacyWebFormsInventory,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(page.FilePath, page.DirectiveLine, page.DirectiveLine, null, "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor),
            targetSymbol: page.PageTypeName,
            contractElement: Path.GetFileName(page.FilePath),
            properties: properties);
    }

    private static CodeFact CreateControlFact(ScanManifest manifest, WebFormsPage page, WebFormsControl control, CodeFact? designerFact)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["controlId"] = control.ControlId,
            ["controlType"] = control.ControlType,
            ["pageTypeName"] = page.PageTypeName,
            ["ruleLimitations"] = "Markup controls are static declarations and do not prove runtime control tree construction."
        };
        AddOptional(properties, "designerFactId", designerFact?.FactId);
        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsControlDeclared,
            RuleIds.LegacyWebFormsInventory,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(page.FilePath, control.Line, control.Line, control.SnippetHash, "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor),
            targetSymbol: control.ControlId,
            contractElement: control.ControlType,
            properties: properties);
    }

    private static CodeFact CreateEventBindingFact(ScanManifest manifest, WebFormsPage page, WebFormsBinding binding, CodeFact? designerFact)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["controlId"] = binding.ControlId,
            ["controlType"] = binding.ControlType,
            ["eventName"] = binding.EventName,
            ["handlerName"] = binding.HandlerName,
            ["pageTypeName"] = page.PageTypeName,
            ["ruleLimitations"] = "Markup event bindings are static declarations and do not prove that the event fires at runtime."
        };
        AddOptional(properties, "designerFactId", designerFact?.FactId);
        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsEventBindingDeclared,
            RuleIds.LegacyWebFormsEventBinding,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(page.FilePath, binding.Line, binding.Line, binding.SnippetHash, "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor),
            targetSymbol: binding.HandlerName,
            contractElement: binding.HandlerName,
            properties: properties);
    }

    private static CodeFact CreateDesignerFact(ScanManifest manifest, WebFormsDesignerField field)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsDesignerControlDeclared,
            RuleIds.LegacyWebFormsDesignerControl,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(field.FilePath, field.Line, field.EndLine, null, "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor),
            sourceSymbol: field.PageTypeName,
            targetSymbol: field.FieldName,
            contractElement: field.FieldName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["controlType"] = field.ControlType,
                ["fieldName"] = field.FieldName,
                ["pageTypeName"] = field.PageTypeName,
                ["ruleLimitations"] = "Designer fields can be generated, missing, or stale and are supporting evidence only."
            });
    }

    private static CodeFact CreateHandlerFact(
        ScanManifest manifest,
        WebFormsPage page,
        WebFormsBinding binding,
        CodeFact bindingFact,
        WebFormsMethod method,
        IReadOnlyList<CodeFact> existingFacts,
        bool isAutoWireup,
        bool hasExplicitSubscription = false)
    {
        var semanticEvidence = FindSemanticHandlerEvidence(method, existingFacts);
        var tier = semanticEvidence is not null
            ? EvidenceTiers.Tier1Semantic
            : method.HasCommonEventSignature && PageTypeMatches(page.PageTypeName, method.PageTypeName)
                ? EvidenceTiers.Tier2Structural
                : EvidenceTiers.Tier3SyntaxOrTextual;
        var handlerSymbol = semanticEvidence?.SourceSymbol ?? $"{method.PageTypeName}.{method.MethodName}";
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["bindingFactId"] = bindingFact.FactId,
            ["controlId"] = binding.ControlId,
            ["eventName"] = binding.EventName,
            ["handlerName"] = binding.HandlerName,
            ["handlerSymbol"] = handlerSymbol,
            ["linkedCodePath"] = method.FilePath,
            ["markupFile"] = page.FilePath,
            ["pageTypeName"] = page.PageTypeName,
            ["resolutionKind"] = semanticEvidence is not null ? "SemanticSourceSymbol" : tier == EvidenceTiers.Tier2Structural ? "StructuralLinkedPartialMethod" : "SyntaxLinkedMethod",
            ["ruleLimitations"] = "Handler resolution is static evidence and does not prove runtime event execution.",
            ["supportingFactIds"] = bindingFact.FactId
        };
        if (isAutoWireup)
        {
            properties["autoEventWireup"] = "True";
        }

        if (hasExplicitSubscription)
        {
            properties["explicitEventSubscription"] = "True";
        }

        AddOptional(properties, "sourceSymbolId", semanticEvidence?.Properties.GetValueOrDefault("sourceSymbolId"));
        AddOptional(properties, "handlerSymbolId", semanticEvidence?.Properties.GetValueOrDefault("sourceSymbolId"));

        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsHandlerResolved,
            RuleIds.LegacyWebFormsHandlerResolution,
            tier,
            new EvidenceSpan(method.FilePath, method.Line, method.EndLine, null, "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor),
            sourceSymbol: page.PageTypeName,
            targetSymbol: handlerSymbol,
            contractElement: binding.HandlerName,
            properties: properties);
    }

    private static CodeFact CreateFlowFact(ScanManifest manifest, CodeFact resolution, IReadOnlyList<CodeFact> allFacts)
    {
        var handlerName = resolution.Properties.GetValueOrDefault("handlerName") ?? resolution.ContractElement ?? string.Empty;
        var handlerSymbol = resolution.Properties.GetValueOrDefault("handlerSymbol") ?? resolution.TargetSymbol ?? handlerName;
        var directFacts = allFacts
            .Where(fact => IsDirectHandlerEvidence(fact, handlerName, handlerSymbol))
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var terminals = directFacts
            .Where(IsTerminalSurfaceFact)
            .Concat(WcfMappingsForCalls(allFacts, directFacts))
            .DistinctBy(fact => fact.FactId)
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var supportingFacts = directFacts
            .Concat(terminals)
            .Append(resolution)
            .DistinctBy(fact => fact.FactId)
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var hasReducedCoverage = manifest.BuildStatus != "Succeeded";
        var classification = terminals.Length > 0
            ? resolution.EvidenceTier == EvidenceTiers.Tier1Semantic && terminals.Any(fact => fact.EvidenceTier == EvidenceTiers.Tier1Semantic)
                ? "StrongStaticEventFlow"
                : resolution.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
                    ? "NeedsReviewEventFlow"
                    : "ProbableStaticEventFlow"
            : hasReducedCoverage ? "UnknownAnalysisGap" : "NoBackendEvidence";
        var terminal = terminals.FirstOrDefault();
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["controlId"] = resolution.Properties.GetValueOrDefault("controlId") ?? string.Empty,
            ["coverage"] = hasReducedCoverage ? "Reduced" : "Full",
            ["eventName"] = resolution.Properties.GetValueOrDefault("eventName") ?? string.Empty,
            ["evidenceTiers"] = string.Join(",", supportingFacts.Select(fact => fact.EvidenceTier).Append(resolution.EvidenceTier).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)),
            ["flowClassification"] = classification,
            ["handlerName"] = handlerName,
            ["handlerSymbolId"] = resolution.Properties.GetValueOrDefault("handlerSymbolId") ?? string.Empty,
            ["markupFile"] = resolution.Properties.GetValueOrDefault("markupFile") ?? string.Empty,
            ["pageTypeName"] = resolution.Properties.GetValueOrDefault("pageTypeName") ?? resolution.SourceSymbol ?? string.Empty,
            ["ruleIds"] = string.Join(",", supportingFacts.Select(fact => fact.RuleId).Append(RuleIds.LegacyWebFormsEventFlow).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)),
            ["ruleLimitations"] = "Event-flow projection is static direct evidence and does not prove runtime execution, branch feasibility, dynamic dispatch, event bubbling, generated-code freshness, service reachability, or SQL execution.",
            ["sourceSymbolId"] = resolution.Properties.GetValueOrDefault("sourceSymbolId") ?? string.Empty,
            ["supportingEdgeIds"] = string.Join(",", directFacts.Where(fact => fact.FactType == FactTypes.CallEdge).Select(fact => fact.FactId).OrderBy(value => value, StringComparer.Ordinal)),
            ["supportingFactIds"] = string.Join(",", supportingFacts.Select(fact => fact.FactId).OrderBy(value => value, StringComparer.Ordinal)),
            ["terminalSurfaceKind"] = terminal is null ? string.Empty : TerminalSurfaceKind(terminal),
            ["terminalSurfaceNameHash"] = terminal is null ? string.Empty : FactFactory.Hash(DisplayTerminalName(terminal), 32)
        };

        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsEventFlowProjected,
            RuleIds.LegacyWebFormsEventFlow,
            WeakestTier(supportingFacts.Select(fact => fact.EvidenceTier).Append(resolution.EvidenceTier)),
            resolution.Evidence,
            sourceSymbol: handlerSymbol,
            targetSymbol: terminal?.TargetSymbol,
            contractElement: handlerName,
            properties: properties);
    }

    private static CodeFact? CreateLogicSignalFact(ScanManifest manifest, CodeFact resolution, WebFormsContext context, IReadOnlyList<CodeFact> allFacts)
    {
        var handlerName = resolution.Properties.GetValueOrDefault("handlerName") ?? resolution.ContractElement ?? string.Empty;
        var methodPath = resolution.Evidence.FilePath;
        var method = context.CodeFiles.FirstOrDefault(file => file.FilePath.Equals(methodPath, StringComparison.Ordinal))?.Methods.FirstOrDefault(method => method.MethodName.Equals(handlerName, StringComparison.Ordinal));
        if (method is null)
        {
            return null;
        }

        var directFacts = allFacts.Where(fact => IsDirectHandlerEvidence(fact, handlerName, resolution.TargetSymbol ?? handlerName)).ToArray();
        var hasBackend = directFacts.Any(IsTerminalSurfaceFact) || WcfMappingsForCalls(allFacts, directFacts).Any();
        var hasLogic = hasBackend
            || method.Declaration.DescendantNodes().Any(node => node is IfStatementSyntax or SwitchStatementSyntax or ConditionalExpressionSyntax)
            || method.Declaration.DescendantNodes().OfType<BinaryExpressionSyntax>().Any(binary => binary.IsKind(SyntaxKind.MultiplyExpression) || binary.IsKind(SyntaxKind.DivideExpression) || binary.IsKind(SyntaxKind.ModuloExpression))
            || method.Declaration.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Any(creation => !LooksLikeUiType(creation.Type.ToString()));
        var hasUiOnly = method.Declaration.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(IsUiAssignment)
            || method.Declaration.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation => InvocationName(invocation).Equals("DataBind", StringComparison.Ordinal));
        if (!hasLogic && !hasUiOnly)
        {
            return null;
        }

        var signalKind = hasLogic ? "StaticLogicSignal" : "UiBoilerplateSignal";
        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsLogicSignalDetected,
            RuleIds.LegacyWebFormsLogicSignal,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(method.FilePath, method.Line, method.EndLine, null, "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor),
            sourceSymbol: resolution.TargetSymbol,
            targetSymbol: signalKind,
            contractElement: handlerName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["handlerName"] = handlerName,
                ["pageTypeName"] = resolution.Properties.GetValueOrDefault("pageTypeName") ?? string.Empty,
                ["signalKind"] = signalKind,
                ["staticLogicSignal"] = hasLogic.ToString(),
                ["uiBoilerplateSignal"] = hasUiOnly.ToString(),
                ["ruleLimitations"] = "Logic signals are deterministic static heuristics, not proof of business logic or code quality."
            });
    }

    private static bool IsDirectHandlerEvidence(CodeFact fact, string handlerName, string handlerSymbol)
    {
        if (fact.FactType is FactTypes.WebFormsHandlerResolved or FactTypes.WebFormsEventBindingDeclared)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(fact.SourceSymbol)
            && (fact.SourceSymbol.Equals(handlerSymbol, StringComparison.Ordinal)
                || fact.SourceSymbol.EndsWith("." + handlerName, StringComparison.Ordinal)
                || fact.SourceSymbol.Contains("." + handlerName + "(", StringComparison.Ordinal)))
        {
            return true;
        }

        return (fact.Properties.GetValueOrDefault("callerName")?.Equals(handlerName, StringComparison.Ordinal) ?? false)
            || (fact.Properties.GetValueOrDefault("containingMember")?.Equals(handlerName, StringComparison.Ordinal) ?? false)
            || (fact.Properties.GetValueOrDefault("callerSymbol")?.Equals(handlerSymbol, StringComparison.Ordinal) ?? false);
    }

    private static bool IsTerminalSurfaceFact(CodeFact fact)
    {
        return fact.FactType is FactTypes.WcfServiceReferenceMapping
            or FactTypes.SqlTextUsed
            or FactTypes.QueryPatternDetected
            or FactTypes.SqlCommandDetected
            or FactTypes.DapperCallDetected
            or FactTypes.HttpCallDetected
            or FactTypes.DependencyResolved
            or FactTypes.DependencyRegistered
            or FactTypes.ConfigBinding;
    }

    private static bool HasExplicitEventSubscription(WebFormsPage page, string handlerName, string eventName, WebFormsContext context)
    {
        var eventMemberName = eventName switch
        {
            "OnLoad" => "Load",
            "OnInit" => "Init",
            _ => eventName.StartsWith("On", StringComparison.Ordinal) ? eventName[2..] : eventName
        };
        return context.CodeFiles
            .Where(file => page.LinkedCodePath is null || file.FilePath.Equals(page.LinkedCodePath, StringComparison.Ordinal))
            .SelectMany(file => file.Subscriptions)
            .Any(subscription => EventSubscriptionMatches(subscription, eventMemberName, handlerName));
    }

    private static bool EventSubscriptionMatches(WebFormsEventSubscription subscription, string eventMemberName, string handlerName)
    {
        var left = subscription.EventName;
        var right = subscription.HandlerName;
        return (left.Equals(eventMemberName, StringComparison.Ordinal)
                || left.EndsWith("." + eventMemberName, StringComparison.Ordinal))
            && (right.Equals(handlerName, StringComparison.Ordinal)
                || right.EndsWith("." + handlerName, StringComparison.Ordinal));
    }

    private static IEnumerable<CodeFact> WcfMappingsForCalls(IReadOnlyList<CodeFact> allFacts, IReadOnlyList<CodeFact> directFacts)
    {
        var callees = directFacts
            .Where(fact => fact.FactType == FactTypes.CallEdge)
            .SelectMany(fact => new[]
            {
                fact.ContractElement,
                fact.TargetSymbol,
                fact.Properties.GetValueOrDefault("calleeName")
            })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);

        return allFacts.Where(fact => fact.FactType == FactTypes.WcfServiceReferenceMapping
            && (callees.Contains(fact.ContractElement ?? string.Empty)
                || callees.Contains(fact.Properties.GetValueOrDefault("clientOperationName") ?? string.Empty)
                || callees.Contains(fact.Properties.GetValueOrDefault("operationName") ?? string.Empty)));
    }

    private static CodeFact? FindSemanticHandlerEvidence(WebFormsMethod method, IReadOnlyList<CodeFact> existingFacts)
    {
        return existingFacts
            .Where(fact => fact.EvidenceTier == EvidenceTiers.Tier1Semantic
                && fact.Evidence.FilePath.Equals(method.FilePath, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(fact.SourceSymbol)
                && (fact.SourceSymbol.EndsWith("." + method.MethodName, StringComparison.Ordinal)
                    || fact.SourceSymbol.Contains("." + method.MethodName + "(", StringComparison.Ordinal)))
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static CodeFact CreateGap(ScanManifest manifest, string filePath, int line, string gapKind, string message)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.LegacyWebFormsHandlerResolution,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(filePath, line, line, null, "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["gapKind"] = gapKind,
                ["message"] = message,
                ["ruleLimitations"] = "WebForms gaps preserve reduced static evidence and are not proof of absence."
            });
    }

    private static SortedDictionary<string, string> ParseAttributes(string text)
    {
        var attributes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex().Matches(text).Cast<Match>())
        {
            var value = match.Groups["dq"].Success ? match.Groups["dq"].Value : match.Groups["sq"].Value;
            attributes[match.Groups["name"].Value] = value;
        }

        return attributes;
    }

    private static bool IsServerControl(IReadOnlyDictionary<string, string> attributes)
    {
        return attributes.TryGetValue("runat", out var runat)
            && runat.Equals("server", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeHandlerName(string? value)
    {
        return SafeIdentifier(value) is not null;
    }

    private static string? SafeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return SafeIdentifierRegex().IsMatch(trimmed) ? trimmed : null;
    }

    private static string? SafeMarkupPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Contains("://", StringComparison.Ordinal)
            || trimmed.Contains('\\', StringComparison.Ordinal)
            || trimmed.StartsWith("/", StringComparison.Ordinal)
            || trimmed.Contains("..", StringComparison.Ordinal)
            || trimmed.Contains('$', StringComparison.Ordinal)
            || trimmed.Contains('%', StringComparison.Ordinal))
        {
            return null;
        }

        return FileInventory.NormalizeRelativePath(trimmed.TrimStart('~', '/'));
    }

    private static string? ResolveLinkedCodePath(string markupPath, string? directivePath)
    {
        var directory = FileInventory.NormalizeRelativePath(Path.GetDirectoryName(markupPath) ?? ".");
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }
        var fileName = directivePath ?? Path.GetFileName(markupPath) + ".cs";
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var combined = directory is "." ? fileName : $"{directory}/{fileName}";
        return FileInventory.NormalizeRelativePath(combined);
    }

    private static string MarkupKind(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".ascx", StringComparison.OrdinalIgnoreCase) ? "Control"
            : extension.Equals(".master", StringComparison.OrdinalIgnoreCase) ? "Master"
            : "Page";
    }

    private static bool? ParseAutoEventWireup(string? value)
    {
        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int LineAt(SourceText source, int position)
    {
        return source.Lines.GetLineFromPosition(position).LineNumber + 1;
    }

    private static bool PageTypeMatches(string pageTypeName, string? methodTypeName)
    {
        if (string.IsNullOrWhiteSpace(methodTypeName))
        {
            return true;
        }

        return methodTypeName.Equals(pageTypeName, StringComparison.Ordinal)
            || methodTypeName.EndsWith("." + pageTypeName, StringComparison.Ordinal)
            || pageTypeName.EndsWith("." + methodTypeName, StringComparison.Ordinal);
    }

    private static string QualifiedClassName(ClassDeclarationSyntax? classDeclaration)
    {
        if (classDeclaration is null)
        {
            return string.Empty;
        }

        var names = new Stack<string>();
        names.Push(classDeclaration.Identifier.ValueText);
        foreach (var ancestor in classDeclaration.Ancestors())
        {
            if (ancestor is ClassDeclarationSyntax parentClass)
            {
                names.Push(parentClass.Identifier.ValueText);
            }
            else if (ancestor is NamespaceDeclarationSyntax namespaceDeclaration)
            {
                names.Push(namespaceDeclaration.Name.ToString());
            }
            else if (ancestor is FileScopedNamespaceDeclarationSyntax fileScopedNamespace)
            {
                names.Push(fileScopedNamespace.Name.ToString());
            }
        }

        return string.Join(".", names);
    }

    private static string PageFieldKey(string pageTypeName, string fieldName)
    {
        return $"{pageTypeName}|{fieldName}";
    }

    private static void AddOptional(IDictionary<string, string> properties, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            properties[key] = value;
        }
    }

    private static string TerminalSurfaceKind(CodeFact fact)
    {
        return fact.FactType switch
        {
            FactTypes.WcfServiceReferenceMapping => "wcf-operation",
            FactTypes.SqlTextUsed or FactTypes.QueryPatternDetected or FactTypes.SqlCommandDetected or FactTypes.DapperCallDetected => "sql-query",
            FactTypes.HttpCallDetected => "http-client",
            _ => "dependency-surface"
        };
    }

    private static string DisplayTerminalName(CodeFact fact)
    {
        return fact.ContractElement
            ?? fact.TargetSymbol
            ?? fact.Properties.GetValueOrDefault("operationName")
            ?? fact.Properties.GetValueOrDefault("queryShapeHash")
            ?? fact.FactId;
    }

    private static string WeakestTier(IEnumerable<string> tiers)
    {
        var values = tiers.ToArray();
        if (values.Contains(EvidenceTiers.Tier4Unknown, StringComparer.Ordinal))
        {
            return EvidenceTiers.Tier4Unknown;
        }

        if (values.Contains(EvidenceTiers.Tier3SyntaxOrTextual, StringComparer.Ordinal))
        {
            return EvidenceTiers.Tier3SyntaxOrTextual;
        }

        if (values.Contains(EvidenceTiers.Tier2Structural, StringComparer.Ordinal))
        {
            return EvidenceTiers.Tier2Structural;
        }

        return EvidenceTiers.Tier1Semantic;
    }

    private static bool IsUiAssignment(AssignmentExpressionSyntax assignment)
    {
        return assignment.Left is MemberAccessExpressionSyntax member
            && UiMemberNames.Contains(member.Name.Identifier.ValueText);
    }

    private static string InvocationName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            _ => invocation.Expression.ToString()
        };
    }

    private static bool LooksLikeUiType(string value)
    {
        return value.EndsWith("Label", StringComparison.Ordinal)
            || value.EndsWith("Button", StringComparison.Ordinal)
            || value.EndsWith("TextBox", StringComparison.Ordinal)
            || value.Contains("System.Web.UI.WebControls", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"<%@\s*(?<kind>Page|Control|Master)\b(?<attrs>.*?)%>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DirectiveRegex();

    [GeneratedRegex(@"<(?<prefix>[A-Za-z][\w.-]*):(?<type>[A-Za-z][\w.-]*)\b(?<attrs>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ServerControlRegex();

    [GeneratedRegex(@"(?<name>[A-Za-z_:][\w:.-]*)\s*=\s*(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)')", RegexOptions.Singleline)]
    private static partial Regex AttributeRegex();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.]*$")]
    private static partial Regex SafeIdentifierRegex();

    private sealed record WebFormsContext(
        IReadOnlyList<WebFormsPage> Pages,
        IReadOnlyList<WebFormsCodeFile> CodeFiles,
        IReadOnlyList<WebFormsDesignerField> Designers);

    private sealed record WebFormsPage(
        string FilePath,
        string DirectiveKind,
        string PageTypeName,
        string? CodeBehindPath,
        string? CodeFilePath,
        string? MasterPageFile,
        string? LinkedCodePath,
        bool? AutoEventWireup,
        int DirectiveLine,
        IReadOnlyList<WebFormsControl> Controls,
        IReadOnlyList<WebFormsBinding> Bindings,
        IReadOnlyList<WebFormsGap> Gaps);

    private sealed record WebFormsControl(string ControlType, string ControlId, int Line, string? SnippetHash);

    private sealed record WebFormsBinding(string ControlType, string ControlId, string EventName, string HandlerName, int Line, string? SnippetHash);

    private sealed record WebFormsGap(string GapKind, string Message, int Line);

    private sealed record WebFormsCodeFile(
        string FilePath,
        IReadOnlyList<WebFormsMethod> Methods,
        IReadOnlyList<WebFormsEventSubscription> Subscriptions);

    private sealed record WebFormsEventSubscription(string EventName, string HandlerName);

    private sealed record WebFormsMethod(
        string FilePath,
        string PageTypeName,
        string MethodName,
        int Line,
        int EndLine,
        bool HasCommonEventSignature,
        MethodDeclarationSyntax Declaration);

    private sealed record WebFormsDesignerField(
        string FilePath,
        string PageTypeName,
        string FieldName,
        string ControlType,
        int Line,
        int EndLine);
}
