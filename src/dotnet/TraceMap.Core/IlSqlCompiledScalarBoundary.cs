using System.Globalization;
using Body = TraceMap.Core.IlSqlObjectPathExtractor.Body;

namespace TraceMap.Core;

internal static partial class IlSqlLifecycleExtractor
{
    internal const string CompiledScalarPolicy = "explicit-inspected-compiled-scalar/1";
    internal sealed record CompiledScalarSummary(string Policy, OpaqueCall Call, string Body, string Binary,
        string MetadataToken, int Instructions, int Handlers, int Calls, int Locals, int MaxStack,
        IReadOnlyList<string> HandlerKinds, bool HasBranches, bool DirectFieldWrites,
        string ResultOrigin = "unbound-declared-string;not-passthrough",
        string Boundary = "normal-return-assumed;internal-control-and-handlers-unmodeled;transitive-effects-unmodeled;tracked-heap-noninterference-assumed;not-purity-proof");

    private sealed partial class Machine
    {
        private int compiledSummaries;

        private sealed record ScalarHandler(string Kind, long Start, long End, long Handler, long HandlerEnd, long? Filter);
        private static bool TryScalarHandler(string encoded, out ScalarHandler? handler)
        {
            handler = null;
            var parts = encoded.Split(':', 10);
            if (parts.Length != 10 || parts[0] != "kind" || parts[2] != "try" || parts[4] != "handler"
                || parts[6] != "filter" || parts[8] != "catch" || parts[1] is not ("catch" or "finally" or "filter" or "fault")) return false;
            bool Range(string value, out long start, out long end)
            {
                start = 0; end = 0; var bounds = value.Split('+');
                if (bounds.Length != 2 || !long.TryParse(bounds[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out start)
                    || !long.TryParse(bounds[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var length)
                    || start < 0 || length <= 0 || start > long.MaxValue - length) return false;
                end = start + length; return true;
            }
            if (!Range(parts[3], out var start, out var end) || !Range(parts[5], out var first, out var last)) return false;
            long? filter = null;
            if (parts[1] == "filter")
            {
                if (!long.TryParse(parts[7], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset)
                    || offset < 0 || offset >= first) return false;
                filter = offset;
            }
            else if (parts[7] != "-1") return false;
            handler = new(parts[1], start, end, first, last, filter); return true;
        }

        private bool TryCompiledScalar(Body target, IlCallObservation call, Value[] arguments, Site site,
            out CompiledScalarSummary? summary, out string? reason)
        {
            summary = null; reason = "shape-or-provenance";
            var body = target.Observation;
            var meta = body.ObjectBody;
            var text = Scalar("String");
            if (meta is null || meta.HasThis || meta.IsVirtual || meta.IsGeneric || !meta.ReturnsValue
                || meta.Name is ".ctor" or ".cctor" || meta.ParameterCount != 1
                || !meta.MemberReference.EndsWith("|(" + text + ")->" + text, StringComparison.Ordinal)
                || meta.MemberReference != call.TargetIdentity && body.MethodIdentity != call.TargetIdentity
                || call.StackShape is not { Supported: true, HasThis: false, ParameterCount: 1, ReturnsValue: true }
                || arguments.Length != 1 || arguments[0].Kind is not ("initializer-temporary" or "initializer-literal")
                || arguments[0].DeclaredType != text
                || body.ObjectBodyAgreement is not { RawBody: true, CallShapes: true, ObjectBody: true }
                || target.Input.Outcome != "admitted" || target.Input.GapKinds.Count != 0
                || target.Input.RawFileSha256 is not { Length: 64 } sha || sha.Any(c => !char.IsAsciiHexDigit(c))
                || !Guid.TryParse(target.Input.ModuleMvid, out _)) return false;
            reason = "body-limit";
            if (body.InstructionCount is < 1 or > 128 || meta.Instructions.Count != body.InstructionCount
                || body.ExceptionRegionCount > 4 || meta.ExceptionRegions is null
                || meta.ExceptionRegions.Count != body.ExceptionRegionCount || body.Calls.Count > 32
                || body.LocalCount > 32 || !int.TryParse(body.MaxStack, NumberStyles.None, CultureInfo.InvariantCulture, out var maxStack)
                || maxStack is < 1 or > 16) return false;
            if (!budget.TryConsume(body.InstructionCount + body.Calls.Count + body.ExceptionRegionCount + body.LocalCount))
                throw new Refusal("lifecycle-work-limit");
            var code = meta.Instructions.Select(line => line.Split(':', 4)).ToArray();
            reason = "incomplete-instructions";
            if (code.Any(parts => parts.Length != 4) || code[^1][2] != "ret") return false;
            var offsets = code.Select(parts => Offset(parts[1])).ToArray();
            if (offsets.Distinct().Count() != offsets.Length || !offsets.SequenceEqual(offsets.Order())) return false;
            var offsetIndex = offsets.Select((offset, index) => (offset, index)).ToDictionary(pair => pair.offset, pair => pair.index);
            reason = "direct-write-or-unsafe-effect";
            if (code.Any(parts => parts[2] is "stfld" or "stsfld" or "throw" or "rethrow" or "calli" or "jmp"
                or "localloc" or "cpblk" or "initblk" or "stobj" or "cpobj" or "ldflda" or "ldsflda"
                or "ldftn" or "ldvirtftn" || parts[2].StartsWith("stind", StringComparison.Ordinal))) return false;
            reason = "incomplete-call-metadata";
            if (body.Calls.Any(inner => inner.Opcode is "call" or "callvirt" or "newobj"
                && inner.StackShape is not { Supported: true })) return false;
            var regions = new List<ScalarHandler>();
            foreach (var encoded in meta.ExceptionRegions)
            {
                if (!TryScalarHandler(encoded, out var handler)) { reason = "handler-shape"; return false; }
                regions.Add(handler!);
            }
            var end = offsets[^1] + 1; // Required last ret is a one-byte opcode.
            reason = "handler-range";
            if (regions.Any(region => !offsetIndex.ContainsKey(region.Start) || !offsetIndex.ContainsKey(region.Handler)
                || region.End > end || region.HandlerEnd > end
                || region.End != end && !offsetIndex.ContainsKey(region.End)
                || region.HandlerEnd != end && !offsetIndex.ContainsKey(region.HandlerEnd)
                || region.Filter is { } filter && !offsetIndex.ContainsKey(filter)
                || region.Start < region.HandlerEnd && region.Handler < region.End)) return false;
            var successors = new List<int>[code.Length];
            var hasBranches = false;
            reason = "branch-target";
            for (var index = 0; index < code.Length; index++)
            {
                successors[index] = [];
                var op = code[index][2]; var operand = code[index][3];
                if (operand.StartsWith("br:0x", StringComparison.Ordinal))
                {
                    hasBranches = true;
                    if (!offsetIndex.TryGetValue(Offset(operand[5..]), out var destination)) return false;
                    successors[index].Add(destination);
                    if (op is "br" or "br.s" or "leave" or "leave.s") continue;
                }
                else if (op == "switch")
                {
                    hasBranches = true;
                    var bracket = operand.IndexOf('[');
                    if (!operand.StartsWith("sw:", StringComparison.Ordinal) || bracket < 3 || !operand.EndsWith(']')
                        || !int.TryParse(operand.AsSpan(3, bracket - 3), out var count) || count is < 0 or > 128) return false;
                    var targets = operand[(bracket + 1)..^1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (targets.Length != count) return false;
                    foreach (var token in targets)
                    {
                        if (!token.StartsWith("0x", StringComparison.Ordinal)
                            || !offsetIndex.TryGetValue(Offset(token[2..]), out var destination)) return false;
                        successors[index].Add(destination);
                    }
                }
                if (op is not ("ret" or "endfinally" or "endfilter") && index + 1 < code.Length) successors[index].Add(index + 1);
            }
            // Over-approximate structural exits only. This does not execute branches,
            // handlers or internal calls and does not prove actual normal completion.
            var pending = new Queue<int>(); var seen = new HashSet<int>(); pending.Enqueue(0);
            var exit = false;
            while (pending.TryDequeue(out var index))
            {
                if (!seen.Add(index)) continue;
                if (!budget.TryConsume(1)) throw new Refusal("lifecycle-work-limit");
                if (code[index][2] == "ret") exit = true;
                if (!budget.TryConsume(successors[index].Count + regions.Count)) throw new Refusal("lifecycle-work-limit");
                foreach (var next in successors[index]) pending.Enqueue(next);
                foreach (var region in regions.Where(region => region.Kind is "catch" or "filter" && offsets[index] >= region.Start && offsets[index] < region.End))
                    pending.Enqueue(offsetIndex[region.Handler]);
            }
            reason = "no-structural-return";
            if (!exit) return false;
            summary = new(CompiledScalarPolicy, new(site, Hash(call.TargetIdentity)), body.BodyIdentity,
                target.Input.RawFileSha256!, body.MetadataToken, body.InstructionCount, body.ExceptionRegionCount,
                body.Calls.Count, body.LocalCount, maxStack, regions.Select(region => region.Kind).ToArray(), hasBranches, false);
            reason = null; return true;
        }
    }
}
