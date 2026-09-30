namespace TraceMap.Core;

/// <summary>Configuration candidates within one independently agreed local
/// IL region. This does not resolve framework assemblies or execute providers.</summary>
internal static partial class IlCommandBindingExtractor
{
    internal const string Schema = "il-command-binding.v1";
    internal const int MaxTrackedReceivers = 128;
    internal const int MaxWorkUnits = 200_000;
    internal const string Limitation = "Bounded static command configuration candidate, not SQL execution. Framework APIs are recognized from exact encoded assembly/type/member scope, not loaded or authenticated. Normal control-flow joins retain only identical configuration records, including within protected blocks. Handler/filter entries start without pre-exception configuration; leave and possibly mutating calls discard it. Exception dispatch and finally continuations are unmodelled. Exact modelled parameter-collection operations preserve text/type only, not parameter values or order. Escaped collections invalidate their owning command. Argument slots require separately evidenced caller substitution. No raw command text, branch feasibility, field alias, provider dispatch, parameter values or runtime identity is established.";
    private static readonly IlValueOrigin Unknown = new("unknown", "");

    internal static IlCommandBindingResult Extract(IlBodyObservation body, IlBodyEvidenceExtractor.IlWorkBudget? aggregateBudget = null)
    {
        var bindings = new List<IlCommandBindingObservation>();
        var gaps = new SortedSet<string>(StringComparer.Ordinal);
        if (body.ValueFlow is null) return new([], ["IlCommandValueEvidenceUnavailable"]);
        if (body.ValueFlow.ControlFlow is not null) return ExtractControlFlow(body, aggregateBudget);
        var calls = body.Calls.ToDictionary(call => call.Offset);
        var commands = new Dictionary<IlValueOrigin, Command>();
        var adapters = new Dictionary<IlValueOrigin, Adapter>();
        var collections = new Dictionary<IlValueOrigin, IlValueOrigin>();
        var escaped = new HashSet<IlValueOrigin>();
        var region = -1;
        var work = 0;
        foreach (var values in body.ValueFlow.Calls)
        {
            var cost = 1 + commands.Count + adapters.Count + collections.Count;
            work += cost;
            if (work > MaxWorkUnits || commands.Count + adapters.Count + collections.Count + escaped.Count > MaxTrackedReceivers
                || aggregateBudget?.TryConsume(cost) == false)
                return new([], ["IlCommandBindingWorkLimit"]);
            if (values.Region != region)
            {
                commands.Clear(); adapters.Clear(); collections.Clear(); escaped.Clear(); region = values.Region;
            }
            var call = calls[values.Offset];
            if (values.State != "straight-line-candidate")
            {
                commands.Clear(); adapters.Clear(); collections.Clear(); gaps.Add("IlCommandOperandsUnavailable"); continue;
            }
            var api = Classify(call);
            if (api == Api.CommandConstructor && ObjectOrigin(values.Result))
            {
                commands[values.Result] = new(Unknown, new("constant-int32", "1"), null, null, call.Offset);
                if (values.Arguments.Count > 0 && FirstParameterIsString(call.TargetIdentity))
                    commands[values.Result] = new(values.Arguments[0], new("constant-int32", "1"), call.Offset, null, call.Offset);
                continue;
            }
            if (api is Api.CommandText or Api.CommandType && ObjectOrigin(values.Receiver) && values.Arguments.Count == 1)
            {
                var state = commands.GetValueOrDefault(values.Receiver, new(Unknown, Unknown, null, null, null));
                commands[values.Receiver] = api == Api.CommandText
                    ? state with { Text = values.Arguments[0], TextOffset = call.Offset }
                    : state with { Type = values.Arguments[0], TypeOffset = call.Offset };
                continue;
            }
            if (api == Api.AdapterConstructor && ObjectOrigin(values.Result))
            {
                if (values.Arguments.Count == 1 && IsCommandParameter(call.TargetIdentity))
                    adapters[values.Result] = new(values.Arguments[0], call.Offset);
                else gaps.Add("IlCommandAdapterConstructorUnsupported");
                continue;
            }
            if (api == Api.SelectCommand && ObjectOrigin(values.Receiver) && values.Arguments.Count == 1)
            {
                adapters[values.Receiver] = new(values.Arguments[0], call.Offset); continue;
            }
            if (api == Api.Parameters && ObjectOrigin(values.Result) && commands.ContainsKey(values.Receiver))
            { collections[values.Result] = values.Receiver; continue; }
            if (api == Api.ParameterMutation && collections.ContainsKey(values.Receiver))
            { gaps.Add("IlCommandParameterFlowUnavailable"); continue; }
            if (api is Api.Fill or Api.Execute)
            {
                var commandOrigin = values.Receiver;
                long? attachment = null;
                if (api == Api.Fill)
                {
                    if (!adapters.TryGetValue(values.Receiver, out var adapter))
                    { gaps.Add("IlCommandAdapterBindingUnavailable"); continue; }
                    commandOrigin = adapter.Command; attachment = adapter.Offset;
                }
                if (!ObjectOrigin(commandOrigin) || !commands.TryGetValue(commandOrigin, out var command)
                    || command.Text.Kind is not ("constant-string-hash" or "argument-slot" or "call-result"))
                { gaps.Add("IlCommandTextBindingUnavailable"); continue; }
                if (command.Text.Kind == "argument-slot") gaps.Add("IlCommandTextCallerBindingRequired");
                if (command.Text.Kind == "call-result") gaps.Add("IlCommandTextReturnBindingUnavailable");
                if (command.Type.Kind != "constant-int32") gaps.Add("IlCommandTypeBindingUnavailable");
                gaps.Add("IlCommandParameterFlowUnavailable");
                bindings.Add(new(call.Offset, region, values.Receiver, commandOrigin,
                    command.Text, command.Type,
                    new[] { command.ConstructorOffset, command.TextOffset, command.TypeOffset }
                        .Where(offset => offset.HasValue).Select(offset => offset!.Value).Distinct().Order().ToArray(), attachment));
                // Execution APIs can invoke provider/event code. Do not carry
                // their pre-call configuration into another endpoint.
                InvalidateEffects(values);
                continue;
            }
            // Every non-modelled call is a potential mutation. Non-escaped
            // allocation-local commands cannot be changed through unrelated
            // receivers, but argument-derived/escaped objects may be aliased.
            InvalidateEffects(values);
        }
        return new(bindings, gaps.ToArray());

        void InvalidateEffects(IlCallValueObservation values)
        {
            var passed = values.Arguments.Append(values.Receiver).ToHashSet();
            foreach (var pair in collections)
                if (passed.Contains(pair.Key) || escaped.Contains(pair.Key)) escaped.Add(pair.Value);
            // An adapter exposes its command to the callee/provider too.
            // Invalidating only the adapter would leave stale command state
            // available through a subsequently constructed adapter.
            foreach (var pair in adapters)
                if (pair.Key.Kind == "argument-slot" || escaped.Contains(pair.Key) || passed.Contains(pair.Key))
                    escaped.Add(pair.Value.Command);
            foreach (var key in commands.Keys.ToArray())
                if (key.Kind == "argument-slot" || escaped.Contains(key) || passed.Contains(key))
                { commands.Remove(key); escaped.Add(key); gaps.Add("IlCommandUnknownCallEffects"); }
            foreach (var key in adapters.Keys.ToArray())
                if (key.Kind == "argument-slot" || escaped.Contains(key) || passed.Contains(key))
                { adapters.Remove(key); escaped.Add(key); gaps.Add("IlCommandUnknownCallEffects"); }
            foreach (var key in collections.Keys.ToArray())
                if (passed.Contains(key) || escaped.Contains(key) || escaped.Contains(collections[key]))
                { collections.Remove(key); escaped.Add(key); }
        }
    }

    private static bool ObjectOrigin(IlValueOrigin value) => value.Kind is "allocation-site" or "call-result" or "argument-slot";
    private static bool FirstParameterIsString(string identity)
        => ParameterSection(identity).StartsWith("(type(namespace:6:System|names:6:String)", StringComparison.Ordinal);
    private static string ParameterSection(string identity)
    {
        const string marker = "|explicitThis:false|";
        var start = identity.IndexOf(marker, StringComparison.Ordinal);
        return start < 0 ? string.Empty : identity[(start + marker.Length)..];
    }
    private static bool IsCommandParameter(string identity)
        => identity.Contains("|names:10:SqlCommand)", StringComparison.Ordinal)
            || identity.Contains("|names:11:OdbcCommand)", StringComparison.Ordinal)
            || identity.Contains("|names:12:OleDbCommand)", StringComparison.Ordinal)
            || identity.Contains("|names:9:DbCommand)", StringComparison.Ordinal);

    private static Api Classify(IlCallObservation call)
    {
        if (call.ReferenceKind != "memberref" || call.StackShape is not { HasThis: true, Supported: true }) return Api.Unknown;
        var identity = call.TargetIdentity;
        if (!identity.StartsWith("memberref|type:scope(assembly:name:", StringComparison.Ordinal)) return Api.Unknown;
        var member = identity.IndexOf("|member:", StringComparison.Ordinal);
        if (member < 0) return Api.Unknown;
        var owner = identity[..member];
        var framework = owner.StartsWith("memberref|type:scope(assembly:name:11:System.Data|", StringComparison.Ordinal)
            && owner.Contains("|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)", StringComparison.Ordinal)
            || owner.StartsWith("memberref|type:scope(assembly:name:18:System.Data.Common|", StringComparison.Ordinal)
            && owner.Contains("|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)", StringComparison.Ordinal);
        if (!framework) return Api.Unknown;
        bool Type(string ns, string name) => owner.EndsWith($"type(namespace:{ns.Length}:{ns}|names:{name.Length}:{name})", StringComparison.Ordinal);
        bool Method(string name) => identity.AsSpan(member).StartsWith($"|member:{name.Length}:{name}|", StringComparison.Ordinal);
        var command = Type("System.Data.Common", "DbCommand") || Type("System.Data", "IDbCommand")
            || Type("System.Data.SqlClient", "SqlCommand") || Type("System.Data.Odbc", "OdbcCommand")
            || Type("System.Data.OleDb", "OleDbCommand");
        var adapter = Type("System.Data.Common", "DbDataAdapter") || Type("System.Data.Common", "DataAdapter")
            || Type("System.Data.SqlClient", "SqlDataAdapter") || Type("System.Data.Odbc", "OdbcDataAdapter")
            || Type("System.Data.OleDb", "OleDbDataAdapter");
        if (command && Method(".ctor") && call.Opcode == "newobj") return Api.CommandConstructor;
        if (command && Method("set_CommandText") && call.StackShape.ParameterCount == 1
            && ParameterSection(identity) == "(type(namespace:6:System|names:6:String))->type(namespace:6:System|names:4:Void)") return Api.CommandText;
        if (command && Method("set_CommandType") && call.StackShape.ParameterCount == 1
            && ParameterSection(identity) == "(" + owner["memberref|type:".Length..owner.IndexOf(")type(", StringComparison.Ordinal)]
                + ")type(namespace:11:System.Data|names:11:CommandType))->type(namespace:6:System|names:4:Void)") return Api.CommandType;
        if (adapter && Method(".ctor") && call.Opcode == "newobj") return Api.AdapterConstructor;
        if (adapter && Method("set_SelectCommand") && call.StackShape.ParameterCount == 1
            && IsCommandParameter(identity)) return Api.SelectCommand;
        if (adapter && Method("Fill")) return Api.Fill;
        if (command && (Method("ExecuteReader") || Method("ExecuteNonQuery") || Method("ExecuteScalar"))) return Api.Execute;
        var scope = owner["memberref|type:".Length..owner.IndexOf(")type(", StringComparison.Ordinal)];
        var collections = new[] { ("System.Data.Common", "DbParameterCollection"), ("System.Data.SqlClient", "SqlParameterCollection"),
            ("System.Data.Odbc", "OdbcParameterCollection"), ("System.Data.OleDb", "OleDbParameterCollection") };
        if (command && Method("get_Parameters") && call.StackShape.ParameterCount == 0
            && collections.Any(collection => Type(collection.Item1, collection.Item2.Replace("ParameterCollection", "Command", StringComparison.Ordinal))
                && ParameterSection(identity) == $"()->{scope})type(namespace:{collection.Item1.Length}:{collection.Item1}|names:{collection.Item2.Length}:{collection.Item2})"))
            return Api.Parameters;
        if (collections.Any(collection => Type(collection.Item1, collection.Item2)))
        {
            var parameters = ParameterSection(identity);
            if (Method("Add") && call.StackShape.ParameterCount == 1
                && parameters == "(type(namespace:6:System|names:6:Object))->type(namespace:6:System|names:5:Int32)") return Api.ParameterMutation;
            if (Method("AddRange") && call.StackShape.ParameterCount == 1
                && parameters == "(type(namespace:6:System|names:5:Array))->type(namespace:6:System|names:4:Void)") return Api.ParameterMutation;
            if (Method("Clear") && call.StackShape.ParameterCount == 0
                && parameters == "()->type(namespace:6:System|names:4:Void)") return Api.ParameterMutation;
            if (Method("RemoveAt") && call.StackShape.ParameterCount == 1
                && parameters == "(type(namespace:6:System|names:5:Int32))->type(namespace:6:System|names:4:Void)") return Api.ParameterMutation;
            foreach (var collection in collections)
            {
                if (!Type(collection.Item1, collection.Item2) || collection.Item2 == "DbParameterCollection") continue;
                var name = collection.Item2.Replace("Collection", "", StringComparison.Ordinal);
                var parameterType = $"{scope})type(namespace:{collection.Item1.Length}:{collection.Item1}|names:{name.Length}:{name})";
                if (Method("Add") && call.StackShape.ParameterCount == 1 && parameters == $"({parameterType})->{parameterType}") return Api.ParameterMutation;
                if (Method("AddRange") && call.StackShape.ParameterCount == 1
                    && parameters == $"({parameterType}[])->type(namespace:6:System|names:4:Void)") return Api.ParameterMutation;
                if (Method("AddWithValue") && call.StackShape.ParameterCount == 2
                    && parameters == $"(type(namespace:6:System|names:6:String),type(namespace:6:System|names:6:Object))->{parameterType}") return Api.ParameterMutation;
            }
        }
        return Api.Unknown;
    }

    private enum Api { Unknown, CommandConstructor, CommandText, CommandType, AdapterConstructor, SelectCommand, Fill, Execute, Parameters, ParameterMutation }
    private sealed record Command(IlValueOrigin Text, IlValueOrigin Type, long? TextOffset, long? TypeOffset, long? ConstructorOffset);
    private sealed record Adapter(IlValueOrigin Command, long Offset);
}

internal sealed record IlCommandBindingObservation(long EndpointOffset, int Region,
    IlValueOrigin EndpointReceiver, IlValueOrigin CommandReceiver, IlValueOrigin CommandText,
    IlValueOrigin CommandType, IReadOnlyList<long> ConfigurationOffsets, long? AdapterBindingOffset);
internal sealed record IlCommandBindingResult(IReadOnlyList<IlCommandBindingObservation> Bindings, IReadOnlyList<string> Gaps);
