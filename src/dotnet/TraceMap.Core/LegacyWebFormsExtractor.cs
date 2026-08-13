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
            designerFactsByPageAndField[SurfaceFieldKey(designer.MarkupFilePath, designer.FieldName)] = fact;
        }

        foreach (var page in context.Pages)
        {
            var pageFact = CreatePageFact(manifest, page);
            facts.Add(pageFact);
            var registrationFacts = new List<CodeFact>();
            foreach (var registration in page.Registrations.Where(item => item.SourceReference is not null))
            {
                var registrationFact = CreateUserControlRegistrationFact(manifest, page, registration);
                facts.Add(registrationFact);
                registrationFacts.Add(registrationFact);
            }
            var controlFacts = new List<CodeFact>();
            foreach (var control in page.Controls)
            {
                var designerFact = designerFactsByPageAndField.GetValueOrDefault(SurfaceFieldKey(page.FilePath, control.ControlId));
                var controlFact = CreateControlFact(manifest, page, control, designerFact);
                facts.Add(controlFact);
                controlFacts.Add(controlFact);
            }

            facts.AddRange(CreateCompositionFacts(manifest, page, pageFact, registrationFacts, controlFacts));

            foreach (var binding in page.Bindings)
            {
                var designerFact = designerFactsByPageAndField.GetValueOrDefault(SurfaceFieldKey(page.FilePath, binding.ControlId));
                var bindingFact = CreateEventBindingFact(manifest, page, binding, designerFact, ResolveHandlerIdentity(page, binding, context, existingFacts));
                facts.Add(bindingFact);
                AddHandlerResolutionFacts(manifest, page, binding, bindingFact, context, existingFacts, facts);
            }

            AddExplicitControlSubscriptionFacts(manifest, page, context, existingFacts, facts);

            foreach (var gap in page.Gaps)
            {
                facts.Add(CreateGap(manifest, page.FilePath, gap.Line, gap.GapKind, gap.Message));
            }

            AddAutoWireupFacts(manifest, page, context, existingFacts, facts);
        }

        var allFacts = existingFacts.Concat(facts).ToArray();
        var wcfMappings = allFacts
            .Where(fact => fact.FactType == FactTypes.WcfServiceReferenceMapping)
            .ToArray();
        var candidateDirectFacts = allFacts
            .Where(fact => fact.FactType is not (FactTypes.WebFormsHandlerResolved or FactTypes.WebFormsEventBindingDeclared))
            .ToArray();
        foreach (var resolution in facts.Where(fact => fact.FactType == FactTypes.WebFormsHandlerResolved).ToArray())
        {
            facts.Add(CreateFlowFact(manifest, resolution, candidateDirectFacts, wcfMappings));
            var logicSignal = CreateLogicSignalFact(manifest, resolution, context, candidateDirectFacts, wcfMappings);
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
        var inventoryPathIndex = CreateInventoryPathIndex(inventory);
        var pages = inventory
            .Where(item => item.Kind == "WebFormsMarkup")
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .Select(item => ParseMarkupFile(repoPath, item, inventory, inventoryPathIndex))
            .ToArray();
        var linkedPaths = pages
            .Select(page => page.LinkedCodePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToHashSet(StringComparer.Ordinal);
        var codeFiles = inventory
            .Where(item => item.Kind == "WebFormsCodeBehind"
                || item.Kind == "CSharp" && linkedPaths.Contains(item.RelativePath))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .Select(item => ParseCodeFile(repoPath, item.RelativePath))
            .Where(file => file is not null)
            .Select(file => file!)
            .ToArray();
        var allDesigners = inventory
            .Where(item => item.Kind == "WebFormsDesigner")
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .SelectMany(item => ParseDesignerFile(
                repoPath,
                item.RelativePath,
                ResolveInventoryPath(MarkupPathForDesigner(item.RelativePath), inventoryPathIndex)
                    ?? MarkupPathForDesigner(item.RelativePath)))
            .ToArray();
        var designers = allDesigners
            .Where(field => pages.Any(page => PageTypeMatches(page.PageTypeName, field.PageTypeName)))
            .OrderBy(field => field.FilePath, StringComparer.Ordinal)
            .ThenBy(field => field.Line)
            .ThenBy(field => field.FieldName, StringComparer.Ordinal)
            .ToArray();

        return new WebFormsContext(pages, codeFiles, designers);
    }

    private static WebFormsPage ParseMarkupFile(
        string repoPath,
        FileInventoryItem file,
        IReadOnlyList<FileInventoryItem> inventory,
        InventoryPathIndex inventoryPathIndex)
    {
        var fullPath = Path.Combine(repoPath, file.RelativePath);
        try
        {
            var text = File.ReadAllText(fullPath);
            var source = SourceText.From(text);
            var activeMarkup = MaskServerComments(text);
            var directive = DirectiveRegex().Match(activeMarkup);
            var directiveAttributes = !directive.Success
                ? new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : ParseAttributes(directive.Groups["attrs"].Value);
            var directiveKind = directive.Success ? directive.Groups["kind"].Value : MarkupKind(file.RelativePath);
            var declaredPageTypeName = SafeIdentifier(directiveAttributes.GetValueOrDefault("Inherits"));
            var pageTypeName = declaredPageTypeName
                ?? SafeIdentifier(Path.GetFileNameWithoutExtension(file.RelativePath))
                ?? "unknown";
            var codeBehind = SafeMarkupPath(directiveAttributes.GetValueOrDefault("CodeBehind"));
            var codeFile = SafeMarkupPath(directiveAttributes.GetValueOrDefault("CodeFile"));
            var linkedCodeReference = ResolveLinkedCodePath(file.RelativePath, codeBehind ?? codeFile);
            var linkedCodePath = ResolveInventoryPath(linkedCodeReference, inventoryPathIndex);
            var autoEventWireup = ParseAutoEventWireup(directiveAttributes.GetValueOrDefault("AutoEventWireup"));
            var masterPageValue = directiveAttributes.GetValueOrDefault("MasterPageFile");
            var webApplicationRoot = FindWebApplicationRoot(file.RelativePath, inventory);
            var masterPageReference = ResolveMarkupReferencePath(file.RelativePath, webApplicationRoot, masterPageValue);
            var resolvedMasterPageFile = ResolveInventoryPath(masterPageReference, inventoryPathIndex);
            var masterPageFile = resolvedMasterPageFile is not null
                && Path.GetExtension(resolvedMasterPageFile).Equals(".master", StringComparison.OrdinalIgnoreCase)
                    ? resolvedMasterPageFile
                    : null;
            var titleValue = directiveAttributes.GetValueOrDefault("Title");
            var titleHash = SafeDisplayMetadata(titleValue);
            var registrations = ParseUserControlRegistrations(file.RelativePath, webApplicationRoot, activeMarkup, source, inventoryPathIndex);
            var initialGaps = new List<WebFormsGap>();
            if (!directive.Success)
            {
                initialGaps.Add(new WebFormsGap("MalformedWebFormsDirective", "Unable to parse a WebForms page/control/master directive.", 1));
            }
            else if (declaredPageTypeName is null)
            {
                initialGaps.Add(new WebFormsGap("UnresolvedWebFormsPageType", "The WebForms directive does not contain a supported static Inherits type.", LineAt(source, directive.Index)));
            }

            if (!string.IsNullOrWhiteSpace(titleValue) && titleHash is null)
            {
                initialGaps.Add(new WebFormsGap("UnsupportedWebFormsTitle", "The page title uses an unsupported dynamic or unsafe shape and was omitted.", LineAt(source, directive.Index)));
            }

            if ((codeBehind is not null || codeFile is not null) && linkedCodePath is null)
            {
                initialGaps.Add(new WebFormsGap("MissingWebFormsCodeBehind", "The declared code-behind file is not present in the scan input.", LineAt(source, directive.Index)));
            }

            if (!string.IsNullOrWhiteSpace(masterPageValue) && masterPageReference is null)
            {
                initialGaps.Add(new WebFormsGap("UnsupportedWebFormsMasterPageReference", "The MasterPageFile value is not a supported static repository-relative path.", LineAt(source, directive.Index)));
            }
            else if (masterPageReference is not null && masterPageFile is null)
            {
                initialGaps.Add(new WebFormsGap(
                    resolvedMasterPageFile is null ? "MissingWebFormsMasterPage" : "UnsupportedWebFormsMasterPageTarget",
                    resolvedMasterPageFile is null
                        ? "The declared master page is not present in the scan input."
                        : "The MasterPageFile target is present but is not a supported .master markup surface.",
                    LineAt(source, directive.Index)));
            }

            foreach (var registration in registrations.Where(item => item.SourceReference is null))
            {
                initialGaps.Add(new WebFormsGap("UnsupportedWebFormsUserControlRegistration", "A user-control registration does not contain a supported repository-relative Src path.", registration.Line));
            }

            foreach (var registration in registrations.Where(item => item.SourceReference is not null
                && item.SourcePath is null))
            {
                initialGaps.Add(new WebFormsGap("MissingWebFormsUserControl", "The registered user-control source is not present in the scan input.", registration.Line));
            }

            var page = new WebFormsPage(
                file.RelativePath,
                directiveKind,
                pageTypeName,
                codeBehind,
                codeFile,
                masterPageFile,
                linkedCodePath,
                autoEventWireup,
                !string.IsNullOrWhiteSpace(titleValue),
                titleHash,
                directive.Success ? LineAt(source, directive.Index) : 1,
                registrations,
                [],
                [],
                initialGaps);

            var controls = new List<WebFormsControl>();
            var bindings = new List<WebFormsBinding>();
            var gaps = page.Gaps.ToList();
            var duplicateRegistrationKeys = registrations
                .GroupBy(item => RegistrationKey(item.TagPrefix, item.TagName), StringComparer.OrdinalIgnoreCase)
                .Where(group => group
                    .Select(item => item.SourceReference ?? $"unsupported@{item.Line}")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var duplicate in registrations.Where(item => duplicateRegistrationKeys.Contains(RegistrationKey(item.TagPrefix, item.TagName))))
            {
                gaps.Add(new WebFormsGap("AmbiguousWebFormsUserControlRegistration", "Multiple user-control registrations map the same tag to different static sources; TraceMap did not choose one.", duplicate.Line));
            }

            foreach (Match match in ServerControlRegex().Matches(activeMarkup))
            {
                var attrs = ParseAttributes(match.Groups["attrs"].Value);
                if (!IsServerControl(attrs))
                {
                    continue;
                }

                var line = LineAt(source, match.Index);
                var controlPrefix = SafeIdentifier(match.Groups["prefix"].Value) ?? "unknown";
                var controlType = SafeIdentifier(match.Groups["type"].Value) ?? "unknown";
                var controlId = SafeIdentifier(attrs.GetValueOrDefault("ID")) ?? $"{controlType}@{line}";
                var registrationKey = RegistrationKey(controlPrefix, controlType);
                var registration = duplicateRegistrationKeys.Contains(registrationKey)
                    ? null
                    : registrations.FirstOrDefault(item =>
                    item.TagPrefix.Equals(controlPrefix, StringComparison.OrdinalIgnoreCase)
                    && item.TagName.Equals(controlType, StringComparison.OrdinalIgnoreCase)
                    && item.SourcePath is not null);
                controls.Add(new WebFormsControl(
                    controlPrefix,
                    controlType,
                    controlId,
                    ClassifyControl(controlType, registration is not null),
                    registration?.SourcePath,
                    SafeIdentifier(attrs.GetValueOrDefault("CommandName")),
                    SafeIdentifier(attrs.GetValueOrDefault("ContentPlaceHolderID")),
                    line,
                    FactFactory.Hash(match.Value, 32)));
                if (controlType.Equals("Content", StringComparison.OrdinalIgnoreCase)
                    && SafeIdentifier(attrs.GetValueOrDefault("ContentPlaceHolderID")) is null)
                {
                    gaps.Add(new WebFormsGap("UnresolvedWebFormsContentPlaceholder", "A Content control does not declare a supported static ContentPlaceHolderID.", line));
                }
                else if (controlType.Equals("Content", StringComparison.OrdinalIgnoreCase)
                    && masterPageFile is null)
                {
                    gaps.Add(new WebFormsGap("UnresolvedWebFormsContentMaster", "A Content control declares a placeholder target, but no supported static master-page reference is available.", line));
                }
                if (registration is null
                    && !controlPrefix.Equals("asp", StringComparison.OrdinalIgnoreCase)
                    && !controlPrefix.Equals("html", StringComparison.OrdinalIgnoreCase))
                {
                    gaps.Add(new WebFormsGap("UnresolvedWebFormsControlRegistration", "A prefixed server control has no supported static Register directive in this markup file.", line));
                }
                foreach (var (name, value) in attrs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    if (SupportedEvents.Contains(name) && LooksLikeHandlerName(value))
                    {
                        bindings.Add(new WebFormsBinding(controlType, controlId, name, SafeIdentifier(value)!, file.RelativePath, line, line, FactFactory.Hash(match.Value, 32), WebFormsBindingKind.MarkupAttribute));
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
            return new WebFormsPage(file.RelativePath, MarkupKind(file.RelativePath), Path.GetFileNameWithoutExtension(file.RelativePath), null, null, null, ResolveLinkedCodePath(file.RelativePath, null), null, false, null, 1, [], [], [], [new WebFormsGap("UnreadableWebFormsMarkup", "Unable to read WebForms markup for extraction.", 1)]);
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
                .Select(assignment => ToEventSubscription(tree, assignment))
                .OrderBy(subscription => subscription.Line)
                .ThenBy(subscription => subscription.ReceiverName, StringComparer.Ordinal)
                .ThenBy(subscription => subscription.EventName, StringComparer.Ordinal)
                .ThenBy(subscription => subscription.HandlerName, StringComparer.Ordinal)
                .ToArray();
            return new WebFormsCodeFile(relativePath, methods, subscriptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IReadOnlyList<WebFormsDesignerField> ParseDesignerFile(string repoPath, string relativePath, string markupFilePath)
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
                    markupFilePath,
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

    private static WebFormsEventSubscription ToEventSubscription(SyntaxTree tree, AssignmentExpressionSyntax assignment)
    {
        var span = tree.GetLineSpan(assignment.Span);
        var containingTypeName = QualifiedClassName(assignment.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault());
        var (receiverName, eventName) = assignment.Left switch
        {
            MemberAccessExpressionSyntax memberAccess =>
                (StaticSubscriptionReceiver(memberAccess.Expression) ?? "unsupported-receiver", memberAccess.Name.Identifier.ValueText),
            IdentifierNameSyntax identifier => (null, identifier.Identifier.ValueText),
            _ => (null, string.Empty)
        };
        var handlerName = assignment.Right switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax or BaseExpressionSyntax } memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => null
        };
        return new WebFormsEventSubscription(
            tree.FilePath,
            containingTypeName,
            receiverName,
            eventName,
            handlerName,
            span.StartLinePosition.Line + 1,
            FactFactory.Hash(assignment.ToString(), 32));
    }

    private static string? StaticSubscriptionReceiver(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            ThisExpressionSyntax => "this",
            BaseExpressionSyntax => "base",
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax identifier } => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax, Name: IdentifierNameSyntax identifier } => identifier.Identifier.ValueText,
            _ => null
        };
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
        var candidates = CandidateMethods(page, binding.HandlerName, context, existingFacts).ToArray();
        if (candidates.Length == 0)
        {
            var unprovenCrossFile = context.CodeFiles
                .Where(file => page.LinkedCodePath is null || !file.FilePath.Equals(page.LinkedCodePath, StringComparison.Ordinal))
                .SelectMany(file => file.Methods)
                .Any(method => method.MethodName.Equals(binding.HandlerName, StringComparison.Ordinal)
                    && PageTypeMatches(page.PageTypeName, method.PageTypeName));
            facts.Add(CreateGap(
                manifest,
                binding.FilePath,
                binding.Line,
                unprovenCrossFile ? "UnprovenCrossFileWebFormsHandler" : "MissingWebFormsHandler",
                unprovenCrossFile
                    ? $"A cross-file partial method named `{binding.HandlerName}` is visible, but semantic type and method identity could not be proven."
                    : $"No linked code-behind method matched handler `{binding.HandlerName}`."));
            return;
        }

        if (candidates.Length > 1)
        {
            facts.Add(CreateGap(manifest, binding.FilePath, binding.Line, "AmbiguousWebFormsHandler", $"Multiple linked code-behind methods matched handler `{binding.HandlerName}`; TraceMap did not choose one."));
            return;
        }

        var method = candidates[0];
        facts.Add(CreateHandlerFact(
            manifest,
            page,
            binding,
            bindingFact,
            method,
            existingFacts,
            isAutoWireup: false,
            hasExplicitSubscription: IsExplicitBinding(binding.BindingKind)));
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
            var candidates = CandidateMethods(page, handlerName, context, existingFacts).ToArray();
            if (candidates.Length == 0)
            {
                continue;
            }

            var hasExplicitSubscription = HasExplicitEventSubscription(page, handlerName, eventName, context);
            if (hasExplicitSubscription)
            {
                continue;
            }

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

            var syntheticBinding = new WebFormsBinding(page.DirectiveKind, page.PageTypeName, eventName, handlerName, page.FilePath, page.DirectiveLine, null, null, WebFormsBindingKind.AutoEventWireup);
            var bindingFact = CreateEventBindingFact(manifest, page, syntheticBinding, null, ResolveHandlerIdentity(page, syntheticBinding, context, existingFacts));
            facts.Add(bindingFact);
            facts.Add(CreateHandlerFact(manifest, page, syntheticBinding, bindingFact, candidates[0], existingFacts, isAutoWireup: page.AutoEventWireup == true, hasExplicitSubscription));
        }
    }

    private static IEnumerable<WebFormsMethod> CandidateMethods(
        WebFormsPage page,
        string handlerName,
        WebFormsContext context,
        IReadOnlyList<CodeFact> existingFacts)
    {
        var linkedCodePath = page.LinkedCodePath;
        if (linkedCodePath is null)
        {
            return [];
        }

        var linked = context.CodeFiles
            .Where(file => file.FilePath.Equals(linkedCodePath, StringComparison.Ordinal))
            .SelectMany(file => file.Methods)
            .Where(method => method.MethodName.Equals(handlerName, StringComparison.Ordinal))
            .Where(method => PageTypeMatches(page.PageTypeName, method.PageTypeName))
            .OrderBy(method => method.FilePath, StringComparer.Ordinal)
            .ThenBy(method => method.Line)
            .ToArray();
        if (linked.Length > 0)
        {
            return linked;
        }

        return context.CodeFiles
            .Where(file => !file.FilePath.Equals(linkedCodePath, StringComparison.Ordinal))
            .SelectMany(file => file.Methods)
            .Where(method => method.MethodName.Equals(handlerName, StringComparison.Ordinal))
            .Where(method => PageTypeMatches(page.PageTypeName, method.PageTypeName))
            .Where(method => FindSemanticHandlerEvidence(method, existingFacts) is { } semantic
                && SemanticHandlerTypeMatches(page.PageTypeName, semantic.SourceSymbol, method.MethodName))
            .OrderBy(method => method.FilePath, StringComparer.Ordinal)
            .ThenBy(method => method.Line)
            .ToArray();
    }

    private static void AddExplicitControlSubscriptionFacts(
        ScanManifest manifest,
        WebFormsPage page,
        WebFormsContext context,
        IReadOnlyList<CodeFact> existingFacts,
        List<CodeFact> facts)
    {
        if (page.LinkedCodePath is null)
        {
            return;
        }

        var subscriptions = context.CodeFiles
            .Where(file => file.FilePath.Equals(page.LinkedCodePath, StringComparison.Ordinal))
            .SelectMany(file => file.Subscriptions)
            .Where(subscription => PageTypeMatches(page.PageTypeName, subscription.ContainingTypeName))
            .ToArray();
        foreach (var subscription in subscriptions)
        {
            if (subscription.HandlerName is null)
            {
                facts.Add(CreateGap(manifest, subscription.FilePath, subscription.Line, "DynamicWebFormsEventSubscription", "An event subscription uses a lambda, delegate expression, or unsupported dynamic handler shape; TraceMap did not infer a handler."));
                continue;
            }

            if (subscription.ReceiverName is null or "this" or "base" or "Page")
            {
                var lifecycleEventName = "On" + subscription.EventName;
                if (!SupportedEvents.Contains(lifecycleEventName))
                {
                    continue;
                }

                var lifecycleBinding = new WebFormsBinding(
                    page.DirectiveKind,
                    page.PageTypeName,
                    lifecycleEventName,
                    subscription.HandlerName,
                    subscription.FilePath,
                    subscription.Line,
                    null,
                    subscription.SnippetHash,
                    WebFormsBindingKind.ExplicitLifecycleSubscription);
                var lifecycleBindingFact = CreateEventBindingFact(
                    manifest,
                    page,
                    lifecycleBinding,
                    null,
                    ResolveHandlerIdentity(page, lifecycleBinding, context, existingFacts));
                facts.Add(lifecycleBindingFact);
                AddHandlerResolutionFacts(manifest, page, lifecycleBinding, lifecycleBindingFact, context, existingFacts, facts);
                continue;
            }

            var eventAttributeName = "On" + subscription.EventName;
            if (!SupportedEvents.Contains(eventAttributeName))
            {
                facts.Add(CreateGap(manifest, subscription.FilePath, subscription.Line, "UnsupportedWebFormsEventSubscription", $"Static control subscription event `{subscription.EventName}` is outside the documented representative event set."));
                continue;
            }

            var controls = page.Controls
                .Where(control => control.ControlId.Equals(subscription.ReceiverName, StringComparison.Ordinal))
                .OrderBy(control => control.Line)
                .ToArray();
            if (controls.Length == 0)
            {
                facts.Add(CreateGap(manifest, subscription.FilePath, subscription.Line, "UnknownWebFormsEventSubscriptionReceiver", $"Static event subscription receiver `{subscription.ReceiverName}` could not be matched to one control on the linked markup surface."));
                continue;
            }

            if (controls.Length > 1)
            {
                facts.Add(CreateGap(manifest, subscription.FilePath, subscription.Line, "AmbiguousWebFormsEventSubscriptionReceiver", $"Static event subscription receiver `{subscription.ReceiverName}` matched multiple controls on the linked markup surface; TraceMap did not choose one."));
                continue;
            }

            var control = controls[0];
            var binding = new WebFormsBinding(
                control.ControlType,
                control.ControlId,
                eventAttributeName,
                subscription.HandlerName,
                subscription.FilePath,
                subscription.Line,
                control.Line,
                subscription.SnippetHash,
                WebFormsBindingKind.ExplicitControlSubscription);
            var bindingFact = CreateEventBindingFact(manifest, page, binding, null, ResolveHandlerIdentity(page, binding, context, existingFacts));
            facts.Add(bindingFact);
            AddHandlerResolutionFacts(manifest, page, binding, bindingFact, context, existingFacts, facts);
        }
    }

    private static CodeFact CreatePageFact(ScanManifest manifest, WebFormsPage page)
    {
        var surfaceIdentity = SurfaceIdentity(page.FilePath);
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverageLabel"] = "bounded-static-webforms-inventory",
            ["directiveKind"] = page.DirectiveKind,
            ["pageTypeName"] = page.PageTypeName,
            ["surfaceIdentity"] = surfaceIdentity,
            ["ruleLimitations"] = "WebForms file inventory is static evidence and does not prove runtime page activation."
        };
        AddOptional(properties, "codeBehindPath", page.CodeBehindPath);
        AddOptional(properties, "codeFilePath", page.CodeFilePath);
        AddOptional(properties, "linkedCodePath", page.LinkedCodePath);
        AddOptional(properties, "masterPageFile", page.MasterPageFile);
        AddOptional(properties, "titleHash", page.TitleHash);
        if (page.TitlePresent)
        {
            properties["titlePresent"] = "True";
        }
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
            sourceSymbol: surfaceIdentity,
            targetSymbol: page.PageTypeName,
            contractElement: Path.GetFileName(page.FilePath),
            properties: properties);
    }

    private static CodeFact CreateControlFact(ScanManifest manifest, WebFormsPage page, WebFormsControl control, CodeFact? designerFact)
    {
        var surfaceIdentity = SurfaceIdentity(page.FilePath);
        var controlIdentity = ControlIdentity(surfaceIdentity, control);
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverageLabel"] = "bounded-static-webforms-inventory",
            ["controlCategory"] = control.ControlCategory,
            ["controlId"] = control.ControlId,
            ["controlIdentity"] = controlIdentity,
            ["controlPrefix"] = control.ControlPrefix,
            ["controlType"] = control.ControlType,
            ["pageTypeName"] = page.PageTypeName,
            ["surfaceIdentity"] = surfaceIdentity,
            ["ruleLimitations"] = "Markup controls are static declarations and do not prove runtime control tree construction."
        };
        AddOptional(properties, "designerFactId", designerFact?.FactId);
        AddOptional(properties, "registeredSourcePath", control.RegisteredSourcePath);
        AddOptional(properties, "commandName", control.CommandName);
        AddOptional(properties, "contentPlaceHolderId", control.ContentPlaceHolderId);
        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsControlDeclared,
            RuleIds.LegacyWebFormsInventory,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(page.FilePath, control.Line, control.Line, control.SnippetHash, "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor),
            sourceSymbol: surfaceIdentity,
            targetSymbol: controlIdentity,
            contractElement: control.ControlType,
            properties: properties);
    }

    private static CodeFact CreateUserControlRegistrationFact(ScanManifest manifest, WebFormsPage page, WebFormsUserControlRegistration registration)
    {
        var surfaceIdentity = SurfaceIdentity(page.FilePath);
        var registrationIdentity = $"webforms-registration:{FactFactory.Hash($"{surfaceIdentity}|{registration.TagPrefix}|{registration.TagName}|{registration.Line}", 24)}";
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverageLabel"] = registration.SourcePath is null ? "reduced-static-webforms-composition" : "bounded-static-webforms-composition",
            ["registrationIdentity"] = registrationIdentity,
            ["surfaceIdentity"] = surfaceIdentity,
            ["tagName"] = registration.TagName,
            ["tagPrefix"] = registration.TagPrefix,
            ["ruleLimitations"] = "A static Register directive does not prove runtime loading, control construction, or use by a rendered page."
        };
        AddOptional(properties, "sourcePath", registration.SourcePath);
        if (registration.SourcePath is null)
        {
            AddOptional(properties, "declaredSourcePath", registration.SourceReference);
        }
        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsUserControlRegistered,
            RuleIds.LegacyWebFormsInventory,
            registration.SourcePath is null ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural,
            new EvidenceSpan(page.FilePath, registration.Line, registration.Line, registration.SnippetHash, "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor),
            sourceSymbol: surfaceIdentity,
            targetSymbol: registrationIdentity,
            contractElement: $"{registration.TagPrefix}:{registration.TagName}",
            properties: properties);
    }

    private static IReadOnlyList<CodeFact> CreateCompositionFacts(
        ScanManifest manifest,
        WebFormsPage page,
        CodeFact pageFact,
        IReadOnlyList<CodeFact> registrationFacts,
        IReadOnlyList<CodeFact> controlFacts)
    {
        var facts = new List<CodeFact>();
        var sourceSurfaceIdentity = SurfaceIdentity(page.FilePath);
        if (page.MasterPageFile is not null)
        {
            facts.Add(CreateCompositionFact(
                manifest,
                page.FilePath,
                page.DirectiveLine,
                "UsesMasterPage",
                sourceSurfaceIdentity,
                SurfaceIdentity(page.MasterPageFile),
                page.MasterPageFile,
                [pageFact.FactId]));
        }

        foreach (var controlFact in controlFacts)
        {
            var controlCategory = controlFact.Properties.GetValueOrDefault("controlCategory");
            if (controlCategory == "RegisteredUserControl"
                && controlFact.Properties.GetValueOrDefault("registeredSourcePath") is { } registeredSourcePath)
            {
                var matchingRegistration = registrationFacts.FirstOrDefault(fact =>
                    fact.Properties.GetValueOrDefault("sourcePath") == registeredSourcePath
                    && string.Equals(fact.Properties.GetValueOrDefault("tagPrefix"), controlFact.Properties.GetValueOrDefault("controlPrefix"), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(fact.Properties.GetValueOrDefault("tagName"), controlFact.Properties.GetValueOrDefault("controlType"), StringComparison.OrdinalIgnoreCase));
                facts.Add(CreateCompositionFact(
                    manifest,
                    controlFact.Evidence.FilePath,
                    controlFact.Evidence.StartLine,
                    "UsesRegisteredUserControl",
                    controlFact.TargetSymbol ?? sourceSurfaceIdentity,
                    SurfaceIdentity(registeredSourcePath),
                    registeredSourcePath,
                    new[] { controlFact.FactId, matchingRegistration?.FactId }
                        .Where(value => value is not null)
                        .Select(value => value!)
                        .ToArray()));
            }

            if (controlCategory == "MasterContent"
                && page.MasterPageFile is not null
                && controlFact.Properties.GetValueOrDefault("contentPlaceHolderId") is { } contentPlaceHolderId)
            {
                var targetIdentity = $"webforms-placeholder:{FactFactory.Hash($"{SurfaceIdentity(page.MasterPageFile)}|{contentPlaceHolderId}", 24)}";
                facts.Add(CreateCompositionFact(
                    manifest,
                    controlFact.Evidence.FilePath,
                    controlFact.Evidence.StartLine,
                    "FillsMasterPlaceholder",
                    controlFact.TargetSymbol ?? sourceSurfaceIdentity,
                    targetIdentity,
                    page.MasterPageFile,
                    [pageFact.FactId, controlFact.FactId]));
            }
        }

        return facts;
    }

    private static CodeFact CreateCompositionFact(
        ScanManifest manifest,
        string filePath,
        int line,
        string relationshipKind,
        string sourceIdentity,
        string targetIdentity,
        string targetFilePath,
        IReadOnlyList<string> supportingFactIds)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsCompositionDeclared,
            RuleIds.LegacyWebFormsComposition,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(filePath, line, line, null, "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor),
            sourceSymbol: sourceIdentity,
            targetSymbol: targetIdentity,
            contractElement: relationshipKind,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverageLabel"] = "bounded-static-webforms-composition",
                ["relationshipKind"] = relationshipKind,
                ["supportingFactIds"] = string.Join(",", supportingFactIds.OrderBy(value => value, StringComparer.Ordinal)),
                ["targetFilePath"] = targetFilePath,
                ["ruleLimitations"] = "Static WebForms composition evidence does not prove runtime loading, rendering, control construction, or navigation."
            });
    }

    private static string ResolveHandlerIdentity(
        WebFormsPage page,
        WebFormsBinding binding,
        WebFormsContext context,
        IReadOnlyList<CodeFact> existingFacts)
    {
        var candidates = CandidateMethods(page, binding.HandlerName, context, existingFacts).ToArray();
        if (candidates.Length != 1)
        {
            return StructuralHandlerIdentity(page, binding.HandlerName);
        }

        var method = candidates[0];
        return FindSemanticHandlerEvidence(method, existingFacts)?.Properties.GetValueOrDefault("sourceSymbolId")
            ?? StructuralHandlerIdentity(page, method.MethodName, method.FilePath, method.Line);
    }

    private static CodeFact CreateEventBindingFact(
        ScanManifest manifest,
        WebFormsPage page,
        WebFormsBinding binding,
        CodeFact? designerFact,
        string handlerIdentity)
    {
        var surfaceIdentity = SurfaceIdentity(page.FilePath);
        var control = page.Controls.FirstOrDefault(candidate =>
            candidate.ControlId.Equals(binding.ControlId, StringComparison.Ordinal)
            && candidate.ControlType.Equals(binding.ControlType, StringComparison.Ordinal)
            && candidate.Line == binding.ControlLine);
        var sourceIdentity = control is null
            ? surfaceIdentity
            : ControlIdentity(surfaceIdentity, control);
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["bindingKind"] = binding.BindingKind.ToString(),
            ["controlId"] = binding.ControlId,
            ["controlType"] = binding.ControlType,
            ["eventSourceIdentity"] = sourceIdentity,
            ["eventName"] = binding.EventName,
            ["handlerName"] = binding.HandlerName,
            ["handlerSymbolId"] = handlerIdentity,
            ["pageTypeName"] = page.PageTypeName,
            ["surfaceIdentity"] = surfaceIdentity,
            ["ruleLimitations"] = "Static WebForms event bindings do not prove that an event fires, a postback occurs, validation succeeds, or a handler executes at runtime."
        };
        if (control is not null)
        {
            properties["controlIdentity"] = sourceIdentity;
        }
        AddOptional(properties, "designerFactId", designerFact?.FactId);
        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsEventBindingDeclared,
            RuleIds.LegacyWebFormsEventBinding,
            binding.BindingKind == WebFormsBindingKind.MarkupAttribute ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(binding.FilePath, binding.Line, binding.Line, binding.SnippetHash, "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor),
            sourceSymbol: sourceIdentity,
            targetSymbol: handlerIdentity,
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
        var handlerSymbolId = semanticEvidence?.Properties.GetValueOrDefault("sourceSymbolId")
            ?? StructuralHandlerIdentity(page, method.MethodName, method.FilePath, method.Line);
        var eventSourceIdentity = bindingFact.SourceSymbol ?? SurfaceIdentity(page.FilePath);
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["bindingFactId"] = bindingFact.FactId,
            ["controlId"] = binding.ControlId,
            ["eventName"] = binding.EventName,
            ["eventSourceIdentity"] = eventSourceIdentity,
            ["handlerName"] = binding.HandlerName,
            ["handlerSymbol"] = handlerSymbol,
            ["handlerSymbolId"] = handlerSymbolId,
            ["linkedCodePath"] = method.FilePath,
            ["markupFile"] = page.FilePath,
            ["pageTypeName"] = page.PageTypeName,
            ["resolutionKind"] = semanticEvidence is not null ? "SemanticSourceSymbol" : tier == EvidenceTiers.Tier2Structural ? "StructuralLinkedPartialMethod" : "SyntaxLinkedMethod",
            ["ruleLimitations"] = "Handler resolution is static evidence and does not prove runtime event execution.",
            ["sourceSymbolId"] = handlerSymbolId,
            ["supportingFactIds"] = bindingFact.FactId,
            ["surfaceIdentity"] = SurfaceIdentity(page.FilePath)
        };
        AddOptional(properties, "controlIdentity", bindingFact.Properties.GetValueOrDefault("controlIdentity"));
        if (isAutoWireup)
        {
            properties["autoEventWireup"] = "True";
        }

        if (hasExplicitSubscription)
        {
            properties["explicitEventSubscription"] = "True";
        }

        return FactFactory.Create(
            manifest,
            FactTypes.WebFormsHandlerResolved,
            RuleIds.LegacyWebFormsHandlerResolution,
            tier,
            new EvidenceSpan(method.FilePath, method.Line, method.EndLine, null, "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor),
            sourceSymbol: eventSourceIdentity,
            targetSymbol: handlerSymbolId,
            contractElement: binding.HandlerName,
            properties: properties);
    }

    private static CodeFact CreateFlowFact(
        ScanManifest manifest,
        CodeFact resolution,
        IReadOnlyList<CodeFact> candidateDirectFacts,
        IReadOnlyList<CodeFact> wcfMappings)
    {
        var handlerName = resolution.Properties.GetValueOrDefault("handlerName") ?? resolution.ContractElement ?? string.Empty;
        var handlerSymbol = resolution.Properties.GetValueOrDefault("handlerSymbol") ?? resolution.TargetSymbol ?? handlerName;
        var directFacts = candidateDirectFacts
            .Where(fact => IsDirectHandlerEvidence(fact, handlerName, handlerSymbol, resolution.Evidence.FilePath))
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var terminals = directFacts
            .Where(IsTerminalSurfaceFact)
            .Concat(WcfMappingsForCalls(wcfMappings, directFacts))
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

    private static CodeFact? CreateLogicSignalFact(
        ScanManifest manifest,
        CodeFact resolution,
        WebFormsContext context,
        IReadOnlyList<CodeFact> candidateDirectFacts,
        IReadOnlyList<CodeFact> wcfMappings)
    {
        var handlerName = resolution.Properties.GetValueOrDefault("handlerName") ?? resolution.ContractElement ?? string.Empty;
        var methodPath = resolution.Evidence.FilePath;
        var method = context.CodeFiles.FirstOrDefault(file => file.FilePath.Equals(methodPath, StringComparison.Ordinal))?.Methods.FirstOrDefault(method => method.MethodName.Equals(handlerName, StringComparison.Ordinal));
        if (method is null)
        {
            return null;
        }

        var directFacts = candidateDirectFacts
            .Where(fact => IsDirectHandlerEvidence(fact, handlerName, resolution.TargetSymbol ?? handlerName, resolution.Evidence.FilePath))
            .ToArray();
        var hasBackend = directFacts.Any(IsTerminalSurfaceFact) || WcfMappingsForCalls(wcfMappings, directFacts).Any();
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

    private static bool IsDirectHandlerEvidence(CodeFact fact, string handlerName, string handlerSymbol, string handlerFilePath)
    {
        if (fact.FactType is FactTypes.WebFormsHandlerResolved or FactTypes.WebFormsEventBindingDeclared)
        {
            return false;
        }

        var sameFile = fact.Evidence.FilePath.Equals(handlerFilePath, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(fact.SourceSymbol))
        {
            if (fact.SourceSymbol.Equals(handlerSymbol, StringComparison.Ordinal)
                || fact.SourceSymbol.EndsWith("." + handlerName, StringComparison.Ordinal)
                || fact.SourceSymbol.Contains("." + handlerName + "(", StringComparison.Ordinal))
            {
                return true;
            }

            if (sameFile && fact.SourceSymbol.Equals(handlerName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (fact.Properties.GetValueOrDefault("callerSymbol")?.Equals(handlerSymbol, StringComparison.Ordinal) ?? false)
        {
            return true;
        }

        return sameFile
            && ((fact.Properties.GetValueOrDefault("callerName")?.Equals(handlerName, StringComparison.Ordinal) ?? false)
                || (fact.Properties.GetValueOrDefault("containingMember")?.Equals(handlerName, StringComparison.Ordinal) ?? false)
                || (fact.Properties.GetValueOrDefault("containingMethod")?.Equals(handlerName, StringComparison.Ordinal) ?? false));
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
            .Where(subscription => PageTypeMatches(page.PageTypeName, subscription.ContainingTypeName))
            .Any(subscription => EventSubscriptionMatches(subscription, eventMemberName, handlerName));
    }

    private static bool EventSubscriptionMatches(WebFormsEventSubscription subscription, string eventMemberName, string handlerName)
    {
        var left = subscription.EventName;
        var right = subscription.HandlerName;
        if (right is null || subscription.ReceiverName is not (null or "this" or "base" or "Page"))
        {
            return false;
        }
        return (left.Equals(eventMemberName, StringComparison.Ordinal)
                || left.EndsWith("." + eventMemberName, StringComparison.Ordinal))
            && (right.Equals(handlerName, StringComparison.Ordinal)
                || right.EndsWith("." + handlerName, StringComparison.Ordinal));
    }

    private static IEnumerable<CodeFact> WcfMappingsForCalls(IReadOnlyList<CodeFact> wcfMappings, IReadOnlyList<CodeFact> directFacts)
    {
        var clientSymbols = directFacts
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

        return wcfMappings.Where(fact => !string.IsNullOrWhiteSpace(fact.SourceSymbol)
            && clientSymbols.Contains(fact.SourceSymbol));
    }

    private static CodeFact? FindSemanticHandlerEvidence(WebFormsMethod method, IReadOnlyList<CodeFact> existingFacts)
    {
        return existingFacts
            .Where(fact => fact.EvidenceTier == EvidenceTiers.Tier1Semantic
                && fact.Evidence.FilePath.Equals(method.FilePath, StringComparison.Ordinal)
                && fact.Evidence.StartLine >= method.Line
                && fact.Evidence.StartLine <= method.EndLine
                && !string.IsNullOrWhiteSpace(fact.SourceSymbol)
                && !string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("sourceSymbolId"))
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
            RuleIdForGapKind(gapKind),
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(filePath, line, line, null, "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverageLabel"] = "reduced-static-webforms-evidence",
                ["gapKind"] = gapKind,
                ["message"] = message,
                ["ruleLimitations"] = "WebForms gaps preserve reduced static evidence and are not proof of absence."
            });
    }

    private static string RuleIdForGapKind(string gapKind)
    {
        return gapKind switch
        {
            "MalformedWebFormsDirective" or "UnreadableWebFormsMarkup" or "UnresolvedWebFormsPageType"
                or "MissingWebFormsCodeBehind" or "UnsupportedWebFormsTitle" => RuleIds.LegacyWebFormsInventory,
            "UnsupportedWebFormsMasterPageReference" or "MissingWebFormsMasterPage" or "UnsupportedWebFormsMasterPageTarget"
                or "UnsupportedWebFormsUserControlRegistration" or "MissingWebFormsUserControl"
                or "UnresolvedWebFormsContentPlaceholder" or "UnresolvedWebFormsContentMaster"
                or "UnresolvedWebFormsControlRegistration" or "AmbiguousWebFormsUserControlRegistration" => RuleIds.LegacyWebFormsComposition,
            "UnsupportedWebFormsEventAttribute" or "DynamicWebFormsEventSubscription"
                or "UnsupportedWebFormsEventSubscription" or "UnknownWebFormsEventSubscriptionReceiver"
                or "AmbiguousWebFormsEventSubscriptionReceiver" => RuleIds.LegacyWebFormsEventBinding,
            _ => RuleIds.LegacyWebFormsHandlerResolution
        };
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

        var trimmed = value.Trim().Replace('\\', '/');
        if (trimmed.Contains("://", StringComparison.Ordinal)
            || trimmed.Contains(':', StringComparison.Ordinal)
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

        pageTypeName = pageTypeName.StartsWith("global::", StringComparison.Ordinal) ? pageTypeName[8..] : pageTypeName;
        methodTypeName = methodTypeName.StartsWith("global::", StringComparison.Ordinal) ? methodTypeName[8..] : methodTypeName;

        return methodTypeName.Equals(pageTypeName, StringComparison.Ordinal)
            || methodTypeName.EndsWith("." + pageTypeName, StringComparison.Ordinal)
            || pageTypeName.EndsWith("." + methodTypeName, StringComparison.Ordinal);
    }

    private static bool SemanticHandlerTypeMatches(string pageTypeName, string? sourceSymbol, string methodName)
    {
        if (string.IsNullOrWhiteSpace(sourceSymbol))
        {
            return false;
        }

        var marker = "." + methodName + "(";
        var markerIndex = sourceSymbol.LastIndexOf(marker, StringComparison.Ordinal);
        return markerIndex > 0 && PageTypeMatches(pageTypeName, sourceSymbol[..markerIndex]);
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

    private static string SurfaceFieldKey(string markupFilePath, string fieldName)
    {
        return $"{SurfaceIdentity(markupFilePath)}|{fieldName}";
    }

    private static string SurfaceIdentity(string markupFilePath)
    {
        return $"webforms-surface:{FactFactory.Hash(FileInventory.NormalizeRelativePath(markupFilePath), 24)}";
    }

    private static string ControlIdentity(string surfaceIdentity, WebFormsControl control)
    {
        return $"webforms-control:{FactFactory.Hash($"{surfaceIdentity}|{control.ControlId}|{control.Line}", 24)}";
    }

    private static string StructuralHandlerIdentity(
        WebFormsPage page,
        string handlerName,
        string? filePath = null,
        int? line = null)
    {
        var location = filePath ?? page.LinkedCodePath ?? page.FilePath;
        return $"webforms-handler:{FactFactory.Hash($"{SurfaceIdentity(page.FilePath)}|{FileInventory.NormalizeRelativePath(location)}|{page.PageTypeName}|{handlerName}|{line?.ToString() ?? "unresolved"}", 24)}";
    }

    private static bool IsExplicitBinding(WebFormsBindingKind bindingKind)
    {
        return bindingKind is WebFormsBindingKind.ExplicitControlSubscription
            or WebFormsBindingKind.ExplicitLifecycleSubscription;
    }

    private static string MarkupPathForDesigner(string designerPath)
    {
        const string suffix = ".designer.cs";
        return designerPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? designerPath[..^suffix.Length]
            : designerPath;
    }

    private static IReadOnlyList<WebFormsUserControlRegistration> ParseUserControlRegistrations(
        string markupFilePath,
        string webApplicationRoot,
        string text,
        SourceText source,
        InventoryPathIndex inventoryPathIndex)
    {
        return RegisterDirectiveRegex()
            .Matches(text)
            .Cast<Match>()
            .Select(match =>
            {
                var attributes = ParseAttributes(match.Groups["attrs"].Value);
                var sourceReference = ResolveMarkupReferencePath(markupFilePath, webApplicationRoot, attributes.GetValueOrDefault("Src"));
                return new WebFormsUserControlRegistration(
                    SafeIdentifier(attributes.GetValueOrDefault("TagPrefix")) ?? "unknown",
                    SafeIdentifier(attributes.GetValueOrDefault("TagName")) ?? "unknown",
                    sourceReference,
                    ResolveInventoryPath(sourceReference, inventoryPathIndex),
                    LineAt(source, match.Index),
                    FactFactory.Hash(match.Value, 32));
            })
            .OrderBy(item => item.Line)
            .ThenBy(item => item.TagPrefix, StringComparer.Ordinal)
            .ThenBy(item => item.TagName, StringComparer.Ordinal)
            .ToArray();
    }

    private static string MaskServerComments(string text)
    {
        return ServerCommentRegex().Replace(text, match =>
            new string(match.Value.Select(character => character is '\r' or '\n' ? character : ' ').ToArray()));
    }

    private static InventoryPathIndex CreateInventoryPathIndex(IReadOnlyList<FileInventoryItem> inventory)
    {
        var exact = inventory
            .Select(item => item.RelativePath)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(path => path, path => path, StringComparer.Ordinal);
        var caseInsensitive = inventory
            .Select(item => item.RelativePath)
            .Distinct(StringComparer.Ordinal)
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Take(2).Count() == 1 ? group.First() : null,
                StringComparer.OrdinalIgnoreCase);
        return new InventoryPathIndex(exact, caseInsensitive);
    }

    private static string? ResolveInventoryPath(string? referencePath, InventoryPathIndex inventoryPathIndex)
    {
        if (referencePath is null)
        {
            return null;
        }

        if (inventoryPathIndex.Exact.TryGetValue(referencePath, out var exact))
        {
            return exact;
        }

        return inventoryPathIndex.CaseInsensitive.TryGetValue(referencePath, out var caseInsensitive)
            ? caseInsensitive
            : null;
    }

    private static string RegistrationKey(string tagPrefix, string tagName)
    {
        return $"{tagPrefix}|{tagName}";
    }

    private static string FindWebApplicationRoot(string markupPath, IReadOnlyList<FileInventoryItem> inventory)
    {
        var markupDirectory = FileInventory.NormalizeRelativePath(Path.GetDirectoryName(markupPath) ?? ".");
        var projectCandidates = inventory
            .Where(item => item.Kind == "Project")
            .Select(item => FileInventory.NormalizeRelativePath(Path.GetDirectoryName(item.RelativePath) ?? "."))
            .Where(directory => IsSameOrAncestor(directory, markupDirectory))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(directory => directory.Length)
            .ThenBy(directory => directory, StringComparer.Ordinal)
            .ToArray();
        if (projectCandidates.Length > 0)
        {
            return projectCandidates[0];
        }

        var webConfigCandidates = inventory
            .Where(item => Path.GetFileName(item.RelativePath).Equals("Web.config", StringComparison.OrdinalIgnoreCase))
            .Select(item => FileInventory.NormalizeRelativePath(Path.GetDirectoryName(item.RelativePath) ?? "."))
            .Where(directory => IsSameOrAncestor(directory, markupDirectory))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(directory => directory.Count(character => character == '/'))
            .ThenBy(directory => directory.Length)
            .ThenBy(directory => directory, StringComparer.Ordinal)
            .ToArray();
        return webConfigCandidates.FirstOrDefault() ?? ".";
    }

    private static bool IsSameOrAncestor(string candidate, string path)
    {
        return candidate == "."
            || path.Equals(candidate, StringComparison.Ordinal)
            || path.StartsWith(candidate + "/", StringComparison.Ordinal);
    }

    private static string? ResolveMarkupReferencePath(string markupPath, string webApplicationRoot, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Replace('\\', '/');
        var rootedAtScan = trimmed.StartsWith("~/", StringComparison.Ordinal);
        var safePath = SafeMarkupPath(trimmed);
        if (safePath is null)
        {
            return null;
        }

        if (rootedAtScan)
        {
            return webApplicationRoot == "."
                ? safePath
                : FileInventory.NormalizeRelativePath($"{webApplicationRoot}/{safePath}");
        }

        var directory = FileInventory.NormalizeRelativePath(Path.GetDirectoryName(markupPath) ?? ".");
        var combined = directory is "." ? safePath : $"{directory}/{safePath}";
        return FileInventory.NormalizeRelativePath(combined);
    }

    private static string? SafeDisplayMetadata(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Contains("<%", StringComparison.Ordinal)
            || trimmed.Contains("%>", StringComparison.Ordinal)
            || trimmed.Contains("$(", StringComparison.Ordinal)
            ? null
            : FactFactory.Hash(trimmed, 32);
    }

    private static string ClassifyControl(string controlType, bool isRegisteredUserControl)
    {
        if (isRegisteredUserControl)
        {
            return "RegisteredUserControl";
        }

        if (controlType.Equals("Content", StringComparison.OrdinalIgnoreCase))
        {
            return "MasterContent";
        }

        if (controlType.Equals("ContentPlaceHolder", StringComparison.OrdinalIgnoreCase))
        {
            return "MasterPlaceholder";
        }

        if (controlType.EndsWith("Validator", StringComparison.OrdinalIgnoreCase)
            || controlType.Equals("ValidationSummary", StringComparison.OrdinalIgnoreCase))
        {
            return "Validator";
        }

        if (controlType.EndsWith("DataSource", StringComparison.OrdinalIgnoreCase))
        {
            return "DataSource";
        }

        return "ServerControl";
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

    [GeneratedRegex(@"<%@\s*Register\b(?<attrs>.*?)%>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegisterDirectiveRegex();

    [GeneratedRegex(@"<%--.*?--%>", RegexOptions.Singleline)]
    private static partial Regex ServerCommentRegex();

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
        bool TitlePresent,
        string? TitleHash,
        int DirectiveLine,
        IReadOnlyList<WebFormsUserControlRegistration> Registrations,
        IReadOnlyList<WebFormsControl> Controls,
        IReadOnlyList<WebFormsBinding> Bindings,
        IReadOnlyList<WebFormsGap> Gaps);

    private sealed record WebFormsControl(
        string ControlPrefix,
        string ControlType,
        string ControlId,
        string ControlCategory,
        string? RegisteredSourcePath,
        string? CommandName,
        string? ContentPlaceHolderId,
        int Line,
        string? SnippetHash);

    private sealed record WebFormsUserControlRegistration(
        string TagPrefix,
        string TagName,
        string? SourceReference,
        string? SourcePath,
        int Line,
        string? SnippetHash);

    private sealed record InventoryPathIndex(
        IReadOnlyDictionary<string, string> Exact,
        IReadOnlyDictionary<string, string?> CaseInsensitive);

    private sealed record WebFormsBinding(
        string ControlType,
        string ControlId,
        string EventName,
        string HandlerName,
        string FilePath,
        int Line,
        int? ControlLine,
        string? SnippetHash,
        WebFormsBindingKind BindingKind);

    private enum WebFormsBindingKind
    {
        MarkupAttribute,
        AutoEventWireup,
        ExplicitLifecycleSubscription,
        ExplicitControlSubscription
    }

    private sealed record WebFormsGap(string GapKind, string Message, int Line);

    private sealed record WebFormsCodeFile(
        string FilePath,
        IReadOnlyList<WebFormsMethod> Methods,
        IReadOnlyList<WebFormsEventSubscription> Subscriptions);

    private sealed record WebFormsEventSubscription(
        string FilePath,
        string ContainingTypeName,
        string? ReceiverName,
        string EventName,
        string? HandlerName,
        int Line,
        string SnippetHash);

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
        string MarkupFilePath,
        string PageTypeName,
        string FieldName,
        string ControlType,
        int Line,
        int EndLine);
}
