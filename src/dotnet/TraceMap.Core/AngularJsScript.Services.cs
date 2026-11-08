using Acornima.Ast;

namespace TraceMap.Core;

internal sealed partial class AngularJsScript
{
    internal sealed class ProviderOwnerGap(Site provider, string reason, string receiver, string token, int factories)
        : AngularClickGap("AngularServiceModuleUnavailable")
    {
        internal Site Provider { get; } = provider;
        internal string Reason { get; } = reason;
        internal string Receiver { get; } = receiver;
        internal string Token { get; } = token;
        internal int Factories { get; } = factories;
    }

    private readonly CallExpression[] providers;
    private readonly Expression[] factoryMutations;
    private readonly HashSet<CallExpression> directCalls;
    internal sealed record ServiceCall(Site Site, string Token, string Member, int Slot);
    internal sealed record FactoryLink(string Token, Site Site);
    internal sealed record ServiceMethod(AngularJsScript Script, Site Registration, Site Module, Site Member,
        FunctionExpression Body, FunctionExpression Constructor, ArrayExpression Annotation, IReadOnlyList<FactoryLink> Factories);
    internal sealed record Post(Site Site, string Url, int HttpSlot);

    private static string? ProviderMember(MemberExpression member) => member.Computed
        ? (member.Property as StringLiteral)?.Value : (member.Property as Identifier)?.Name;

    private static bool IsProviderCall(CallExpression call)
    {
        if (call.Callee is not MemberExpression member) return false;
        return ProviderMember(member) is "service" or "factory" or "provider" or "value" or "constant" or "decorator";
    }

    // Only the selected method's immediate statements supply call sites. An enclosing
    // fulfillment registration or the body of another callback cannot supply one.
    private static IEnumerable<CallExpression> ImmediateCalls(Expression function)
    {
        Node? body = function switch
        {
            FunctionExpression fn => fn.Body,
            ArrowFunctionExpression arrow => arrow.Body,
            _ => null
        };
        IEnumerable<Expression?> expressions = body is BlockStatement block
            ? block.Body.Select(s => s switch { ExpressionStatement es => es.Expression, ReturnStatement ret => ret.Argument, _ => null })
            : [body as Expression];
        foreach (var expression in expressions)
        {
            if (expression is not CallExpression { Optional: false } call) continue;
            if (Member(call.Callee, "then", out var receiver) && receiver is CallExpression { Optional: false } inner) call = inner;
            yield return call;
        }
    }

    private static bool ExclusiveReceiver(FunctionExpression owner, string parameter, bool callable)
    {
        if (parameter == "arguments") return false;
        var nodes = Walk(owner.Body).ToArray();
        var allowed = new HashSet<Node>();
        foreach (var call in nodes.OfType<CallExpression>().Where(call => !call.Optional))
        {
            if (callable) allowed.Add(call.Callee);
            if (call.Callee is MemberExpression { Optional: false, Computed: false } member) allowed.Add(member.Object);
        }
        return nodes.OfType<Identifier>().Where(id => id.Name == parameter).All(allowed.Contains);
    }

    internal IEnumerable<ServiceCall> ServiceCalls(Handler handler)
    {
        var count = 0;
        var immediate = ImmediateCalls(handler.MethodBody).ToHashSet();
        foreach (var call in Walk(handler.MethodBody).OfType<CallExpression>())
        {
            if (call.Callee is not MemberExpression { Optional: false, Computed: false, Object: Identifier id, Property: Identifier key }) continue;
            var slot = Array.IndexOf(handler.Parameters, id.Name);
            if (slot < 0 || handler.Dependencies[slot] is not { } token || token.StartsWith('$')) continue;
            if (!immediate.Contains(call) || call.Optional) throw new AngularClickGap("UnsupportedAngularServiceCall");
            if (!handler.Annotated) throw new AngularClickGap("AngularServiceInjectionUnavailable");
            if (++count > 16) throw new AngularClickGap("AngularServiceCallLimit");
            if (!ExclusiveReceiver(handler.Controller, id.Name, false))
                throw new AngularClickGap("AngularServiceReceiverBindingUnavailable");
            yield return new ServiceCall(Location(call), token, key.Name, slot);
        }
    }

    private (Expression Root, List<FactoryLink> Factories) ServiceReceiver(MemberExpression service)
    {
        var factories = new List<FactoryLink>();
        Expression root = service.Object;
        while (root is CallExpression { Optional: false } call && Member(call.Callee, "factory", out var receiver))
        {
            // Count the terminal service as the last registration.
            if (factories.Count >= 31) throw new AngularClickGap("AngularServiceProviderChainLimit");
            if (call.Arguments.Count != 2 || call.Arguments[0] is not StringLiteral token
                || !LiteralFactory(call.Arguments[1]))
                throw new AngularClickGap("UnsupportedAngularFactoryRegistration");
            var property = ((MemberExpression)call.Callee).Property;
            factories.Add(new FactoryLink(token.Value, Location(property) with { EndLine = call.Location.End.Line }));
            root = receiver;
        }
        factories.Reverse();
        return (root, factories);
    }

    private static bool LiteralFactory(Node argument) => argument is FunctionExpression
        || argument is ArrayExpression annotated && annotated.Elements.Count > 0
            && annotated.Elements[^1] is FunctionExpression factory && factory.Params.Count == annotated.Elements.Count - 1
            && annotated.Elements.SkipLast(1).All(item => item is StringLiteral);

    internal static ServiceMethod ResolveService(AngularJsScript[] sources, string module, ServiceCall target,
        Action<AngularJsScript, Site, string> recordUnboundProvider)
    {
        ServiceMethod? result = null;
        var checkedCalls = 0;
        var unboundCalls = 0;
        foreach (var source in sources)
        foreach (var registration in source.providers)
        {
            if (++checkedCalls > 1024) throw new AngularClickGap("AngularProviderLimit");
            if (registration.Arguments.Count > 0 && registration.Arguments[0] is StringLiteral name && name.Value != target.Token) continue;
            var member = (MemberExpression)registration.Callee;
            var (root, factories) = source.ServiceReceiver(member);
            var owner = ResolveModule(source, root, sources, out var failure);
            if (owner is null)
            {
                // Use the same parsed shapes reported by the owner diagnostic. All calls
                // reaching here already have a recognized, statically named provider member.
                if (failure == "unsupported-receiver" && factories.Count == 0 && root.Type == NodeType.ThisExpression
                    && registration.Arguments.Count > 0 && registration.Arguments[0].Type == NodeType.CallExpression)
                {
                    if (++unboundCalls > 16) throw new AngularClickGap("AngularUnboundProviderLimit");
                    recordUnboundProvider(source, source.Location(member.Property), ProviderMember(member)!);
                    continue;
                }
                throw new ProviderOwnerGap(source.Location(member.Property), failure, root.Type.ToString(),
                    registration.Arguments.Count == 0 ? "absent" : registration.Arguments[0] is StringLiteral ? "string-literal" : registration.Arguments[0].Type.ToString(), factories.Count);
            }
            if (owner.Name != module) continue;
            if (factories.Count != 0 && sources.Any(file => file.factoryMutations.Any(receiver =>
                ResolveModule(file, receiver, sources)?.Name is not { } name || name == module)))
                throw new AngularClickGap("AngularFactoryReceiverMutation");
            if (registration.Optional || registration.Arguments.Count != 2 || registration.Arguments[0] is not StringLiteral
                || !source.directCalls.Contains(registration) || !Member(member, "service", out _))
                throw new AngularClickGap("UnsupportedAngularServiceRegistration");
            if (result is not null) throw new AngularClickGap("AmbiguousAngularService");
            if (registration.Arguments[1] is not ArrayExpression annotation || annotation.Elements.Count < 2
                || annotation.Elements[^1] is not FunctionExpression { Async: false, Generator: false } fn)
                throw new AngularClickGap("AngularServiceInjectionUnavailable");
            var binding = ServiceMember(fn, target.Member);
            var site = factories.Count == 0 ? source.Location(registration)
                : source.Location(member.Property) with { EndLine = registration.Location.End.Line };
            result = new ServiceMethod(source, site, owner.Declaration, source.Location(binding),
                (FunctionExpression)binding.Right, fn, annotation, factories);
        }
        return result ?? throw new AngularClickGap(unboundCalls > 0 ? "AngularServiceOnlyUnboundProviders" : "AngularServiceRegistrationUnavailable");
    }

    private static bool ConstructorReturns(BlockStatement body)
    {
        var pending = new Stack<Node>();
        pending.Push(body);
        while (pending.TryPop(out var node))
        {
            if (node is ReturnStatement) return true;
            if (node is FunctionExpression or ArrowFunctionExpression or FunctionDeclaration) continue;
            foreach (var child in node.ChildNodes) pending.Push(child);
        }
        return false;
    }

    private static AssignmentExpression ServiceMember(FunctionExpression fn, string name)
    {
        var direct = fn.Body.Body.OfType<ExpressionStatement>().Select(s => s.Expression).OfType<AssignmentExpression>().ToHashSet();
        if (ConstructorReturns(fn.Body)) throw new AngularClickGap("AngularServiceInstanceUnavailable");
        var nodes = Walk(fn.Body).ToArray();
        var allowedThis = new HashSet<Node>();
        AssignmentExpression? found = null;
        foreach (var assignment in direct)
        {
            if (assignment.Left is not MemberExpression { Computed: false, Object: ThisExpression self, Property: Identifier key }) continue;
            allowedThis.Add(self);
            if (key.Name != name) continue;
            if (found is not null) throw new AngularClickGap("AmbiguousAngularServiceMember");
            if (assignment.Operator != Acornima.Operator.Assignment || assignment.Right is not FunctionExpression)
                throw new AngularClickGap("UnsupportedAngularServiceMember");
            found = assignment;
        }
        if (nodes.OfType<ThisExpression>().Any(node => !allowedThis.Contains(node))) throw new AngularClickGap("AngularServiceInstanceUnavailable");
        return found ?? throw new AngularClickGap("AngularServiceMemberUnavailable");
    }

    internal static Post ResolvePost(ServiceMethod method)
    {
        var tokens = method.Annotation.Elements.SkipLast(1).Select(e => (e as StringLiteral)?.Value).ToArray();
        var parameters = method.Constructor.Params.Select(p => (p as Identifier)?.Name).ToArray();
        var slot = Array.IndexOf(tokens, "$http");
        if (slot < 0 || tokens.Length != parameters.Length || tokens.Any(t => t is null) || tokens.Count(t => t == "$http") != 1
            || parameters.Any(p => p is null) || parameters.Distinct(StringComparer.Ordinal).Count() != parameters.Length
            || !ExclusiveReceiver(method.Constructor, parameters[slot]!, true)) throw new AngularClickGap("AngularHttpInjectionUnavailable");
        var calls = Walk(method.Body.Body).OfType<CallExpression>()
            .Where(c => c.Callee is Identifier id && id.Name == parameters[slot]).Take(2).ToArray();
        if (calls.Length != 1 || !ImmediateCalls(method.Body).Contains(calls[0]) || calls[0].Arguments.Count != 1 || calls[0].Arguments[0] is not ObjectExpression obj)
            throw new AngularClickGap("AngularHttpRequestUnavailable");
        var values = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var property in obj.Properties)
        {
            if (property is not Property { Computed: false, Method: false, Shorthand: false, Kind: PropertyKind.Init } p
                || (p.Key switch { Identifier id => id.Name, StringLiteral literal => literal.Value, _ => null }) is not { } key
                || key == "__proto__" || !values.TryAdd(key, p.Value)) throw new AngularClickGap("AngularHttpOptionsUnavailable");
        }
        if (values.GetValueOrDefault("method") is not StringLiteral verb || !verb.Value.Equals("POST", StringComparison.OrdinalIgnoreCase)
            || values.GetValueOrDefault("url") is not StringLiteral url) throw new AngularClickGap("AngularHttpPostUnavailable");
        return new Post(method.Script.Location(calls[0]), url.Value, slot);
    }
}
