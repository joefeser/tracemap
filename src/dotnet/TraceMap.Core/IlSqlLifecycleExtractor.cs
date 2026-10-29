using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Body = TraceMap.Core.IlSqlObjectPathExtractor.Body;

namespace TraceMap.Core;

/// <summary>
/// Bounded lifecycle interpreter, emitted through a separate candidate contract.
/// Values describe successful-path symbolic origins, never actual database rows.
/// </summary>
internal static partial class IlSqlLifecycleExtractor
{
    internal const string Schema = "il-sql-lifecycle/1";
    internal const string Limitation = "Tier3 conditional encoded static lineage candidate. Successful normal-completion paths only; symbolic existing-row summaries do not prove rows or execution. SQL schema ancestry is separate from current application-written value origin. Catch continuations are not executed; failure boundaries permit partial updates and make no rollback claim. ArrayList support is exact signed net48, single complete enumeration of at most 16 inspected custom objects frozen on Add; unsupported mutation or escape is unknown. Entry producers/loaders retain unbound argument origins; unknown control or required scalar values refuse. Entry facts are separate from column-read comparison facts. Opt-in opaque root scalar initialization uses a separate entry schema, conditional normal-return and tracked-heap noninterference assumptions, and unmodeled external effects; it is not constructor-completion or purity proof. No source, deployment, session, framework, type, nullability, value or migration proof.";
    internal static bool IsReadRoot(IlBodyObservation body)
        => body.ObjectBody is { HasThis: true, ParameterCount: <= 1, ReturnsValue: true, IsGeneric: false, IsVirtual: false } meta
            && (meta.ParameterCount == 0
                ? meta.MemberReference.EndsWith("|()->type(namespace:6:System|names:6:Object)", StringComparison.Ordinal)
                : meta.MemberReference.EndsWith("|(type(namespace:6:System|names:7:Boolean))->type(namespace:6:System|names:6:Object)", StringComparison.Ordinal));
    internal const string EntrySchema = "il-sql-lifecycle-entry/1";
    internal static bool IsRoot(IlBodyObservation body)
    {
        if (IsReadRoot(body)) return true;
        var meta = body.ObjectBody;
        if (meta is { Name: ".ctor" or ".cctor" }) return false;
        if (meta is not null && IlSqlLifecycleApis.ReturnsDataSet(meta.MemberReference)) return true;
        // SQL-related methods are also selected when metadata/entry admission fails,
        // so a rejected entry has an explicit gap rather than disappearing silently.
        return body.Calls.Any(call => IlSqlLifecycleApis.MentionsDataSet(call.TargetIdentity)
            || IlSqlObjectPathExtractor.KnownFrameworkApi(call.TargetIdentity) is "command" or "fill" or "adapter"
            || IlSqlLifecycleApis.Resolve(call.TargetIdentity, []) is "arraylist-new" or "arraylist-add" or "arraylist-enumerator");
    }
    internal static bool IsGuardedRoot(IlBodyObservation body)
        => body.ObjectBody is { } meta && IlSqlLifecycleApis.HasSingleBooleanArgument(meta);
    internal sealed record RootArgument(int Index, string Origin, string? DeclaredType);
    internal sealed record DataSetOutput(string Allocation, IReadOnlyList<string> Tables);
    internal sealed record StoredDataSet(string OwnerAllocation, string FieldSha256, string DataSetAllocation);
    internal sealed record ParameterOutput(string Allocation, string NameHash, int SqlDbType, int Size, string ValueOrigin);
    internal sealed record CommandOutput(string Execution, string Allocation, IReadOnlyList<ParameterOutput> Parameters);
    internal sealed record EntryOutput(string Kind, string RootSignatureSha256, IReadOnlyList<RootArgument> Arguments,
        string? ReturnedDataSet, IReadOnlyList<DataSetOutput> DataSets, IReadOnlyList<StoredDataSet> StoredDataSets,
        IReadOnlyList<TableState> Tables, IReadOnlyList<CommandOutput> Commands);
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    internal sealed record Site(string Body, string Binary, long Offset, int Invocation);
    internal sealed record Execution(string Id, string CommandHash, Site Site);
    internal sealed record Origin(string Kind, string Table, string? Execution, string ColumnHash, Site? Assignment = null);
    internal sealed record Read(Site Site, string Table, string ColumnHash,
        IReadOnlyList<string> SchemaExecutions, Origin Value);
    internal sealed record Loop(Site Site, string Table, string Meaning);
    internal sealed record TableState(string Allocation, bool RowsCleared, string? Execution,
        IReadOnlyList<string> SchemaExecutions, IReadOnlyDictionary<string, Origin> Writes);
    internal sealed record ObjectSnapshot(string Allocation, string Kind, IReadOnlyList<string> Slots,
        IReadOnlyDictionary<string, string> Fields);
    internal sealed record Failure(Site Site, string Operation, Site CatchHandler,
        IReadOnlyList<TableState> Before, IReadOnlyList<TableState> NormalCompletion,
        bool MayPartiallyMutate, bool MayHaveCompletedEarlierRows,
        IReadOnlyList<ObjectSnapshot> BeforeObjects, IReadOnlyList<ObjectSnapshot> NormalCompletionObjects)
    {
        public string Limitation => "possible-exception-boundary;not-observed;catch-continuation-not-executed;no-rollback-claim";
    }
    internal sealed record Result(IReadOnlyList<Execution> Executions, IReadOnlyList<Read> Reads,
        Read? Returned, IReadOnlyList<string> Gaps, IReadOnlyList<Loop>? Loops = null,
        IReadOnlyList<Failure>? Failures = null, IReadOnlyList<Site>? Bodies = null)
    {
        public string Claim => (OpaqueInitializers is { Count: > 0 }
            ? "conditional-static-lineage;opaque-initialization-normal-return-assumed;tracked-heap-noninterference-assumed;external-effects-unmodeled;row-existence-unproven"
            : "conditional-static-lineage;normal-completion;row-existence-unproven")
            + (OpaqueInitializers?.Any(initializer => initializer.CompiledCallees is { Count: > 0 }) == true
                ? ";compiled-scalar-normal-return-assumed;compiled-scalar-internal-control-unmodeled;transitive-effects-unmodeled" : "")
            + (TraceStringComputations is { Count: > 0 } ? ";computed-string-values-unbound;argument-computation-normal-return-assumed" : "")
            + (ExternalTraces is { Count: > 0 } ? ";external-trace-normal-return-assumed;trace-receiver-non-null-unproven;tracked-heap-noninterference-assumed;external-effects-unmodeled" : "");
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<ExternalTrace>? ExternalTraces { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<TraceStringComputation>? TraceStringComputations { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<OpaqueInitializer>? OpaqueInitializers { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public EntryOutput? Entry { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public BodyMetadataFailure? MetadataFailure { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public FieldOwnerRejection? FieldOwnerFailure { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public FieldAccessRejection? FieldAccessFailure { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public OpaqueChainRejection? OpaqueInitializerRejection { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<ConstructorOwnerView>? ConstructorOwnerViews { get; init; }
    }
    internal sealed record BodyMetadataFailure(string Stage, string Body, string Binary, string MetadataToken,
        int InstructionCount, IlObjectBodyRetention? Retention, IlObjectBodyAgreement? Agreement,
        int? AdmittedConstructorMatches = null, int? UnavailableBodiesInModule = null);
    private static BodyMetadataFailure MetadataFailure(Body body, string stage,
        int? constructorMatches = null, int? unavailableInModule = null)
        => new(stage, body.Observation.BodyIdentity, body.Input.RawFileSha256!, body.Observation.MetadataToken,
            body.Observation.InstructionCount, body.Observation.ObjectBodyRetention, body.Observation.ObjectBodyAgreement,
            constructorMatches, unavailableInModule);
    internal sealed record GuardedRun(string Predicate, bool Value, Result Result);

    internal static IReadOnlyList<GuardedRun> ExtractGuarded(Body root, IReadOnlyList<Body> bodies,
        IlBodyEvidenceExtractor.IlWorkBudget budget)
        => [new("argument:0", false, Extract(root, bodies, false, budget)),
            new("argument:0", true, Extract(root, bodies, true, budget))];
    private sealed record Region(string Kind, long Start, long End, long Handler, long HandlerEnd);
    private sealed record Value(string Kind, string Text, string? DeclaredType = null)
    {
        internal static readonly Value Unknown = new("unknown", "");
    }
    private sealed class ObjectState(string kind, string owner)
    {
        internal readonly string Kind = kind, Owner = owner;
        internal readonly HashSet<string> FieldOwners = new(StringComparer.Ordinal) { owner };
        internal readonly Dictionary<string, Value> Fields = new(StringComparer.Ordinal);
        internal readonly List<Value> Slots = [];
        internal readonly List<string> SchemaExecutions = [];
        internal readonly HashSet<string> Columns = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, Origin> Writes = new(StringComparer.Ordinal);
        internal Value Parent = Value.Unknown, Command = Value.Unknown, Type = Value.Unknown;
        internal string? Execution;
        internal bool Empty = true;
        internal bool Frozen, Attached;
        internal int Moves = 0;
    }
    private sealed class Refusal(string message, BodyMetadataFailure? metadataFailure = null) : Exception(message)
    {
        internal BodyMetadataFailure? MetadataFailure { get; } = metadataFailure;
    }

    internal static Result Extract(Body root, IReadOnlyList<Body> bodies, bool enabled,
        IlBodyEvidenceExtractor.IlWorkBudget budget, bool opaqueRootInitializers = false, bool externalTraceBoundary = false, bool compiledScalarBoundary = false)
    {
        var machine = new Machine(bodies, budget, opaqueRootInitializers, externalTraceBoundary, compiledScalarBoundary);
        try { return machine.Run(root, enabled); }
        catch (Refusal error) { return new Result([], [], null, [error.Message]) { MetadataFailure = error.MetadataFailure,
            OpaqueInitializers = machine.Initializers.Count > 0 ? machine.Initializers.ToArray() : null,
            ExternalTraces = machine.TraceBoundaries.Count > 0 ? machine.TraceBoundaries.ToArray() : null,
            TraceStringComputations = machine.StringComputations.Count > 0 ? machine.StringComputations.ToArray() : null,
            FieldOwnerFailure = machine.OwnerFailure,
            FieldAccessFailure = machine.FieldAccessFailure,
            OpaqueInitializerRejection = machine.ReportedOpaqueRejection,
            ConstructorOwnerViews = machine.OwnerViews.Count > 0 ? machine.OwnerViews.ToArray() : null }; }
    }

    private sealed partial class Machine(IReadOnlyList<Body> bodies, IlBodyEvidenceExtractor.IlWorkBudget budget, bool opaqueRootInitializers, bool externalTraceBoundary, bool compiledScalarBoundary)
    {
        private readonly Dictionary<string, ObjectState> heap = new(StringComparer.Ordinal);
        private readonly List<Execution> executions = [];
        private readonly List<Read> reads = [];
        private readonly List<Loop> loops = [];
        private readonly List<Failure> failures = [];
        private readonly List<Site> invokedBodies = [];
        private readonly Stack<Site> catchHandlers = new();
        private readonly HashSet<string> active = new(StringComparer.Ordinal);
        private int frame, work;
        private Value loopEnumerator = Value.Unknown, loopRow = Value.Unknown;
        private int loopAllocationStart;
        private Value New(string kind, string owner = "")
        {
            if (heap.Count >= 256) throw new Refusal("lifecycle-object-limit");
            var id = "allocation:" + heap.Count.ToString(CultureInfo.InvariantCulture);
            heap.Add(id, new(kind, owner)); return new("object", id);
        }
        private ObjectState Get(Value value, params string[] kinds)
        {
            if (value.Kind != "object" || !heap.TryGetValue(value.Text, out var state)
                || !kinds.Contains(state.Kind, StringComparer.Ordinal)) throw new Refusal("lifecycle-object-origin:" + string.Join('/', kinds));
            return state;
        }
        private Value View(string kind, Value parent)
        { var result = New(kind); heap[result.Text].Parent = parent; return result; }
        private static int Number(Value value)
            => value.Kind == "int" && int.TryParse(value.Text, out var number) ? number : throw new Refusal("lifecycle-integer-unresolved");
        private static string Text(Value value)
            => value.Kind == "string" ? value.Text : throw new Refusal("lifecycle-string-unresolved");
        private static Value Int(int value) => new("int", value.ToString(CultureInfo.InvariantCulture));
        private static Site At(Body body, long offset, int invocation)
            => new(body.Observation.BodyIdentity, body.Input.RawFileSha256!, offset, invocation);

        internal Result Run(Body root, bool enabled)
        {
            if (bodies.Count > 4096 || !budget.TryConsume(bodies.Count)) throw new Refusal("lifecycle-index-limit");
            if (!bodies.Contains(root) || bodies.GroupBy(b => b.Observation.MethodIdentity, StringComparer.Ordinal).Any(g => g.Count() != 1))
                throw new Refusal("lifecycle-ambiguous-input");
            var meta = root.Observation.ObjectBody ?? throw new Refusal("lifecycle-body-unavailable", MetadataFailure(root, "root-metadata"));
            var readRoot = IsReadRoot(root.Observation);
            var producer = IlSqlLifecycleApis.ReturnsDataSet(meta.MemberReference);
            var loader = IlSqlLifecycleApis.ReturnsVoid(meta.MemberReference);
            if (meta.IsVirtual || meta.IsGeneric || meta.Name is ".ctor" or ".cctor"
                || meta.ParameterCount is < 0 or > 16 || (!readRoot && !producer && !loader)
                || producer && !meta.ReturnsValue || loader && meta.ReturnsValue)
                throw new Refusal("lifecycle-root-admission-shape");
            if (externalTraceBoundary && !readRoot) externalTraceRoot = root.Observation.BodyIdentity;
            var instance = Value.Unknown;
            if (meta.HasThis)
            {
                instance = New("custom", meta.OwnerType);
                var ctor = bodies.Where(b => b.Observation.ObjectBody is { Name: ".ctor", ParameterCount: 0 } m
                    && m.OwnerType == meta.OwnerType).Take(2).ToArray();
                if (ctor.Length != 1) throw new Refusal("lifecycle-constructor-unresolved",
                    MetadataFailure(root, "constructor-selection", ctor.Length,
                        bodies.Count(body => body.Input.RawFileSha256 == root.Input.RawFileSha256 && body.Observation.ObjectBody is null)));
                if (opaqueRootInitializers && !readRoot)
                { opaqueConstructor = ctor[0].Observation.BodyIdentity; opaqueInstance = instance.Text; }
                Invoke(ctor[0], [instance], 0);
                opaqueConstructor = null;
            }
            var guarded = IsGuardedRoot(root.Observation);
            var inputTypes = IlSqlLifecycleApis.RootArgumentTypes(meta);
            var inputs = Enumerable.Range(0, meta.ParameterCount)
                .Select(i => guarded ? Int(enabled ? 1 : 0) : new Value("input", i.ToString(CultureInfo.InvariantCulture), inputTypes[i])).ToArray();
            var returned = Invoke(root, meta.HasThis ? new[] { instance }.Concat(inputs).ToArray() : inputs, 0);
            var result = returned.Kind == "read" ? reads[int.Parse(returned.Text, CultureInfo.InvariantCulture)] : null;
            if (readRoot && result is null) throw new Refusal("lifecycle-return-unresolved");
            if (loopEnumerator.Kind != "unknown") throw new Refusal("lifecycle-loop-incomplete");
            if (heap.Values.Any(state => state.Kind == "collection-enumerator"
                && state.Moves != Get(state.Parent, "arraylist").Slots.Count + 1))
                throw new Refusal("lifecycle-collection-enumeration-incomplete");
            var completed = new Result(executions.ToArray(), reads.ToArray(), result, [], loops.ToArray(), failures.ToArray(), invokedBodies.ToArray())
                { OpaqueInitializers = Initializers.Count > 0 ? Initializers.ToArray() : null,
                    ExternalTraces = TraceBoundaries.Count > 0 ? TraceBoundaries.ToArray() : null,
                    TraceStringComputations = StringComputations.Count > 0 ? StringComputations.ToArray() : null,
                    ConstructorOwnerViews = OwnerViews.Count > 0 ? OwnerViews.ToArray() : null };
            if (readRoot) return completed;
            if (executions.Count == 0) throw new Refusal("lifecycle-entry-no-sql-execution");
            string? returnedSet = null;
            if (producer) { _ = Get(returned, "dataset"); returnedSet = returned.Text; }
            var stored = instance.Kind == "object" ? Get(instance, "custom").Fields
                .Where(field => field.Value.Kind == "object" && heap[field.Value.Text].Kind == "dataset")
                .Select(field => new StoredDataSet(instance.Text, Hash(field.Key), field.Value.Text)).ToArray() : [];
            var visible = stored.Select(field => field.DataSetAllocation)
                .Concat(returnedSet is null ? Array.Empty<string>() : [returnedSet]).Distinct(StringComparer.Ordinal).ToArray();
            var outputs = visible.Select(id => new DataSetOutput(id, heap[id].Slots.Select(slot => slot.Text).ToArray())).ToArray();
            var outputTables = outputs.SelectMany(output => output.Tables).ToHashSet(StringComparer.Ordinal);
            var tables = Snapshot().Where(table => outputTables.Contains(table.Allocation)).ToArray();
            if (producer && !tables.Any(table => table.SchemaExecutions.Count > 0))
                throw new Refusal("lifecycle-entry-returned-dataset-origin");
            if (loader && !tables.Any(table => table.SchemaExecutions.Count > 0) && reads.Count == 0)
                throw new Refusal("lifecycle-entry-void-output-unresolved");
            var commands = heap.Where(pair => pair.Value.Kind == "command" && pair.Value.Execution is not null)
                .Select(pair => new CommandOutput(pair.Value.Execution!, pair.Key, pair.Value.Slots.Select(value =>
                {
                    var parameter = Get(value, "parameter");
                    var origin = parameter.Fields.GetValueOrDefault("parameter-value", Value.Unknown);
                    return new ParameterOutput(value.Text, Text(parameter.Command), Number(parameter.Type),
                        Number(parameter.Slots[0]), origin.Kind == "input" ? "argument:" + origin.Text
                            : origin.Kind == "opaque-initializer" ? "opaque-field:" + origin.Text
                            : origin.Kind == "computed-string" ? origin.Text : origin.Kind);
                }).ToArray())).ToArray();
            return completed with { Entry = new(producer ? "dataset-return" : "void-completion", Hash(meta.MemberReference),
                Enumerable.Range(0, meta.ParameterCount).Select(i => new RootArgument(i,
                    guarded ? "guarded-boolean" : "unbound-root-argument", inputTypes[i])).ToArray(),
                returnedSet, outputs, stored, tables, commands) };
        }

        private Value Invoke(Body body, Value[] args, int depth)
        {
            var meta = body.Observation.ObjectBody ?? throw new Refusal("lifecycle-body-unavailable", MetadataFailure(body, "callee-metadata"));
            if (depth >= 24 || !active.Add(body.Observation.MethodIdentity)) throw new Refusal("lifecycle-recursion-limit");
            try
            {
                if (body.Input.Outcome != "admitted" || body.Input.GapKinds.Count != 0
                    || body.Input.RawFileSha256 is not { Length: 64 } sha || sha.Any(c => !char.IsAsciiHexDigit(c))
                    || !Guid.TryParse(body.Input.ModuleMvid, out _)) throw new Refusal("lifecycle-provenance-unavailable");
                if (meta.IsVirtual || meta.IsGeneric || meta.Instructions.Count > 256
                    || args.Length != meta.ParameterCount + (meta.HasThis ? 1 : 0)) throw new Refusal("lifecycle-body-shape");
                var invocation = ++frame;
                invokedBodies.Add(At(body, 0, invocation));
                var code = meta.Instructions.Select(line => line.Split(':', 4)).ToArray();
                if (code.Any(p => p.Length != 4)) throw new Refusal("lifecycle-invalid-instruction");
                var offsets = code.Select(p => Offset(p[1])).ToArray();
                if (offsets.Distinct().Count() != offsets.Length || !offsets.SequenceEqual(offsets.Order()))
                    throw new Refusal("lifecycle-invalid-instruction-offsets");
                if (body.Observation.Calls.GroupBy(c => c.Offset).Any(g => g.Count() != 1))
                    throw new Refusal("lifecycle-duplicate-call-offset");
                var calls = body.Observation.Calls.ToDictionary(c => c.Offset);
                var regions = (meta.ExceptionRegions ?? []).Select(ParseRegion).ToArray();
                var continuations = new Stack<int>();
                var stack = new List<Value>(); var locals = new Dictionary<int, Value>();
                Value Pop()
                {
                    if (stack.Count == 0) throw new Refusal("lifecycle-stack-underflow");
                    var value = stack[^1]; stack.RemoveAt(stack.Count - 1); return value;
                }
                int Target(string operand)
                {
                    if (!operand.StartsWith("br:0x", StringComparison.Ordinal)) throw new Refusal("lifecycle-branch-invalid");
                    var target = Array.IndexOf(offsets, Offset(operand[5..]));
                    return target < 0 ? throw new Refusal("lifecycle-branch-invalid") : target;
                }
                for (var i = 0; i < code.Length; i++)
                {
                    if (++work > 8192 || !budget.TryConsume(1) || stack.Count > 64 || locals.Count > 64) throw new Refusal("lifecycle-work-limit");
                    var op = code[i][2]; var operand = code[i][3]; var offset = offsets[i];
                    if (op == "nop") continue;
                    if (stack.Count == 0 && TryExternalTrace(body, args, depth, code, offsets, calls, locals, regions, i, invocation, out var traceEnd))
                    { i = traceEnd; continue; }
                    if (stack.Count == 0 && TryOpaqueInitializer(body, args, depth, code, offsets, calls, i, invocation, out var terminal))
                    { i = terminal; continue; }
                    if (Slot(op, operand, "ldarg", out var slot)) { stack.Add(slot < args.Length ? args[slot] : Value.Unknown); continue; }
                    if (Slot(op, operand, "ldloc", out slot)) { stack.Add(locals.GetValueOrDefault(slot, Value.Unknown)); continue; }
                    if (Slot(op, operand, "stloc", out slot)) { locals[slot] = Pop(); continue; }
                    if (op == "ldstr") { stack.Add(new("string", operand)); continue; }
                    if (op == "ldnull") { stack.Add(new("null", "")); continue; }
                    if (op.StartsWith("ldc.i4", StringComparison.Ordinal))
                    { stack.Add(new("int", op == "ldc.i4.m1" ? "-1" : op is "ldc.i4" or "ldc.i4.s" ? operand[2..] : op[7..])); continue; }
                    if (op == "dup") { var value = Pop(); stack.Add(value); stack.Add(value); continue; }
                    if (op == "pop") { _ = Pop(); continue; }
                    if (op == "box")
                    {
                        var value = Pop();
                        if (value.Kind is ("input" or "opaque-initializer") && IlSqlLifecycleApis.IsInputBox(operand, value.DeclaredType))
                        { stack.Add(value); continue; }
                        if (IlSqlLifecycleApis.TypeOperand(operand, "Boolean"))
                        {
                            if (Number(value) is not (0 or 1)) throw new Refusal("lifecycle-box-type");
                        }
                        else if (IlSqlLifecycleApis.TypeOperand(operand, "Int32")) _ = Number(value);
                        else throw new Refusal("lifecycle-box-type");
                        stack.Add(value); continue;
                    }
                    if (op == "ldtoken") { stack.Add(new("type-token", operand)); continue; }
                    if (op == "newarr")
                    {
                        if (!IlSqlLifecycleApis.TypeOperand(operand, "Object")
                            && !(body.Observation.BodyIdentity == externalTraceRoot && depth == 0 && IlSqlLifecycleApis.TypeOperand(operand, "String")))
                            throw new Refusal("lifecycle-array-type");
                        var count = Number(Pop()); if (count is < 0 or > 16) throw new Refusal("lifecycle-array-limit");
                        var array = New("array"); heap[array.Text].Slots.AddRange(Enumerable.Repeat(Value.Unknown, count)); stack.Add(array); continue;
                    }
                    if (op == "stelem.ref")
                    {
                        var value = Pop(); var index = Number(Pop()); var array = Get(Pop(), "array");
                        if (index < 0 || index >= array.Slots.Count) throw new Refusal("lifecycle-array-index");
                        array.Slots[index] = value; continue;
                    }
                    if (op is "castclass" or "isinst")
                    {
                        if (op == "castclass" && stack.Count > 0 && stack[^1].Kind == "object"
                            && heap.TryGetValue(stack[^1].Text, out var customCast) && customCast.Kind == "custom"
                            && operand == "t:" + customCast.Owner)
                            continue;
                        if (op == "isinst" && IlSqlLifecycleApis.TypeOperand(operand, "IDisposable")
                            && stack.Count > 0 && stack[^1].Kind == "object"
                            && heap.TryGetValue(stack[^1].Text, out var collectionCast) && collectionCast.Kind == "collection-enumerator")
                        { _ = Pop(); stack.Add(new("null", "")); continue; }
                        if (!IlSqlLifecycleApis.TypeOperand(operand, op == "castclass" ? "DataRow" : "IDisposable"))
                            throw new Refusal("lifecycle-cast-type");
                        var value = Pop(); _ = Get(value, op == "castclass" ? "row" : "enumerator");
                        stack.Add(value); continue;
                    }
                    if (op is "br" or "br.s")
                    {
                        var destination = Target(operand);
                        if (destination <= i || loopEnumerator.Kind != "unknown") throw new Refusal("lifecycle-unproven-loop");
                        i = destination - 1; continue;
                    }
                    if (op is "brtrue" or "brtrue.s" or "brfalse" or "brfalse.s")
                    {
                        var value = Pop(); var destination = Target(operand);
                        var truth = value.Kind == "object" || value.Kind == "int" && Number(value) != 0
                            || value.Kind is "loop-test" or "collection-test" && value.Text == "1";
                        if (value.Kind is not ("object" or "int" or "null" or "loop-test" or "collection-test")) throw new Refusal("lifecycle-guard-unresolved");
                        if ((destination <= i || loopEnumerator.Kind != "unknown") && value.Kind is not ("loop-test" or "collection-test"))
                            throw new Refusal("lifecycle-loop-dependent-control");
                        if (value.Kind is "loop-test" or "collection-test" && (!op.StartsWith("brtrue", StringComparison.Ordinal) || destination >= i))
                            throw new Refusal("lifecycle-loop-test-shape");
                        if (truth == op.StartsWith("brtrue", StringComparison.Ordinal)) i = destination - 1;
                        continue;
                    }
                    if (op is "leave" or "leave.s")
                    {
                        var destination = Target(operand);
                        var handlers = regions.Where(r => r.Kind == "finally" && offset >= r.Start && offset < r.End
                            && (offsets[destination] < r.Start || offsets[destination] >= r.End))
                            .OrderBy(r => r.End - r.Start).ToArray();
                        stack.Clear();
                        if (handlers.Length == 0) i = destination - 1;
                        else
                        {
                            continuations.Push(destination);
                            for (var h = handlers.Length - 1; h > 0; h--) continuations.Push(Index(handlers[h].Handler));
                            i = Index(handlers[0].Handler) - 1;
                        }
                        continue;
                    }
                    if (op == "endfinally")
                    {
                        if (continuations.Count == 0 || stack.Count != 0) throw new Refusal("lifecycle-finally-flow-invalid");
                        i = continuations.Pop() - 1; continue;
                    }
                    if (op is "ldfld" or "stfld")
                    {
                        var parts = operand.Split(':', 4);
                        if (parts.Length != 4 || parts[1] != "field") throw new Refusal("lifecycle-field-invalid");
                        var identity = parts[3][(parts[3].IndexOf('|') + 1)..];
                        var value = op == "stfld" ? Pop() : Value.Unknown;
                        var instanceValue = Pop();
                        heap.TryGetValue(instanceValue.Text, out var receiverState);
                        FieldAccessFailure = new(At(body, offset, invocation), body.Observation.MetadataToken, op,
                            instanceValue.Kind, instanceValue.Kind == "object" ? instanceValue.Text : null,
                            receiverState?.Kind, receiverState is null ? null : Hash(receiverState.Owner),
                            Hash(meta.OwnerType), Hash(identity),
                            DiagnosticFieldType(identity),
                            receiverState is null ? null : receiverState.FieldOwners.Any(owner => identity.StartsWith("type:" + owner + "|member:", StringComparison.Ordinal)),
                            args.Length > 0 && args[0] == instanceValue, meta.Name == ".ctor",
                            op == "stfld" ? value.Kind : "not-a-store", body.Observation.ObjectBodyAgreement,
                            code.Skip(Math.Max(0, i - 7)).Take(Math.Min(7, i)).Select(line => line[2]).ToArray());
                        ObjectState instance;
                        try { instance = Get(instanceValue, "custom"); }
                        catch (Refusal error) { throw new Refusal(error.Message, MetadataFailure(body, "field-receiver")); }
                        if (!instance.FieldOwners.Any(owner => identity.StartsWith("type:" + owner + "|member:", StringComparison.Ordinal)))
                        {
                            OwnerFailure = new(At(body, offset, invocation), op, instanceValue.Text, Hash(instance.Owner),
                                Hash(meta.OwnerType), Hash(identity), instance.FieldOwners.Select(Hash).Order(StringComparer.Ordinal).ToArray(),
                                meta.Name == ".ctor", args.Length > 0 && args[0] == instanceValue,
                                identity.StartsWith("type:" + meta.OwnerType + "|member:", StringComparison.Ordinal),
                                op == "stfld" ? value.Kind : "not-a-store");
                            throw new Refusal("lifecycle-field-owner", MetadataFailure(body, "field-owner"));
                        }
                        if (op == "stfld")
                        {
                            if (opaqueFields.Contains(instanceValue.Text + ":" + identity))
                                throw new Refusal("lifecycle-opaque-field-mutated");
                            if (instance.Frozen) throw new Refusal("lifecycle-parameter-object-mutated");
                            if (loopEnumerator.Kind != "unknown" && !LoopAllocated(instanceValue)) throw new Refusal("lifecycle-loop-carried-field");
                            instance.Fields[identity] = value;
                        }
                        else stack.Add(instance.Fields.GetValueOrDefault(identity, Value.Unknown));
                        FieldAccessFailure = null;
                        continue;
                    }
                    if (op is "call" or "callvirt" or "newobj")
                    {
                        if (!calls.TryGetValue(offset, out var call) || call.StackShape is not { Supported: true } shape
                            || shape.ParameterCount > 16 || shape.ByReferenceParameters.Contains('1')) throw new Refusal("lifecycle-call-shape");
                        var values = new Value[shape.ParameterCount];
                        for (var arg = values.Length - 1; arg >= 0; arg--) values[arg] = Pop();
                        var receiver = op != "newobj" && shape.HasThis ? Pop() : Value.Unknown;
                        var site = At(body, offset, invocation);
                        Value returned;
                        var localCatch = regions.Where(r => r.Kind == "catch" && offset >= r.Start && offset < r.End)
                            .OrderBy(r => r.End - r.Start).FirstOrDefault();
                        if (localCatch is not null) catchHandlers.Push(At(body, localCatch.Handler, invocation));
                        try
                        {
                        var api = IlSqlObjectPathExtractor.KnownFrameworkApi(call.TargetIdentity)
                            ?? IlSqlLifecycleApis.Resolve(call.TargetIdentity, bodies.Select(b => b.Observation.ObjectBody?.OwnerType));
                        if (body.Observation.BodyIdentity == externalTraceRoot && depth == 0 && IlSqlTraceStringApis.Resolve(call.TargetIdentity) is { } stringRule)
                            returned = BuildTraceString(call, stringRule, receiver, values, site);
                        else if (api is not null)
                        {
                            var recordFailure = catchHandlers.Count > 0 && api is "fill" or "table-merge" or "row-write" or "list-add";
                            var before = recordFailure ? Snapshot() : [];
                            var beforeObjects = recordFailure ? ObjectsSnapshot() : [];
                            var inLoop = loopEnumerator.Kind != "unknown";
                            returned = Framework(api, receiver, values, site);
                            if (recordFailure)
                            {
                                if (failures.Count >= 256) throw new Refusal("lifecycle-failure-limit");
                                failures.Add(new(site, api, catchHandlers.Peek(), before, Snapshot(), true, inLoop,
                                    beforeObjects, ObjectsSnapshot()));
                            }
                        }
                        else
                        {
                            if (loopEnumerator.Kind != "unknown" && op != "newobj") throw new Refusal("lifecycle-loop-helper-effect");
                            if (!budget.TryConsume(bodies.Count)) throw new Refusal("lifecycle-work-limit");
                            var targets = bodies.Where(b => b.Observation.MethodIdentity == call.TargetIdentity
                                || b.Observation.ObjectBody?.MemberReference == call.TargetIdentity
                                || b.MemberReferenceAlias == call.TargetIdentity).Take(2).ToArray();
                            if (targets.Length != 1 && pendingOpaqueRejection is { } rejected
                                && rejected.ChainStart.Body == body.Observation.BodyIdentity
                                && rejected.ChainStart.Invocation == invocation
                                && offset >= rejected.ChainStart.Offset && offset <= rejected.RejectedCall.Site.Offset)
                            {
                                ReportedOpaqueRejection = rejected with { TriggerCall = new(site, Hash(call.TargetIdentity)) };
                                throw new Refusal("lifecycle-opaque-initializer-" + rejected.Reason
                                    + ":call-sha256:" + rejected.RejectedCall.SignatureSha256,
                                    MetadataFailure(body, "opaque-initializer-lookahead"));
                            }
                            if (targets.Length != 1) throw new Refusal(
                                (opaqueConstructor == body.Observation.BodyIdentity ? "lifecycle-opaque-initializer-" + opaqueRejection : "lifecycle-unknown-call")
                                    + ":" + call.TargetIdentity,
                                opaqueConstructor == body.Observation.BodyIdentity ? MetadataFailure(body, "opaque-initializer") : null);
                            var targetMeta = targets[0].Observation.ObjectBody
                                ?? throw new Refusal("lifecycle-body-unavailable", MetadataFailure(targets[0], "callee-metadata"));
                            if (targetMeta.ParameterCount != shape.ParameterCount || targetMeta.HasThis != shape.HasThis
                                || targetMeta.ReturnsValue != shape.ReturnsValue || op == "newobj" && targetMeta.Name != ".ctor")
                                throw new Refusal("lifecycle-callee-shape");
                            if (op == "newobj") receiver = New("custom", targetMeta.OwnerType);
                            else if (targetMeta.Name == ".ctor")
                                AdmitConstructorOwnerView(body, meta, targets[0], targetMeta, receiver, args, op, site);
                            returned = Invoke(targets[0], shape.HasThis ? new[] { receiver }.Concat(values).ToArray() : values, depth + 1);
                            if (op == "newobj") returned = receiver;
                        }
                        }
                        finally { if (localCatch is not null) _ = catchHandlers.Pop(); }
                        if (op == "newobj" || shape.ReturnsValue) stack.Add(returned);
                        continue;
                    }
                    if (op == "ret")
                    {
                        if (stack.Count != (meta.ReturnsValue ? 1 : 0)) throw new Refusal("lifecycle-return-stack");
                        return meta.ReturnsValue ? Pop() : Value.Unknown;
                    }
                    throw new Refusal("lifecycle-opcode:" + op);
                }
                throw new Refusal("lifecycle-no-return");
                int Index(long offset)
                {
                    var index = Array.IndexOf(offsets, offset);
                    return index >= 0 ? index : throw new Refusal("lifecycle-handler-offset-invalid");
                }
            }
            finally { active.Remove(body.Observation.MethodIdentity); }
        }

        private Value Framework(string api, Value receiver, Value[] args, Site site)
        {
            if (loopEnumerator.Kind != "unknown" && api is not ("enumerator-next" or "enumerator-current"
                or "rows" or "row" or "consumer" or "row-write" or "object-value" or "object-base" or "list-add"))
                throw new Refusal("lifecycle-loop-effect:" + api);
            switch (api)
            {
                case "arraylist-new": return New("arraylist");
                case "arraylist-add":
                    var collection = Get(receiver, "arraylist");
                    if (collection.Frozen || collection.Slots.Count >= 16) throw new Refusal("lifecycle-collection-mutated-or-limit");
                    Get(args[0], "custom").Frozen = true;
                    collection.Slots.Add(args[0]); return Int(collection.Slots.Count - 1);
                case "arraylist-enumerator":
                    collection = Get(receiver, "arraylist");
                    if (collection.Frozen) throw new Refusal("lifecycle-collection-enumerated-twice");
                    collection.Frozen = true;
                    foreach (var item in collection.Slots) Get(item, "custom").Frozen = true;
                    return View("collection-enumerator", receiver);
                case "enumerator":
                    var rows = Get(receiver, "rows");
                    if (Get(rows.Parent, "table").Empty) throw new Refusal("lifecycle-enumerate-empty-table");
                    return View("enumerator", rows.Parent);
                case "enumerator-next":
                    if (Get(receiver, "enumerator", "collection-enumerator").Kind == "collection-enumerator")
                    {
                        var concrete = Get(receiver, "collection-enumerator");
                        collection = Get(concrete.Parent, "arraylist");
                        if (concrete.Moves > collection.Slots.Count) throw new Refusal("lifecycle-collection-enumerator-reused");
                        return new("collection-test", concrete.Moves++ < collection.Slots.Count ? "1" : "0");
                    }
                    var enumerator = Get(receiver, "enumerator");
                    if (enumerator.Moves > 1) throw new Refusal("lifecycle-enumerator-reused");
                    if (enumerator.Moves++ == 0)
                    {
                        if (loopEnumerator.Kind != "unknown") throw new Refusal("lifecycle-nested-enumerator");
                        loopEnumerator = receiver; loopAllocationStart = heap.Count;
                        loops.Add(new(site, enumerator.Parent.Text, "per-existing-row-on-normal-completion;row-count-unproven"));
                        return new("loop-test", "1");
                    }
                    if (loopEnumerator != receiver) throw new Refusal("lifecycle-enumerator-order");
                    loopEnumerator = Value.Unknown; loopRow = Value.Unknown;
                    return new("loop-test", "0");
                case "enumerator-current":
                    if (Get(receiver, "enumerator", "collection-enumerator").Kind == "collection-enumerator")
                    {
                        var concrete = Get(receiver, "collection-enumerator");
                        collection = Get(concrete.Parent, "arraylist");
                        if (concrete.Moves < 1 || concrete.Moves > collection.Slots.Count) throw new Refusal("lifecycle-collection-position");
                        return collection.Slots[concrete.Moves - 1];
                    }
                    enumerator = Get(receiver, "enumerator");
                    if (enumerator.Moves != 1 || loopEnumerator != receiver || loopRow.Kind != "unknown")
                        throw new Refusal("lifecycle-enumerator-position");
                    loopRow = View("row", enumerator.Parent); return loopRow;
                case "enumerator-dispose": _ = Get(receiver, "enumerator"); return Value.Unknown;
                case "row-write":
                    if (loopEnumerator.Kind == "unknown" || receiver != loopRow) throw new Refusal("lifecycle-row-write-scope");
                    var writeParent = Get(receiver, "row").Parent;
                    var writeTable = Get(writeParent, "table"); var writeColumn = Text(args[0]);
                    if (args[1].Kind != "read" || !int.TryParse(args[1].Text, out var readIndex)
                        || readIndex < 0 || readIndex >= reads.Count) throw new Refusal("lifecycle-write-value-unresolved");
                    writeTable.Writes[writeColumn] = reads[readIndex].Value with { Assignment = site };
                    return Value.Unknown;
                case "list-new": return New("list");
                case "list-clear": Get(receiver, "list").Slots.Clear(); return Value.Unknown;
                case "list-add":
                    var list = Get(receiver, "list");
                    if (list.Slots.Count >= 16) throw new Refusal("lifecycle-list-limit");
                    if (loopEnumerator.Kind != "unknown" && !LoopAllocated(args[0])) throw new Refusal("lifecycle-loop-shared-projection");
                    _ = Get(args[0], "custom"); list.Slots.Add(args[0]); return Value.Unknown;
                case "list-item":
                    list = Get(receiver, "list"); var listIndex = Number(args[0]);
                    if (listIndex < 0 || listIndex >= list.Slots.Count) throw new Refusal("lifecycle-list-index");
                    return list.Slots[listIndex];
                case "object-value":
                    if (args[0].Kind is not ("read" or "int" or "string" or "null" or "opaque-initializer" or "computed-string")) throw new Refusal("lifecycle-object-copy-unresolved");
                    return args[0];
                case "type-handle":
                    if (args[0].Kind != "type-token") throw new Refusal("lifecycle-type-handle-unresolved");
                    return new("type", args[0].Text);
                case "table-new":
                    var created = New("table"); heap[created.Text].Command = new("string", Text(args[0])); return created;
                case "columns": _ = Get(receiver, "table"); return View("columns", receiver);
                case "column-add":
                    var columnTable = Get(Get(receiver, "columns").Parent, "table");
                    if (columnTable.Execution is not null || !columnTable.Empty || args[1].Kind != "type"
                        || columnTable.Columns.Count >= 64 || !columnTable.Columns.Add(Text(args[0]))) throw new Refusal("lifecycle-column-declaration");
                    return New("column");
                case "rows-add":
                    var rowParent = Get(receiver, "rows").Parent;
                    var rowTable = Get(rowParent, "table"); var array = Get(args[0], "array");
                    if (!rowTable.Empty || rowTable.Execution is not null || array.Slots.Count != rowTable.Columns.Count
                        || array.Slots.Any(v => v.Kind is not ("int" or "string" or "null"))) throw new Refusal("lifecycle-application-row-shape");
                    rowTable.Empty = false; return View("row", rowParent);
                case "table-clear":
                    var cleared = Get(receiver, "table"); cleared.Empty = true; cleared.Writes.Clear(); return Value.Unknown;
                case "object-base": _ = Get(receiver, "custom"); return Value.Unknown;
                case "dataset": return New("dataset");
                case "command":
                    var command = New("command"); heap[command.Text].Command = new("string", Text(args[0])); return command;
                case "command-type": Get(receiver, "command").Type = args[0]; return Value.Unknown;
                case "parameters": _ = Get(receiver, "command"); return View("parameters", receiver);
                case "parameter":
                    var parameter = New("parameter");
                    heap[parameter.Text].Command = new("string", Text(args[0]));
                    heap[parameter.Text].Type = Int(Number(args[1]));
                    heap[parameter.Text].Slots.Add(Int(Number(args[2])));
                    return parameter;
                case "parameter-value":
                    var valuedParameter = Get(receiver, "parameter");
                    if (valuedParameter.Attached) throw new Refusal("lifecycle-parameter-mutated-after-attachment");
                    valuedParameter.Fields["parameter-value"] = args[0]; return Value.Unknown;
                case "add":
                    var attachedCommand = Get(Get(receiver, "parameters").Parent, "command");
                    var attachedParameter = Get(args[0], "parameter");
                    if (attachedCommand.Attached || attachedParameter.Attached || attachedCommand.Slots.Count >= 16)
                        throw new Refusal("lifecycle-parameter-reused-or-limit");
                    attachedParameter.Attached = true; attachedCommand.Slots.Add(args[0]); return args[0];
                case "adapter": _ = Get(args[0], "command"); return View("adapter", args[0]);
                case "fill":
                    var cmd = Get(Get(receiver, "adapter").Parent, "command"); var set = Get(args[0], "dataset");
                    if (Number(cmd.Type) != 4 || cmd.Attached || set.Slots.Count != 0 || executions.Count >= 8) throw new Refusal("lifecycle-execution-shape");
                    cmd.Attached = true;
                    var execution = "execution:" + executions.Count.ToString(CultureInfo.InvariantCulture);
                    cmd.Execution = execution;
                    executions.Add(new(execution, Text(cmd.Command), site));
                    var table = New("table"); var state = heap[table.Text];
                    state.Parent = args[0]; state.Execution = execution; state.SchemaExecutions.Add(execution); state.Empty = false;
                    set.Slots.Add(table); return Int(0);
                case "tables": _ = Get(receiver, "dataset"); return View("tables", receiver);
                case "table-add":
                    set = Get(Get(receiver, "tables").Parent, "dataset");
                    if (set.Slots.Count >= 16) throw new Refusal("lifecycle-table-limit");
                    if (set.Slots.Any(slot => Get(slot, "table").Command.Text == Text(args[0])))
                        throw new Refusal("lifecycle-duplicate-table-name");
                    table = New("table"); heap[table.Text].Command = new("string", Text(args[0])); set.Slots.Add(table); return table;
                case "table":
                    set = Get(Get(receiver, "tables").Parent, "dataset"); var index = Number(args[0]);
                    if (index < 0 || index >= set.Slots.Count) throw new Refusal("lifecycle-table-index");
                    return set.Slots[index];
                case "table-merge":
                    var target = Get(receiver, "table"); var source = Get(args[0], "table");
                    if (!target.Empty || source.Execution is null) throw new Refusal("lifecycle-merge-existing-or-unknown");
                    target.Execution = source.Execution; target.SchemaExecutions.AddRange(source.SchemaExecutions);
                    target.Empty = false; target.Writes.Clear(); return Value.Unknown;
                case "rows": _ = Get(receiver, "table"); return View("rows", receiver);
                case "row":
                    if (Number(args[0]) != 0) throw new Refusal("lifecycle-row-index");
                    var rowSource = Get(receiver, "rows").Parent;
                    if (Get(rowSource, "table").Empty) throw new Refusal("lifecycle-read-cleared-table");
                    if (loopEnumerator.Kind != "unknown" && rowSource == Get(loopEnumerator, "enumerator").Parent)
                        throw new Refusal("lifecycle-cross-row-read");
                    return View("row", rowSource);
                case "consumer":
                    var parent = Get(receiver, "row").Parent; state = Get(parent, "table"); var column = Text(args[0]);
                    var origin = state.Writes.GetValueOrDefault(column,
                        new(state.Execution is null ? "application" : "sql", parent.Text, state.Execution, column));
                    if (state.Execution is null && !state.Columns.Contains(column)) throw new Refusal("lifecycle-application-column-unknown");
                    reads.Add(new(site, parent.Text, column, state.SchemaExecutions.Distinct(StringComparer.Ordinal).ToArray(), origin));
                    return new("read", (reads.Count - 1).ToString(CultureInfo.InvariantCulture));
                default: throw new Refusal("lifecycle-framework-pending:" + api);
            }
        }
        private bool LoopAllocated(Value value)
            => value.Kind == "object" && value.Text.StartsWith("allocation:", StringComparison.Ordinal)
                && int.TryParse(value.Text[11..], NumberStyles.None, CultureInfo.InvariantCulture, out var allocation)
                && allocation >= loopAllocationStart;
        private TableState[] Snapshot()
            => heap.Where(pair => pair.Value.Kind == "table").Select(pair => new TableState(pair.Key,
                pair.Value.Empty, pair.Value.Execution, pair.Value.SchemaExecutions.ToArray(),
                new Dictionary<string, Origin>(pair.Value.Writes, StringComparer.Ordinal))).ToArray();
        private ObjectSnapshot[] ObjectsSnapshot()
            => heap.Where(pair => pair.Value.Kind is "custom" or "dataset" or "list").Select(pair => new ObjectSnapshot(pair.Key,
                pair.Value.Kind, pair.Value.Slots.Select(v => v.Kind + ":" + v.Text).ToArray(),
                pair.Value.Fields.ToDictionary(f => f.Key, f => f.Value.Kind + ":" + f.Value.Text, StringComparer.Ordinal))).ToArray();
        private static bool Slot(string op, string operand, string prefix, out int slot)
        {
            slot = -1;
            if (op == prefix || op == prefix + ".s") return operand.StartsWith("v:", StringComparison.Ordinal) && int.TryParse(operand[2..], out slot) && slot >= 0;
            return op.StartsWith(prefix + ".", StringComparison.Ordinal) && int.TryParse(op[(prefix.Length + 1)..], out slot) && slot >= 0;
        }
    }

    private static long Offset(string value)
        => long.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset) && offset >= 0
            ? offset : throw new Refusal("lifecycle-invalid-offset");

    private static Region ParseRegion(string text)
    {
        var parts = text.Split(':', 10);
        if (parts.Length != 10 || parts[0] != "kind" || parts[2] != "try" || parts[4] != "handler"
            || parts[6] != "filter" || parts[7] != "-1" || parts[8] != "catch"
            || parts[1] is not ("finally" or "catch")) throw new Refusal("lifecycle-handler-shape");
        (long Start, long End) Range(string value)
        {
            var range = value.Split('+');
            if (range.Length != 2 || !long.TryParse(range[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var start)
                || !long.TryParse(range[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var length)
                || start < 0 || length <= 0 || start > long.MaxValue - length) throw new Refusal("lifecycle-handler-range");
            return (start, start + length);
        }
        var body = Range(parts[3]); var handler = Range(parts[5]);
        return new(parts[1], body.Start, body.End, handler.Start, handler.End);
    }
}
