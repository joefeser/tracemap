using Acornima;
using Acornima.Ast;

namespace TraceMap.Core;

// Source locations and bindings come from parsed JavaScript. No loader or JS runtime is involved.
internal sealed partial class AngularJsScript
{
    internal sealed record Site(string File, int Start, int Line, int EndLine);
    internal sealed record Handler(Site Method, Site Registration, Site Module, Site? ThenCallback, Site? RejectionCallback, FunctionExpression Controller, Expression MethodBody, string[] Parameters, string?[] Dependencies, bool Annotated);
    private sealed record ModuleBinding(string Name, Site Declaration);
    private Site Location(Node node) => new(File, node.Start, node.Location.Start.Line, node.Location.End.Line);
    private readonly Dictionary<CallExpression, Expression> registrationRoots = new();
    private readonly CallExpression[] controllerCalls;
    private readonly ILookup<string, VariableDeclarator> globals;
    private readonly HashSet<string> writes;
    private readonly HashSet<string> namedDeclarations;
    private readonly ILookup<string, CallExpression> creations;
    internal int NodeCount { get; }
    internal string File { get; }
    internal string Hash { get; }
    internal string? SettingsScriptGap { get; }

    internal AngularJsScript(string file, string text, IReadOnlyList<int>? unknownInitializers = null,
        IReadOnlyList<(int Start, int End)>? settings = null)
    {
        File = file;
        Hash = FactFactory.Hash(text, 64);
        var tokenCount = 0;
        var script = new Parser(new ParserOptions { OnToken = (in Token _) =>
        {
            if (++tokenCount > 100_000) throw new AngularClickGap("AngularScriptTokenLimit");
        } }).ParseScript(text);
        var nodes = Walk(script).ToArray();
        NodeCount = nodes.Length;
        if (unknownInitializers is { Count: > 0 })
        {
            var initializers = nodes.OfType<VariableDeclarator>().Where(d => d.Id is Identifier && d.Init is NumericLiteral)
                .Select(d => d.Init!.Start).ToHashSet();
            if (!unknownInitializers.All(initializers.Contains)) throw new AngularClickGap("UnsupportedAngularServerInitializer");
        }
        if (settings is { Count: > 0 })
            SettingsScriptGap = CheckSettingsStrings(script, nodes, settings);
        providers = nodes.OfType<CallExpression>().Where(IsProviderCall).ToArray();
        directCalls = script.Body.OfType<ExpressionStatement>().Select(s => s.Expression).OfType<CallExpression>().ToHashSet();
        controllerCalls = nodes.OfType<CallExpression>().Where(call => Member(call.Callee, "controller", out _)).ToArray();
        foreach (var statement in script.Body.OfType<ExpressionStatement>())
        {
            Expression root = statement.Expression;
            var chain = new List<CallExpression>();
            while (root is CallExpression { Optional: false } link && Member(link.Callee, "controller", out var receiver))
            {
                if (chain.Count == 32) throw new AngularClickGap("AngularControllerChainLimit");
                chain.Add(link);
                root = receiver;
            }
            // Only a complete top-level chain can carry its module through successive calls.
            if (chain.Count > 1 && chain.Any(link => link.Arguments.Count != 2 || link.Arguments[0] is not StringLiteral))
                continue;
            foreach (var link in chain) registrationRoots.Add(link, root);
        }
        globals = script.Body.OfType<VariableDeclaration>().SelectMany(v => v.Declarations.ToArray())
            .Where(d => d.Id is Identifier).ToLookup(d => ((Identifier)d.Id).Name, StringComparer.Ordinal);
        namedDeclarations = script.Body.Select(statement => statement switch
        {
            FunctionDeclaration fn => fn.Id?.Name,
            ClassDeclaration cls => cls.Id?.Name,
            _ => null
        }).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var id in script.Body.OfType<VariableDeclaration>().SelectMany(v => v.Declarations.ToArray())
                     .Where(d => d.Id is ArrayPattern or ObjectPattern).SelectMany(d => Walk(d.Id).OfType<Identifier>()))
            namedDeclarations.Add(id.Name);
        creations = nodes.OfType<CallExpression>().Where(c => c.Arguments.Count == 2 && Module(c) is not null)
            .ToLookup(c => Module(c)!, StringComparer.Ordinal);
        writes = nodes.Select(n => n switch {
            AssignmentExpression { Left: Identifier target } => target.Name,
            AssignmentExpression { Left: MemberExpression { Object: Identifier receiver, Computed: true } } => receiver.Name,
            AssignmentExpression { Left: MemberExpression { Object: Identifier receiver, Property: Identifier { Name: "module" or "controller" or "service" } } } => receiver.Name,
            UpdateExpression { Argument: Identifier target } => target.Name,
            _ => null
        }).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var assignment in nodes.OfType<AssignmentExpression>().Where(a => a.Left is ArrayPattern or ObjectPattern))
            foreach (var id in Walk(assignment.Left).OfType<Identifier>()) writes.Add(id.Name);
    }

    // Admitted holes may contribute data to an IIFE's local string initializer.
    // A masked value cannot establish a module name, controller name or resource URL.
    private static string? CheckSettingsStrings(Script script, Node[] nodes, IReadOnlyList<(int Start, int End)> settings)
    {
        var iifes = script.Body.OfType<ExpressionStatement>()
            .Select(statement => statement.Expression)
            .OfType<CallExpression>()
            .Where(call => !call.Optional && call.Callee is FunctionExpression).ToArray();
        var bodies = iifes.Select(call => ((FunctionExpression)call.Callee).Body).ToArray();
        var arguments = iifes.SelectMany(call => call.Arguments.ToArray()).OfType<StringLiteral>().ToHashSet();
        var literals = nodes.OfType<StringLiteral>().OrderBy(literal => literal.Start).ToArray();
        var locals = nodes.OfType<VariableDeclarator>().Where(v => v.Id is Identifier && v.Init is not null)
            .SelectMany(v => StringTerms(v.Init!).Select(literal => (literal, local: (Identifier)v.Id)))
            .ToDictionary(pair => pair.literal, pair => pair.local);
        var identifiers = nodes.OfType<Identifier>().ToLookup(id => id.Name, StringComparer.Ordinal);
        var resources = nodes.OfType<AssignmentExpression>().Where(a => a.Left is Expression target && Member(target, "src", out _))
            .SelectMany(a => StringTerms(a.Right)).ToHashSet();
        var literalIndex = 0;
        var unknownDataFlow = false;
        var resourceUse = false;
        foreach (var setting in settings)
        {
            while (literalIndex < literals.Length && literals[literalIndex].End <= setting.Start) literalIndex++;
            if (literalIndex == literals.Length) throw new AngularClickGap("UnsupportedAngularServerInitializer");
            var literal = literals[literalIndex];
            if (literal.Start >= setting.Start || literal.End <= setting.End) throw new AngularClickGap("UnsupportedAngularServerInitializer");
            if (arguments.Contains(literal)) unknownDataFlow = true;
            else if (!bodies.Any(body => body.Start < literal.Start && literal.End < body.End))
                throw new AngularClickGap("UnsupportedAngularServerInitializer");
            else if (resources.Contains(literal)) resourceUse = true;
            else if (locals.TryGetValue(literal, out var local))
                unknownDataFlow |= identifiers[local.Name].Any(id => id != local);
            else throw new AngularClickGap("AngularServerStringUseUnavailable");
        }
        if (resourceUse || nodes.OfType<CallExpression>().Any(call =>
            Member(call.Callee, "createElement", out _) && call.Arguments.FirstOrDefault() is StringLiteral tag
                && tag.Value.Equals("script", StringComparison.OrdinalIgnoreCase)
            || call.Callee is Identifier { Name: "eval" or "Function" }))
            return "AngularDynamicScriptSourceUnavailable";
        return unknownDataFlow ? "AngularServerStringFlowUnavailable" : null;
    }

    private static IEnumerable<StringLiteral> StringTerms(Expression value)
    {
        var pending = new Stack<Expression>();
        pending.Push(value);
        while (pending.TryPop(out var term))
        {
            if (term is StringLiteral literal) yield return literal;
            else if (term is BinaryExpression { Operator: Operator.Addition } sum)
            {
                pending.Push(sum.Right);
                pending.Push(sum.Left);
            }
        }
    }

    private static IEnumerable<Node> Walk(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        var count = 0;
        while (stack.TryPop(out var node))
        {
            if (++count > 100_000) throw new AngularClickGap("AngularScriptNodeLimit");
            yield return node;
            foreach (var child in node.ChildNodes) stack.Push(child);
        }
    }

    private static bool Member(Expression expression, string property, out Expression receiver)
    {
        if (expression is MemberExpression { Optional: false } member && (member.Computed
            ? member.Property is StringLiteral literal && literal.Value == property
            : member.Property is Identifier key && key.Name == property))
        {
            receiver = member.Object;
            return true;
        }
        receiver = expression;
        return false;
    }

    private static string? Module(Expression? expression) =>
        expression is CallExpression call && Member(call.Callee, "module", out var receiver)
        && receiver is Identifier { Name: "angular" } && call.Arguments.Count is 1 or 2
        && call.Arguments[0] is StringLiteral name
        && (call.Arguments.Count == 1 || call.Arguments[1] is ArrayExpression dependencies
            && dependencies.Elements.All(item => item is StringLiteral)) ? name.Value : null;

    private static ModuleBinding? ResolveModule(AngularJsScript file, Expression receiver, AngularJsScript[] scripts)
    {
        if (scripts.Any(s => s.globals["angular"].Any() || s.writes.Contains("angular") || s.namedDeclarations.Contains("angular"))) return null;
        if (Module(receiver) is { } literal) return new(literal, file.Location(receiver));
        if (receiver is not Identifier id || scripts.Any(s => s.writes.Contains(id.Name) || s.namedDeclarations.Contains(id.Name))) return null;
        var vars = scripts.SelectMany(s => s.globals[id.Name].Select(d => (File: s, Declaration: d))).Take(2).ToArray();
        if (vars.Length != 1 || Module(vars[0].Declaration.Init) is not { } name) return null;
        return new(name, vars[0].File.Location(vars[0].Declaration));
    }

    private sealed record FulfillmentRegistration(CallExpression Then, FunctionExpression? Catch);

    private static Dictionary<AssignmentExpression, FulfillmentRegistration?> ScopeAssignmentSites(FunctionExpression controller, string scope)
    {
        var sites = new Dictionary<AssignmentExpression, FulfillmentRegistration?>();
        foreach (var statement in controller.Body.Body.OfType<ExpressionStatement>())
        {
            if (statement.Expression is AssignmentExpression direct)
            {
                sites.Add(direct, null);
                continue;
            }
            if (statement.Expression is not CallExpression { Optional: false } then) continue;
            FunctionExpression? rejection = null;
            if (Member(then.Callee, "catch", out var previous))
            {
                if (previous is not CallExpression { Optional: false } fulfillment || then.Arguments.Count != 1
                    || then.Arguments[0] is not FunctionExpression { Async: false, Generator: false } handler
                    || handler.Params.Count > 1 || handler.Params.Any(p => p is not Identifier))
                    continue;
                rejection = handler;
                then = fulfillment;
            }
            if (!Member(then.Callee, "then", out var receiver)
                || receiver is not CallExpression { Optional: false, Callee: MemberExpression
                    { Optional: false, Computed: false, Object: Identifier, Property: Identifier } }
                || then.Arguments.Count != 1 || then.Arguments[0] is not FunctionExpression
                    { Id: null, Async: false, Generator: false } callback
                || scope == "arguments" || callback.Params.Any(p => p is not Identifier id || id.Name == scope))
                continue;
            // Even an uninitialized var in the callback shadows the controller parameter.
            if (Walk(callback.Body).OfType<VariableDeclarator>().Any(v => Walk(v.Id).OfType<Identifier>().Any(id => id.Name == scope)))
                continue;
            var registration = new FulfillmentRegistration(then, rejection);
            foreach (var member in callback.Body.Body.OfType<ExpressionStatement>())
                if (member.Expression is AssignmentExpression assignment) sites.Add(assignment, registration);
        }
        return sites;
    }

    internal static Handler Resolve(AngularJsScript[] scripts, string module, string controller, string method)
    {
        if (scripts.SelectMany(s => s.creations[module]).Take(2).Count() > 1)
            throw new AngularClickGap("AmbiguousAngularModule");
        var registrations = new List<(AngularJsScript Source, CallExpression Registration, Site Module, Expression Callback)>();
        foreach (var source in scripts)
        {
            foreach (var call in source.controllerCalls)
            {
                if (!Member(call.Callee, "controller", out var receiver)) continue;
                if (call.Arguments.Count > 0 && call.Arguments[0] is StringLiteral other && other.Value != controller) continue;
                var admitted = source.registrationRoots.TryGetValue(call, out var root);
                var owner = ResolveModule(source, root ?? receiver, scripts);
                if (call.Arguments.Count == 0 || call.Arguments[0] is not StringLiteral)
                {
                    if (owner is null || owner.Name == module) throw new AngularClickGap("DynamicAngularControllerRegistration");
                    continue;
                }
                if (owner is null) throw new AngularClickGap("AngularModuleOwnershipUnavailable");
                if (owner.Name != module) continue;
                if (call.Arguments.Count != 2 || !admitted)
                    throw new AngularClickGap("UnsupportedAngularControllerRegistration");
                registrations.Add((source, call, owner.Declaration, call.Arguments[1]));
            }
        }

        if (registrations.Count != 1)
            throw new AngularClickGap(registrations.Count == 0 ? "AngularControllerUnavailable" : "AmbiguousAngularController");
        var (file, registration, moduleSite, callback) = registrations[0];
        var injection = callback as ArrayExpression;
        if (injection is not null)
        {
            if (injection.Elements.Count == 0 || injection.Elements[^1] is not Expression last)
                throw new AngularClickGap("AngularScopeInjectionUnavailable");
            callback = last;
        }
        if (callback is not FunctionExpression fn || fn.Params.Any(p => p is not Identifier))
            throw new AngularClickGap("UnsupportedAngularControllerCallback");
        var parameters = fn.Params.Select(p => ((Identifier)p).Name).ToArray();
        var dependencies = injection is null ? parameters :
            injection.Elements.Take(injection.Elements.Count - 1).Select(p => (p as StringLiteral)?.Value).ToArray();
        var index = Array.IndexOf(dependencies, "$scope");
        if (parameters.Distinct(StringComparer.Ordinal).Count() != parameters.Length || dependencies.Length != parameters.Length || dependencies.Any(d => d is null) || index < 0
            || dependencies.Count(d => d == "$scope") != 1)
            throw new AngularClickGap("AngularScopeInjectionUnavailable");
        var scope = parameters[index];
        var sites = ScopeAssignmentSites(fn, scope);
        var candidates = new List<AssignmentExpression>();
        foreach (var node in Walk(fn.Body))
        {
            if (node is VariableDeclarator { Id: Identifier redeclared, Init: not null } && redeclared.Name == scope
                || node is FunctionDeclaration { Id: { } fnName } && fnName.Name == scope
                || node is ClassDeclaration { Id: { } clsName } && clsName.Name == scope
                || node is AssignmentExpression pattern && pattern.Left is ArrayPattern or ObjectPattern
                    && Walk(pattern.Left).OfType<Identifier>().Any(i => i.Name == scope))
                throw new AngularClickGap("AngularScopeReassigned");
            if (node is VariableDeclarator { Init: Identifier alias } && alias.Name == scope
                || node is AssignmentExpression { Right: Identifier stored } && stored.Name == scope
                || node is CallExpression invocation && invocation.Arguments.Any(a => a is Identifier passed && passed.Name == scope))
                throw new AngularClickGap("AngularScopeEscaped");
            if (node is UpdateExpression { Argument: MemberExpression { Object: Identifier changed } } && changed.Name == scope
                || node is UnaryExpression { Operator: Operator.Delete, Argument: MemberExpression { Object: Identifier removed } } && removed.Name == scope)
                throw new AngularClickGap("AngularScopeMemberMutation");
            if (node is not AssignmentExpression assignment) continue;
            if (assignment.Left is Identifier variable && variable.Name == scope)
                throw new AngularClickGap("AngularScopeReassigned");
            if (assignment.Left is not MemberExpression { Object: Identifier target } left || target.Name != scope) continue;
            if (left.Computed) throw new AngularClickGap("DynamicAngularScopeMember");
            if (left.Property is not Identifier key || key.Name != method) continue;
            if (assignment.Operator != Operator.Assignment
                || assignment.Right is not (FunctionExpression or ArrowFunctionExpression)
                || !sites.ContainsKey(assignment))
                throw new AngularClickGap("UnsupportedAngularScopeAssignment");
            candidates.Add(assignment);
        }
        if (candidates.Count != 1)
            throw new AngularClickGap(candidates.Count == 0 ? "AngularScopeMethodUnavailable" : "AmbiguousAngularScopeMethod");
        var binding = candidates[0];
        // Nested call nodes share the start of the entire chain. Anchor each registration
        // at its own controller property, through the end of that particular invocation.
        var member = (MemberExpression)registration.Callee;
        var registrationSite = file.registrationRoots[registration] == member.Object
            ? file.Location(registration)
            : file.Location(member.Property) with { EndLine = registration.Location.End.Line };
        var fulfillment = sites[binding];
        return new Handler(file.Location(binding), registrationSite, moduleSite,
            fulfillment is null ? null : file.Location(fulfillment.Then),
            fulfillment?.Catch is { } rejection ? file.Location(rejection) : null, fn, binding.Right, parameters, dependencies, injection is not null);
    }
}

internal sealed class AngularClickGap(string kind) : Exception(kind)
{
    internal string Kind { get; } = kind;
}
