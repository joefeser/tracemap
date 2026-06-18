using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace TraceMap.Core;

public static class LegacyWinFormsExtractor
{
    private static readonly HashSet<string> SurfaceBaseNames = new(StringComparer.Ordinal)
    {
        "Form",
        "UserControl",
        "Control",
        "Component",
        "ApplicationContext",
        "System.Windows.Forms.Form",
        "System.Windows.Forms.UserControl",
        "System.Windows.Forms.Control",
        "System.ComponentModel.Component",
        "System.Windows.Forms.ApplicationContext"
    };

    private static readonly HashSet<string> CommonControlNames = new(StringComparer.Ordinal)
    {
        "Button",
        "MenuStrip",
        "ToolStrip",
        "ToolStripMenuItem",
        "DataGridView",
        "ListView",
        "TreeView",
        "TabControl",
        "Timer",
        "BackgroundWorker",
        "CheckBox",
        "RadioButton",
        "ComboBox",
        "TextBox",
        "Label",
        "Panel",
        "GroupBox",
        "Form"
    };

    private static readonly HashSet<string> CallbackEvents = new(StringComparer.Ordinal)
    {
        "DoWork",
        "RunWorkerCompleted",
        "ProgressChanged",
        "Tick"
    };

    private static readonly HashSet<string> TerminalFactTypes = new(StringComparer.Ordinal)
    {
        FactTypes.WcfServiceReferenceMapping,
        FactTypes.AsmxServiceReferenceMapping,
        FactTypes.RemotingClientActivationDeclared,
        FactTypes.LegacyDataMetadataDeclared,
        FactTypes.LegacyDataEntityDeclared,
        FactTypes.LegacyDataStorageObjectDeclared,
        FactTypes.LegacyDataMappingDeclared,
        FactTypes.SqlTextUsed,
        FactTypes.QueryPatternDetected,
        FactTypes.SqlCommandDetected,
        FactTypes.DapperCallDetected,
        FactTypes.HttpCallDetected,
        FactTypes.DependencyResolved,
        FactTypes.DependencyRegistered,
        FactTypes.ConfigBinding
    };

    public static IReadOnlyList<CodeFact> Extract(
        string repoPath,
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<CodeFact> existingFacts)
    {
        var context = BuildContext(repoPath, inventory);
        var facts = new List<CodeFact>();

        foreach (var surface in context.Surfaces)
        {
            facts.Add(CreateSurfaceFact(manifest, surface, existingFacts));
        }

        foreach (var control in context.Controls)
        {
            facts.Add(CreateControlFact(manifest, control, context.Surfaces));
        }

        foreach (var resource in context.Resources)
        {
            facts.Add(CreateResourceFact(manifest, resource));
        }

        foreach (var gap in context.Gaps)
        {
            facts.Add(CreateGap(manifest, gap.FilePath, gap.Line, gap.Classification, gap.Message, gap.RuleId));
        }

        foreach (var binding in context.Bindings)
        {
            var bindingFact = CreateEventBindingFact(manifest, binding);
            facts.Add(bindingFact);
            if (binding.HandlerName is null)
            {
                facts.Add(CreateGap(manifest, binding.FilePath, binding.Line, "UnsupportedWinFormsEventSubscription", "WinForms event subscription target could not be resolved statically.", RuleIds.LegacyWinFormsEventBinding));
                continue;
            }

            var candidates = context.Methods
                .Where(method => SameType(method.TypeName, binding.TypeName))
                .Where(method => method.MethodName.Equals(binding.HandlerName, StringComparison.Ordinal))
                .OrderBy(method => method.FilePath, StringComparer.Ordinal)
                .ThenBy(method => method.Line)
                .ToArray();
            if (candidates.Length == 0)
            {
                facts.Add(CreateGap(manifest, binding.FilePath, binding.Line, "MissingWinFormsPartialClass", "No scoped WinForms partial method matched the event binding.", RuleIds.LegacyWinFormsHandlerResolution));
                continue;
            }

            if (candidates.Length > 1)
            {
                facts.Add(CreateGap(manifest, binding.FilePath, binding.Line, "AmbiguousWinFormsHandler", "Multiple scoped WinForms methods matched the event binding; TraceMap did not choose one.", RuleIds.LegacyWinFormsHandlerResolution));
                continue;
            }

            var handlerFact = CreateHandlerFact(manifest, binding, bindingFact, candidates[0], existingFacts);
            facts.Add(handlerFact);
            if (CallbackEvents.Contains(binding.EventName))
            {
                facts.Add(CreateCallbackBoundaryFact(manifest, binding, bindingFact, handlerFact, candidates[0]));
            }
        }

        foreach (var callback in context.UiMarshalCallbacks)
        {
            facts.Add(CreateUiMarshalBoundaryFact(manifest, callback));
        }

        foreach (var navigation in context.NavigationEdges)
        {
            facts.Add(CreateNavigationFact(manifest, navigation, facts));
        }

        var allFacts = existingFacts.Concat(facts).ToArray();
        foreach (var handler in facts.Where(fact => fact.FactType == FactTypes.WinFormsHandlerResolved).ToArray())
        {
            var flowFact = CreateHandlerFlowFact(manifest, handler, allFacts);
            facts.Add(flowFact);
            if (flowFact.Properties.GetValueOrDefault("flowClassification") == "UnknownAnalysisGap")
            {
                facts.Add(CreateGap(manifest, handler.Evidence.FilePath, handler.Evidence.StartLine, "WinFormsBackendPathUnavailable", "WinForms handler backend path evidence is unavailable or reduced; TraceMap did not treat absence as clean no-backend evidence.", RuleIds.LegacyWinFormsHandlerFlow));
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

    private static WinFormsContext BuildContext(string repoPath, IReadOnlyList<FileInventoryItem> inventory)
    {
        var files = inventory
            .Where(item => FileInventory.IsCSharpKind(item.Kind))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .Select(item => ParseCSharpFile(repoPath, item))
            .ToArray();
        var surfaces = files
            .SelectMany(file => file.Classes)
            .Where(type => IsWinFormsSurface(type) || files.Any(file => file.IsDesigner && file.Classes.Any(designerType => SameType(designerType.TypeName, type.TypeName))))
            .OrderBy(type => type.FilePath, StringComparer.Ordinal)
            .ThenBy(type => type.Line)
            .ThenBy(type => type.TypeName, StringComparer.Ordinal)
            .ToArray();
        var surfaceNames = surfaces.Select(surface => surface.TypeName).ToHashSet(StringComparer.Ordinal);
        var methods = files.SelectMany(file => file.Methods).Where(method => surfaceNames.Any(surface => SameType(surface, method.TypeName))).ToArray();
        var controls = files.SelectMany(file => file.Controls).Where(control => surfaceNames.Any(surface => SameType(surface, control.TypeName))).ToArray();
        var bindings = files.SelectMany(file => file.Bindings).Where(binding => surfaceNames.Any(surface => SameType(surface, binding.TypeName))).ToArray();
        var navigation = files.SelectMany(file => file.NavigationEdges).Where(edge => string.IsNullOrWhiteSpace(edge.SourceTypeName) || surfaceNames.Any(surface => SameType(surface, edge.SourceTypeName))).ToArray();
        var controlKeys = controls
            .Select(control => $"{control.TypeName}\0{LastExpressionPart(control.ControlId)}")
            .ToHashSet(StringComparer.Ordinal);
        var callbacks = files
            .SelectMany(file => file.UiMarshalCallbacks)
            .Where(callback => surfaceNames.Any(surface => SameType(surface, callback.TypeName)))
            .Select(callback => callback with
            {
                IsControlReceiver = callback.ReceiverName == "this"
                    || SameType(callback.ReceiverName, callback.TypeName)
                    || controlKeys.Contains($"{callback.TypeName}\0{LastExpressionPart(callback.ReceiverName)}")
            })
            .ToArray();
        var resources = inventory
            .Where(item => item.Kind == "Resource")
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .SelectMany(item => ParseResourceFile(repoPath, item, surfaces))
            .ToArray();
        var gaps = files.SelectMany(file => file.Gaps).Concat(resources.SelectMany(resource => resource.Gaps)).ToArray();
        return new WinFormsContext(surfaces, controls, bindings, methods, navigation, callbacks, resources, gaps);
    }

    private static WinFormsFile ParseCSharpFile(string repoPath, FileInventoryItem item)
    {
        try
        {
            var text = File.ReadAllText(Path.Combine(repoPath, item.RelativePath));
            var tree = CSharpSyntaxTree.ParseText(SourceText.From(text), path: item.RelativePath);
            var root = tree.GetCompilationUnitRoot();
            var isDesigner = item.Kind == "WinFormsDesigner" || item.RelativePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) && root.DescendantNodes().OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == "InitializeComponent");
            var gaps = new List<WinFormsGap>();
            if (isDesigner && tree.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            {
                gaps.Add(new WinFormsGap(item.RelativePath, 1, "MalformedWinFormsDesigner", "Designer C# could not be parsed cleanly; WinForms evidence is reduced.", RuleIds.LegacyWinFormsInventory));
            }

            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Select(type => ToTypeInfo(tree, item.RelativePath, type, isDesigner))
                .ToArray();
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Select(method => ToMethod(tree, item.RelativePath, method))
                .ToArray();
            var controls = ExtractControls(tree, item.RelativePath, root, isDesigner).ToArray();
            var bindings = ExtractBindings(tree, item.RelativePath, root, isDesigner, gaps).ToArray();
            var navigation = ExtractNavigation(tree, item.RelativePath, root).ToArray();
            var callbacks = ExtractUiMarshalCallbacks(tree, item.RelativePath, root).ToArray();
            gaps.AddRange(ExtractDynamicGaps(tree, item.RelativePath, root));
            return new WinFormsFile(item.RelativePath, isDesigner, classes, methods, controls, bindings, navigation, callbacks, gaps);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new WinFormsFile(item.RelativePath, item.Kind == "WinFormsDesigner", [], [], [], [], [], [], [new WinFormsGap(item.RelativePath, 1, "MalformedWinFormsDesigner", "Unable to read WinForms C# file for static extraction.", RuleIds.LegacyWinFormsInventory)]);
        }
    }

    private static WinFormsType ToTypeInfo(SyntaxTree tree, string filePath, ClassDeclarationSyntax type, bool isDesigner)
    {
        var span = tree.GetLineSpan(type.Span);
        var bases = type.BaseList?.Types.Select(baseType => NormalizeTypeName(baseType.Type.ToString())).ToArray() ?? [];
        return new WinFormsType(
            filePath,
            QualifiedClassName(type),
            type.Identifier.ValueText,
            bases,
            type.Modifiers.Any(SyntaxKind.PartialKeyword),
            isDesigner,
            span.StartLinePosition.Line + 1,
            Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1));
    }

    private static WinFormsMethod ToMethod(SyntaxTree tree, string filePath, MethodDeclarationSyntax method)
    {
        var span = tree.GetLineSpan(method.Span);
        var parameters = method.ParameterList.Parameters.Select(parameter => parameter.Type?.ToString() ?? string.Empty).ToArray();
        return new WinFormsMethod(
            filePath,
            QualifiedClassName(method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()),
            method.Identifier.ValueText,
            parameters.Length == 2 && parameters[0].EndsWith("object", StringComparison.OrdinalIgnoreCase) && parameters[1].Contains("EventArgs", StringComparison.Ordinal),
            span.StartLinePosition.Line + 1,
            Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1));
    }

    private static IEnumerable<WinFormsControl> ExtractControls(SyntaxTree tree, string filePath, CompilationUnitSyntax root, bool isDesigner)
    {
        foreach (var variable in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (variable.Parent?.Parent is FieldDeclarationSyntax field)
            {
                var typeName = NormalizeTypeName(field.Declaration.Type.ToString());
                if (!LooksLikeControlOrComponent(typeName))
                {
                    continue;
                }

                var span = tree.GetLineSpan(variable.Span);
                yield return new WinFormsControl(
                    filePath,
                    QualifiedClassName(variable.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()),
                    variable.Identifier.ValueText,
                    typeName,
                    ControlKind(typeName),
                    isDesigner ? "DesignerField" : "FieldDeclaration",
                    span.StartLinePosition.Line + 1,
                    Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1),
                    isDesigner);
            }
        }

        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            var typeName = NormalizeTypeName(creation.Type.ToString());
            if (!LooksLikeControlOrComponent(typeName))
            {
                continue;
            }

            var id = AssignedName(creation) ?? $"control@{LineAt(tree, creation.SpanStart)}";
            var span = tree.GetLineSpan(creation.Span);
            yield return new WinFormsControl(
                filePath,
                QualifiedClassName(creation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()),
                id,
                typeName,
                ControlKind(typeName),
                IsInsideInitializeComponent(creation) ? "InitializeComponentObjectCreation" : "ObjectCreation",
                span.StartLinePosition.Line + 1,
                Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1),
                isDesigner);
        }
    }

    private static IReadOnlyList<WinFormsBinding> ExtractBindings(SyntaxTree tree, string filePath, CompilationUnitSyntax root, bool isDesigner, List<WinFormsGap> gaps)
    {
        var bindings = new List<WinFormsBinding>();
        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>().Where(node => node.IsKind(SyntaxKind.AddAssignmentExpression)))
        {
            if (assignment.Left is not MemberAccessExpressionSyntax memberAccess)
            {
                gaps.Add(new WinFormsGap(filePath, LineAt(tree, assignment.SpanStart), "UnsupportedWinFormsEventSubscription", "WinForms event subscription left side is outside supported member-access shapes.", RuleIds.LegacyWinFormsEventBinding));
                continue;
            }

            var handler = HandlerName(assignment.Right);
            var span = tree.GetLineSpan(assignment.Span);
            bindings.Add(new WinFormsBinding(
                filePath,
                QualifiedClassName(assignment.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()),
                SafeExpressionName(memberAccess.Expression) ?? "unknown",
                memberAccess.Name.Identifier.ValueText,
                handler.Name,
                handler.BindingKind,
                handler.NeedsReview,
                IsInsideInitializeComponent(assignment),
                span.StartLinePosition.Line + 1,
                Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1),
                isDesigner));
        }

        return bindings;
    }

    private static IEnumerable<WinFormsNavigationEdge> ExtractNavigation(SyntaxTree tree, string filePath, CompilationUnitSyntax root)
    {
        foreach (var method in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
        {
            var localCreations = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var variable in method.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                .Where(variable => variable.Initializer?.Value is ObjectCreationExpressionSyntax))
            {
                var name = variable.Identifier.ValueText;
                if (!localCreations.ContainsKey(name))
                {
                    localCreations[name] = NormalizeTypeName(((ObjectCreationExpressionSyntax)variable.Initializer!.Value).Type.ToString());
                }
            }

            foreach (var assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assignment.Left is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "MdiParent" } member)
                {
                    var receiver = SafeExpressionName(member.Expression);
                    var target = receiver is not null && localCreations.TryGetValue(receiver, out var createdType) ? createdType : receiver;
                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        yield return Navigation(tree, filePath, assignment, method, target!, "MdiParentAssignment", "ProbableStaticNavigation");
                    }
                }
            }

            foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    var name = memberAccess.Name.Identifier.ValueText;
                    if (name is "Show" or "ShowDialog")
                    {
                        var receiver = SafeExpressionName(memberAccess.Expression);
                        var target = memberAccess.Expression is ObjectCreationExpressionSyntax creation
                            ? NormalizeTypeName(creation.Type.ToString())
                            : receiver is not null && localCreations.TryGetValue(receiver, out var createdType)
                                ? createdType
                                : receiver;
                        if (!string.IsNullOrWhiteSpace(target))
                        {
                            yield return Navigation(tree, filePath, invocation, method, target!, name, target == receiver ? "NeedsReviewNavigation" : "ProbableStaticNavigation");
                        }
                    }
                    else if (name is "Run" && SafeExpressionName(memberAccess.Expression) is "Application" or "System.Windows.Forms.Application")
                    {
                        var target = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression switch
                        {
                            ObjectCreationExpressionSyntax creation => NormalizeTypeName(creation.Type.ToString()),
                            IdentifierNameSyntax identifier when localCreations.TryGetValue(identifier.Identifier.ValueText, out var createdType) => createdType,
                            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                            _ => "ApplicationContext"
                        };
                        yield return Navigation(tree, filePath, invocation, method, target, "Application.Run", target == "ApplicationContext" ? "NeedsReviewNavigation" : "StrongStaticNavigation");
                    }
                }
            }
        }
    }

    private static WinFormsNavigationEdge Navigation(SyntaxTree tree, string filePath, SyntaxNode node, BaseMethodDeclarationSyntax method, string target, string navigationKind, string classification)
    {
        var span = tree.GetLineSpan(node.Span);
        return new WinFormsNavigationEdge(
            filePath,
            QualifiedClassName(method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()),
            MethodName(method),
            target,
            navigationKind,
            classification,
            span.StartLinePosition.Line + 1,
            Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1));
    }

    private static IEnumerable<WinFormsCallback> ExtractUiMarshalCallbacks(SyntaxTree tree, string filePath, CompilationUnitSyntax root)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            var name = memberAccess.Name.Identifier.ValueText;
            if (name is not ("Invoke" or "BeginInvoke"))
            {
                continue;
            }

            var span = tree.GetLineSpan(invocation.Span);
            yield return new WinFormsCallback(
                filePath,
                QualifiedClassName(invocation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()),
                SafeExpressionName(memberAccess.Expression) ?? "unknown",
                name,
                MethodName(invocation.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault()),
                span.StartLinePosition.Line + 1,
                Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1));
        }
    }

    private static IEnumerable<WinFormsGap> ExtractDynamicGaps(SyntaxTree tree, string filePath, CompilationUnitSyntax root)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = InvocationName(invocation.Expression);
            var line = LineAt(tree, invocation.SpanStart);
            if (name.Contains("Create", StringComparison.OrdinalIgnoreCase) && invocation.Ancestors().OfType<BaseMethodDeclarationSyntax>().Any())
            {
                if (IsInsideInitializeComponent(invocation) && invocation.Parent is AssignmentExpressionSyntax or EqualsValueClauseSyntax)
                {
                    yield return new WinFormsGap(filePath, line, "DynamicWinFormsControlCreation", "Factory-like control creation may require runtime state; TraceMap did not infer generated controls.", RuleIds.LegacyWinFormsControl);
                }
            }
            else if (name is "InvokeMember" || name.Contains("Activator", StringComparison.Ordinal))
            {
                yield return new WinFormsGap(filePath, line, "WinFormsReflectionBoundary", "Reflection blocks deterministic WinForms target resolution.", RuleIds.LegacyWinFormsNavigation);
            }
        }
    }

    private static IReadOnlyList<WinFormsResource> ParseResourceFile(string repoPath, FileInventoryItem item, IReadOnlyList<WinFormsType> surfaces)
    {
        var baseName = Path.GetFileNameWithoutExtension(item.RelativePath);
        var owningSurface = surfaces.FirstOrDefault(surface => SameType(surface.ShortName, ResourceOwnerName(baseName)));
        if (owningSurface is null)
        {
            return [];
        }

        var gaps = new List<WinFormsGap>();
        try
        {
            var document = SafeXml.LoadDocument(Path.Combine(repoPath, item.RelativePath));
            var keys = document.Descendants("data")
                .Select(element => element.Attribute("name")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => FactFactory.Hash(value!, 32))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return [new WinFormsResource(item.RelativePath, owningSurface.TypeName, CultureSuffix(baseName), keys, ResourceKind(document), 1, gaps)];
        }
        catch (SafeXmlException ex)
        {
            var classification = ex.FailureKind == SafeXmlFailureKind.SecurityRejected ? "WinFormsResourceParserSecurityRejected" : "UnsupportedWinFormsResourceMetadata";
            gaps.Add(new WinFormsGap(item.RelativePath, 1, classification, "WinForms resource metadata could not be safely parsed; raw values were omitted.", RuleIds.LegacyWinFormsResourceMetadata));
            return [new WinFormsResource(item.RelativePath, owningSurface.TypeName, CultureSuffix(baseName), [], "unsupported", 1, gaps)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            gaps.Add(new WinFormsGap(item.RelativePath, 1, "UnsupportedWinFormsResourceMetadata", "WinForms resource metadata could not be read safely; raw values were omitted.", RuleIds.LegacyWinFormsResourceMetadata));
            return [new WinFormsResource(item.RelativePath, owningSurface.TypeName, CultureSuffix(baseName), [], "unsupported", 1, gaps)];
        }
    }

    private static CodeFact CreateSurfaceFact(ScanManifest manifest, WinFormsType surface, IReadOnlyList<CodeFact> existingFacts)
    {
        var semantic = existingFacts.FirstOrDefault(fact =>
            fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.FactType == FactTypes.TypeDeclared
            && fact.Evidence.FilePath.Equals(surface.FilePath, StringComparison.Ordinal)
            && (fact.TargetSymbol?.EndsWith(surface.ShortName, StringComparison.Ordinal) ?? false));
        return FactFactory.Create(
            manifest,
            FactTypes.WinFormsSurfaceDeclared,
            RuleIds.LegacyWinFormsInventory,
            semantic is null ? (surface.IsDesigner ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier3SyntaxOrTextual) : EvidenceTiers.Tier1Semantic,
            new EvidenceSpan(surface.FilePath, surface.Line, surface.EndLine, null, "LegacyWinFormsExtractor", ScannerVersions.LegacyWinFormsExtractor),
            sourceSymbol: semantic?.SourceSymbol,
            targetSymbol: surface.TypeName,
            contractElement: surface.ShortName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["baseTypes"] = string.Join(",", surface.BaseTypes),
                ["isDesignerPartial"] = surface.IsDesigner.ToString(),
                ["isPartial"] = surface.IsPartial.ToString(),
                ["ruleLimitations"] = "WinForms surface inventory is static repository evidence and does not prove runtime form creation, visibility, reachability, authorization, deployment, or production usage.",
                ["surfaceKind"] = SurfaceKind(surface),
                ["typeName"] = surface.TypeName
            });
    }

    private static CodeFact CreateControlFact(ScanManifest manifest, WinFormsControl control, IReadOnlyList<WinFormsType> surfaces)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.WinFormsControlDeclared,
            RuleIds.LegacyWinFormsControl,
            control.IsDesigner ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(control.FilePath, control.Line, control.EndLine, null, "LegacyWinFormsExtractor", ScannerVersions.LegacyWinFormsExtractor),
            sourceSymbol: control.TypeName,
            targetSymbol: control.ControlId,
            contractElement: control.ControlType,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["controlId"] = control.ControlId,
                ["controlKind"] = control.ControlKind,
                ["controlType"] = control.ControlType,
                ["declarationKind"] = control.DeclarationKind,
                ["formTypeName"] = control.TypeName,
                ["hasSurfaceFact"] = surfaces.Any(surface => SameType(surface.TypeName, control.TypeName)).ToString(),
                ["ruleLimitations"] = "WinForms control/component declarations are static evidence and do not prove runtime parentage, layout, visibility, enabled state, data binding, localization, or user access."
            });
    }

    private static CodeFact CreateEventBindingFact(ScanManifest manifest, WinFormsBinding binding)
    {
        var tier = binding.IsDesigner && binding.InInitializeComponent && binding.HandlerName is not null
            ? EvidenceTiers.Tier2Structural
            : binding.HandlerName is null ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier3SyntaxOrTextual;
        return FactFactory.Create(
            manifest,
            FactTypes.WinFormsEventBindingDeclared,
            RuleIds.LegacyWinFormsEventBinding,
            tier,
            new EvidenceSpan(binding.FilePath, binding.Line, binding.EndLine, null, "LegacyWinFormsExtractor", ScannerVersions.LegacyWinFormsExtractor),
            sourceSymbol: binding.TypeName,
            targetSymbol: binding.HandlerName,
            contractElement: binding.EventName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["bindingKind"] = binding.BindingKind,
                ["controlId"] = binding.ControlId,
                ["eventName"] = binding.EventName,
                ["formTypeName"] = binding.TypeName,
                ["handlerName"] = binding.HandlerName ?? string.Empty,
                ["needsReview"] = binding.NeedsReview.ToString(),
                ["ruleLimitations"] = "WinForms event bindings are static declarations and do not prove event firing, user reachability, branch feasibility, authorization, deployment, or production usage."
            });
    }

    private static CodeFact CreateHandlerFact(ScanManifest manifest, WinFormsBinding binding, CodeFact bindingFact, WinFormsMethod method, IReadOnlyList<CodeFact> existingFacts)
    {
        var semantic = FindSemanticHandlerEvidence(method, existingFacts);
        var tier = semantic is not null ? EvidenceTiers.Tier1Semantic
            : method.HasCommonEventSignature && binding.InInitializeComponent ? EvidenceTiers.Tier2Structural
            : EvidenceTiers.Tier3SyntaxOrTextual;
        var handlerSymbol = semantic?.SourceSymbol ?? $"{method.TypeName}.{method.MethodName}";
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["bindingFactId"] = bindingFact.FactId,
            ["controlId"] = binding.ControlId,
            ["eventName"] = binding.EventName,
            ["formTypeName"] = binding.TypeName,
            ["handlerName"] = method.MethodName,
            ["handlerSymbol"] = handlerSymbol,
            ["linkedCodePath"] = method.FilePath,
            ["resolutionKind"] = semantic is not null ? "SemanticSourceSymbol" : tier == EvidenceTiers.Tier2Structural ? "StructuralPartialMethod" : "SyntaxScopedMethod",
            ["ruleLimitations"] = "WinForms handler resolution is static evidence and does not prove runtime event execution, scheduling, branch feasibility, authorization, deployment, or production usage.",
            ["supportingFactIds"] = bindingFact.FactId
        };
        AddOptional(properties, "sourceSymbolId", semantic?.Properties.GetValueOrDefault("sourceSymbolId"));
        return FactFactory.Create(
            manifest,
            FactTypes.WinFormsHandlerResolved,
            RuleIds.LegacyWinFormsHandlerResolution,
            tier,
            new EvidenceSpan(method.FilePath, method.Line, method.EndLine, null, "LegacyWinFormsExtractor", ScannerVersions.LegacyWinFormsExtractor),
            sourceSymbol: binding.TypeName,
            targetSymbol: handlerSymbol,
            contractElement: method.MethodName,
            properties: properties);
    }

    private static CodeFact CreateNavigationFact(ScanManifest manifest, WinFormsNavigationEdge edge, IReadOnlyList<CodeFact> facts)
    {
        var supporting = facts
            .Where(fact => fact.FactType is FactTypes.WinFormsEventBindingDeclared or FactTypes.WinFormsHandlerResolved or FactTypes.ObjectCreated or FactTypes.CallEdge)
            .Where(fact => fact.Evidence.FilePath.Equals(edge.FilePath, StringComparison.Ordinal)
                && (fact.Properties.GetValueOrDefault("handlerName")?.Equals(edge.SourceMethodName, StringComparison.Ordinal) == true
                    || fact.Properties.GetValueOrDefault("callerName")?.Equals(edge.SourceMethodName, StringComparison.Ordinal) == true
                    || fact.ContractElement?.Equals(edge.SourceMethodName, StringComparison.Ordinal) == true))
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        return FactFactory.Create(
            manifest,
            FactTypes.WinFormsNavigationEdgeDeclared,
            RuleIds.LegacyWinFormsNavigation,
            NavigationTier(edge, supporting),
            new EvidenceSpan(edge.FilePath, edge.Line, edge.EndLine, null, "LegacyWinFormsExtractor", ScannerVersions.LegacyWinFormsExtractor),
            sourceSymbol: edge.SourceTypeName,
            targetSymbol: edge.TargetTypeName,
            contractElement: edge.NavigationKind,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["formTypeName"] = edge.SourceTypeName,
                ["navigationClassification"] = edge.Classification,
                ["navigationKind"] = edge.NavigationKind,
                ["ruleLimitations"] = "WinForms navigation evidence is static and does not prove a form opens at runtime, user reachability, visibility, authorization, branch feasibility, deployment, or production usage.",
                ["sourceMethodName"] = edge.SourceMethodName,
                ["supportingFactIds"] = string.Join(",", supporting.Select(fact => fact.FactId)),
                ["targetFormTypeName"] = edge.TargetTypeName
            });
    }

    private static CodeFact CreateCallbackBoundaryFact(ScanManifest manifest, WinFormsBinding binding, CodeFact bindingFact, CodeFact handlerFact, WinFormsMethod method)
    {
        var classification = binding.EventName == "Tick" ? "TimerCallbackBoundary" : "BackgroundWorkerBoundary";
        return FactFactory.Create(
            manifest,
            FactTypes.WinFormsCallbackBoundaryDeclared,
            RuleIds.LegacyWinFormsCallbackBoundary,
            bindingFact.EvidenceTier == EvidenceTiers.Tier4Unknown ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural,
            new EvidenceSpan(method.FilePath, method.Line, method.EndLine, null, "LegacyWinFormsExtractor", ScannerVersions.LegacyWinFormsExtractor),
            sourceSymbol: binding.TypeName,
            targetSymbol: handlerFact.TargetSymbol,
            contractElement: binding.EventName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["boundaryClassification"] = classification,
                ["controlId"] = binding.ControlId,
                ["eventName"] = binding.EventName,
                ["handlerName"] = method.MethodName,
                ["ruleLimitations"] = "WinForms callback boundaries do not prove scheduling, execution order, cancellation, progress semantics, thread affinity, race freedom, or completion.",
                ["supportingFactIds"] = string.Join(",", new[] { bindingFact.FactId, handlerFact.FactId }.OrderBy(value => value, StringComparer.Ordinal))
            });
    }

    private static CodeFact CreateUiMarshalBoundaryFact(ScanManifest manifest, WinFormsCallback callback)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.WinFormsCallbackBoundaryDeclared,
            RuleIds.LegacyWinFormsCallbackBoundary,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(callback.FilePath, callback.Line, callback.EndLine, null, "LegacyWinFormsExtractor", ScannerVersions.LegacyWinFormsExtractor),
            sourceSymbol: callback.TypeName,
            targetSymbol: callback.ReceiverName,
            contractElement: callback.CallbackKind,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["boundaryClassification"] = callback.IsControlReceiver ? "UiMarshalBoundary" : "AsyncDelegateBoundary",
                ["callbackKind"] = callback.CallbackKind,
                ["containingMethod"] = callback.MethodName,
                ["receiverName"] = callback.ReceiverName,
                ["ruleLimitations"] = "WinForms UI marshal evidence is static and does not prove thread scheduling, order, thread affinity, race freedom, or completion."
            });
    }

    private static CodeFact CreateHandlerFlowFact(ScanManifest manifest, CodeFact handler, IReadOnlyList<CodeFact> allFacts)
    {
        var handlerName = handler.Properties.GetValueOrDefault("handlerName") ?? handler.ContractElement ?? string.Empty;
        var handlerSymbol = handler.Properties.GetValueOrDefault("handlerSymbol") ?? handler.TargetSymbol ?? handlerName;
        var directFacts = allFacts
            .Where(fact => fact.FactId != handler.FactId)
            .Where(fact => IsDirectHandlerEvidence(fact, handlerName, handlerSymbol, handler.Evidence.FilePath))
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var terminals = directFacts.Where(fact => TerminalFactTypes.Contains(fact.FactType)).OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray();
        var supporting = directFacts.Append(handler).DistinctBy(fact => fact.FactId).OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray();
        var hasReducedCoverage = manifest.BuildStatus != "Succeeded";
        var classification = terminals.Length > 0
            ? handler.EvidenceTier == EvidenceTiers.Tier1Semantic && terminals.Any(fact => fact.EvidenceTier == EvidenceTiers.Tier1Semantic) ? "StrongStaticHandlerFlow"
                : handler.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual ? "NeedsReviewHandlerFlow"
                : "ProbableStaticHandlerFlow"
            : hasReducedCoverage ? "UnknownAnalysisGap" : "NoBackendEvidence";
        var terminal = terminals.FirstOrDefault();
        return FactFactory.Create(
            manifest,
            FactTypes.WinFormsHandlerFlowProjected,
            RuleIds.LegacyWinFormsHandlerFlow,
            WeakestTier(supporting.Select(fact => fact.EvidenceTier).Append(handler.EvidenceTier)),
            handler.Evidence,
            sourceSymbol: handlerSymbol,
            targetSymbol: terminal?.TargetSymbol,
            contractElement: handlerName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["controlId"] = handler.Properties.GetValueOrDefault("controlId") ?? string.Empty,
                ["coverage"] = hasReducedCoverage ? "Reduced" : "Full",
                ["eventName"] = handler.Properties.GetValueOrDefault("eventName") ?? string.Empty,
                ["evidenceTiers"] = string.Join(",", supporting.Select(fact => fact.EvidenceTier).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)),
                ["flowClassification"] = classification,
                ["formTypeName"] = handler.Properties.GetValueOrDefault("formTypeName") ?? handler.SourceSymbol ?? string.Empty,
                ["handlerName"] = handlerName,
                ["ruleIds"] = string.Join(",", supporting.Select(fact => fact.RuleId).Append(RuleIds.LegacyWinFormsHandlerFlow).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)),
                ["ruleLimitations"] = classification == "UnknownAnalysisGap"
                    ? "WinForms handler-flow projection is reduced because backend path evidence is unavailable; it does not prove runtime execution, service reachability, SQL execution, database existence, or production usage."
                    : "WinForms handler-flow projection is static direct evidence and does not prove runtime execution, branch feasibility, dynamic dispatch, dependency injection targets, callback ordering, service reachability, SQL execution, database existence, or production usage.",
                ["sourceSymbolId"] = handler.Properties.GetValueOrDefault("sourceSymbolId") ?? string.Empty,
                ["supportingEdgeIds"] = string.Join(",", directFacts.Where(fact => fact.FactType == FactTypes.CallEdge).Select(fact => fact.FactId).OrderBy(value => value, StringComparer.Ordinal)),
                ["supportingFactIds"] = string.Join(",", supporting.Select(fact => fact.FactId).OrderBy(value => value, StringComparer.Ordinal)),
                ["terminalSurfaceKind"] = terminal is null ? string.Empty : TerminalSurfaceKind(terminal),
                ["terminalSurfaceNameHash"] = terminal is null ? string.Empty : FactFactory.Hash(DisplayTerminalName(terminal), 32)
            });
    }

    private static CodeFact CreateResourceFact(ScanManifest manifest, WinFormsResource resource)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.WinFormsResourceMetadataDeclared,
            RuleIds.LegacyWinFormsResourceMetadata,
            resource.ResourceKind == "unsupported" ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural,
            new EvidenceSpan(resource.FilePath, resource.Line, resource.Line, null, "LegacyWinFormsExtractor", ScannerVersions.LegacyWinFormsExtractor),
            sourceSymbol: resource.FormTypeName,
            targetSymbol: Path.GetFileName(resource.FilePath),
            contractElement: "resx",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["cultureSuffix"] = resource.CultureSuffix,
                ["formTypeName"] = resource.FormTypeName,
                ["resourceKeyHashes"] = string.Join(",", resource.KeyHashes),
                ["resourceKind"] = resource.ResourceKind,
                ["ruleLimitations"] = "WinForms resource metadata stores presence and key hashes only; raw values, binary payloads, paths, URLs, config-like values, secrets, localization results, and runtime text are omitted."
            });
    }

    private static CodeFact CreateGap(ScanManifest manifest, string filePath, int line, string classification, string message, string ruleId)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            ruleId,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(filePath, line, line, null, "LegacyWinFormsExtractor", ScannerVersions.LegacyWinFormsExtractor),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["classification"] = classification,
                ["gapKind"] = classification,
                ["message"] = message,
                ["ruleLimitations"] = "WinForms gaps preserve reduced static evidence and are not proof of absence."
            });
    }

    private static bool IsWinFormsSurface(WinFormsType type)
    {
        return type.BaseTypes.Any(baseType => SurfaceBaseNames.Contains(baseType) || SurfaceBaseNames.Contains(LastTypePart(baseType)));
    }

    private static string SurfaceKind(WinFormsType type)
    {
        var bases = type.BaseTypes.Select(LastTypePart).ToArray();
        if (bases.Contains("ApplicationContext", StringComparer.Ordinal))
        {
            return "ApplicationContext";
        }

        if (bases.Contains("UserControl", StringComparer.Ordinal))
        {
            return "UserControl";
        }

        if (bases.Contains("Component", StringComparer.Ordinal))
        {
            return "Component";
        }

        if (bases.Contains("Control", StringComparer.Ordinal))
        {
            return "Control";
        }

        return "Form";
    }

    private static bool LooksLikeControlOrComponent(string typeName)
    {
        var last = LastTypePart(typeName);
        return CommonControlNames.Contains(last)
            || typeName.Contains("System.Windows.Forms", StringComparison.Ordinal)
            || typeName.Contains("System.ComponentModel", StringComparison.Ordinal);
    }

    private static string ControlKind(string typeName)
    {
        var last = LastTypePart(typeName);
        return last switch
        {
            "BackgroundWorker" => "BackgroundWorker",
            "Timer" => "Timer",
            "MenuStrip" or "ToolStrip" or "ToolStripMenuItem" => "MenuOrToolStrip",
            "DataGridView" or "ListView" or "TreeView" => "GridOrList",
            _ => CommonControlNames.Contains(last) ? last : "ControlOrComponent"
        };
    }

    private static (string? Name, string BindingKind, bool NeedsReview) HandlerName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => (identifier.Identifier.ValueText, "MethodGroup", false),
            MemberAccessExpressionSyntax member => (member.Name.Identifier.ValueText, "MethodGroup", false),
            ObjectCreationExpressionSyntax creation when creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression is { } inner => HandlerName(inner) with { BindingKind = "DelegateObjectCreation" },
            ParenthesizedLambdaExpressionSyntax or SimpleLambdaExpressionSyntax => (null, "Lambda", true),
            AnonymousMethodExpressionSyntax => (null, "AnonymousDelegate", true),
            _ => (null, expression.Kind().ToString(), true)
        };
    }

    private static bool IsInsideInitializeComponent(SyntaxNode node)
    {
        return node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText == "InitializeComponent";
    }

    private static string? AssignedName(ObjectCreationExpressionSyntax creation)
    {
        if (creation.Parent is AssignmentExpressionSyntax assignment)
        {
            return SafeExpressionName(assignment.Left);
        }

        if (creation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable })
        {
            return variable.Identifier.ValueText;
        }

        return null;
    }

    private static string? SafeExpressionName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member when member.Expression is ThisExpressionSyntax => member.Name.Identifier.ValueText,
            MemberAccessExpressionSyntax member when SafeExpressionName(member.Expression) is { Length: > 0 } prefix => $"{prefix}.{member.Name.Identifier.ValueText}",
            ThisExpressionSyntax => "this",
            ObjectCreationExpressionSyntax creation => NormalizeTypeName(creation.Type.ToString()),
            _ => null
        };
    }

    private static string InvocationName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            _ => expression.ToString()
        };
    }

    private static string MethodName(BaseMethodDeclarationSyntax? method)
    {
        return method switch
        {
            MethodDeclarationSyntax declaration => declaration.Identifier.ValueText,
            ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
            _ => string.Empty
        };
    }

    private static int LineAt(SyntaxTree tree, int position)
    {
        return tree.GetText().Lines.GetLineFromPosition(position).LineNumber + 1;
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

    private static bool SameType(string left, string right)
    {
        return left.Equals(right, StringComparison.Ordinal)
            || left.EndsWith("." + right, StringComparison.Ordinal)
            || right.EndsWith("." + left, StringComparison.Ordinal);
    }

    private static string NormalizeTypeName(string value)
    {
        return value.Replace("global::", string.Empty, StringComparison.Ordinal).Trim();
    }

    private static string LastTypePart(string value)
    {
        var normalized = NormalizeTypeName(value);
        var generic = normalized.IndexOf('<', StringComparison.Ordinal);
        if (generic >= 0)
        {
            normalized = normalized[..generic];
        }

        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? normalized : parts[^1];
    }

    private static string LastExpressionPart(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? value : parts[^1];
    }

    private static string NavigationTier(WinFormsNavigationEdge edge, IReadOnlyList<CodeFact> supporting)
    {
        if (edge.Classification == "StrongStaticNavigation" && supporting.Any(fact => fact.EvidenceTier == EvidenceTiers.Tier1Semantic))
        {
            return EvidenceTiers.Tier1Semantic;
        }

        return edge.Classification is "StrongStaticNavigation" or "ProbableStaticNavigation"
            ? EvidenceTiers.Tier2Structural
            : EvidenceTiers.Tier3SyntaxOrTextual;
    }

    private static string ResourceOwnerName(string baseName)
    {
        var parts = baseName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? baseName : parts[0];
    }

    private static string CultureSuffix(string baseName)
    {
        var parts = baseName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 2 ? parts[^1] : string.Empty;
    }

    private static string ResourceKind(XDocument document)
    {
        return document.Descendants("data").Any(element => element.Attribute("type") is not null || element.Attribute("mimetype") is not null)
            ? "mixed"
            : "resx";
    }

    private static CodeFact? FindSemanticHandlerEvidence(WinFormsMethod method, IReadOnlyList<CodeFact> existingFacts)
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

    private static bool IsDirectHandlerEvidence(CodeFact fact, string handlerName, string handlerSymbol, string handlerFilePath)
    {
        if (fact.FactType.StartsWith("WinForms", StringComparison.Ordinal))
        {
            return false;
        }

        var sameFile = fact.Evidence.FilePath.Equals(handlerFilePath, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(fact.SourceSymbol)
            && (fact.SourceSymbol.Equals(handlerSymbol, StringComparison.Ordinal)
                || sameFile && fact.SourceSymbol.EndsWith("." + handlerName, StringComparison.Ordinal)
                || sameFile && fact.SourceSymbol.Contains("." + handlerName + "(", StringComparison.Ordinal)
                || sameFile && fact.SourceSymbol.Equals(handlerName, StringComparison.Ordinal)))
        {
            return true;
        }

        return sameFile
            && ((fact.Properties.GetValueOrDefault("callerName")?.Equals(handlerName, StringComparison.Ordinal) ?? false)
                || (fact.Properties.GetValueOrDefault("containingMember")?.Equals(handlerName, StringComparison.Ordinal) ?? false)
                || (fact.Properties.GetValueOrDefault("containingMethod")?.Equals(handlerName, StringComparison.Ordinal) ?? false));
    }

    private static string TerminalSurfaceKind(CodeFact fact)
    {
        return fact.FactType switch
        {
            FactTypes.WcfServiceReferenceMapping => "wcf-operation",
            FactTypes.AsmxServiceReferenceMapping => "asmx-operation",
            FactTypes.RemotingClientActivationDeclared => "remoting-call",
            FactTypes.LegacyDataMetadataDeclared or FactTypes.LegacyDataEntityDeclared or FactTypes.LegacyDataStorageObjectDeclared or FactTypes.LegacyDataMappingDeclared => "legacy-data",
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

    private static void AddOptional(IDictionary<string, string> properties, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            properties[key] = value;
        }
    }

    private sealed record WinFormsContext(
        IReadOnlyList<WinFormsType> Surfaces,
        IReadOnlyList<WinFormsControl> Controls,
        IReadOnlyList<WinFormsBinding> Bindings,
        IReadOnlyList<WinFormsMethod> Methods,
        IReadOnlyList<WinFormsNavigationEdge> NavigationEdges,
        IReadOnlyList<WinFormsCallback> UiMarshalCallbacks,
        IReadOnlyList<WinFormsResource> Resources,
        IReadOnlyList<WinFormsGap> Gaps);

    private sealed record WinFormsFile(
        string FilePath,
        bool IsDesigner,
        IReadOnlyList<WinFormsType> Classes,
        IReadOnlyList<WinFormsMethod> Methods,
        IReadOnlyList<WinFormsControl> Controls,
        IReadOnlyList<WinFormsBinding> Bindings,
        IReadOnlyList<WinFormsNavigationEdge> NavigationEdges,
        IReadOnlyList<WinFormsCallback> UiMarshalCallbacks,
        IReadOnlyList<WinFormsGap> Gaps);

    private sealed record WinFormsType(string FilePath, string TypeName, string ShortName, IReadOnlyList<string> BaseTypes, bool IsPartial, bool IsDesigner, int Line, int EndLine);
    private sealed record WinFormsMethod(string FilePath, string TypeName, string MethodName, bool HasCommonEventSignature, int Line, int EndLine);
    private sealed record WinFormsControl(string FilePath, string TypeName, string ControlId, string ControlType, string ControlKind, string DeclarationKind, int Line, int EndLine, bool IsDesigner);
    private sealed record WinFormsBinding(string FilePath, string TypeName, string ControlId, string EventName, string? HandlerName, string BindingKind, bool NeedsReview, bool InInitializeComponent, int Line, int EndLine, bool IsDesigner);
    private sealed record WinFormsNavigationEdge(string FilePath, string SourceTypeName, string SourceMethodName, string TargetTypeName, string NavigationKind, string Classification, int Line, int EndLine);
    private sealed record WinFormsCallback(string FilePath, string TypeName, string ReceiverName, string CallbackKind, string MethodName, int Line, int EndLine)
    {
        public bool IsControlReceiver { get; init; }
    }
    private sealed record WinFormsResource(string FilePath, string FormTypeName, string CultureSuffix, IReadOnlyList<string> KeyHashes, string ResourceKind, int Line, IReadOnlyList<WinFormsGap> Gaps);
    private sealed record WinFormsGap(string FilePath, int Line, string Classification, string Message, string RuleId);
}
