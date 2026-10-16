using System.Globalization;

namespace TraceMap.Core;

/// <summary>Normal-flow fixed point over independently decoded canonical IL.
/// Joins retain bounded scalar alternatives or unknown. This does
/// not model exception edges, branch feasibility, heap aliases or execution.</summary>
internal static class IlControlFlowValueExtractor
{
    internal const int MaxInstructions = 20_000;
    internal const int MaxSlots = 256;
    internal const int MaxExceptionEntries = 1024;
    internal const int MaxWorkUnits = 200_000;
    private static readonly IlValueOrigin Unknown = new("unknown", "");
    private sealed record Instruction(long Offset, string Opcode, string Operand);
    private sealed class State
    {
        public List<IlValueOrigin> Stack { get; } = [];
        public Dictionary<int, IlValueOrigin> Locals { get; } = [];
        public Dictionary<int, IlValueOrigin> Arguments { get; } = [];
        public HashSet<int> ExposedLocals { get; } = [];
        public HashSet<int> ExposedArguments { get; } = [];
        public bool InvalidStack { get; set; }
        public int Cost => 1 + Stack.Count + Locals.Count + Arguments.Count + ExposedLocals.Count + ExposedArguments.Count;
        public State Copy()
        {
            var copy = new State { InvalidStack = InvalidStack };
            copy.Stack.AddRange(Stack);
            foreach (var pair in Locals) copy.Locals.Add(pair.Key, pair.Value);
            foreach (var pair in Arguments) copy.Arguments.Add(pair.Key, pair.Value);
            copy.ExposedLocals.UnionWith(ExposedLocals); copy.ExposedArguments.UnionWith(ExposedArguments);
            return copy;
        }
    }

    internal static IlValueFlowObservation Extract(IReadOnlyList<string> encoded,
        IReadOnlyList<IlCallObservation> calls, int maxStack, IReadOnlyList<IlValueExceptionEntry>? exceptionEntries = null,
        IReadOnlyList<IlValueExceptionRegion>? exceptionRegions = null)
    {
        if (encoded.Count > MaxInstructions) return new([], ["IlValueControlFlowInstructionLimit"]);
        if (exceptionEntries?.Count > MaxExceptionEntries) return new([], ["IlValueExceptionEntryLimit"]);
        var instructions = encoded.Select(value =>
        {
            var parts = value.Split(':', 4);
            if (parts.Length != 4 || !long.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset))
                throw new ArgumentException("Invalid canonical IL instruction.", nameof(encoded));
            return new Instruction(offset, parts[2], parts[3]);
        }).ToArray();
        if (instructions.Length == 0) return new([], []);
        var positions = instructions.Select((value, index) => (value.Offset, index)).ToDictionary(value => value.Offset, value => value.index);
        if (exceptionRegions?.Count > MaxExceptionEntries || exceptionRegions?.Any(region =>
            !positions.ContainsKey(region.HandlerStart) || region.HandlerEnd <= region.HandlerStart
            || region.FilterStart is { } filter && (!positions.ContainsKey(filter) || filter >= region.HandlerStart)) == true)
            return new([], ["IlValueExceptionEntryUnavailable"]);
        // A scalar argument survives leave only with complete handler ranges,
        // no address exposure anywhere, and no write in ANY handler/filter.
        // This overapproximates crossed finally blocks; it does not dispatch EH.
        var unsafeArguments = new HashSet<int>();
        var unsafeHandlerMemory = false;
        var regionWork = 0;
        foreach (var instruction in instructions)
        {
            regionWork += 1 + (exceptionRegions?.Count ?? 0);
            if (regionWork > MaxWorkUnits) return new([], ["IlValueControlFlowWorkLimit"], WorkUnits: MaxWorkUnits);
            var inHandler = exceptionRegions?.Any(region => instruction.Offset >= (region.FilterStart ?? region.HandlerStart)
                && instruction.Offset < region.HandlerEnd) == true;
            if (inHandler && (instruction.Opcode is "calli" or "jmp" or "localloc" or "cpblk" or "initblk" or "arglist" or "stobj"
                || instruction.Opcode.StartsWith("stind.", StringComparison.Ordinal))) unsafeHandlerMemory = true;
            if (Slot(instruction.Opcode, instruction.Operand, "ldarga", out var slot)
                || Slot(instruction.Opcode, instruction.Operand, "starg", out slot)
                && inHandler)
                unsafeArguments.Add(slot);
        }
        var byOffset = calls.Where(call => call.Opcode is "call" or "callvirt" or "newobj").ToDictionary(call => call.Offset);
        var gaps = new SortedSet<string>(StringComparer.Ordinal);
        var entries = new Dictionary<int, int>();
        foreach (var entry in exceptionEntries ?? [])
        {
            if (entry.StackCount is not (0 or 1) || !positions.TryGetValue(entry.Offset, out var position)
                || entries.TryGetValue(position, out var stackCount) && stackCount != entry.StackCount)
                return new([], ["IlValueExceptionEntryUnavailable"]);
            entries[position] = entry.StackCount;
        }
        if (entries.Count > 0) gaps.Add("IlValueExceptionFlowUnavailable");
        if (exceptionRegions is not null && !entries.Keys.Order().SequenceEqual(exceptionRegions
            .SelectMany(region => region.FilterStart is { } filter ? new[] { positions[region.HandlerStart], positions[filter] }
                : new[] { positions[region.HandlerStart] }).Distinct().Order()))
            return new([], ["IlValueExceptionEntryUnavailable"]);
        var successors = new int[instructions.Length][];
        for (var index = 0; index < instructions.Length; index++)
        {
            var instruction = instructions[index];
            var next = index + 1 < instructions.Length ? new[] { index + 1 } : [];
            if (instruction.Opcode is "ret" or "throw" or "rethrow" or "endfinally" or "endfilter") successors[index] = [];
            else if (instruction.Operand.StartsWith("br:0x", StringComparison.Ordinal))
            {
                if (!long.TryParse(instruction.Operand.AsSpan(5), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var target)
                    || !positions.TryGetValue(target, out var targetIndex)) return new([], ["IlValueControlFlowTargetUnavailable"]);
                successors[index] = instruction.Opcode is "br" or "br.s" or "leave" or "leave.s"
                    ? [targetIndex] : next.Append(targetIndex).Distinct().Order().ToArray();
            }
            else if (instruction.Opcode == "switch")
            {
                var opening = instruction.Operand.IndexOf('[');
                if (opening < 0 || !instruction.Operand.EndsWith(']')) return new([], ["IlValueControlFlowTargetUnavailable"]);
                var targets = new List<int>(next);
                foreach (var target in instruction.Operand[(opening + 1)..^1].Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!target.StartsWith("0x", StringComparison.Ordinal)
                        || !long.TryParse(target.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset)
                        || !positions.TryGetValue(offset, out var targetIndex)) return new([], ["IlValueControlFlowTargetUnavailable"]);
                    targets.Add(targetIndex);
                }
                successors[index] = targets.Distinct().Order().ToArray();
            }
            else successors[index] = next;
        }
        var inputs = new State?[instructions.Length];
        var events = new Dictionary<long, IlCallValueObservation>();
        var invalidates = new bool[instructions.Length];
        var exposed = new SortedDictionary<long, HashSet<IlValueOrigin>>();
        var queue = new Queue<int>(); var queued = new HashSet<int>();
        inputs[0] = new State();
        foreach (var instruction in instructions)
            if (Slot(instruction.Opcode, instruction.Operand, "starg", out var modified)
                || Slot(instruction.Opcode, instruction.Operand, "ldarga", out modified))
                inputs[0]!.Arguments[modified] = Argument(modified);
        if (inputs[0]!.Arguments.Count > MaxSlots) return new([], ["IlValueControlFlowSlotLimit"]);
        queue.Enqueue(0); queued.Add(0);
        var work = regionWork;
        foreach (var entry in entries.OrderBy(pair => pair.Key))
        {
            if ((work += 1 + inputs[0]!.Arguments.Count + entry.Value) > MaxWorkUnits)
                return new([], ["IlValueControlFlowWorkLimit"], WorkUnits: MaxWorkUnits);
            // Handler/filter roots receive no pre-exception local origins.
            // Potentially rewritten/byref arguments are unknown too. This is
            // a conservative entry seed, not reconstructed exception dispatch.
            var seed = new State();
            foreach (var key in inputs[0]!.Arguments.Keys) seed.Arguments[key] = Unknown;
            seed.Stack.AddRange(Enumerable.Repeat(Unknown, entry.Value));
            if (inputs[entry.Key] is null) inputs[entry.Key] = seed;
            else Merge(inputs[entry.Key]!, seed);
            if (queued.Add(entry.Key)) queue.Enqueue(entry.Key);
        }
        while (queue.TryDequeue(out var index))
        {
            queued.Remove(index);
            var input = inputs[index]!;
            if ((work += input.Cost) > MaxWorkUnits)
                return new([], ["IlValueControlFlowWorkLimit"], WorkUnits: MaxWorkUnits);
            var state = input.Copy(); var instruction = instructions[index];
            invalidates[index] = Transfer(instruction, state);
            if (work > MaxWorkUnits) return new([], ["IlValueControlFlowWorkLimit"], WorkUnits: MaxWorkUnits);
            if (exposed.GetValueOrDefault(instruction.Offset)?.Count > MaxSlots)
                return new([], ["IlValueExposureOriginLimit"], WorkUnits: work);
            if (state.Cost > MaxSlots + 1)
                return new([], ["IlValueControlFlowSlotLimit"], WorkUnits: work);
            foreach (var successor in successors[index])
            {
                // Charge cloning/merging before allocating a successor state;
                // a wide switch cannot multiply large states for one unit.
                if ((work += state.Cost + (inputs[successor]?.Cost ?? 0)) > MaxWorkUnits)
                    return new([], ["IlValueControlFlowWorkLimit"], WorkUnits: MaxWorkUnits);
                if (inputs[successor] is null)
                {
                    inputs[successor] = state.Copy();
                    if (queued.Add(successor)) queue.Enqueue(successor);
                }
                else if (Merge(inputs[successor]!, state) && queued.Add(successor)) queue.Enqueue(successor);
            }
        }
        return new(events.Values.OrderBy(value => value.Offset).ToArray(), gaps.ToArray(),
            instructions.Select((value, index) => new IlValueControlNode(value.Offset,
                successors[index].Select(successor => instructions[successor].Offset).ToArray(), invalidates[index] || entries.ContainsKey(index),
                entries.GetValueOrDefault(index, -1), exposed.GetValueOrDefault(value.Offset)?.OrderBy(origin => origin.Kind, StringComparer.Ordinal)
                    .ThenBy(origin => origin.Identity, StringComparer.Ordinal).ToArray())).ToArray(), work,
            instructions.Select((instruction, index) => (instruction, input: inputs[index]))
                .Where(item => item.instruction.Opcode == "ret" && item.input is not null)
                .Select(item => new IlReturnValueObservation(item.instruction.Offset,
                    !item.input!.InvalidStack && item.input.Stack.Count == 1 ? "return-operand-candidate" : "return-operand-unavailable",
                    !item.input.InvalidStack && item.input.Stack.Count == 1 ? item.input.Stack[0] : Unknown)).ToArray());

        bool Transfer(Instruction instruction, State state)
        {
            var opcode = instruction.Opcode; var operand = instruction.Operand;
            void Expose(IlValueOrigin value)
            {
                if (!exposed.TryGetValue(instruction.Offset, out var origins)) exposed.Add(instruction.Offset, origins = []);
                if (origins.Add(IlValueAddresses.Target(value))) work++;
                if (IlValueAddresses.Slot(value, out var slot))
                {
                    var local = value.Kind == "local-address";
                    if (origins.Add(local ? state.Locals.GetValueOrDefault(slot, Unknown) : state.Arguments.GetValueOrDefault(slot, Argument(slot)))) work++;
                    (local ? state.ExposedLocals : state.ExposedArguments).Add(slot);
                }
            }
            void RewriteAddress(IlValueOrigin address, IlValueOrigin value)
            {
                if (IlValueAddresses.Slot(address, out var slot))
                {
                    if (address.Kind == "local-address") state.Locals[slot] = value;
                    else state.Arguments[slot] = value;
                }
            }
            void Clear(string reason)
            {
                state.Stack.Clear(); state.Locals.Clear();
                // An invalidating operation cannot recreate original caller
                // arguments after an earlier starg or by-reference exposure.
                foreach (var key in state.Arguments.Keys.ToArray()) state.Arguments[key] = Unknown;
                state.InvalidStack = true; gaps.Add(reason);
            }
            IlValueOrigin Pop()
            {
                if (state.Stack.Count == 0) { state.InvalidStack = true; gaps.Add("IlValueStackUnavailable"); return Unknown; }
                var value = state.Stack[^1]; state.Stack.RemoveAt(state.Stack.Count - 1); return value;
            }
            void Push(IlValueOrigin value)
            {
                if (state.Stack.Count >= Math.Min(maxStack, MaxSlots)) Clear("IlValueStackLimit");
                else state.Stack.Add(value);
            }
            if (opcode == "nop") return false;
            if (opcode == "ldstr") { Push(new("constant-string-hash", operand)); return false; }
            if (opcode == "ldnull") { Push(new("null", "")); return false; }
            if (opcode.StartsWith("ldc.i4", StringComparison.Ordinal))
            {
                Push(new("constant-int32", opcode == "ldc.i4.m1" ? "-1"
                    : opcode is "ldc.i4" or "ldc.i4.s" ? operand[2..] : opcode[7..])); return false;
            }
            if (Slot(opcode, operand, "ldarg", out var slot))
            { Push(state.Arguments.GetValueOrDefault(slot, Argument(slot))); return false; }
            if (Slot(opcode, operand, "starg", out slot)) { var value = Pop(); if (state.ExposedArguments.Contains(slot)) Expose(value); state.Arguments[slot] = value; return false; }
            if (Slot(opcode, operand, "ldloc", out slot)) { Push(state.Locals.GetValueOrDefault(slot, Unknown)); return false; }
            if (Slot(opcode, operand, "stloc", out slot)) { var value = Pop(); if (state.ExposedLocals.Contains(slot)) Expose(value); state.Locals[slot] = value; return false; }
            if (Slot(opcode, operand, "ldloca", out slot))
            { var value = state.Locals.GetValueOrDefault(slot, Unknown); Push(IlValueAddresses.Create("local-address", slot, value)); return false; }
            if (Slot(opcode, operand, "ldarga", out slot))
            { var value = state.Arguments.GetValueOrDefault(slot, Argument(slot)); Push(IlValueAddresses.Create("argument-address", slot, value)); state.Arguments[slot] = Unknown; return false; }
            if (opcode == "dup") { var value = Pop(); Push(value); Push(value); return false; }
            if (opcode == "pop") { _ = Pop(); return false; }
            if (byOffset.TryGetValue(instruction.Offset, out var call))
            {
                if (call.StackShape is not { Supported: true, ParameterCount: >= 0 and <= MaxSlots } shape)
                {
                    events[instruction.Offset] = new(instruction.Offset, 0, "call-shape-unavailable", Unknown, [], Unknown);
                    Clear("IlValueCallShapeUnavailable"); return true;
                }
                var underflow = state.InvalidStack || state.Stack.Count < shape.ParameterCount + (shape.HasThis && opcode != "newobj" ? 1 : 0);
                var arguments = new IlValueOrigin[shape.ParameterCount];
                for (var argument = arguments.Length - 1; argument >= 0; argument--) arguments[argument] = Pop();
                var receiver = opcode == "newobj" || !shape.HasThis ? Unknown : Pop();
                // Addresses never become scalar caller values. A callee can
                // rewrite the referenced slot and mutate its former object.
                foreach (var address in arguments.Append(receiver).Where(IlValueAddresses.IsAddress))
                { Expose(address); RewriteAddress(address, Unknown); gaps.Add("IlValueByReferenceEffectsUnavailable"); }
                var result = opcode == "newobj" ? new IlValueOrigin("allocation-site", instruction.Offset.ToString(CultureInfo.InvariantCulture))
                    : shape.ReturnsValue ? new IlValueOrigin("call-result", instruction.Offset.ToString(CultureInfo.InvariantCulture)) : Unknown;
                events[instruction.Offset] = new(instruction.Offset, 0, underflow ? "stack-unavailable" : "control-flow-candidate", receiver, arguments, result);
                gaps.Add("IlValueCallEffectsUnmodeled");
                if (underflow) Clear("IlValueStackUnavailable");
                if (opcode == "newobj" || shape.ReturnsValue) Push(result);
                return underflow;
            }
            if (opcode is "br" or "br.s") return false;
            if (opcode is "leave" or "leave.s")
            {
                var preserved = exceptionRegions is null || unsafeHandlerMemory ? [] : state.Arguments
                    .Where(pair => !unsafeArguments.Contains(pair.Key)).ToArray();
                Clear("IlValueExceptionFlowUnavailable");
                foreach (var pair in preserved) state.Arguments[pair.Key] = pair.Value;
                // leave establishes an empty evaluation stack. Locals and
                // arguments without the conservative mutation proof stay unknown.
                state.InvalidStack = false; return true;
            }
            if (opcode == "switch" || opcode.StartsWith("brtrue", StringComparison.Ordinal) || opcode.StartsWith("brfalse", StringComparison.Ordinal))
            { _ = Pop(); return false; }
            if (instruction.Operand.StartsWith("br:0x", StringComparison.Ordinal)) { _ = Pop(); _ = Pop(); return false; }
            if (opcode is "ret" or "throw" or "rethrow" or "endfinally" or "endfilter") return false;
            // Numeric/array values remain unknown. Heap values are not
            // reconstructed; stores and addresses report object exposure.
            if (opcode is "add" or "add.ovf" or "add.ovf.un" or "sub" or "sub.ovf" or "sub.ovf.un" or "mul" or "mul.ovf" or "mul.ovf.un" or "div" or "div.un" or "rem" or "rem.un" or "and" or "or" or "xor"
                or "shl" or "shr" or "shr.un" or "ceq" or "cgt" or "cgt.un" or "clt" or "clt.un")
            { _ = Pop(); _ = Pop(); Push(Unknown); return false; }
            if (opcode.StartsWith("conv.", StringComparison.Ordinal) || opcode is "neg" or "not" or "ldlen")
            { _ = Pop(); Push(Unknown); return false; }
            if (opcode == "castclass") { var value = Pop(); Push(value); return false; }
            if (opcode is "box" or "unbox.any") { var value = Pop(); Push(value); return false; }
            if (opcode == "ldtoken") { Push(Unknown); return false; }
            // Delegate construction loads a function pointer, not a call result.
            // Preserve only its stack effect; no delegate dispatch/value is proven.
            if (opcode == "ldftn") { Push(Unknown); return false; }
            if (opcode == "ldvirtftn") { Expose(Pop()); Push(Unknown); return false; }
            if (opcode == "newarr") { _ = Pop(); Push(new("allocation-site", instruction.Offset.ToString(CultureInfo.InvariantCulture))); return false; }
            if (opcode == "ldfld") { Expose(Pop()); Push(Unknown); gaps.Add("IlValueFieldOriginUnavailable"); return false; }
            if (opcode == "ldsfld") { Push(Unknown); gaps.Add("IlValueFieldOriginUnavailable"); return false; }
            if (opcode is "ldflda" or "ldsflda")
            { var owner = opcode == "ldflda" ? Pop() : Unknown; Push(IlValueAddresses.Create("field-address", -1, owner)); gaps.Add("IlValueFieldOriginUnavailable"); return false; }
            if (opcode is "stfld" or "stsfld")
            { Expose(Pop()); if (opcode == "stfld") Expose(Pop()); gaps.Add("IlValueHeapStoreUnavailable"); return false; }
            if (opcode.StartsWith("ldind.", StringComparison.Ordinal) || opcode == "ldobj")
            { Expose(Pop()); Push(Unknown); gaps.Add("IlValueIndirectOriginUnavailable"); return false; }
            if (opcode.StartsWith("stind.", StringComparison.Ordinal) || opcode == "stobj")
            { var value = Pop(); var address = Pop(); Expose(address); Expose(value); RewriteAddress(address, value); gaps.Add("IlValueIndirectStoreUnavailable"); return false; }
            if (opcode == "initobj")
            { var address = Pop(); Expose(address); RewriteAddress(address, Unknown); return false; }
            if (opcode.StartsWith("stelem", StringComparison.Ordinal))
            { Expose(Pop()); _ = Pop(); Expose(Pop()); gaps.Add("IlValueHeapStoreUnavailable"); return false; }
            if (opcode.StartsWith("ldelem", StringComparison.Ordinal) && opcode != "ldelema")
            { _ = Pop(); _ = Pop(); Push(Unknown); return false; }
            Clear("IlValueInstructionUnavailable"); return true;
        }

        bool Merge(State target, State incoming)
        {
            var changed = false;
            var oldExposed = target.ExposedLocals.Count + target.ExposedArguments.Count;
            target.ExposedLocals.UnionWith(incoming.ExposedLocals); target.ExposedArguments.UnionWith(incoming.ExposedArguments);
            changed |= oldExposed != target.ExposedLocals.Count + target.ExposedArguments.Count;
            if (target.InvalidStack != (target.InvalidStack || incoming.InvalidStack)) { target.InvalidStack = true; changed = true; }
            if (target.Stack.Count != incoming.Stack.Count)
            {
                if (!target.InvalidStack || target.Stack.Count != 0) changed = true;
                target.InvalidStack = true; target.Stack.Clear(); gaps.Add("IlValueStackMergeUnavailable");
            }
            else for (var index = 0; index < target.Stack.Count; index++)
            {
                var joined = IlArgumentAlternatives.Join(target.Stack[index], incoming.Stack[index]);
                if (target.Stack[index] != joined) { target.Stack[index] = joined; changed = true; }
            }
            foreach (var key in target.Locals.Keys.Concat(incoming.Locals.Keys).Distinct().ToArray())
            {
                var value = target.Locals.GetValueOrDefault(key, Unknown);
                var joined = IlArgumentAlternatives.Join(value, incoming.Locals.GetValueOrDefault(key, Unknown));
                if (value != joined) { target.Locals[key] = joined; changed = true; }
            }
            foreach (var key in target.Arguments.Keys.Concat(incoming.Arguments.Keys).Distinct().ToArray())
            {
                var value = target.Arguments.GetValueOrDefault(key, Argument(key));
                var joined = IlArgumentAlternatives.Join(value, incoming.Arguments.GetValueOrDefault(key, Argument(key)));
                if (value != joined) { target.Arguments[key] = joined; changed = true; }
            }
            return changed;
        }
    }

    private static IlValueOrigin Argument(int slot) => new("argument-slot", slot.ToString(CultureInfo.InvariantCulture));
    private static bool Slot(string opcode, string operand, string operation, out int slot)
    {
        slot = -1;
        if (opcode == operation || opcode == operation + ".s")
            return operand.StartsWith("v:", StringComparison.Ordinal)
                && int.TryParse(operand.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out slot);
        return opcode.StartsWith(operation + ".", StringComparison.Ordinal)
            && int.TryParse(opcode.AsSpan(operation.Length + 1), NumberStyles.None, CultureInfo.InvariantCulture, out slot);
    }
}
