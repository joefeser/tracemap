using Body = TraceMap.Core.IlSqlObjectPathExtractor.Body;

namespace TraceMap.Core;

/// <summary>Opt-in external event boundary. It never invokes callbacks or evaluates XML.</summary>
internal static class IlSqlInfoMessageApis
{
    internal const string Core = "scope(assembly:name:8:mscorlib|version:7:4.0.0.0|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)";
    internal const string Data = "scope(assembly:name:11:System.Data|version:7:4.0.0.0|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)";
    internal static string T(string ns, string name) => $"type(namespace:{ns.Length}:{ns}|names:{name.Length}:{name})";
    internal static string P(string name) => T("System", name);
    internal static string D(string name) => Data + T("System.Data", name);
    internal static string S(string name) => Data + T("System.Data.SqlClient", name);
    internal static string M(string owner, string name, string result, bool instance, params string[] args)
        => $"memberref|type:{owner}|member:{name.Length}:{name}|arity:0|call:default|hasThis:{instance.ToString().ToLowerInvariant()}|explicitThis:false|({string.Join(',', args)})->{result}";
    internal static readonly string DelegateCtor = M(S("SqlInfoMessageEventHandler"), ".ctor", P("Void"), true, P("Object"), P("IntPtr"));
    internal static readonly string Add = M(S("SqlConnection"), "add_InfoMessage", P("Void"), true, S("SqlInfoMessageEventHandler"));
    internal static readonly string Remove = M(S("SqlConnection"), "remove_InfoMessage", P("Void"), true, S("SqlInfoMessageEventHandler"));
    internal static readonly string Connection = M(S("SqlConnection"), ".ctor", P("Void"), true, P("String"));
    internal static readonly string Open = M(S("SqlConnection"), "Open", P("Void"), true);
    internal static readonly string Close = M(S("SqlConnection"), "Close", P("Void"), true);
    internal static readonly string DbOpen = M(Data + T("System.Data.Common", "DbConnection"), "Open", P("Void"), true);
    internal static readonly string DbClose = M(Data + T("System.Data.Common", "DbConnection"), "Close", P("Void"), true);
    internal static readonly string Command = M(S("SqlCommand"), ".ctor", P("Void"), true, P("String"), S("SqlConnection"));
    internal static bool Callback(IlObjectBody meta) => meta.HasThis && !meta.IsVirtual && !meta.IsGeneric
        && meta.ParameterCount == 2 && !meta.ReturnsValue
        && meta.MemberReference.EndsWith("|(" + P("Object") + "," + S("SqlInfoMessageEventArgs") + ")->" + P("Void"), StringComparison.Ordinal);
    internal static bool Shape(IlCallObservation call)
    {
        var signature = call.TargetIdentity;
        var start = signature.LastIndexOf("|(", StringComparison.Ordinal);
        var end = signature.LastIndexOf(")->", StringComparison.Ordinal);
        if (start < 0 || end < start + 2 || call.ReferenceKind != "memberref" || call.StackShape is not { Supported: true } shape) return false;
        var count = end == start + 2 ? 0 : signature[(start + 2)..end].Split(',').Length;
        return shape.ParameterCount == count && !shape.ByReferenceParameters.Contains('1')
            && shape.HasThis == signature.Contains("|hasThis:true|", StringComparison.Ordinal)
            && shape.ReturnsValue == !signature.EndsWith("->" + P("Void"), StringComparison.Ordinal);
    }
    internal static string? Rule(string identity) => Rules.GetValueOrDefault(identity);
    private static readonly Dictionary<string, string> Rules = Build();
    private static Dictionary<string, string> Build()
    {
        var rules = new Dictionary<string, string>(StringComparer.Ordinal);
        void A(string rule, string owner, string name, string result, params string[] args) => rules.Add(M(owner, name, result, true, args), rule);
        var text = P("String"); var integer = P("Int32"); var nothing = P("Void"); var obj = P("Object");
        A("external", S("SqlInfoMessageEventArgs"), "get_Message", text);
        A("dataset-name", D("DataSet"), "set_DataSetName", nothing, text);
        A("table-name", D("DataTable"), "set_TableName", nothing, text);
        A("tables", D("DataSet"), "get_Tables", D("DataTableCollection"));
        A("table", D("DataTableCollection"), "get_Item", D("DataTable"), integer);
        foreach (var core in new[] { Core, Core.Replace("4.0.0.0", "2.0.0.0", StringComparison.Ordinal) })
        {
            var list = core + T("System.Collections", "ArrayList");
            A("list-add", list, "Add", integer, obj);
            A("list-count", list, "get_Count", integer);
            A("list-item", list, "get_Item", obj, integer);
            A("external", core + P("Object"), "ToString", text);
            A("external", core + P("String"), "Substring", text, integer);
            A("external", core + P("String"), "Substring", text, integer, integer);
            A("external", core + P("String"), "get_Length", integer);
            A("external", core + P("String"), "StartsWith", P("Boolean"), text);
            rules.Add(M(core + P("String"), "op_Equality", P("Boolean"), false, text, text), "external");
            A("external", core + P("IDisposable"), "Dispose", nothing);
            A("external", core + T("System.Collections", "IEnumerator"), "MoveNext", P("Boolean"));
            A("external", core + T("System.Collections", "IEnumerator"), "get_Current", obj);
        }
        var xml = "scope(assembly:name:10:System.Xml|version:7:4.0.0.0|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)";
        string X(string name) => xml + T("System.Xml", name);
        A("external", X("XmlDocument"), ".ctor", nothing);
        A("external", X("XmlDocument"), "LoadXml", nothing, text);
        A("external", X("XmlNode"), "SelectSingleNode", X("XmlNode"), text);
        A("external", X("XmlNode"), "SelectNodes", X("XmlNodeList"), text);
        A("external", X("XmlNode"), "get_Attributes", X("XmlAttributeCollection"));
        A("external", X("XmlNode"), "get_InnerText", text);
        A("external", X("XmlNode"), "get_Value", text);
        A("external", X("XmlAttribute"), "get_Value", text);
        A("external", X("XmlDocument"), "get_DocumentElement", X("XmlElement"));
        A("external", X("XmlNode"), "get_ChildNodes", X("XmlNodeList"));
        A("external", X("XmlAttributeCollection"), "get_ItemOf", X("XmlAttribute"), text);
        A("external", X("XmlNodeList"), "GetEnumerator", Core + T("System.Collections", "IEnumerator"));
        var vb = "scope(assembly:name:21:Microsoft.VisualBasic|version:8:10.0.0.0|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)";
        rules.Add(M(vb + T("Microsoft.VisualBasic.CompilerServices", "Operators"), "CompareString", integer, false, text, text, P("Boolean")), "external");
        foreach (var name in new[] { "SetProjectError", "ClearProjectError" })
            rules.Add(M(vb + T("Microsoft.VisualBasic.CompilerServices", "ProjectData"), name, nothing, false,
                name == "SetProjectError" ? [Core + P("Exception")] : []), "external");
        return rules;
    }
}

internal static partial class IlSqlLifecycleExtractor
{
    internal const string InfoMessageSchema = "il-sql-lifecycle-info-message-entry/1";
    internal const string InfoMessagePolicy = "explicit-sql-info-message/1";
    internal sealed record InfoMessageRejection(string Body, string Binary, long Offset, string MetadataToken, string Phase, string Opcode, string? CallSha256);
    internal sealed record InfoMessageReceipt(string Policy, Site Site, string Action, string Connection,
        string Delegate, string Receiver, string CallbackBody, string CallbackBinary,
        IReadOnlyList<string> FieldHashes,
        string Limitation = "event-timing-and-values-unproven;callback-not-executed;external-effects-unmodeled;normal-return-assumed");
    internal sealed record ResultNamingReceipt(string Policy, Site Site, string Body, string Binary,
        string DataSet, string Execution, IReadOnlyList<string> Fields, bool DataSetName, bool TableNames,
        string Limitation = "names-and-ordinal-association-unbound;event-may-not-fire;may-throw;normal-return-assumed;no-table-count-or-name-proof");
    private sealed partial class Machine
    {
        internal readonly List<InfoMessageReceipt> InfoReceipts = [];
        internal InfoMessageRejection? InfoFailure;
        internal readonly List<ResultNamingReceipt> NamingReceipts = [];
        private readonly HashSet<string> infoCapturedFields = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Body> infoTargets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string[]> infoFields = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (string Connection, string? Execution)> infoLists = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Value>> infoSubscriptions = new(StringComparer.Ordinal);

        private Value InfoFunction(IlCallObservation call)
        {
            if (!budget.TryConsume(bodies.Count)) throw new Refusal("lifecycle-work-limit");
            var matches = bodies.Where(b => b.Observation.MethodIdentity == call.TargetIdentity
                || b.Observation.ObjectBody?.MemberReference == call.TargetIdentity || b.MemberReferenceAlias == call.TargetIdentity).Take(2).ToArray();
            if (matches.Length != 1 || matches[0].Observation.ObjectBody is not { } meta || !IlSqlInfoMessageApis.Callback(meta))
                throw new Refusal("lifecycle-info-callback-target");
            var target = matches[0];
            var proof = ScanProgressAmbient.Observe(ScanProgressStages.IlCallbackInspection, () => InspectInfoBody(target, false));
            if (proof.Fields.Length == 0) throw new Refusal("lifecycle-info-no-name-list");
            if (infoTargets.Count >= 4 && !infoTargets.ContainsKey(call.TargetIdentity)) throw new Refusal("lifecycle-info-target-limit");
            infoTargets[call.TargetIdentity] = target; infoFields[call.TargetIdentity] = proof.Fields;
            return new("info-function", call.TargetIdentity);
        }

        private bool TryInfoCall(IlCallObservation call, string op, Value receiver, Value[] args, Site site, out Value result)
        {
            result = Value.Unknown;
            if (new[] { IlSqlInfoMessageApis.DelegateCtor, IlSqlInfoMessageApis.Connection, IlSqlInfoMessageApis.Command,
                IlSqlInfoMessageApis.Open, IlSqlInfoMessageApis.Close, IlSqlInfoMessageApis.DbOpen, IlSqlInfoMessageApis.DbClose,
                IlSqlInfoMessageApis.Add, IlSqlInfoMessageApis.Remove }.Contains(call.TargetIdentity, StringComparer.Ordinal)
                && (!IlSqlInfoMessageApis.Shape(call) || (op == "newobj") != new[] { IlSqlInfoMessageApis.DelegateCtor,
                    IlSqlInfoMessageApis.Connection, IlSqlInfoMessageApis.Command }.Contains(call.TargetIdentity, StringComparer.Ordinal)))
                throw new Refusal("lifecycle-info-call-shape");
            if (call.TargetIdentity == IlSqlInfoMessageApis.DelegateCtor)
            {
                if (op != "newobj" || args.Length != 2 || args[1].Kind != "info-function"
                    || !infoTargets.TryGetValue(args[1].Text, out var delegateTarget)
                    || Get(args[0], "custom").Owner != delegateTarget.Observation.ObjectBody!.OwnerType)
                    throw new Refusal("lifecycle-info-delegate-shape");
                result = View("info-delegate", args[0]); heap[result.Text].Command = args[1]; return true;
            }
            if (call.TargetIdentity == IlSqlInfoMessageApis.Connection)
            {
                if (op != "newobj" || args.Length != 1 || args[0].Kind is not ("string" or "input" or "opaque-initializer")
                    || args[0].Kind != "string" && args[0].DeclaredType != IlSqlInfoMessageApis.P("String"))
                    throw new Refusal("lifecycle-info-connection-shape");
                result = New("sql-connection"); return true;
            }
            if (call.TargetIdentity == IlSqlInfoMessageApis.Command)
            {
                _ = Get(args[1], "sql-connection");
                result = New("command"); heap[result.Text].Command = new("string", Text(args[0])); heap[result.Text].Parent = args[1]; return true;
            }
            if (call.TargetIdentity == IlSqlInfoMessageApis.Open || call.TargetIdentity == IlSqlInfoMessageApis.Close
                || call.TargetIdentity == IlSqlInfoMessageApis.DbOpen || call.TargetIdentity == IlSqlInfoMessageApis.DbClose)
            { _ = Get(receiver, "sql-connection"); return true; }
            if (call.TargetIdentity != IlSqlInfoMessageApis.Add && call.TargetIdentity != IlSqlInfoMessageApis.Remove) return false;
            _ = Get(receiver, "sql-connection");
            var state = Get(args[0], "info-delegate"); var owner = Get(state.Parent, "custom");
            var fields = infoFields[state.Command.Text];
            var subscriptions = infoSubscriptions.GetValueOrDefault(receiver.Text);
            if (subscriptions is null) { subscriptions = []; infoSubscriptions.Add(receiver.Text, subscriptions); }
            if (call.TargetIdentity == IlSqlInfoMessageApis.Add)
            {
                if (subscriptions.Count >= 4 || subscriptions.Contains(args[0])) throw new Refusal("lifecycle-info-subscription-reused-or-limit");
                foreach (var field in fields)
                {
                    var list = owner.Fields.GetValueOrDefault(field, Value.Unknown); var collection = Get(list, "arraylist");
                    if (collection.Slots.Count != 0 || collection.Frozen || infoLists.ContainsKey(list.Text)) throw new Refusal("lifecycle-info-list-reused");
                    infoLists.Add(list.Text, (receiver.Text, null)); collection.Frozen = true;
                    infoCapturedFields.Add(state.Parent.Text + ":" + field);
                }
                subscriptions.Add(args[0]);
            }
            else if (!subscriptions.Remove(args[0])) throw new Refusal("lifecycle-info-unmatched-unsubscribe");
            if (InfoReceipts.Count >= 16) throw new Refusal("lifecycle-info-receipt-limit");
            var target = infoTargets[state.Command.Text];
            InfoReceipts.Add(new(InfoMessagePolicy, site, call.TargetIdentity == IlSqlInfoMessageApis.Add ? "subscribe" : "unsubscribe",
                receiver.Text, args[0].Text, state.Parent.Text, target.Observation.BodyIdentity, target.Input.RawFileSha256!, fields.Select(Hash).ToArray()));
            return true;
        }

        private void RecordInfoFill(Value connection, Value set, string execution)
        {
            if (!infoMessageBoundary || connection.Kind != "object") return;
            foreach (var list in infoLists.Where(pair => pair.Value.Connection == connection.Text).ToArray())
            {
                if (list.Value.Execution is not null) throw new Refusal("lifecycle-info-execution-reused");
                infoLists[list.Key] = (connection.Text, execution);
            }
        }

        private bool TryResultNaming(Body body, Value receiver, Value[] args, Site site)
        {
            var meta = body.Observation.ObjectBody!;
            if (!meta.HasThis || meta.ParameterCount != 1 || meta.ReturnsValue
                || !meta.MemberReference.EndsWith("|(" + IlSqlInfoMessageApis.D("DataSet") + ")->" + IlSqlInfoMessageApis.P("Void"), StringComparison.Ordinal)) return false;
            var owner = Get(receiver, "custom");
            if (!owner.Fields.Values.Any(value => infoLists.ContainsKey(value.Text))) return false;
            var proof = ScanProgressAmbient.Observe(ScanProgressStages.IlResultNaming, () => InspectInfoBody(body, true));
            var set = Get(args[0], "dataset");
            if (set.Slots.Count == 0 || proof.Fields.Length == 0 || !proof.DataSetName && !proof.TableNames)
                throw new Refusal("lifecycle-info-naming-shape");
            var execution = Get(set.Slots[0], "table").Execution ?? throw new Refusal("lifecycle-info-naming-before-fill");
            foreach (var field in proof.Fields)
            {
                var value = owner.Fields.GetValueOrDefault(field, Value.Unknown);
                if (!infoLists.TryGetValue(value.Text, out var list) || list.Execution != execution)
                    throw new Refusal("lifecycle-info-naming-execution-mismatch");
            }
            if (NamingReceipts.Count >= 8) throw new Refusal("lifecycle-info-naming-limit");
            NamingReceipts.Add(new(InfoMessagePolicy, site, body.Observation.BodyIdentity, body.Input.RawFileSha256!,
                args[0].Text, execution, proof.Fields.Select(Hash).ToArray(), proof.DataSetName, proof.TableNames));
            invokedBodies.Add(At(body, 0, ++frame));
            return true;
        }

        private sealed record InfoBodyProof(string[] Fields, bool DataSetName, bool TableNames);
        private InfoBodyProof InspectInfoBody(Body body, bool naming)
        {
            var meta = body.Observation.ObjectBody ?? throw new Refusal("lifecycle-info-body-unavailable");
            if (body.Input.Outcome != "admitted" || body.Input.GapKinds.Count != 0 || body.Input.RawFileSha256 is not { Length: 64 } binarySha || binarySha.Any(c => !char.IsAsciiHexDigit(c))
                || !Guid.TryParse(body.Input.ModuleMvid, out _) || body.Observation.ObjectBodyAgreement is not { RawBody: true, CallShapes: true, ObjectBody: true }
                || meta.IsGeneric || meta.IsVirtual || meta.Instructions.Count is 0 or > 256)
                throw new Refusal("lifecycle-info-body-metadata-or-limit");
            if ((meta.ExceptionRegions?.Count ?? 0) > 8) throw new Refusal("lifecycle-info-handler-limit");
            var code = meta.Instructions.Select(line => line.Split(':', 4)).ToArray();
            if (code.Any(line => line.Length != 4)) throw new Refusal("lifecycle-info-instruction-metadata");
            var offsets = code.Select(line => Offset(line[1])).ToArray();
            if (offsets.Distinct().Count() != offsets.Length) throw new Refusal("lifecycle-info-offset-metadata");
            if (body.Observation.Calls.GroupBy(call => call.Offset).Any(group => group.Count() != 1)) throw new Refusal("lifecycle-info-call-metadata");
            var calls = body.Observation.Calls.ToDictionary(call => call.Offset);
            var fields = new HashSet<string>(StringComparer.Ordinal); var datasetName = false; var tableNames = false;
            var states = new Dictionary<int, (string[] Stack, string[] Locals)>(); var pending = new Queue<int>();
            Seed(0, [], Enumerable.Repeat(body.Observation.InitLocals ? "external" : "unset", 64).ToArray());
            var regions = (meta.ExceptionRegions ?? []).Select(ParseRegion).ToArray();
            if (regions.Any(region => naming || region.Kind is not ("catch" or "finally")))
                throw new Refusal("lifecycle-info-exception-shape");
            foreach (var region in regions) _ = Index(region.Handler);
            var steps = 0; var returned = false;
            while (pending.Count > 0)
            {
                if (++steps > 4096 || !budget.TryConsume(1)) throw new Refusal("lifecycle-info-inspection-limit");
                var i = pending.Dequeue(); var (savedStack, savedLocals) = states[i];
                var stack = savedStack.ToList(); var locals = savedLocals.ToArray();
                // Exceptional transfers preserve locals. Seeding handlers with untracked
                // values would erase a captured receiver/list alias and permit escape.
                // Every protected instruction contributes a conservative handler edge.
                foreach (var region in regions.Where(region => offsets[i] >= region.Start && offsets[i] < region.End))
                    Seed(Index(region.Handler), region.Kind == "catch" ? ["external"] : [], locals);
                var op = code[i][2]; var operand = code[i][3]; var next = true;
                InfoFailure = new(body.Observation.BodyIdentity, body.Input.RawFileSha256!, offsets[i], body.Observation.MetadataToken, naming ? "result-naming" : "callback-inspection", op,
                    calls.TryGetValue(offsets[i], out var diagnosticCall) ? Hash(diagnosticCall.TargetIdentity) : null);
                string Pop() { if (stack.Count == 0) throw new Refusal("lifecycle-info-stack"); var v = stack[^1]; stack.RemoveAt(stack.Count - 1); return v; }
                void Plain(string value) { if (value == "unset") throw new Refusal("lifecycle-info-local-uninitialized"); if (Protected(value)) throw new Refusal("lifecycle-info-reference-escape"); }
                if (op == "nop") { }
                else if (Slot(op, operand, "ldarg", out var slot))
                {
                    if (!naming && slot == 1) throw new Refusal("lifecycle-info-sender-access");
                    stack.Add(slot == 0 ? "this" : naming && slot == 1 ? "dataset" : !naming && slot == 2 ? "external" : throw new Refusal("lifecycle-info-argument"));
                }
                else if (Slot(op, operand, "ldloc", out slot)) stack.Add(slot < 64 ? locals[slot] : throw new Refusal("lifecycle-info-local-limit"));
                else if (Slot(op, operand, "stloc", out slot)) { if (slot >= 64) throw new Refusal("lifecycle-info-local-limit"); locals[slot] = Pop(); }
                else if (op == "ldstr") stack.Add("text");
                else if (op == "ldnull" || op.StartsWith("ldc.i4", StringComparison.Ordinal)) stack.Add("external");
                else if (op == "dup") { var v = Pop(); stack.Add(v); stack.Add(v); }
                else if (op == "pop") { Plain(Pop()); }
                else if (op == "ldfld")
                {
                    var parts = operand.Split(':', 4);
                    if (parts.Length != 4 || parts[1] != "field" || Pop() != "this") throw new Refusal("lifecycle-info-field-owner");
                    var field = parts[3][(parts[3].IndexOf('|') + 1)..];
                    if (!meta.PrivateInstanceFields.Contains(field, StringComparer.Ordinal) || !field.StartsWith("type:" + meta.OwnerType + "|member:", StringComparison.Ordinal)
                        || !new[] { IlSqlInfoMessageApis.Core, IlSqlInfoMessageApis.Core.Replace("4.0.0.0", "2.0.0.0", StringComparison.Ordinal) }
                            .Any(core => field.EndsWith(core + IlSqlInfoMessageApis.T("System.Collections", "ArrayList"), StringComparison.Ordinal)))
                        throw new Refusal("lifecycle-info-callback-tracked-field");
                    if (fields.Count >= 4 && !fields.Contains(field)) throw new Refusal("lifecycle-info-field-limit");
                    fields.Add(field); stack.Add("list:" + field);
                }
                else if (op is "call" or "callvirt" or "newobj")
                {
                    if (!calls.TryGetValue(offsets[i], out var call) || !IlSqlInfoMessageApis.Shape(call) || call.StackShape is not { Supported: true } shape
                        || shape.ParameterCount > 16 || shape.ByReferenceParameters.Contains('1')) throw new Refusal("lifecycle-info-call-shape");
                    var arguments = new string[shape.ParameterCount];
                    for (var n = arguments.Length - 1; n >= 0; n--) arguments[n] = Pop();
                    var receiver = op != "newobj" && shape.HasThis ? Pop() : "external";
                    var rule = IlSqlInfoMessageApis.Rule(call.TargetIdentity) ?? throw new Refusal("lifecycle-info-uninspected-call:" + Hash(call.TargetIdentity));
                    foreach (var argument in arguments) Plain(argument);
                    var result = "external";
                    if (rule.StartsWith("list-", StringComparison.Ordinal))
                    {
                        if (!receiver.StartsWith("list:", StringComparison.Ordinal) || naming == (rule == "list-add")) throw new Refusal("lifecycle-info-list-effect");
                        if (rule == "list-add" && arguments[0] != "text") throw new Refusal("lifecycle-info-list-element");
                        if (rule == "list-item") result = "text";
                    }
                    else if (rule is "dataset-name" or "table-name")
                    {
                        if (!naming || receiver != (rule == "dataset-name" ? "dataset" : "table") || arguments[0] != "text") throw new Refusal("lifecycle-info-naming-sink");
                        datasetName |= rule == "dataset-name"; tableNames |= rule == "table-name";
                    }
                    else if (rule == "tables")
                    { if (!naming || receiver != "dataset") throw new Refusal("lifecycle-info-naming-receiver"); result = "tables"; }
                    else if (rule == "table")
                    { if (!naming || receiver != "tables") throw new Refusal("lifecycle-info-naming-receiver"); result = "table"; }
                    else
                    {
                        Plain(receiver);
                        if (naming && !call.TargetIdentity.EndsWith("|()->" + IlSqlInfoMessageApis.P("String"), StringComparison.Ordinal))
                            throw new Refusal("lifecycle-info-naming-call");
                        if (call.TargetIdentity.EndsWith("->" + IlSqlInfoMessageApis.P("String"), StringComparison.Ordinal)) result = "text";
                    }
                    if (op == "newobj" || shape.ReturnsValue) stack.Add(result);
                }
                else if (op is "castclass" or "isinst" || op.StartsWith("conv.", StringComparison.Ordinal)) { Plain(Pop()); stack.Add("external"); }
                else if (op is "add" or "add.ovf" or "sub" or "sub.ovf" or "ceq" or "clt" or "cgt" or "cgt.un" or "and" or "or")
                { Plain(Pop()); Plain(Pop()); stack.Add("external"); }
                else if (op is "br" or "br.s" or "leave" or "leave.s")
                { if (op.StartsWith("leave", StringComparison.Ordinal)) stack.Clear(); Seed(Branch(operand), stack.ToArray(), locals); next = false; }
                else if (op is "brtrue" or "brtrue.s" or "brfalse" or "brfalse.s")
                { Plain(Pop()); Seed(Branch(operand), stack.ToArray(), locals); }
                else if (new[] { "beq", "bne.un", "bge", "bge.un", "bgt", "bgt.un", "ble", "ble.un", "blt", "blt.un" }
                    .Any(branch => op == branch || op == branch + ".s"))
                { Plain(Pop()); Plain(Pop()); Seed(Branch(operand), stack.ToArray(), locals); }
                else if (op == "ret") { if (stack.Count != 0) throw new Refusal("lifecycle-info-return-escape"); returned = true; next = false; }
                else if (op == "endfinally") { if (stack.Count != 0) throw new Refusal("lifecycle-info-finally-stack"); next = false; }
                else throw new Refusal("lifecycle-info-inspection-opcode:" + op);
                if (next) Seed(i + 1, stack.ToArray(), locals);
            }
            if (!returned || fields.Count == 0) throw new Refusal("lifecycle-info-incomplete-body");
            InfoFailure = null;
            return new(fields.Order(StringComparer.Ordinal).ToArray(), datasetName, tableNames);
            int Index(long offset) { var i = Array.IndexOf(offsets, offset); return i >= 0 ? i : throw new Refusal("lifecycle-info-branch"); }
            int Branch(string operand) => operand.StartsWith("br:0x", StringComparison.Ordinal) ? Index(Offset(operand[5..])) : throw new Refusal("lifecycle-info-branch");
            static bool Protected(string value) => value is "this" or "dataset" or "tables" or "table" || value.StartsWith("list:", StringComparison.Ordinal);
            void Seed(int index, string[] stack, string[] locals)
            {
                if (index >= code.Length || stack.Length > 64) throw new Refusal("lifecycle-info-flow-limit");
                if (!states.TryGetValue(index, out var old)) { states[index] = (stack.ToArray(), locals.ToArray()); pending.Enqueue(index); return; }
                if (old.Stack.Length != stack.Length) throw new Refusal("lifecycle-info-merge-stack");
                var changed = false;
                string Merge(string a, string b)
                {
                    if (a == b) return a;
                    if (Protected(a) || Protected(b)) throw new Refusal("lifecycle-info-reference-merge");
                    if (a != "external") changed = true;
                    return "external";
                }
                var mergedStack = old.Stack.Zip(stack, Merge).ToArray(); var mergedLocals = old.Locals.Zip(locals, Merge).ToArray();
                if (changed) { states[index] = (mergedStack, mergedLocals); pending.Enqueue(index); }
            }
        }
    }
}
