using System.Globalization;

namespace TraceMap.Core;

/// <summary>Local, straight-line symbolic operands over a dual-decoded canonical
/// IL stream. No source text, object field state, runtime dispatch, or SQL parsing.</summary>
internal static class IlCallValueExtractor
{
    internal const string Schema = "il-call-values.v1";
    internal const int MaxRetainedReturnSites = 256;
    internal const string Limitation = "Bounded method-local operand origins only. Normal branch/loop flow uses equality-only fixed-point joins, including protected blocks with known exception entries. Handler/filter roots have unknown pre-exception local state; leave discards locals and potentially rewritten arguments across unmodelled finally effects, re-establishing only an empty operand stack. Exception dispatch and finally continuations are not reconstructed. Strings are length plus SHA-256 of exact UTF-16 code units, never raw text. Argument slots are not values. Object origins are allocation/call-site identities, not runtime objects. Ordinary field/array/indirect operations preserve stack shape but their loaded values remain unknown. Stores and address exposure conservatively invalidate affected configuration; exposed slots remain exposure-capable on later writes. Known byref call shapes retain non-byref scalar operands only; addresses and byref arguments are exported as unknown, never dereferenced caller values. Unsupported instructions, byref returns, stack failures and work limits invalidate or withhold operand state. Consumers must invalidate configuration across unknown call effects. No heap contents, field value, branch feasibility, SQL execution or runtime dispatch is proven.";
    private static readonly IlValueOrigin Unknown = new("unknown", "");

    internal static IlValueFlowObservation Extract(IReadOnlyList<string> instructions,
        IReadOnlyList<IlCallObservation> calls, int maxStack, bool hasExceptionRegions,
        IReadOnlyCollection<long>? exceptionBoundaries = null,
        IReadOnlyList<IlValueExceptionEntry>? exceptionEntries = null)
    {
        // Exception edges remain in the explicitly reduced local lane. Normal
        // branch/loop flow uses a bounded fixed point with equality-only joins.
        if ((!hasExceptionRegions || exceptionEntries is not null) && (calls.Any(call => call.StackShape?.ByReferenceParameters.Contains('1') == true)
            || instructions.Any(instruction => instruction.Split(':', 4)[3].StartsWith("br:0x", StringComparison.Ordinal)
                || instruction.Split(':', 4)[3].StartsWith("sw:", StringComparison.Ordinal)
                || RequiresControlFlow(instruction.Split(':', 4)[2]))))
            return IlControlFlowValueExtractor.Extract(instructions, calls, maxStack, exceptionEntries);
        var events = new List<IlCallValueObservation>();
        var returns = new List<IlReturnValueObservation>();
        var afterReturn = false;
        var gaps = new SortedSet<string>(StringComparer.Ordinal);
        var stack = new List<IlValueOrigin>();
        var locals = new Dictionary<int, IlValueOrigin>();
        var arguments = new Dictionary<int, IlValueOrigin>();
        var byOffset = calls.Where(call => call.Opcode is "call" or "callvirt" or "newobj")
            .ToDictionary(call => call.Offset);
        var region = 0;
        var boundaries = new HashSet<long>(exceptionBoundaries ?? []);
        var modifiedArguments = new HashSet<int>();
        foreach (var instruction in instructions)
        {
            var encoded = instruction.Split(':', 4);
            if (encoded.Length != 4) throw new ArgumentException("Invalid canonical IL instruction.", nameof(instructions));
            // Every branch target is a possible merge, including a forward
            // jump over configuration and a backward edge from a later block.
            var operand = encoded[3];
            // Never re-label a potentially overwritten argument as the
            // original caller slot after local state has been discarded.
            if (TrySlot(encoded[2], operand, "starg", out var modified)
                || TrySlot(encoded[2], operand, "ldarga", out modified)) modifiedArguments.Add(modified);
            if (operand.StartsWith("br:0x", StringComparison.Ordinal))
                boundaries.Add(long.Parse(operand.AsSpan(5), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            else if (operand.StartsWith("sw:", StringComparison.Ordinal))
            {
                var opening = operand.IndexOf('[');
                foreach (var target in operand[(opening + 1)..^1].Split(',', StringSplitOptions.RemoveEmptyEntries))
                    boundaries.Add(long.Parse(target.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }
        }
        // Exception entry points are not reconstructed by this local lane.
        // Retain unknown call operands rather than pretending handler state is
        // the lexical continuation of the protected block.
        if (hasExceptionRegions) gaps.Add("IlValueExceptionFlowUnavailable");
        void Invalidate(string reason)
        {
            stack.Clear(); locals.Clear(); arguments.Clear(); region++;
            gaps.Add(reason);
        }
        void Push(IlValueOrigin value)
        {
            if (stack.Count >= Math.Min(maxStack, 4096)) { Invalidate("IlValueStackLimit"); return; }
            stack.Add(value);
        }
        IlValueOrigin Pop()
        {
            if (stack.Count == 0) { gaps.Add("IlValueStackUnavailable"); return Unknown; }
            var value = stack[^1]; stack.RemoveAt(stack.Count - 1); return value;
        }
        foreach (var instruction in instructions)
        {
            var parts = instruction.Split(':', 4);
            if (parts.Length != 4 || !long.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset))
                throw new ArgumentException("Invalid canonical IL instruction.", nameof(instructions));
            var opcode = parts[2]; var operand = parts[3];
            if (boundaries.Contains(offset)) Invalidate("IlValueControlFlowBoundary");
            if (hasExceptionRegions && exceptionBoundaries is null)
            {
                if (byOffset.TryGetValue(offset, out var blockedCall))
                    events.Add(new(offset, region, "exception-flow-unavailable", Unknown,
                        Enumerable.Repeat(Unknown, Math.Clamp(blockedCall.StackShape?.ParameterCount ?? 0, 0, 1024)).ToArray(), Unknown));
                continue;
            }
            if (opcode == "nop") continue;
            if (opcode == "ldstr") { Push(new("constant-string-hash", operand)); continue; }
            if (opcode == "ldnull") { Push(new("null", "")); continue; }
            if (opcode.StartsWith("ldc.i4", StringComparison.Ordinal))
            {
                var number = opcode == "ldc.i4.m1" ? "-1"
                    : opcode is "ldc.i4" or "ldc.i4.s" ? operand[2..] : opcode[7..];
                Push(new("constant-int32", number)); continue;
            }
            if (TrySlot(opcode, operand, "ldarg", out var argument))
            {
                Push(arguments.GetValueOrDefault(argument, modifiedArguments.Contains(argument)
                    ? Unknown : new("argument-slot", argument.ToString(CultureInfo.InvariantCulture)))); continue;
            }
            if (TrySlot(opcode, operand, "starg", out argument)) { arguments[argument] = Pop(); continue; }
            if (TrySlot(opcode, operand, "ldloc", out var local)) { Push(locals.GetValueOrDefault(local, Unknown)); continue; }
            if (TrySlot(opcode, operand, "stloc", out local)) { locals[local] = Pop(); continue; }
            if (opcode == "dup") { var value = Pop(); Push(value); Push(value); continue; }
            if (opcode == "pop") { _ = Pop(); continue; }
            if (byOffset.TryGetValue(offset, out var call))
            {
                if (call.StackShape is not { Supported: true, ParameterCount: >= 0 and <= 1024 } shape)
                {
                    events.Add(new(offset, region, "call-shape-unavailable", Unknown, [], Unknown));
                    Invalidate("IlValueCallShapeUnavailable"); continue;
                }
                var underflow = stack.Count < shape.ParameterCount + (shape.HasThis && opcode != "newobj" ? 1 : 0);
                var values = new IlValueOrigin[shape.ParameterCount];
                for (var index = values.Length - 1; index >= 0; index--) values[index] = Pop();
                var receiver = opcode == "newobj" || !shape.HasThis ? Unknown : Pop();
                var result = opcode == "newobj" ? new IlValueOrigin("allocation-site", offset.ToString(CultureInfo.InvariantCulture))
                    : shape.ReturnsValue ? new IlValueOrigin("call-result", offset.ToString(CultureInfo.InvariantCulture)) : Unknown;
                events.Add(new(offset, region, underflow ? "stack-unavailable" : "straight-line-candidate", receiver, values, result));
                // Non-byref operands preserve local/argument slot origins,
                // not object state. A downstream command binder must process
                // every call and invalidate configuration across unknown effects.
                // Unsupported/byref signatures took the invalidating path above.
                gaps.Add("IlValueCallEffectsUnmodeled");
                if (underflow) Invalidate("IlValueStackUnavailable");
                if (opcode == "newobj" || shape.ReturnsValue) Push(result);
                continue;
            }
            if (opcode == "ret")
            {
                var available = !afterReturn && !hasExceptionRegions && stack.Count == 1;
                returns.Add(new(offset, available ? "return-operand-candidate" : "return-operand-unavailable",
                    available ? stack[0] : Unknown));
                afterReturn = true;
                Invalidate("IlValueReturnBoundary"); continue;
            }
            Invalidate(opcode.StartsWith('b') || opcode is "switch" or "leave" or "leave.s"
                ? "IlValueControlFlowUnavailable" : "IlValueInstructionUnavailable");
        }
        return new(events, gaps.ToArray(), Returns: returns);
    }

    private static bool RequiresControlFlow(string opcode) => opcode is "stfld" or "stsfld" or "ldflda" or "ldsflda" or "ldsfld"
        or "newarr" or "box" or "ldtoken" or "initobj" or "stobj"
        || opcode.StartsWith("ldloca", StringComparison.Ordinal) || opcode.StartsWith("ldarga", StringComparison.Ordinal)
        || opcode.StartsWith("ldind", StringComparison.Ordinal) || opcode.StartsWith("stind", StringComparison.Ordinal)
        || opcode.StartsWith("stelem", StringComparison.Ordinal);

    private static bool TrySlot(string opcode, string operand, string operation, out int slot)
    {
        slot = -1;
        if (opcode == operation || opcode == operation + ".s")
            return operand.StartsWith("v:", StringComparison.Ordinal)
                && int.TryParse(operand.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out slot);
        return opcode.StartsWith(operation + ".", StringComparison.Ordinal)
            && int.TryParse(opcode.AsSpan(operation.Length + 1), NumberStyles.None, CultureInfo.InvariantCulture, out slot);
    }
}
