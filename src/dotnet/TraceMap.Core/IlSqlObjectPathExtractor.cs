using System.Globalization;

namespace TraceMap.Core;

/// <summary>Closed, bounded static object protocol. Never executes IL or provider code.</summary>
internal static class IlSqlObjectPathExtractor
{
    internal const string Schema = "il-sql-object-path.v1";
    internal const int MaxSteps = 2048, MaxFrames = 16, MaxObjects = 64, MaxSites = 256;
    internal const string Limitation = "Tier3 encoded static object/dataflow candidate only. Exact unique admitted compiled bodies, no deployment/source equivalence, SQL contract match, rows, values, types, nullability or runtime proof. Fresh bounded objects, constructor stores/getter loads, positional nonvirtual calls, one Fill for legacy DataSet paths; at most two Fills for explicit fresh named-table destinations with first source table/row named access. Fresh root property stores require an inspected private backing field. Existing destinations, repeated merges, mutation, escapes, virtual/generic/recursive/branch/exception flow are unknown. Merge is a schema-union-from-empty candidate, never original-result identity or runtime ownership.";
    private const string DataScope = "scope(assembly:name:11:System.Data|version:7:4.0.0.0|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)";
    private const string CoreScope = "scope(assembly:name:8:mscorlib|version:7:4.0.0.0|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)";
    private static string Primitive(string name) => $"type(namespace:6:System|names:{name.Length}:{name})";
    private static string Data(string ns, string name) => $"{DataScope}type(namespace:{ns.Length}:{ns}|names:{name.Length}:{name})";
    private static string Api(string ns, string type, string name, string result, params string[] args) =>
        $"memberref|type:{Data(ns, type)}|member:{name.Length}:{name}|arity:0|call:default|hasThis:true|explicitThis:false|({string.Join(",", args)})->{result}";
    private static readonly string Void = Primitive("Void"), Int = Primitive("Int32"), String = Primitive("String"), Object = Primitive("Object");
    private static readonly string Parameter = Data("System.Data.SqlClient", "SqlParameter"), Command = Data("System.Data.SqlClient", "SqlCommand"),
        Parameters = Data("System.Data.SqlClient", "SqlParameterCollection"), Set = Data("System.Data", "DataSet"),
        Tables = Data("System.Data", "DataTableCollection"), Table = Data("System.Data", "DataTable"),
        Rows = Data("System.Data", "DataRowCollection"), Row = Data("System.Data", "DataRow");
    private static readonly IReadOnlyDictionary<string, string> Apis = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Api("System.Data.SqlClient", "SqlCommand", ".ctor", Void, String)] = "command",
        [Api("System.Data.SqlClient", "SqlCommand", "set_CommandType", Void, Data("System.Data", "CommandType"))] = "command-type",
        [Api("System.Data.Common", "DbCommand", "set_CommandType", Void, Data("System.Data", "CommandType"))] = "command-type",
        [Api("System.Data.SqlClient", "SqlCommand", "get_Parameters", Parameters)] = "parameters",
        [Api("System.Data.SqlClient", "SqlParameter", ".ctor", Void, String, Data("System.Data", "SqlDbType"), Int)] = "parameter",
        [Api("System.Data.SqlClient", "SqlParameterCollection", "Add", Parameter, Parameter)] = "add",
        [Api("System.Data.SqlClient", "SqlDataAdapter", ".ctor", Void, Command)] = "adapter",
        [Api("System.Data", "DataSet", ".ctor", Void)] = "dataset",
        [Api("System.Data.Common", "DbDataAdapter", "Fill", Int, Set)] = "fill",
        [Api("System.Data.Common", "DataAdapter", "Fill", Int, Set)] = "fill",
        [Api("System.Data", "DataSet", "Merge", Void, Set)] = "merge",
        [Api("System.Data", "DataTableCollection", "Add", Table, String)] = "table-add",
        [Api("System.Data", "DataTable", "Merge", Void, Table)] = "table-merge",
        [Api("System.Data", "DataSet", "get_Tables", Tables)] = "tables",
        [Api("System.Data", "DataTableCollection", "get_Item", Table, Int)] = "table",
        [Api("System.Data", "DataTable", "get_Rows", Rows)] = "rows",
        [Api("System.Data", "DataRowCollection", "get_Item", Row, Int)] = "row",
        [Api("System.Data", "DataRow", "get_Item", Object, String)] = "consumer",
        [$"memberref|type:{CoreScope}type(namespace:6:System|names:6:Object)|member:5:.ctor|arity:0|call:default|hasThis:true|explicitThis:false|()->{Void}"] = "object-base"
    };

    internal sealed record Body(IlBodyObservation Observation, IlInputOutcome Input, string? MemberReferenceAlias = null);
    internal sealed record Site(string BodyIdentity, string BinarySha256, long Offset, int Frame, string Action);
    internal sealed record ParameterShape(string ObjectIdentity, IlValueOrigin Name, IlValueOrigin SqlDbType, IlValueOrigin Size);
    internal sealed record Path(string RootBodyIdentity, string ExecutionIdentity, IlValueOrigin CommandText,
        IlValueOrigin CommandType, IReadOnlyList<ParameterShape> Parameters, IlValueOrigin Column,
        string FilledDataSetIdentity, string ConsumerDataSetIdentity, string Relation, IReadOnlyList<Site> Sites,
        string? ConsumerTableIdentity = null, string? FilledTableIdentity = null);
    internal sealed record Result(IReadOnlyList<Path> Paths, IReadOnlyList<string> Gaps);
    private sealed record Value(string Kind, string Text)
    {
        internal static readonly Value Unknown = new("unknown", "");
        internal IlValueOrigin Origin => new(Kind, Text);
    }
    private sealed class Heap(string kind, string owner)
    {
        internal string Kind = kind, Owner = owner;
        internal Dictionary<string, Value> Fields = new(StringComparer.Ordinal);
        internal List<Value> Parameters = [];
        internal List<Value> TableSlots = [];
        internal Value Text = Value.Unknown, Type = Value.Unknown, Size = Value.Unknown, Parent = Value.Unknown;
        internal string? Execution, FilledIdentity, FilledTable;
        internal bool Attached, Merged, FieldOwned;
    }
    private sealed class Refusal(string gap) : Exception(gap);

    internal static string? KnownFrameworkApi(string identity) => Apis.GetValueOrDefault(identity);

    internal static bool IsRoot(IlBodyObservation body) => body.Calls.Any(call => Apis.GetValueOrDefault(call.TargetIdentity) == "consumer");

    internal static Result Extract(Body root, IReadOnlyList<Body> bodies, IlBodyEvidenceExtractor.IlWorkBudget budget)
    {
        if (!IsRoot(root.Observation)) return new([], []);
        if (bodies.Count > 4096 || !budget.TryConsume(bodies.Count)) return new([], ["IlSqlObjectIndexLimit"]);
        try { return new([new Machine(bodies, budget).Run(root)], []); }
        catch (Refusal refusal) { return new([], [refusal.Message]); }
    }

    private sealed class Machine
    {
        private readonly IReadOnlyList<Body> bodies;
        private readonly IlBodyEvidenceExtractor.IlWorkBudget budget;
        private readonly Dictionary<string, Heap> heap = new(StringComparer.Ordinal);
        private readonly List<Site> sites = [];
        private readonly HashSet<string> active = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string[]> binaries;
        private int frame, steps, executions;
        private Path? result;

        internal Machine(IReadOnlyList<Body> bodies, IlBodyEvidenceExtractor.IlWorkBudget budget)
        {
            this.bodies = bodies; this.budget = budget;
            binaries = bodies.GroupBy(body => ReferenceIdentity(body.Input.AssemblyIdentity), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(body => body.Input.RawFileSha256 ?? "")
                    .Distinct(StringComparer.Ordinal).Take(2).ToArray(), StringComparer.Ordinal);
        }

        // Read four length-framed identity fields, never split on attacker-controlled
        // punctuation embedded inside an assembly name/culture/version.
        private static string ReferenceIdentity(string? identity)
        {
            if (identity is null) throw new Refusal("IlSqlObjectProvenanceUnavailable");
            var position = 0;
            foreach (var label in new[] { "assembly:name:", "|version:", "|culture:", "|publicKeyToken:" })
            {
                if (!identity.AsSpan(position).StartsWith(label, StringComparison.Ordinal)) throw new Refusal("IlSqlObjectProvenanceUnavailable");
                position += label.Length;
                var colon = identity.IndexOf(':', position);
                if (colon < 0 || !int.TryParse(identity.AsSpan(position, colon - position), NumberStyles.None,
                        CultureInfo.InvariantCulture, out var length) || length < 0 || length > identity.Length - colon - 1)
                    throw new Refusal("IlSqlObjectProvenanceUnavailable");
                position = colon + 1 + length;
            }
            if (!identity.AsSpan(position).StartsWith("|module:", StringComparison.Ordinal)) throw new Refusal("IlSqlObjectProvenanceUnavailable");
            return identity[..position];
        }

        internal Path Run(Body root)
        {
            var metadata = root.Observation.ObjectBody ?? throw new Refusal("IlSqlObjectBodyUnavailable");
            var args = Enumerable.Repeat(Value.Unknown, metadata.ParameterCount + (metadata.HasThis ? 1 : 0)).ToArray();
            if (metadata.HasThis) args[0] = New("root", metadata.OwnerType, "root-this");
            var returned = Invoke(root, args, 0);
            if (returned.Kind != "consumer" || result is null
                || (result.ConsumerTableIdentity is null && executions != 1))
                throw new Refusal("IlSqlObjectConsumerReturnUnresolved");
            return result with { RootBodyIdentity = root.Observation.BodyIdentity, Sites = sites.ToArray() };
        }

        private Value New(string kind, string owner, string id)
        {
            if (heap.Count >= MaxObjects || heap.ContainsKey(id)) throw new Refusal("IlSqlObjectAllocationLimit");
            heap.Add(id, new(kind, owner)); return new("object", id);
        }
        private Heap Get(Value value, params string[] kinds)
        {
            if (value.Kind != "object" || !heap.TryGetValue(value.Text, out var found) || !kinds.Contains(found.Kind, StringComparer.Ordinal))
                throw new Refusal("IlSqlObjectOriginUnresolved");
            return found;
        }
        private void SiteAt(Body body, long offset, int invocation, string action)
        {
            if (sites.Count >= MaxSites) throw new Refusal("IlSqlObjectSiteLimit");
            sites.Add(new(body.Observation.BodyIdentity, body.Input.RawFileSha256!, offset, invocation, action));
        }
        private Value Invoke(Body body, Value[] args, int depth)
        {
            var observation = body.Observation;
            var meta = observation.ObjectBody ?? throw new Refusal("IlSqlObjectBodyUnavailable");
            if (depth >= MaxFrames || !active.Add(observation.MethodIdentity)) throw new Refusal("IlSqlObjectRecursionOrDepth");
            try
            {
                if (body.Input.RawFileSha256 is not { Length: 64 } sha || sha.Any(c => !char.IsAsciiHexDigit(c))
                    || !Guid.TryParse(body.Input.ModuleMvid, out _) || body.Input.Outcome != "admitted" || body.Input.GapKinds.Count != 0)
                    throw new Refusal("IlSqlObjectProvenanceUnavailable");
                if (binaries[ReferenceIdentity(body.Input.AssemblyIdentity)].Length != 1)
                    throw new Refusal("IlSqlObjectBinaryIdentityAmbiguous");
                if (meta.IsVirtual || meta.IsGeneric || observation.ExceptionRegionCount != 0
                    || meta.Instructions.Count > 256 || args.Length != meta.ParameterCount + (meta.HasThis ? 1 : 0))
                    throw new Refusal("IlSqlObjectBodyUnsupported");
                var invocation = ++frame;
                var stack = new List<Value>(); var locals = new Dictionary<int, Value>();
                var calls = observation.Calls.ToDictionary(call => call.Offset);
                var instructions = meta.Instructions.Select(encoded => encoded.Split(':', 4)).ToArray();
                Value Pop()
                {
                    if (stack.Count == 0) throw new Refusal("IlSqlObjectStackUnavailable");
                    var value = stack[^1]; stack.RemoveAt(stack.Count - 1); return value;
                }
                for (var i = 0; i < instructions.Length; i++)
                {
                    if (++steps > MaxSteps || !budget.TryConsume(1) || stack.Count > 64 || locals.Count > 64)
                        throw new Refusal("IlSqlObjectWorkLimit");
                    var parts = instructions[i];
                    if (parts.Length != 4) throw new Refusal("IlSqlObjectInstructionUnavailable");
                    if (!long.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset))
                        throw new Refusal("IlSqlObjectInstructionUnavailable");
                    var opcode = parts[2]; var operand = parts[3];
                    if (opcode == "nop") continue;
                    if (Slot(opcode, operand, "ldarg", out var slot)) { stack.Add(slot < args.Length ? args[slot] : Value.Unknown); continue; }
                    if (Slot(opcode, operand, "ldloc", out slot)) { stack.Add(locals.GetValueOrDefault(slot, Value.Unknown)); continue; }
                    if (Slot(opcode, operand, "stloc", out slot)) { locals[slot] = Pop(); continue; }
                    if (opcode == "ldstr") { stack.Add(new("constant-string-hash", operand)); continue; }
                    if (opcode.StartsWith("ldc.i4", StringComparison.Ordinal))
                    { stack.Add(new("constant-int32", opcode == "ldc.i4.m1" ? "-1" : opcode is "ldc.i4" or "ldc.i4.s" ? operand[2..] : opcode[7..])); continue; }
                    if (opcode == "dup") { var value = Pop(); stack.Add(value); stack.Add(value); continue; }
                    if (opcode == "pop") { _ = Pop(); continue; }
                    if (opcode is "br" or "br.s")
                    {
                        if (!operand.StartsWith("br:0x", StringComparison.Ordinal)) throw new Refusal("IlSqlObjectControlFlowUnsupported");
                        var target = long.Parse(operand[5..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        var next = Array.FindIndex(instructions, item => long.Parse(item[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture) == target);
                        if (next <= i || instructions.Skip(i + 1).Take(next - i - 1).Any(item => item[2] != "nop"))
                            throw new Refusal("IlSqlObjectControlFlowUnsupported");
                        i = next - 1; continue;
                    }
                    if (opcode is "ldfld" or "stfld")
                    {
                        var encoded = operand.Split(':', 4);
                        if (encoded.Length != 4 || encoded[1] != "field") throw new Refusal("IlSqlObjectFieldUnavailable");
                        var identity = encoded[3]; var bar = identity.IndexOf('|');
                        if (bar < 0) throw new Refusal("IlSqlObjectFieldUnavailable");
                        identity = identity[(bar + 1)..];
                        var stored = opcode == "stfld" ? Pop() : Value.Unknown; var receiver = Pop();
                        var instance = Get(receiver, "custom", "root");
                        if (!identity.StartsWith("type:" + instance.Owner + "|member:", StringComparison.Ordinal))
                            throw new Refusal("IlSqlObjectFieldOwnerMismatch");
                        if (opcode == "stfld")
                        {
                            if (instance.Fields.ContainsKey(identity)) throw new Refusal("IlSqlObjectFieldMutation");
                            if (instance.Kind == "custom" && (meta.Name != ".ctor" || args.Length == 0 || receiver != args[0]))
                                throw new Refusal("IlSqlObjectFieldMutation");
                            if (instance.Kind == "custom" && stored.Kind is not ("constant-int32" or "constant-string-hash"))
                                throw new Refusal("IlSqlObjectFieldOwnershipUnavailable");
                            if (instance.Kind == "root")
                            {
                                var owned = Get(stored, "dataset");
                                if ((depth != 0 && !meta.Name.StartsWith("set_", StringComparison.Ordinal))
                                    || !meta.PrivateInstanceFields.Contains(identity, StringComparer.Ordinal)
                                    || owned.Execution is not null || owned.FieldOwned) throw new Refusal("IlSqlObjectFieldOwnershipUnavailable");
                                owned.FieldOwned = true;
                            }
                            instance.Fields.Add(identity, stored);
                        }
                        else stack.Add(instance.Fields.GetValueOrDefault(identity, Value.Unknown));
                        SiteAt(body, offset, invocation, opcode); continue;
                    }
                    if (opcode is "call" or "callvirt" or "newobj")
                    {
                        if (!calls.TryGetValue(offset, out var call) || call.StackShape is not { Supported: true } shape
                            || shape.ParameterCount > 16 || shape.ByReferenceParameters.Contains('1')) throw new Refusal("IlSqlObjectCallShapeUnavailable");
                        var values = new Value[shape.ParameterCount];
                        for (var arg = values.Length - 1; arg >= 0; arg--) values[arg] = Pop();
                        var receiver = opcode != "newobj" && shape.HasThis ? Pop() : Value.Unknown;
                        SiteAt(body, offset, invocation, "call");
                        Value returned;
                        if (Apis.TryGetValue(call.TargetIdentity, out var api))
                            returned = Framework(body, meta, offset, invocation, opcode, api, receiver, values);
                        else
                        {
                            if (!budget.TryConsume(bodies.Count)) throw new Refusal("IlSqlObjectWorkLimit");
                            var targets = bodies.Where(candidate => candidate.Observation.MethodIdentity == call.TargetIdentity
                                || candidate.Observation.ObjectBody?.MemberReference == call.TargetIdentity).Take(2).ToArray();
                            if (targets.Length != 1) throw new Refusal("IlSqlObjectCalleeMissingOrAmbiguous");
                            var target = targets[0]; var targetMeta = target.Observation.ObjectBody ?? throw new Refusal("IlSqlObjectBodyUnavailable");
                            if (targetMeta.ParameterCount != shape.ParameterCount || targetMeta.HasThis != shape.HasThis
                                || targetMeta.ReturnsValue != shape.ReturnsValue || opcode == "newobj" && targetMeta.Name != ".ctor")
                                throw new Refusal("IlSqlObjectCallShapeUnavailable");
                            if (opcode == "newobj") receiver = New("custom", targetMeta.OwnerType, $"{invocation}:{offset}");
                            returned = Invoke(target, targetMeta.HasThis ? new[] { receiver }.Concat(values).ToArray() : values, depth + 1);
                            if (opcode == "newobj") returned = receiver;
                        }
                        if (opcode == "newobj" || shape.ReturnsValue) stack.Add(returned);
                        continue;
                    }
                    if (opcode == "ret")
                    {
                        if (i != instructions.Length - 1 || stack.Count != (meta.ReturnsValue ? 1 : 0)) throw new Refusal("IlSqlObjectReturnUnavailable");
                        SiteAt(body, offset, invocation, "return"); return meta.ReturnsValue ? Pop() : Value.Unknown;
                    }
                    throw new Refusal("IlSqlObjectOpcodeUnsupported");
                }
                throw new Refusal("IlSqlObjectReturnUnavailable");
            }
            finally { active.Remove(observation.MethodIdentity); }
        }

        private Value Framework(Body body, IlObjectBody meta, long offset, int frameId, string opcode,
            string api, Value receiver, Value[] args)
        {
            var id = $"{frameId}:{offset}";
            if (api is "command" or "parameter" or "adapter" or "dataset")
            {
                if (opcode != "newobj") throw new Refusal("IlSqlObjectConstructorUnsupported");
                var value = New(api, "", id); var instance = heap[id];
                if (api == "command") { Require(args[0], "constant-string-hash"); instance.Text = args[0]; }
                if (api == "parameter")
                { Require(args[0], "constant-string-hash"); Require(args[1], "constant-int32"); Require(args[2], "constant-int32"); instance.Text = args[0]; instance.Type = args[1]; instance.Size = args[2]; }
                if (api == "adapter") { _ = Get(args[0], "command"); instance.Parent = args[0]; }
                return value;
            }
            if (opcode is not ("call" or "callvirt")) throw new Refusal("IlSqlObjectCallShapeUnavailable");
            switch (api)
            {
                case "object-base":
                    if (meta.Name != ".ctor") throw new Refusal("IlSqlObjectConstructorUnsupported");
                    _ = Get(receiver, "custom"); return Value.Unknown;
                case "command-type":
                    var command = Get(receiver, "command"); Require(args[0], "constant-int32");
                    if (command.Type != Value.Unknown || command.Attached) throw new Refusal("IlSqlObjectCommandMutation");
                    command.Type = args[0]; return Value.Unknown;
                case "parameters":
                    _ = Get(receiver, "command"); var collection = New("parameters", "", id); heap[id].Parent = receiver; return collection;
                case "add":
                    command = Get(Get(receiver, "parameters").Parent, "command"); var parameter = Get(args[0], "parameter");
                    if (command.Attached || parameter.Attached || command.Parameters.Count >= 16) throw new Refusal("IlSqlObjectParameterMutationOrLimit");
                    parameter.Attached = true; command.Parameters.Add(args[0]); return args[0];
                case "fill":
                    command = Get(Get(receiver, "adapter").Parent, "command"); var dataSet = Get(args[0], "dataset");
                    if (++executions > 2 || command.Attached || command.Parameters.Count == 0 || command.Type != new Value("constant-int32", "4")
                        || dataSet.Execution is not null || dataSet.TableSlots.Count != 0) throw new Refusal("IlSqlObjectExecutionUnresolved");
                    command.Attached = true; dataSet.Execution = id; dataSet.FilledIdentity = args[0].Text; dataSet.Parent = Get(receiver, "adapter").Parent;
                    SiteAt(body, offset, frameId, "fill"); return Value.Unknown;
                case "merge":
                    var destination = Get(receiver, "dataset"); var incoming = Get(args[0], "dataset");
                    if (receiver == args[0] || destination.Execution is not null || destination.Merged || incoming.Execution is null || incoming.Merged
                        || destination.TableSlots.Count != 0 || incoming.TableSlots.Count != 0)
                        throw new Refusal("IlSqlObjectMergeDestinationUnresolved");
                    destination.Execution = incoming.Execution; destination.FilledIdentity = incoming.FilledIdentity; destination.Parent = incoming.Parent; destination.Merged = true;
                    SiteAt(body, offset, frameId, "fresh-empty-schema-union"); return Value.Unknown;
                case "tables": return View(receiver, "dataset", "tables", id);
                case "table-add":
                    Require(args[0], "constant-string-hash");
                    var ownerValue = Get(receiver, "tables").Parent;
                    var owner = Get(ownerValue, "dataset");
                    if (owner.Execution is not null || owner.Merged || owner.TableSlots.Count >= 2
                        || owner.TableSlots.Any(v => Get(v, "owned-table").Text == args[0]))
                        throw new Refusal("IlSqlObjectTableAllocationUnsupported");
                    var added = New("owned-table", "", id);
                    heap[id].Parent = ownerValue; heap[id].Text = args[0];
                    owner.TableSlots.Add(added);
                    SiteAt(body, offset, frameId, "fresh-named-table"); return added;
                case "table":
                    ownerValue = Get(receiver, "tables").Parent; owner = Get(ownerValue, "dataset");
                    if (owner.TableSlots.Count == 0) { RequireZero(args[0]); return View(receiver, "tables", "table", id); }
                    Require(args[0], "constant-int32");
                    if (!int.TryParse(args[0].Text, out var ordinal) || ordinal < 0 || ordinal >= owner.TableSlots.Count)
                        throw new Refusal("IlSqlObjectSelectorUnsupported");
                    return owner.TableSlots[ordinal];
                case "table-merge":
                    var targetTable = Get(receiver, "owned-table");
                    var sourceTable = Get(args[0], "table");
                    incoming = Get(sourceTable.Parent, "dataset");
                    if (targetTable.Execution is not null || incoming.Execution is null || incoming.Merged)
                        throw new Refusal("IlSqlObjectMergeDestinationUnresolved");
                    targetTable.Execution = incoming.Execution; targetTable.FilledIdentity = incoming.FilledIdentity;
                    targetTable.FilledTable = sourceTable.Parent.Text + ":table:0"; targetTable.Merged = true;
                    // Preserve the destination owner separately from the source command.
                    targetTable.Fields.Add("source-command", incoming.Parent);
                    SiteAt(body, offset, frameId, "fresh-empty-table-schema-union"); return Value.Unknown;
                case "rows": return View(receiver, "table", "rows", id);
                case "row": RequireZero(args[0]); return View(receiver, "rows", "row", id);
                case "consumer":
                    Require(args[0], "constant-string-hash"); var row = Get(receiver, "row"); var setValue = row.Parent;
                    var source = Get(setValue, "dataset", "owned-table");
                    var tableValue = source.Kind == "owned-table" ? setValue : Value.Unknown;
                    dataSet = source;
                    if (source.Kind == "owned-table") setValue = source.Parent;
                    if (dataSet.Execution is null || result is not null) throw new Refusal("IlSqlObjectConsumerUnresolved");
                    command = Get(source.Kind == "owned-table" ? source.Fields["source-command"] : dataSet.Parent, "command");
                    result = new("", dataSet.Execution, command.Text.Origin, command.Type.Origin,
                        command.Parameters.Select(value => { var p = Get(value, "parameter"); return new ParameterShape(value.Text, p.Text.Origin, p.Type.Origin, p.Size.Origin); }).ToArray(),
                        args[0].Origin, dataSet.FilledIdentity!, setValue.Text,
                        source.Kind == "owned-table" ? "fresh-empty-table-schema-union-candidate" :
                        dataSet.Merged ? "fresh-empty-schema-union-candidate" : "same-returned-dataset-candidate", [],
                        tableValue == Value.Unknown ? null : tableValue.Text, source.FilledTable);
                    SiteAt(body, offset, frameId, "column-consumer"); return new("consumer", id);
            }
            throw new Refusal("IlSqlObjectFrameworkCallUnsupported");
        }
        private Value View(Value receiver, string sourceKind, string kind, string id)
        {
            var parent = sourceKind == "table" ? Get(receiver, "table", "owned-table") : Get(receiver, sourceKind);
            var value = New(kind, "", id);
            heap[id].Parent = sourceKind == "dataset" || parent.Kind == "owned-table" ? receiver : parent.Parent;
            return value;
        }
        private static void Require(Value value, string kind)
        { if (value.Kind != kind) throw new Refusal("IlSqlObjectScalarUnresolved"); }
        private static void RequireZero(Value value)
        { if (value != new Value("constant-int32", "0")) throw new Refusal("IlSqlObjectSelectorUnsupported"); }
        private static bool Slot(string opcode, string operand, string prefix, out int slot)
        {
            slot = -1;
            if (opcode == prefix || opcode == prefix + ".s") return operand.StartsWith("v:", StringComparison.Ordinal) && int.TryParse(operand[2..], out slot) && slot >= 0;
            return opcode.StartsWith(prefix + ".", StringComparison.Ordinal) && int.TryParse(opcode[(prefix.Length + 1)..], out slot) && slot >= 0;
        }
    }
}
