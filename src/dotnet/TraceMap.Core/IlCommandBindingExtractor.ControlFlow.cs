namespace TraceMap.Core;

internal static partial class IlCommandBindingExtractor
{
    private sealed class ConfigurationState
    {
        public Dictionary<IlValueOrigin, Command> Commands { get; } = [];
        public Dictionary<IlValueOrigin, Adapter> Adapters { get; } = [];
        public Dictionary<IlValueOrigin, IlValueOrigin> Collections { get; } = [];
        public HashSet<IlValueOrigin> Escaped { get; } = [];
        public int Cost => 1 + Commands.Count + Adapters.Count + Collections.Count + Escaped.Count;
        public ConfigurationState Copy()
        {
            var result = new ConfigurationState();
            foreach (var pair in Commands) result.Commands.Add(pair.Key, pair.Value);
            foreach (var pair in Adapters) result.Adapters.Add(pair.Key, pair.Value);
            foreach (var pair in Collections) result.Collections.Add(pair.Key, pair.Value);
            result.Escaped.UnionWith(Escaped); return result;
        }
        public void Clear() { Commands.Clear(); Adapters.Clear(); Collections.Clear(); }
        public bool Merge(ConfigurationState other)
        {
            var changed = Intersect(Commands, other.Commands) | Intersect(Adapters, other.Adapters) | Intersect(Collections, other.Collections);
            var old = Escaped.Count; Escaped.UnionWith(other.Escaped); return changed || old != Escaped.Count;
        }
        private static bool Intersect<T>(Dictionary<IlValueOrigin, T> target, Dictionary<IlValueOrigin, T> incoming)
        {
            var changed = false;
            foreach (var key in target.Keys.ToArray())
                if (!incoming.TryGetValue(key, out var value) || !EqualityComparer<T>.Default.Equals(target[key], value))
                { target.Remove(key); changed = true; }
            return changed;
        }
    }

    private static IlCommandBindingResult ExtractControlFlow(IlBodyObservation body, IlBodyEvidenceExtractor.IlWorkBudget? aggregateBudget)
    {
        var nodes = body.ValueFlow!.ControlFlow!;
        if (nodes.Count == 0) return new([], ["IlCommandControlFlowUnavailable"]);
        var byOffset = nodes.ToDictionary(node => node.Offset);
        if (nodes.Any(node => node.Successors.Any(successor => !byOffset.ContainsKey(successor)))) return new([], ["IlCommandControlFlowUnavailable"]);
        var calls = body.Calls.ToDictionary(call => call.Offset);
        var operands = body.ValueFlow.Calls.ToDictionary(call => call.Offset);
        var states = new Dictionary<long, ConfigurationState> { [nodes[0].Offset] = new() };
        var queue = new Queue<long>(); var queued = new HashSet<long>();
        var gaps = new SortedSet<string>(StringComparer.Ordinal); var bindings = new List<IlCommandBindingObservation>();
        queue.Enqueue(nodes[0].Offset); queued.Add(nodes[0].Offset); var work = 0;
        foreach (var node in nodes.Where(node => node.ExceptionEntryStackCount >= 0))
        {
            if (++work > MaxWorkUnits || aggregateBudget?.TryConsume(1) == false)
                return new([], ["IlCommandBindingWorkLimit"]);
            if (!states.TryAdd(node.Offset, new ConfigurationState())) states[node.Offset].Clear();
            if (queued.Add(node.Offset)) queue.Enqueue(node.Offset);
        }
        while (queue.TryDequeue(out var offset))
        {
            queued.Remove(offset); var input = states[offset]; var cost = input.Cost * (1 + (byOffset[offset].ExposedOrigins?.Count ?? 0));
            if ((work += cost) > MaxWorkUnits || input.Cost > MaxTrackedReceivers + 1 || aggregateBudget?.TryConsume(cost) == false)
                return new([], ["IlCommandBindingWorkLimit"]);
            var state = input.Copy(); Apply(offset, state, false);
            foreach (var successor in byOffset[offset].Successors)
            {
                var successorCost = state.Cost + (states.GetValueOrDefault(successor)?.Cost ?? 0);
                if ((work += successorCost) > MaxWorkUnits || aggregateBudget?.TryConsume(successorCost) == false)
                    return new([], ["IlCommandBindingWorkLimit"]);
                if (!states.TryGetValue(successor, out var existing))
                { states.Add(successor, state.Copy()); if (queued.Add(successor)) queue.Enqueue(successor); }
                else if (existing.Merge(state) && queued.Add(successor)) queue.Enqueue(successor);
            }
        }
        // Emit from converged input states only, never intermediate loop visits.
        foreach (var offset in states.Keys.Order())
        {
            var cost = states[offset].Cost * (1 + (byOffset[offset].ExposedOrigins?.Count ?? 0));
            if ((work += cost) > MaxWorkUnits || aggregateBudget?.TryConsume(cost) == false)
                return new([], ["IlCommandBindingWorkLimit"]);
            Apply(offset, states[offset].Copy(), true);
        }
        return new(bindings, gaps.ToArray());

        void Apply(long offset, ConfigurationState state, bool emit)
        {
            if (byOffset[offset].InvalidatesConfiguration) state.Clear();
            foreach (var origin in byOffset[offset].ExposedOrigins ?? []) InvalidatePassed([origin]);
            if (!operands.TryGetValue(offset, out var values)) return;
            if (values.State != "control-flow-candidate" || !calls.TryGetValue(offset, out var call))
            { state.Clear(); gaps.Add("IlCommandOperandsUnavailable"); return; }
            var api = Classify(call);
            if (api == Api.NonTextCommandConfiguration) return;
            if (api == Api.CommandConstructor && ObjectOrigin(values.Result))
            {
                var textConstructor = values.Arguments.Count > 0 && FirstParameterIsString(call.TargetIdentity);
                state.Commands[values.Result] = new(textConstructor ? values.Arguments[0] : Unknown, new("constant-int32", "1"),
                    textConstructor ? offset : null, null, offset); return;
            }
            if (api is Api.CommandText or Api.CommandType && ObjectOrigin(values.Receiver) && values.Arguments.Count == 1)
            {
                var command = state.Commands.GetValueOrDefault(values.Receiver, new(Unknown, Unknown, null, null, null));
                state.Commands[values.Receiver] = api == Api.CommandText ? command with { Text = values.Arguments[0], TextOffset = offset }
                    : command with { Type = values.Arguments[0], TypeOffset = offset }; return;
            }
            if (api == Api.AdapterConstructor && ObjectOrigin(values.Result))
            {
                if (values.Arguments.Count == 1 && IsCommandParameter(call.TargetIdentity)) state.Adapters[values.Result] = new(values.Arguments[0], offset);
                else if (emit) gaps.Add("IlCommandAdapterConstructorUnsupported"); return;
            }
            if (api == Api.SelectCommand && ObjectOrigin(values.Receiver) && values.Arguments.Count == 1)
            { state.Adapters[values.Receiver] = new(values.Arguments[0], offset); return; }
            if (api == Api.Parameters && ObjectOrigin(values.Result) && state.Commands.ContainsKey(values.Receiver))
            { state.Collections[values.Result] = values.Receiver; return; }
            if (api == Api.TableMappings && ObjectOrigin(values.Result) && state.Adapters.ContainsKey(values.Receiver))
            { state.Collections[values.Result] = values.Receiver; return; }
            if (api == Api.TableMappingMutation && state.Collections.ContainsKey(values.Receiver)) return;
            if (api == Api.ParameterMutation && state.Collections.ContainsKey(values.Receiver))
            { if (emit) gaps.Add("IlCommandParameterFlowUnavailable"); return; }
            if (api is Api.Fill or Api.Execute)
            {
                var commandOrigin = values.Receiver; long? attachment = null;
                if (api == Api.Fill)
                {
                    if (!state.Adapters.TryGetValue(values.Receiver, out var adapter))
                    { if (emit) gaps.Add("IlCommandAdapterBindingUnavailable"); Invalidate(); return; }
                    commandOrigin = adapter.Command; attachment = adapter.Offset;
                }
                if (!ObjectOrigin(commandOrigin) || !state.Commands.TryGetValue(commandOrigin, out var command)
                    || command.Text.Kind is not ("constant-string-hash" or "argument-slot" or "call-result"))
                { if (emit) gaps.Add("IlCommandTextBindingUnavailable"); Invalidate(); return; }
                if (emit)
                {
                    if (command.Text.Kind == "argument-slot") gaps.Add("IlCommandTextCallerBindingRequired");
                    if (command.Text.Kind == "call-result") gaps.Add("IlCommandTextReturnBindingUnavailable");
                    if (command.Type.Kind != "constant-int32") gaps.Add("IlCommandTypeBindingUnavailable");
                    gaps.Add("IlCommandParameterFlowUnavailable");
                    bindings.Add(new(offset, 0, values.Receiver, commandOrigin, command.Text, command.Type,
                        new[] { command.ConstructorOffset, command.TextOffset, command.TypeOffset }.OfType<long>().Distinct().Order().ToArray(), attachment));
                }
                Invalidate(); return;
            }
            Invalidate();

            void Invalidate()
            {
                InvalidatePassed(values.Arguments.Append(values.Receiver));
            }

            void InvalidatePassed(IEnumerable<IlValueOrigin> origins)
            {
                // An unknown heap value cannot name an unexposed allocation.
                // Every heap store/address exposure is processed separately;
                // external/argument-derived objects remain conservatively aliased.
                var passed = origins.Select(IlValueAddresses.Target).ToHashSet();
                if (passed.Any(value => value.Kind == "address-unavailable"))
                { state.Clear(); gaps.Add("IlCommandUnknownCallEffects"); return; }
                foreach (var pair in state.Collections) if (passed.Contains(pair.Key) || state.Escaped.Contains(pair.Key)) state.Escaped.Add(pair.Value);
                foreach (var pair in state.Adapters)
                    if (pair.Key.Kind is "argument-slot" or "call-result" || passed.Contains(pair.Key) || state.Escaped.Contains(pair.Key)) state.Escaped.Add(pair.Value.Command);
                foreach (var key in state.Commands.Keys.ToArray())
                    if (key.Kind is "argument-slot" or "call-result" || passed.Contains(key) || state.Escaped.Contains(key))
                    { state.Commands.Remove(key); state.Escaped.Add(key); gaps.Add("IlCommandUnknownCallEffects"); }
                foreach (var key in state.Adapters.Keys.ToArray())
                    if (key.Kind is "argument-slot" or "call-result" || passed.Contains(key) || state.Escaped.Contains(key))
                    { state.Adapters.Remove(key); state.Escaped.Add(key); gaps.Add("IlCommandUnknownCallEffects"); }
                foreach (var key in state.Collections.Keys.ToArray())
                    if (passed.Contains(key) || state.Escaped.Contains(key) || state.Escaped.Contains(state.Collections[key]))
                    { state.Collections.Remove(key); state.Escaped.Add(key); }
            }
        }
    }
}
