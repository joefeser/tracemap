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
    internal const string StateAlternativesSchema = "il-sql-lifecycle-state-alternatives/1";
    internal const string StateControlPolicy = "explicit-sql-state-control/1";
    /// <summary>Bound on completed state-alternative paths and distinct decision sites.</summary>
    internal const int MaxStatePaths = 128, MaxStateSites = 12;
    internal const string Limitation = "Tier3 conditional encoded static lineage candidate. Successful normal-completion paths only; symbolic existing-row summaries do not prove rows or execution. SQL schema ancestry is separate from current application-written value origin. Catch continuations are not executed; failure boundaries permit partial updates and make no rollback claim. ArrayList support is exact signed net48, single complete enumeration of at most 16 inspected custom objects frozen on Add; unsupported mutation or escape is unknown. Entry producers/loaders retain unbound argument origins; unknown control or required scalar values refuse. Entry facts are separate from column-read comparison facts. Opt-in opaque root scalar initialization uses a separate entry schema, conditional normal-return and tracked-heap noninterference assumptions, and unmodeled external effects; it is not constructor-completion or purity proof. Opt-in inspected InfoMessage callbacks retain external-event dependence and partial post-Fill result naming under a separate schema; callback execution, names, table count and naming success remain unproven. Runtime connection State is never replaced by an enum constant: state-dependent control produces bounded explicit path alternatives with per-alternative identities, never a silently selected path or a merge of incompatible attachments; separate State reads are separate decisions; allocation identity is shared only on the prefix before the first decision. Uncaught exceptional paths terminate their alternative without executing catch or finally continuations. No source, deployment, session, framework, type, nullability, value or migration proof.";
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
    internal sealed record ParameterOutput(string Allocation, string NameHash, int SqlDbType, int? Size, string ValueOrigin);
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
            + (InfoMessages is { Count: > 0 } ? ";info-message-and-naming-normal-return-assumed;info-message-event-timing-unproven;callback-values-unbound;external-effects-unmodeled;result-names-unbound" : "")
            + (ConnectionStateReads is { Count: > 0 } ? ";connection-state-values-unbound;runtime-state-control-unproven" : "")
            + (StateAlternatives is { Count: > 0 } ? ";state-conditional-alternatives;connection-state-values-unbound;runtime-state-control-unproven;no-path-selection;per-alternative-allocation-and-execution-identity;separate-reads-separate-decisions;exceptional-paths-terminate-alternative" : "")
            + (UnboundStringCount is > 0 ? ";computed-string-values-unbound" : "")
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
        public ObjectOriginFailure? ObjectOriginFailure { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public NumberFailure? NumberFailure { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public AddressFailure? AddressFailure { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public PathLimitFailure? PathLimitFailure { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public BodyMetadataFailure? MetadataFailure { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public FieldOwnerRejection? FieldOwnerFailure { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public FieldAccessRejection? FieldAccessFailure { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public ExternalTraceRejection? ExternalTraceFailure { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public OpcodeRejection? OpcodeFailure { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public OpaqueChainRejection? OpaqueInitializerRejection { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<ConstructorOwnerView>? ConstructorOwnerViews { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<InfoMessageReceipt>? InfoMessages { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<ConnectionStateRead>? ConnectionStateReads { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<StateAlternative>? StateAlternatives { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public int UnboundStringCount { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<ResultNamingReceipt>? ResultNaming { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public InfoMessageRejection? InfoMessageFailure { get; init; }
    }
    internal sealed record InvocationFrame(Site Entry, string MetadataToken, Site? Caller);
    internal sealed record OpcodeRejection(BodyMetadataFailure SelectedRoot, Site Site, string MetadataToken,
        string Opcode, int RetainedInstructionCount, IReadOnlyList<InvocationFrame> InvocationPath,
        string Limitation = "actual-interpreter-invocations;not-whole-program-runtime-reachability;no-opcode-bypass");
    internal sealed record BodyMetadataFailure(string Stage, string Body, string Binary, string MetadataToken,
        int InstructionCount, IlObjectBodyRetention? Retention, IlObjectBodyAgreement? Agreement,
        int? AdmittedConstructorMatches = null, int? UnavailableBodiesInModule = null);
    private static BodyMetadataFailure MetadataFailure(Body body, string stage,
        int? constructorMatches = null, int? unavailableInModule = null)
        => new(stage, body.Observation.BodyIdentity, body.Input.RawFileSha256!, body.Observation.MetadataToken,
            body.Observation.InstructionCount, body.Observation.ObjectBodyRetention, body.Observation.ObjectBodyAgreement,
            constructorMatches, unavailableInModule);
    internal sealed record GuardedRun(string Predicate, bool Value, Result Result);

    /// <summary>One resolved unbound-state comparison. Never an observed enum value.</summary>
    /// <summary>Diagnostic attached to object-origin refusals: the kind actually found and the call in flight. The refusal message itself stays stable.</summary>
    internal sealed record ObjectOriginFailure(string Found, string Expected, string? CallIdentity, string? BodyIdentity, long Offset);
    /// <summary>Diagnostic attached to integer refusals: the kind found and the call in flight. The refusal message stays stable.</summary>
    internal sealed record NumberFailure(string Found, string? CallIdentity, string? BodyIdentity, long Offset);
    /// <summary>Diagnostic attached to stale-address refusals: the address identity and the call in flight.</summary>
    internal sealed record AddressFailure(string Address, string? CallIdentity, string? BodyIdentity, long Offset);
    /// <summary>Diagnostic attached to path-limit refusals: the run counters and the fork census that consumed the budget.</summary>
    internal sealed record PathLimitFailure(int Runs, int Alternatives, int Queued, int VisitedSites,
        IReadOnlyList<string> ForkCensus);

    internal sealed record StateDecision(string Policy, Site Site, string Connection, string Constant,
        string Comparison, bool EqualAssumed, int Occurrence,
        string Limitation = "hypothetical-path-assumption;runtime-state-value-unbound;no-enum-value-or-transition-observed");
    internal sealed record StateAlternative(string Policy, IReadOnlyList<StateDecision> Decisions, string Outcome,
        Site? Termination = null, string? TerminationLimitation = null, Result? Completion = null);

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
    private sealed record StateDecisionKey(string Body, long Offset, int Invocation, int Occurrence);
    /// <summary>Raised at an undecided unbound-state branch so the driver can explore both outcomes.</summary>
    private sealed class StateForkRequired(StateDecision pending, StateDecisionKey key) : Exception("lifecycle-state-fork")
    {
        internal StateDecision Pending { get; } = pending;
        internal StateDecisionKey Key { get; } = key;
    }
    /// <summary>An uncaught throw terminated this alternative; catch/finally continuations stay unexecuted.</summary>
    private sealed class StateTerminated(Site site, string limitation) : Exception("lifecycle-state-terminated")
    {
        internal Site Site { get; } = site;
        internal string Limitation { get; } = limitation;
    }

    internal static Result Extract(Body root, IReadOnlyList<Body> bodies, bool enabled,
        IlBodyEvidenceExtractor.IlWorkBudget budget, bool opaqueRootInitializers = false, bool externalTraceBoundary = false, bool compiledScalarBoundary = false, bool infoMessageBoundary = false)
    {
        // Bounded path alternatives: an unbound runtime-state comparison never picks a
        // side. The first run uses no assumptions and behaves exactly like a state-free
        // extraction; each undecided state branch expands the work list by both
        // outcomes, and every completed alternative keeps its own identities.
        var visited = new HashSet<StateDecisionKey>();
        var census = new Dictionary<string, int>();
        var queue = new Queue<Dictionary<StateDecisionKey, bool>>();
        queue.Enqueue([]);
        var alternatives = new List<StateAlternative>();
        var runs = 0;
        while (queue.Count > 0)
        {
            var assignment = queue.Dequeue();
            var machine = new Machine(bodies, budget, opaqueRootInitializers, externalTraceBoundary,
                compiledScalarBoundary, infoMessageBoundary, assignment, assignment.Count > 0);
            runs++;
            try
            {
                var result = machine.Run(root, enabled);
                if (assignment.Count == 0) return result;
                alternatives.Add(new(StateControlPolicy, machine.RecordedDecisions.ToArray(), machine.EntryOutcome,
                    null, null, result));
            }
            catch (StateForkRequired fork)
            {
                // Forks extend the CURRENT assignment: a key forked in one subtree is
                // legitimately re-forked in a sibling subtree (both sides completing).
                // Only a re-fork of a key already decided on this path is invalid.
                if (assignment.ContainsKey(fork.Key)) return FailureResult(machine, new Refusal("lifecycle-state-refork-invalid"));
                if (!visited.Add(fork.Key)) { }
                var censusKey = fork.Pending.Connection + "/" + fork.Pending.Constant;
                census[censusKey] = census.GetValueOrDefault(censusKey) + 1;
                if (visited.Count > MaxStateSites || alternatives.Count + queue.Count + 2 > MaxStatePaths || runs >= MaxStatePaths * 2)
                {
                    machine.PendingPathLimitFailure = new PathLimitFailure(runs, alternatives.Count, queue.Count, visited.Count,
                        census.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal)
                            .Take(16).Select(pair => pair.Key + " x" + pair.Value).ToArray());
                    return FailureResult(machine, new Refusal("lifecycle-state-path-limit"));
                }
                var assumeUnequal = new Dictionary<StateDecisionKey, bool>(assignment) { [fork.Key] = false };
                var assumeEqual = new Dictionary<StateDecisionKey, bool>(assignment) { [fork.Key] = true };
                queue.Enqueue(assumeUnequal);
                queue.Enqueue(assumeEqual);
            }
            catch (StateTerminated terminated)
            {
                alternatives.Add(new(StateControlPolicy, machine.RecordedDecisions.ToArray(), "exceptional-termination",
                    terminated.Site, terminated.Limitation, null));
            }
            catch (Refusal error) { return FailureResult(machine, error); }
        }
        return new Result([], [], null, []) { StateAlternatives = alternatives.ToArray() };
    }

    private static Result FailureResult(Machine machine, Refusal error)
        => new([], [], null, [error.Message])
        {
            MetadataFailure = error.MetadataFailure,
            OpaqueInitializers = machine.Initializers.Count > 0 ? machine.Initializers.ToArray() : null,
            ExternalTraces = machine.TraceBoundaries.Count > 0 ? machine.TraceBoundaries.ToArray() : null,
            TraceStringComputations = machine.StringComputations.Count > 0 ? machine.StringComputations.ToArray() : null,
            FieldOwnerFailure = machine.OwnerFailure,
            FieldAccessFailure = machine.FieldAccessFailure,
            ExternalTraceFailure = machine.TraceFailure,
            OpcodeFailure = machine.OpcodeFailure,
            ObjectOriginFailure = machine.PendingObjectOriginFailure,
            NumberFailure = machine.PendingNumberFailure,
            AddressFailure = machine.PendingAddressFailure,
            PathLimitFailure = machine.PendingPathLimitFailure,
            OpaqueInitializerRejection = machine.ReportedOpaqueRejection,
            ConnectionStateReads = machine.StateReads.Count > 0 ? machine.StateReads.ToArray() : null,
            InfoMessages = machine.InfoReceipts.Count > 0 ? machine.InfoReceipts.ToArray() : null,
            ResultNaming = machine.NamingReceipts.Count > 0 ? machine.NamingReceipts.ToArray() : null, InfoMessageFailure = machine.InfoFailure,
            ConstructorOwnerViews = machine.OwnerViews.Count > 0 ? machine.OwnerViews.ToArray() : null,
            UnboundStringCount = machine.UnboundStrings
        };

    private sealed partial class Machine(IReadOnlyList<Body> bodies, IlBodyEvidenceExtractor.IlWorkBudget budget, bool opaqueRootInitializers, bool externalTraceBoundary, bool compiledScalarBoundary, bool infoMessageBoundary, IReadOnlyDictionary<StateDecisionKey, bool>? forcedDecisions = null, bool relaxedEntry = false)
    {
        private readonly IReadOnlyDictionary<StateDecisionKey, bool> forcedDecisions = forcedDecisions ?? new Dictionary<StateDecisionKey, bool>();
        internal readonly bool RelaxedEntry = relaxedEntry;
        internal string EntryOutcome = "normal-completion";
        private bool abandonedLoop;
        internal readonly List<StateDecision> RecordedDecisions = [];
        internal int UnboundStrings;
        private readonly Dictionary<long, Dictionary<int, Value>> frameLocals = new();
        private Value UnboundString(string origin)
        {
            UnboundStrings++;
            return new("computed-string", origin);
        }
        // Local addresses (ECMA-335 III.3.42 ldloca) alias one local slot of a live
        // ancestor frame; they never reach the heap, a framework call, or a return.
        internal AddressFailure? PendingAddressFailure;
        private Value ReadLocal(Value address)
        {
            var separator = address.Text.IndexOf(':');
            if (address.Kind != "local-address" || separator < 0
                || !long.TryParse(address.Text.AsSpan(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out var frameId)
                || !int.TryParse(address.Text.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var slot)
                || !frameLocals.TryGetValue(frameId, out var frame))
            {
                PendingAddressFailure = new(address.Text, inFlightCall?.Identity, inFlightCall?.Body, inFlightCall?.Offset ?? -1);
                throw new Refusal("lifecycle-address-frame-unavailable");
            }
            return frame.GetValueOrDefault(slot, Value.Unknown);
        }
        private Value Deref(Value value)
        {
            if (value.Kind == "local-address") return ReadLocal(value);
            if (value.Kind == "field-address")
            {
                var separator = value.Text.IndexOf('#');
                var allocation = value.Text[..separator];
                var fieldIdentity = value.Text[(separator + 1)..];
                if (!heap.TryGetValue(allocation, out var owner)) throw new Refusal("lifecycle-address-frame-unavailable");
                return owner.Fields.GetValueOrDefault(fieldIdentity, Value.Unknown);
            }
            return value;
        }
        private void WriteLocal(Value address, Value value)
        {
            var separator = address.Text.IndexOf(':');
            if (!long.TryParse(address.Text.AsSpan(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out var frameId)
                || !int.TryParse(address.Text.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var slot)
                || !frameLocals.TryGetValue(frameId, out var frame))
                throw new Refusal("lifecycle-address-frame-unavailable");
            frame[slot] = value;
        }
        private readonly List<(StateDecision Template, StateDecisionKey Key)> stateTests = [];
        private readonly Dictionary<(string Body, long Offset, int Invocation), int> stateOccurrences = new();
        private readonly Dictionary<string, ObjectState> heap = new(StringComparer.Ordinal);
        private readonly List<Execution> executions = [];
        private readonly List<Read> reads = [];
        private readonly List<Loop> loops = [];
        private readonly List<Failure> failures = [];
        private readonly List<Site> invokedBodies = [];
        private readonly List<InvocationFrame> invocationPath = [];
        private BodyMetadataFailure? selectedRootMetadata;
        internal OpcodeRejection? OpcodeFailure;
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
        internal ObjectOriginFailure? PendingObjectOriginFailure;
        private (string Identity, string Body, long Offset)? inFlightCall;
        private ObjectState Get(Value value, params string[] kinds)
        {
            if (value.Kind != "object" || !heap.TryGetValue(value.Text, out var state)
                || !kinds.Contains(state.Kind, StringComparer.Ordinal))
            {
                // The refusal message stays byte-stable (exact-match catch lists depend
                // on it); the found kind and the in-flight call ride as a diagnostic.
                PendingObjectOriginFailure = new(OperandKind(value), string.Join('/', kinds),
                    inFlightCall?.Identity, inFlightCall?.Body, inFlightCall?.Offset ?? -1);
                throw new Refusal("lifecycle-object-origin:" + string.Join('/', kinds));
            }
            return state;
        }
        private Value View(string kind, Value parent)
        { var result = New(kind); heap[result.Text].Parent = parent; return result; }
        internal NumberFailure? PendingNumberFailure;
        internal PathLimitFailure? PendingPathLimitFailure;
        private static bool CompareBranch(string op, int left, int right)
        {
            var name = op.Split('.')[0];
            var unsigned = op.EndsWith(".un", StringComparison.Ordinal);
            return (name, unsigned) switch
            {
                ("blt", false) => left < right, ("blt", true) => (uint)left < (uint)right,
                ("ble", false) => left <= right, ("ble", true) => (uint)left <= (uint)right,
                ("bgt", false) => left > right, ("bgt", true) => (uint)left > (uint)right,
                ("bge", false) => left >= right, ("bge", true) => (uint)left >= (uint)right,
                _ => false
            };
        }
        private int Number(Value value)
        {
            if (value.Kind == "int" && int.TryParse(value.Text, out var number)) return number;
            PendingNumberFailure = new(OperandKind(value), inFlightCall?.Identity, inFlightCall?.Body, inFlightCall?.Offset ?? -1);
            throw new Refusal("lifecycle-integer-unresolved");
        }
        private Value Equal(Value left, Value right)
        {
            if (left.Kind == "int" && right.Kind == "int") return Int(Number(left) == Number(right) ? 1 : 0);
            if (left.Kind == "null" && right.Kind == "null") return Int(1);
            // Only concrete allocations prove non-aliasing. Views with distinct
            // symbolic IDs may still denote the same runtime reference.
            bool Fresh(Value value) => value.Kind == "object" && heap.TryGetValue(value.Text, out var state)
                && state.Kind is "custom" or "command" or "parameter" or "adapter" or "dataset" or "sql-connection"
                    or "array" or "arraylist" or "list";
            if (left.Kind == "object" && right.Kind == "object")
            {
                if (left.Text == right.Text && heap.ContainsKey(left.Text)) return Int(1);
                if (Fresh(left) && Fresh(right)) return Int(0);
            }
            if (left.Kind == "null" && (Fresh(right) || right.Kind is ("string" or "computed-int" or "dbnull"))
                || right.Kind == "null" && (Fresh(left) || left.Kind is ("string" or "computed-int" or "dbnull"))) return Int(0); // a boxed value type or the DBNull constant is never null
            // No inferred value, reference identity, nullness or string-content
            // equality for inputs, opaque values, views or unknowns.
            return Value.Unknown;
        }
        private static string Text(Value value)
            => value.Kind == "string" ? value.Text : throw new Refusal("lifecycle-string-unresolved");
        private static Value Int(int value) => new("int", value.ToString(CultureInfo.InvariantCulture));
        private string OperandKind(Value value)
            => value.Kind == "object" && heap.TryGetValue(value.Text, out var state) ? "object-" + state.Kind : value.Kind;
        private readonly Dictionary<string, int> inputNullOccurrences = new();
        private readonly Dictionary<string, int> stringCompareOccurrences = new();
        private static string ProvenanceOf(Value value)
            => value.Kind == "input" ? "input:" + value.Text
             : value.Kind == "object" ? value.Text
             : value.Kind is ("computed-string" or "computed-int") ? "computed:" + value.Text
             : "site";
        private Value NewUnboundIntCompareTest(Value unbound, string comparison, Site site)
        {
            // One unbound integer's relation to a compiled zero/one constant is one
            // runtime fact, keyed by the value's provenance.
            var provenance = ProvenanceOf(unbound);
            var occurrence = stringCompareOccurrences.GetValueOrDefault("int:" + provenance);
            stringCompareOccurrences["int:" + provenance] = occurrence + 1;
            var key = new StateDecisionKey("int-compare:" + provenance + ":" + comparison, 0, 0, occurrence);
            stateTests.Add((new StateDecision(StateControlPolicy, site, "int-compare:" + provenance, comparison, "equal-constant", false, occurrence,
                "hypothetical-unbound-integer-relation;runtime-value-unbound;no-value-observed"), key));
            return new("state-test", (stateTests.Count - 1).ToString(CultureInfo.InvariantCulture));
        }
        private Value NewStringCompareTest(Value left, Site site)
        {
            // Keyed by the LEFT operand's provenance: one runtime value's string
            // equality is one fact, shared wherever the same value is compared.
            var provenance = ProvenanceOf(left);
            var occurrence = stringCompareOccurrences.GetValueOrDefault(provenance);
            stringCompareOccurrences[provenance] = occurrence + 1;
            var key = new StateDecisionKey("string-compare:" + provenance, 0, 0, occurrence);
            stateTests.Add((new StateDecision(StateControlPolicy, site, "string-compare:" + provenance, "0", "equal-constant", false, occurrence,
                "hypothetical-string-equality;runtime-value-unbound;no-string-content-observed"), key));
            return new("state-test", (stateTests.Count - 1).ToString(CultureInfo.InvariantCulture));
        }
        private Value NewInputBoolTest(Value input, Site site)
        {
            var occurrence = inputNullOccurrences.GetValueOrDefault("bool:" + input.Text);
            inputNullOccurrences["bool:" + input.Text] = occurrence + 1;
            var key = new StateDecisionKey("input-bool:" + input.DeclaredType + ":" + input.Text, 0, 0, occurrence);
            stateTests.Add((new StateDecision(StateControlPolicy, site, "input:" + input.Text, "true", "equal-constant", false, occurrence,
                "hypothetical-input-truth;runtime-value-unbound;no-boolean-observed"), key));
            return new("state-test", (stateTests.Count - 1).ToString(CultureInfo.InvariantCulture), input.DeclaredType);
        }
        private Value TestNotNull(Value value, Site site)
        {
            bool NonNull(Value v) => v.Kind == "object" && heap.TryGetValue(v.Text, out var state)
                && state.Kind is ("custom" or "command" or "parameter" or "adapter" or "dataset" or "sql-connection"
                    or "array" or "arraylist" or "list");
            if (NonNull(value) || value.Kind is ("string" or "computed-int" or "dbnull")) return Int(0);
            if (value.Kind is ("input" or "computed-string")) return NegateStateTest(NewInputNullTest(value, site));
            throw new Refusal("lifecycle-guard-unresolved");
        }
        private Value NewInputNullTest(Value input, Site site)
        {
            // An input argument's nullness is one runtime fact: the decision is keyed
            // by the input identity so every guard on the same input shares one fork.
            var occurrence = inputNullOccurrences.GetValueOrDefault(input.Text);
            inputNullOccurrences[input.Text] = occurrence + 1;
            var key = new StateDecisionKey("input-null:" + input.DeclaredType + ":" + input.Text, 0, 0, occurrence);
            stateTests.Add((new StateDecision(StateControlPolicy, site, "input:" + input.Text, "null", "equal-constant", false, occurrence,
                "hypothetical-input-nullness;runtime-value-unbound;no-null-or-value-observed"), key));
            return new("state-test", (stateTests.Count - 1).ToString(CultureInfo.InvariantCulture), input.DeclaredType);
        }
        private Value NewStateTest(Value unbound, Value constant, Site site, bool negated)
        {
            var occurrence = stateOccurrences.GetValueOrDefault((site.Body, site.Offset, site.Invocation));
            stateOccurrences[(site.Body, site.Offset, site.Invocation)] = occurrence + 1;
            var key = new StateDecisionKey(site.Body, site.Offset, site.Invocation, occurrence);
            stateTests.Add((new StateDecision(StateControlPolicy, site, unbound.Text, constant.Text,
                negated ? "not-equal-constant" : "equal-constant", false, occurrence), key));
            return new("state-test", (stateTests.Count - 1).ToString(CultureInfo.InvariantCulture), unbound.DeclaredType);
        }
        private Value NegateStateTest(Value test)
        {
            var (template, key) = stateTests[int.Parse(test.Text, CultureInfo.InvariantCulture)];
            stateTests.Add((template with
            { Comparison = template.Comparison == "equal-constant" ? "not-equal-constant" : "equal-constant" }, key));
            return new("state-test", (stateTests.Count - 1).ToString(CultureInfo.InvariantCulture), test.DeclaredType);
        }
        private Refusal StateEqualityFailure(Body body, IlObjectBody meta, long offset, int invocation, string op, string kinds)
        {
            OpcodeFailure = new(selectedRootMetadata!, At(body, offset, invocation), body.Observation.MetadataToken,
                op, meta.Instructions.Count, invocationPath.ToArray());
            return new Refusal("lifecycle-equality-unresolved:" + kinds, MetadataFailure(body, "equality"));
        }
        private static Site At(Body body, long offset, int invocation)
            => new(body.Observation.BodyIdentity, body.Input.RawFileSha256!, offset, invocation);

        internal Result Run(Body root, bool enabled)
        {
            if (bodies.Count > 4096 || !budget.TryConsume(bodies.Count)) throw new Refusal("lifecycle-index-limit");
            if (!bodies.Contains(root) || bodies.GroupBy(b => b.Observation.MethodIdentity, StringComparer.Ordinal).Any(g => g.Count() != 1))
                throw new Refusal("lifecycle-ambiguous-input");
            selectedRootMetadata = MetadataFailure(root, "selected-root");
            var meta = root.Observation.ObjectBody ?? throw new Refusal("lifecycle-body-unavailable", MetadataFailure(root, "root-metadata"));
            var readRoot = IsReadRoot(root.Observation);
            if (infoMessageBoundary && readRoot) throw new Refusal("lifecycle-info-entry-roots-only");
            var producer = IlSqlLifecycleApis.ReturnsDataSet(meta.MemberReference)
                || infoMessageBoundary && IlSqlInfoMessageApis.EndsWith(meta.MemberReference, "->" + IlSqlInfoMessageApis.D("DataSet"));
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
            if (!RelaxedEntry) CheckEnumerations();
            var completed = new Result(executions.ToArray(), reads.ToArray(), result, [], loops.ToArray(), failures.ToArray(), invokedBodies.ToArray())
                { UnboundStringCount = UnboundStrings,
                    OpaqueInitializers = Initializers.Count > 0 ? Initializers.ToArray() : null,
                    ExternalTraces = TraceBoundaries.Count > 0 ? TraceBoundaries.ToArray() : null,
                    TraceStringComputations = StringComputations.Count > 0 ? StringComputations.ToArray() : null,
                    ConnectionStateReads = StateReads.Count > 0 ? StateReads.ToArray() : null,
                    InfoMessages = InfoReceipts.Count > 0 ? InfoReceipts.ToArray() : null,
                    ResultNaming = NamingReceipts.Count > 0 ? NamingReceipts.ToArray() : null,
                    ConstructorOwnerViews = OwnerViews.Count > 0 ? OwnerViews.ToArray() : null };
            if (readRoot) return completed;
            if (!RelaxedEntry) return CompleteEntry(completed, meta, producer, loader, instance, returned, guarded, inputTypes);
            // On an assumed state path, a completion that cannot support entry evidence
            // (no execution, no returned dataset, an abandoned enumeration) stays an
            // alternative outcome instead of erasing every other explored path.
            if (abandonedLoop) { EntryOutcome = "normal-completion-without-entry-evidence"; return completed; }
            try { return CompleteEntry(completed, meta, producer, loader, instance, returned, guarded, inputTypes); }
            catch (Refusal error) when (error.Message is "lifecycle-entry-no-sql-execution"
                or "lifecycle-entry-returned-dataset-origin" or "lifecycle-entry-void-output-unresolved"
                or "lifecycle-object-origin:dataset" or "lifecycle-loop-incomplete"
                or "lifecycle-collection-enumeration-incomplete")
            { EntryOutcome = "normal-completion-without-entry-evidence"; return completed; }
        }

        private void CheckEnumerations()
        {
            if (loopEnumerator.Kind != "unknown") throw new Refusal("lifecycle-loop-incomplete");
            if (heap.Values.Any(state => state.Kind == "collection-enumerator"
                && state.Moves != Get(state.Parent, "arraylist").Slots.Count + 1))
                throw new Refusal("lifecycle-collection-enumeration-incomplete");
        }

        private Result CompleteEntry(Result completed, IlObjectBody meta, bool producer, bool loader,
            Value instance, Value returned, bool guarded, string?[] inputTypes)
        {
            CheckEnumerations();
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
                        parameter.Slots[0].Kind == "computed-int" ? null : Number(parameter.Slots[0]), origin.Kind == "input" ? "argument:" + origin.Text
                            : origin.Kind == "opaque-initializer" ? "opaque-field:" + origin.Text
                            : origin.Kind == "computed-string" ? origin.Text : origin.Kind);
                }).ToArray())).ToArray();
            return completed with { Entry = new(producer ? "dataset-return" : "void-completion", Hash(meta.MemberReference),
                Enumerable.Range(0, meta.ParameterCount).Select(i => new RootArgument(i,
                    guarded ? "guarded-boolean" : "unbound-root-argument", inputTypes[i])).ToArray(),
                returnedSet, outputs, stored, tables, commands) };
        }

        private Value Invoke(Body body, Value[] args, int depth, Site? caller = null)
        {
            var meta = body.Observation.ObjectBody ?? throw new Refusal("lifecycle-body-unavailable", MetadataFailure(body, "callee-metadata"));
            if (depth >= 24 || !active.Add(body.Observation.MethodIdentity)) throw new Refusal("lifecycle-recursion-limit");
            var pathAdded = false;
            int invocation = 0;
            try
            {
                if (body.Input.Outcome != "admitted" || body.Input.GapKinds.Count != 0
                    || body.Input.RawFileSha256 is not { Length: 64 } sha || sha.Any(c => !char.IsAsciiHexDigit(c))
                    || !Guid.TryParse(body.Input.ModuleMvid, out _)) throw new Refusal("lifecycle-provenance-unavailable");
                if (meta.IsVirtual || meta.IsGeneric || meta.Instructions.Count > 256
                    || args.Length != meta.ParameterCount + (meta.HasThis ? 1 : 0)) throw new Refusal("lifecycle-body-shape", MetadataFailure(body, "body-shape"));
                invocation = ++frame;
                invokedBodies.Add(At(body, 0, invocation));
                invocationPath.Add(new(At(body, 0, invocation), body.Observation.MetadataToken, caller));
                pathAdded = true;
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
                frameLocals[invocation] = locals;
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
                    if (Slot(op, operand, "starg", out slot))
                    { if (slot < args.Length) args[slot] = Pop(); else _ = Pop(); continue; }
                    if (op == "ldsfld")
                    {
                        // The DBNull.Value static singleton is the one admitted static
                        // field (a constant); every other static load refuses.
                        if (operand.Contains("names:6:DBNull", StringComparison.Ordinal)
                            && operand.Contains("member:5:Value", StringComparison.Ordinal))
                        { stack.Add(new("dbnull", "dbnull-value")); continue; }
                        OpcodeFailure = new(selectedRootMetadata!, At(body, offset, invocation), body.Observation.MetadataToken,
                            op, meta.Instructions.Count, invocationPath.ToArray());
                        throw new Refusal("lifecycle-opcode:ldsfld", MetadataFailure(body, "opcode"));
                    }
                    if (Slot(op, operand, "ldloca", out slot))
                    { stack.Add(new("local-address", invocation + ":" + slot.ToString(CultureInfo.InvariantCulture))); continue; }
                    if (op == "ldflda")
                    {
                        // The address of a field on a tracked object: same discipline as
                        // local addresses — it may dereference, store through ByRef into
                        // tracked callees, and never escape to the heap or framework.
                        var parts = operand.Split(':', 4);
                        if (parts.Length != 4 || parts[1] != "field") throw new Refusal("lifecycle-field-invalid");
                        var addressReceiver = Deref(Pop());
                        if (addressReceiver.Kind != "object" || !heap.TryGetValue(addressReceiver.Text, out var addressState) || addressState.Kind != "custom")
                        { OpcodeFailure = new(selectedRootMetadata!, At(body, offset, invocation), body.Observation.MetadataToken, op, meta.Instructions.Count, invocationPath.ToArray()); throw new Refusal("lifecycle-opcode:ldflda", MetadataFailure(body, "opcode")); }
                        var separator = parts[3].IndexOf('|');
                        var fieldIdentity = separator < 0 ? parts[3] : parts[3][(separator + 1)..];
                        if (!addressState.FieldOwners.Any(owner => fieldIdentity.StartsWith("type:" + owner + "|member:", StringComparison.Ordinal)))
                            throw new Refusal("lifecycle-field-owner", MetadataFailure(body, "field-owner"));
                        stack.Add(new("field-address", addressReceiver.Text + "#" + fieldIdentity));
                        continue;
                    }
                    if (op == "ldstr") { stack.Add(new("string", operand)); continue; }
                    if (op == "ldnull") { stack.Add(new("null", "")); continue; }
                    if (op.StartsWith("ldc.i4", StringComparison.Ordinal))
                    { stack.Add(new("int", op == "ldc.i4.m1" ? "-1" : op is "ldc.i4" or "ldc.i4.s" ? operand[2..] : op[7..])); continue; }
                    if (op == "ceq")
                    {
                        var right = Pop(); var left = Pop();
                        if (left.Kind == "connection-state-unbound" || right.Kind == "connection-state-unbound")
                        {
                            // An unbound runtime observation compares only against a compiled constant;
                            // the result stays a hypothetical either-or and never becomes a value.
                            var unbound = left.Kind == "connection-state-unbound" ? left : right;
                            var other = left.Kind == "connection-state-unbound" ? right : left;
                            if (other.Kind != "int")
                                throw StateEqualityFailure(body, meta, offset, invocation, op,
                                    OperandKind(left) + ":" + OperandKind(right));
                            stack.Add(NewStateTest(unbound, other, At(body, offset, invocation), negated: false));
                            continue;
                        }
                        if (left.Kind == "state-test" || right.Kind == "state-test")
                        {
                            // Only the compiler's negation/identity lowerings of the SAME comparison are
                            // recognized; every other use of a hypothetical result keeps refusing.
                            var test = left.Kind == "state-test" ? left : right;
                            var other = left.Kind == "state-test" ? right : left;
                            if (other.Kind == "int" && (other.Text == "0" || other.Text == "1"))
                            { stack.Add(other.Text == "1" ? test : NegateStateTest(test)); continue; }
                            throw StateEqualityFailure(body, meta, offset, invocation, op, "state-test:" + OperandKind(other));
                        }
                        if (left.Kind is ("string-compare" or "string-compare-bool") && right.Kind == "int" && right.Text is "0" or "1"
                            || right.Kind is ("string-compare" or "string-compare-bool") && left.Kind == "int" && left.Text is "0" or "1")
                        {
                            // A VB/C# string comparison with an unbound operand: the equality
                            // outcome is a runtime hypothetical that forks bounded alternatives,
                            // never a selected path. The Int32 marker (0 = equal) and the Boolean
                            // marker (1 = equal) normalize against the compared constant.
                            var marker = left.Kind is ("string-compare" or "string-compare-bool") ? left : right;
                            var constant = marker == left ? right.Text : left.Text;
                            var equalMeans = marker.Kind == "string-compare" ? "0" : "1";
                            var fork = NewStringCompareTest(marker, At(body, offset, invocation));
                            stack.Add(constant == equalMeans ? fork : NegateStateTest(fork));
                            continue;
                        }
                        if (left.Kind is ("input" or "computed-int") && right.Kind == "null"
                            || left.Kind == "null" && right.Kind is ("input" or "computed-int"))
                        {
                            // An input's or unbound integer's nullness is unprovable and
                            // becomes a bounded hypothetical (null-assumed / non-null-assumed
                            // alternatives), never a silently selected path.
                            stack.Add(NewInputNullTest(left.Kind == "null" ? right : left, At(body, offset, invocation)));
                            continue;
                        }
                        var equal = Equal(left, right);
                        if (equal.Kind == "unknown")
                            throw StateEqualityFailure(body, meta, offset, invocation, op,
                                OperandKind(left) + ":" + OperandKind(right));
                        stack.Add(equal); continue;
                    }
                    if (op == "dup") { var value = Pop(); stack.Add(value); stack.Add(value); continue; }
                    if (op == "pop") { _ = Pop(); continue; }
                    if (op is "conv.i4" or "conv.u4" or "conv.i")
                    {
                        // Only a proven Int32, an exact array length or a length-minus
                        // bound passes through; no truncation semantics otherwise.
                        var converted = Pop();
                        if (converted.Kind is not ("int" or "array-length" or "array-length-minus")) throw new Refusal("lifecycle-conversion-unresolved");
                        stack.Add(converted); continue;
                    }
                    if (op == "ldlen")
                    {
                        var arrayValue = Pop(); var array = Get(arrayValue, "array");
                        stack.Add(new("array-length", array.Slots.Count.ToString(CultureInfo.InvariantCulture), arrayValue.Text));
                        continue;
                    }
                    if (op.StartsWith("ldelem", StringComparison.Ordinal) && op != "ldelem.ref")
                    {
                        // Typed element loads (the operand carries the element type —
                        // what renders as bare `ldelem`): a tracked array reads its
                        // exact slot; an untracked array (e.g. the ByRef output
                        // parameter's) yields an unknown element with no claim.
                        var typedIndex = Number(Pop()); var typedArray = Deref(Pop());
                        if (typedArray.Kind == "object" && heap.TryGetValue(typedArray.Text, out var typedState) && typedState.Kind == "array")
                        {
                            if (typedIndex < 0 || typedIndex >= typedState.Slots.Count) throw new Refusal("lifecycle-array-index");
                            stack.Add(typedState.Slots[typedIndex]);
                        }
                        else stack.Add(Value.Unknown);
                        continue;
                    }
                    if (op == "ldelem.ref")
                    {
                        var index = Number(Pop()); var array = Get(Deref(Pop()), "array");
                        if (index < 0 || index >= array.Slots.Count) throw new Refusal("lifecycle-array-index");
                        stack.Add(array.Slots[index]); continue;
                    }
                    if (op is "clt" or "clt.un" or "cgt" or "cgt.un")
                    {
                        var right = Pop(); var left = Pop();
                        // Roslyn lowers an enum comparison against the zero constant to a
                        // cgt.un nonzero test (e.g. State != ConnectionState.Closed, Closed
                        // being 0); the unbound runtime observation stays a hypothetical
                        // either-or, never a value.
                        if (op == "cgt.un" && left.Kind == "connection-state-unbound"
                            && right.Kind == "int" && right.Text == "0")
                        { stack.Add(NewStateTest(left, right, At(body, offset, invocation), negated: true)); continue; }
                        // VB lowers string <> to CompareString + cgt.un against zero
                        // (the nonzero test): the negated equality hypothetical.
                        if (op == "cgt.un" && (left.Kind is ("string-compare" or "string-compare-bool") && right.Kind == "int" && right.Text == "0"
                             || right.Kind is ("string-compare" or "string-compare-bool") && left.Kind == "int" && left.Text == "0"))
                        {
                            // cgt.un(x, 0) is the nonzero test: for the Int32 marker that is
                            // not-equal (negated fork); for the Boolean marker it is equal (direct).
                            var marker = left.Kind is ("string-compare" or "string-compare-bool") ? left : right;
                            var fork = NewStringCompareTest(marker, At(body, offset, invocation));
                            stack.Add(marker.Kind == "string-compare" ? NegateStateTest(fork) : fork);
                            continue;
                        }
                        // The reference non-null test: cgt.un(x, ldnull) / clt.un(ldnull, x).
                        // A provably non-null allocation (tracked object, exact string,
                        // boxed value type) resolves concretely; a maybe-null value
                        // (typed input, unbound string) forks its nullness hypothetical.
                        if ((op == "cgt.un" || op == "clt.un") && (left.Kind == "null" || right.Kind == "null"))
                        {
                            var other = left.Kind == "null" ? right : left;
                            bool NonNull(Value value) => value.Kind == "object" && heap.TryGetValue(value.Text, out var state)
                                && state.Kind is ("custom" or "command" or "parameter" or "adapter" or "dataset" or "sql-connection"
                                    or "array" or "arraylist" or "list");
                            if (NonNull(other) || other.Kind is ("string" or "computed-int")) { stack.Add(Int(1)); continue; }
                            if (other.Kind is ("input" or "computed-string"))
                            { stack.Add(NegateStateTest(NewInputNullTest(other, At(body, offset, invocation)))); continue; }
                            // Both operands proven null: the non-null test is the
                            // constant 0 on this path (cgt.un(null,null) and
                            // clt.un(null,null) are both false) — the branch
                            // consumes it concretely, never as a fork.
                            if (other.Kind == "null") { stack.Add(Int(0)); continue; }
                        }
                        // VB lowers an array bounds check to clt + backward brtrue;
                        // the comparison stays an exact-count test against one array.
                        if (left.Kind == "int" && right.Kind == "int")
                        { stack.Add(Int(op.StartsWith("clt", StringComparison.Ordinal) == (Number(left) < Number(right)) ? 1 : 0)); continue; }
                        if (left.Kind == "int" && right.Kind == "array-length")
                        {
                            var bound = int.Parse(right.Text, CultureInfo.InvariantCulture);
                            var less = Number(left) < bound;
                            stack.Add(new("array-test", (op.StartsWith("clt", StringComparison.Ordinal) == less) ? "1" : "0", right.DeclaredType));
                            continue;
                        }
                        // An unbound integer compared against a compiled constant is a
                        // runtime fact: a bounded hypothetical (the CInt(month) > 0 style
                        // range check), never a selected path. Same for state-test vs
                        // constant negation/identity lowerings.
                        if ((left.Kind == "computed-int" && right.Kind == "int" || right.Kind == "computed-int" && left.Kind == "int")
                            && (left.Kind == "int" ? left.Text : right.Text) is "0" or "1")
                        {
                            var test = NewUnboundIntCompareTest(left.Kind == "computed-int" ? left : right,
                                (left.Kind == "int" ? left.Text : right.Text) == "0" ? "equal-zero" : "equal-one",
                                At(body, offset, invocation));
                            stack.Add(op.StartsWith("clt", StringComparison.Ordinal) || op.EndsWith(".un", StringComparison.Ordinal) && op.StartsWith("cgt", StringComparison.Ordinal)
                                ? NegateStateTest(test) : test);
                            continue;
                        }
                        PendingNumberFailure = new(OperandKind(left) + ":" + OperandKind(right), inFlightCall?.Identity, inFlightCall?.Body, inFlightCall?.Offset ?? -1);
                        throw new Refusal("lifecycle-comparison-unresolved");
                    }
                    if (op is "add" or "add.ovf" or "add.ovf.un")
                    {
                        var right = Pop(); var left = Pop();
                        if (left.Kind == "int" && right.Kind == "int")
                        { stack.Add(Int(Number(left) + Number(right))); continue; }
                        throw new Refusal("lifecycle-arithmetic-unresolved");
                    }
                    if (op is "sub" or "sub.ovf" or "sub.ovf.un")
                    {
                        var right = Pop(); var left = Pop();
                        if (left.Kind == "int" && right.Kind == "int")
                        { stack.Add(Int(Number(left) - Number(right))); continue; }
                        // length - k: the counted-loop bound (For i = 0 To arr.Length - 1)
                        if (left.Kind == "array-length" && right.Kind == "int")
                        { stack.Add(new("array-length-minus", (int.Parse(left.Text, CultureInfo.InvariantCulture) - Number(right)).ToString(CultureInfo.InvariantCulture), left.DeclaredType)); continue; }
                        throw new Refusal("lifecycle-arithmetic-unresolved");
                    }
                    if (op is "beq" or "beq.s" or "bne.un" or "bne.un.s")
                    {
                        // ECMA-335 has no ceq.un: compilers lower "not equal" to
                        // bne.un (or a ceq inversion, handled at ceq). Equality
                        // branches resolve for the operand kinds already proven —
                        // concrete ints, string-compare booleans, state tests — with
                        // the fork polarity matching the branch sense.
                        var right = Pop(); var left = Pop(); var destination = Target(operand);
                        bool branchIfEqual = op is "beq" or "beq.s";
                        if (left.Kind == "int" && right.Kind == "int")
                        { if ((Number(left) == Number(right)) == branchIfEqual) i = destination - 1; continue; }
                        var test = left.Kind is ("string-compare" or "string-compare-bool" or "state-test") ? left
                            : right.Kind is ("string-compare" or "string-compare-bool" or "state-test") ? right : null;
                        if (test is not null && test.Kind == "string-compare")
                            test = NegateStateTest(NewStringCompareTest(test, At(body, offset, invocation))); // Int32 -> Boolean convention
                        if (test is null || (left.Kind != "int" && right.Kind != "int")
                            || (left.Kind == "int" && left.Text is not ("0" or "1")) && (right.Kind == "int" && right.Text is not ("0" or "1")))
                        {
                            OpcodeFailure = new(selectedRootMetadata!, At(body, offset, invocation), body.Observation.MetadataToken,
                                op, meta.Instructions.Count, invocationPath.ToArray());
                            throw new Refusal("lifecycle-equality-branch-unresolved", MetadataFailure(body, "opcode"));
                        }
                        var truth = left.Kind == "int" ? left.Text == "1" : right.Text == "1";
                        if (test!.Kind == "string-compare-bool") test = NewStringCompareTest(test, At(body, offset, invocation));
                        var (template, key) = stateTests[int.Parse(test.Text, CultureInfo.InvariantCulture)];
                        if (!forcedDecisions.TryGetValue(key, out var equalAssumed))
                            throw new StateForkRequired(template, key);
                        if (RecordedDecisions.Count >= 32) throw new Refusal("lifecycle-state-decision-limit");
                        RecordedDecisions.Add(template with { EqualAssumed = equalAssumed });
                        var truthOfTest = equalAssumed ^ template.Comparison == "not-equal-constant";
                        if ((truthOfTest == truth) == branchIfEqual) i = destination - 1;
                        continue;
                    }
                    if (op is "blt" or "blt.s" or "blt.un" or "blt.un.s" or "bgt" or "bgt.s" or "bgt.un" or "bgt.un.s"
                        or "ble" or "ble.s" or "ble.un" or "ble.un.s" or "bge" or "bge.s" or "bge.un" or "bge.un.s")
                    {
                        // A counted loop over a tracked array lowers to a comparison
                        // against the array's exact length; a backward instance of that
                        // shape unrolls concretely, bounded by the array slot cap and
                        // the work limits. Everything else still refuses.
                        var right = Pop(); var left = Pop(); var destination = Target(operand);
                        var backward = destination <= i;
                        if (right.Kind is not ("array-length" or "array-length-minus") || left.Kind != "int")
                        {
                            // A backward int-int loop whose bound came from the tracked
                            // collection's exact count (get_Count) unrolls concretely;
                            // forward int-int comparisons resolve; everything else refuses.
                            if (left.Kind == "int" && right.Kind == "int"
                                && (!backward || op.Split('.')[0] is "blt" or "ble"))
                            {
                                if (backward)
                                {
                                    if (loopEnumerator.Kind != "unknown") throw new Refusal("lifecycle-loop-dependent-control");
                                    if (!loops.Any(loop => loop.Site.Body == body.Observation.BodyIdentity && loop.Table == "indexed:" + right.Text))
                                        loops.Add(new(At(body, offset, invocation), "indexed:" + right.Text, "index-bounded;count-exact"));
                                }
                                if (CompareBranch(op, Number(left), Number(right))) i = destination - 1;
                                continue;
                            }
                            throw new Refusal(backward ? "lifecycle-unproven-loop" : "lifecycle-comparison-branch-unresolved");
                        }
                        var bound = int.Parse(right.Text, CultureInfo.InvariantCulture);
                        var truth = CompareBranch(op, Number(left), bound);
                        if (backward)
                        {
                            if (loopEnumerator.Kind != "unknown") throw new Refusal("lifecycle-loop-dependent-control");
                            if (!loops.Any(loop => loop.Site.Body == body.Observation.BodyIdentity && loop.Table == right.DeclaredType))
                                loops.Add(new(At(body, offset, invocation), right.DeclaredType!, "array-elements;count-exact"));
                        }
                        if (truth) i = destination - 1;
                        continue;
                    }
                    if (op == "box")
                    {
                        var value = Pop();
                        if (value.Kind is ("input" or "opaque-initializer") && IlSqlLifecycleApis.IsInputBox(operand, value.DeclaredType))
                        { stack.Add(value); continue; }
                        if (value.Kind == "object" && heap.TryGetValue(value.Text, out var boxed)
                            && boxed.Kind is ("custom" or "parameter"))
                        { stack.Add(value); continue; } // boxing a tracked allocation preserves its identity
                        if (IlSqlLifecycleApis.TypeOperand(operand, "Boolean"))
                        {
                            if (Number(value) is not (0 or 1)) throw new Refusal("lifecycle-box-type");
                        }
                        else if (IlSqlLifecycleApis.TypeOperand(operand, "Int32"))
                        {
                            // A concrete int or an Int32-DECLARED input boxes without
                            // demanding a parsed constant (the boxed CInt(Integer) shape).
                            if (value.Kind == "int") _ = Number(value);
                            else if (value.Kind == "computed-int") { } // an unbound integer boxes as unproven
                            else if (value.Kind == "input" && value.DeclaredType == "type(namespace:6:System|names:5:Int32)") { }
                            else throw new Refusal("lifecycle-box-type");
                        }
                        else throw new Refusal("lifecycle-box-type");
                        stack.Add(value); continue;
                    }
                    if (op == "ldtoken") { stack.Add(new("type-token", operand)); continue; }
                    if (op == "newarr")
                    {
                        // Any WELL-FORMED encoded element type (the type reference must
                        // resolve in a known scope): the array identity is tracked with
                        // exact slots; unstored elements stay unknown.
                        if (!operand.Contains("type(namespace:", StringComparison.Ordinal))
                            throw new Refusal("lifecycle-array-type");
                        var count = Number(Pop()); if (count is < 0 or > 16) throw new Refusal("lifecycle-array-limit");
                        var array = New("array"); heap[array.Text].Slots.AddRange(Enumerable.Repeat(Value.Unknown, count)); stack.Add(array); continue;
                    }
                    if (op == "unbox.any")
                    {
                        // A typed foreach over boxed struct items lowers Current through
                        // unbox.any; the tracked allocation identity is preserved.
                        var unboxed = Pop();
                        if (unboxed.Kind == "object" && heap.TryGetValue(unboxed.Text, out var unboxedState)
                            && unboxedState.Kind is ("custom" or "parameter"))
                        { stack.Add(unboxed); continue; }
                        throw new Refusal("lifecycle-unbox-type");
                    }
                    if (op == "initobj")
                    {
                        var zeroed = Pop();
                        if (zeroed.Kind == "local-address") { WriteLocal(zeroed, Value.Unknown); continue; }
                        // A value-type constructor prologue zeroes its own allocation
                        // (ldarg.0 + initobj); unset fields already read as unknown, so
                        // the tracked identity simply carries the following stores.
                        if (zeroed.Kind == "object" && heap.TryGetValue(zeroed.Text, out var zeroedState)
                            && zeroedState.Kind is ("custom" or "parameter")) continue;
                        throw new Refusal("lifecycle-initobj-target");
                    }
                    if (op == "stind.ref")
                    {
                        var indirect = Pop(); var target = Pop();
                        if (target.Kind == "field-address" && indirect.Kind != "local-address" && indirect.Kind != "field-address")
                        {
                            var separator = target.Text.IndexOf('#');
                            var allocation = target.Text[..separator];
                            var fieldIdentity = target.Text[(separator + 1)..];
                            if (!heap.TryGetValue(allocation, out var fieldObject) || fieldObject.Frozen) throw new Refusal("lifecycle-parameter-object-mutated");
                            fieldObject.Fields[fieldIdentity] = indirect; continue;
                        }
                        if (target.Kind != "local-address" || indirect.Kind == "local-address")
                            throw new Refusal("lifecycle-indirect-store-unmodeled");
                        WriteLocal(target, indirect); continue;
                    }
                    if (op == "ldind.ref")
                    { stack.Add(Deref(Pop())); continue; }
                    if (op == "stelem.ref")
                    {
                        var value = Pop(); var index = Number(Pop()); var array = Get(Deref(Pop()), "array");
                        if (index < 0 || index >= array.Slots.Count) throw new Refusal("lifecycle-array-index");
                        array.Slots[index] = value; continue;
                    }
                    if (op is "castclass" or "isinst")
                    {
                        // A typed foreach over constant-string items lowers Current to a
                        // castclass on String; the exact text is already the value.
                        if (op == "castclass" && stack.Count > 0 && stack[^1].Kind == "string"
                            && IlSqlLifecycleApis.TypeOperand(operand, "String"))
                            continue;
                        // A tracked array snapshot cast to its exact element type is the
                        // ToArray lowering; the element identity is already in the slots.
                        if (op == "castclass" && stack.Count > 0 && stack[^1].Kind == "object"
                            && heap.TryGetValue(stack[^1].Text, out var arrayCast) && arrayCast.Kind == "array"
                            && operand.EndsWith("[]", StringComparison.Ordinal))
                            continue;
                        if (op == "castclass" && stack.Count > 0 && stack[^1].Kind == "object"
                            && heap.TryGetValue(stack[^1].Text, out var customCast) && customCast.Kind == "custom"
                            && operand == "t:" + customCast.Owner)
                            continue;
                        // A typed foreach over raw parameter items lowers Current to a
                        // castclass on the framework parameter type (either version).
                        if (op == "castclass" && stack.Count > 0 && stack[^1].Kind == "object"
                            && heap.TryGetValue(stack[^1].Text, out var parameterCast) && parameterCast.Kind == "parameter"
                            && (operand.EndsWith("type(namespace:21:System.Data.SqlClient|names:12:SqlParameter)", StringComparison.Ordinal)
                                || operand.EndsWith("type(namespace:18:System.Data.Common|names:11:DbParameter)", StringComparison.Ordinal)))
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
                        // The RAW CompareString result follows the Int32 convention (0 = equal):
                        // brtrue branches when NOT equal. The op_Equality marker follows the
                        // Boolean convention (1 = equal): brtrue branches when equal.
                        if (value.Kind == "string-compare") value = NegateStateTest(NewStringCompareTest(value, At(body, offset, invocation)));
                        if (value.Kind == "string-compare-bool") value = NewStringCompareTest(value, At(body, offset, invocation));
                        // A Boolean-DECLARED input's runtime truth is a runtime fact:
                        // a bounded hypothetical fork keyed by the input identity.
                        if (value.Kind == "input" && value.DeclaredType == "type(namespace:6:System|names:7:Boolean)")
                            value = NewInputBoolTest(value, At(body, offset, invocation));
                        if (value.Kind == "array-test")
                        {
                            // Backward branch on an exact array bounds test: the counted
                            // array loop, unrolled concretely within the array slot cap.
                            if (destination > i) throw new Refusal("lifecycle-array-test-forward");
                            if (loopEnumerator.Kind != "unknown") throw new Refusal("lifecycle-loop-dependent-control");
                            if (!loops.Any(loop => loop.Site.Body == body.Observation.BodyIdentity && loop.Table == value.DeclaredType))
                                loops.Add(new(At(body, offset, invocation), value.DeclaredType!, "array-elements;count-exact"));
                            if ((value.Text == "1") == op.StartsWith("brtrue", StringComparison.Ordinal)) i = destination - 1;
                            continue;
                        }
                        if (value.Kind == "state-test")
                        {
                            // A hypothetical state comparison never selects a side here: the driver
                            // explores both outcomes as separate alternatives with separate identities.
                            if (destination <= i)
                                throw new Refusal("lifecycle-loop-dependent-control", MetadataFailure(body, "state-branch"));
                            var (template, key) = stateTests[int.Parse(value.Text, CultureInfo.InvariantCulture)];
                            if (!forcedDecisions.TryGetValue(key, out var equalAssumed))
                                throw new StateForkRequired(template, key);
                            if (RecordedDecisions.Count >= 32) throw new Refusal("lifecycle-state-decision-limit");
                            RecordedDecisions.Add(template with { EqualAssumed = equalAssumed });
                            var truthOfTest = equalAssumed ^ template.Comparison == "not-equal-constant";
                            if (truthOfTest == op.StartsWith("brtrue", StringComparison.Ordinal)) i = destination - 1;
                            continue;
                        }
                        if (value.Kind == "connection-state-unbound")
                        {
                            OpcodeFailure = new(selectedRootMetadata!, At(body, offset, invocation), body.Observation.MetadataToken,
                                op, meta.Instructions.Count, invocationPath.ToArray());
                            throw new Refusal("lifecycle-connection-state-dependent-control", MetadataFailure(body, "connection-state"));
                        }
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
                        // Leaving with an active enumeration abandons it: the loop never
                        // completes normally on this path, so the path finishes without
                        // entry evidence instead of pretending the loop claim still holds.
                        if (loopEnumerator.Kind != "unknown")
                        {
                            if (!RelaxedEntry) throw new Refusal("lifecycle-loop-dependent-control",
                                MetadataFailure(body, "leave-with-active-loop"));
                            abandonedLoop = true; loopEnumerator = Value.Unknown; loopRow = Value.Unknown;
                        }
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
                    if (op == "ldftn" && infoMessageBoundary)
                    {
                        if (!calls.TryGetValue(offset, out var function)) throw new Refusal("lifecycle-info-target-missing");
                        if (i + 1 >= code.Length || code[i + 1][2] != "newobj" || !calls.TryGetValue(offsets[i + 1], out var delegateCall)
                            || !IlSqlInfoMessageApis.Matches(delegateCall.TargetIdentity, IlSqlInfoMessageApis.DelegateCtor)) throw new Refusal("lifecycle-info-function-escape");
                        stack.Add(InfoFunction(function)); continue;
                    }
                    if (op is "ldfld" or "stfld")
                    {
                        var parts = operand.Split(':', 4);
                        if (parts.Length != 4 || parts[1] != "field") throw new Refusal("lifecycle-field-invalid");
                        var identity = parts[3][(parts[3].IndexOf('|') + 1)..];
                        var value = op == "stfld" ? Pop() : Value.Unknown;
                        if (value.Kind == "local-address") throw new Refusal("lifecycle-address-escape");
                        var instanceValue = Deref(Pop());
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
                            if (value.Kind == "object" && heap[value.Text].Kind == "info-delegate" && heap[value.Text].Parent != instanceValue)
                                throw new Refusal("lifecycle-info-delegate-escape");
                            if (infoCapturedFields.Contains(instanceValue.Text + ":" + identity))
                                throw new Refusal("lifecycle-info-name-list-field-mutated");
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
                            || shape.ParameterCount > 16) throw new Refusal("lifecycle-call-shape");
                        var values = new Value[shape.ParameterCount];
                        for (var arg = values.Length - 1; arg >= 0; arg--) values[arg] = Pop();
                        var receiver = op != "newobj" && shape.HasThis ? Pop() : Value.Unknown;
                        // Local addresses pass only into byref parameters of tracked inlined
                        // callees; every byval position and framework argument stays a value.
                        if (shape.ByReferenceParameters.Contains('1'))
                        {
                            if (op == "newobj") throw new Refusal("lifecycle-byref-unmodeled");
                            for (var parameter = 0; parameter < values.Length; parameter++)
                            {
                                if (shape.ByReferenceParameters[parameter] != '1') { values[parameter] = Deref(values[parameter]); continue; }
                                // Local addresses of ANY live ancestor frame (the ByRef
                                // forwarding chain) and owner-proved field addresses both
                                // pass; dereference validates liveness.
                                if (values[parameter].Kind is not ("local-address" or "field-address"))
                                    throw new Refusal("lifecycle-byref-unmodeled");
                            }
                        }
                        var site = At(body, offset, invocation);
                        var previousCall = inFlightCall;
                        inFlightCall = (call.TargetIdentity, body.Observation.BodyIdentity, offset);
                        Value returned;
                        var localCatch = regions.Where(r => r.Kind == "catch" && offset >= r.Start && offset < r.End)
                            .OrderBy(r => r.End - r.Start).FirstOrDefault();
                        if (localCatch is not null) catchHandlers.Push(At(body, localCatch.Handler, invocation));
                        try
                        {
                        var api = IlSqlObjectPathExtractor.KnownFrameworkApi(call.TargetIdentity, infoMessageBoundary)
                            ?? IlSqlLifecycleApis.Resolve(call.TargetIdentity, bodies.Select(b => b.Observation.ObjectBody?.OwnerType), infoMessageBoundary);
                        if (body.Observation.BodyIdentity == externalTraceRoot && depth == 0 && IlSqlTraceStringApis.Resolve(call.TargetIdentity) is { } stringRule)
                        {
                            // An identity ToString of an already-exact string stays exact:
                            // VB lowers a typed foreach over a late-bound collection through
                            // Conversions.ToString, and the exact text is the whole provenance.
                            // Every other trace-string computation keeps its unbound result.
                            if (!stringRule.Format && !stringRule.ArrayTail && stringRule.Instance && stringRule.Parameters == 0 && receiver.Kind == "string")
                                returned = receiver;
                            else if (!stringRule.Format && !stringRule.ArrayTail && !stringRule.Instance && stringRule.Parameters == 1 && values is [var only] && only.Kind == "string")
                                returned = only;
                            else returned = BuildTraceString(call, stringRule, receiver, values, site);
                        }
                        else if (infoMessageBoundary && TryInfoCall(call, op, Deref(receiver), values, site, out var infoReturned)) returned = infoReturned;
                        else if (api is not null)
                        {
                            receiver = Deref(receiver);
                            if (values.Any(argument => argument.Kind == "local-address"))
                                throw new Refusal("lifecycle-address-escape");
                            var recordFailure = catchHandlers.Count > 0 && api is "fill" or "table-merge" or "row-write" or "list-add";
                            var before = recordFailure ? Snapshot() : [];
                            var beforeObjects = recordFailure ? ObjectsSnapshot() : [];
                            var inLoop = loopEnumerator.Kind != "unknown";
                            returned = Framework(api, receiver, values, site, body, depth);
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
                            if (targets.Length != 1 && op == "newobj" && i + 1 < code.Length && code[i + 1][2] == "throw")
                            {
                                // Direct-throw construction is the only untracked construction: the
                                // fresh object can reach nothing except the immediately following throw.
                                stack.Add(new("external-exception", Hash(call.TargetIdentity)));
                                continue;
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
                            else if (targetMeta.Name == ".ctor" && receiver.Kind == "local-address")
                            {
                                // Value-type construction lowers to ldloca + CALL on the
                                // destination local; the constructed value is a tracked
                                // custom stored back into the addressed slot.
                                var constructed = New("custom", targetMeta.OwnerType);
                                _ = Invoke(targets[0], new[] { constructed }.Concat(values).ToArray(), depth + 1, site);
                                WriteLocal(receiver, constructed);
                                continue;
                            }
                            else if (targetMeta.Name == ".ctor" || shape.HasThis)
                                receiver = Deref(receiver);
                            if (op != "newobj" && targetMeta.Name == ".ctor" && receiver.Kind != "local-address")
                                AdmitConstructorOwnerView(body, meta, targets[0], targetMeta, receiver, args, op, site);
                            if (infoMessageBoundary && TryResultNaming(targets[0], receiver, values, site)) returned = Value.Unknown;
                            else returned = Invoke(targets[0], shape.HasThis ? new[] { receiver }.Concat(values).ToArray() : values, depth + 1, site);
                            if (op == "newobj") returned = receiver;
                        }
                        }
                        finally { if (localCatch is not null) _ = catchHandlers.Pop(); inFlightCall = previousCall; }
                        if (op == "newobj" || shape.ReturnsValue) stack.Add(returned);
                        continue;
                    }
                    if (op is "throw" or "rethrow")
                    {
                        // Uncaught throws terminate their alternative; a throw a catch would
                        // receive keeps its continuation unexecuted and refuses either way.
                        _ = op == "throw" ? Pop() : Value.Unknown;
                        if (op == "rethrow" || regions.Any(region => region.Kind == "catch"
                            && offset >= region.Start && offset < region.End))
                        {
                            OpcodeFailure = new(selectedRootMetadata!, At(body, offset, invocation), body.Observation.MetadataToken,
                                op, meta.Instructions.Count, invocationPath.ToArray());
                            throw new Refusal("lifecycle-throw-catch-continuation-unmodeled", MetadataFailure(body, "throw"));
                        }
                        if (!RelaxedEntry)
                        {
                            OpcodeFailure = new(selectedRootMetadata!, At(body, offset, invocation), body.Observation.MetadataToken,
                                op, meta.Instructions.Count, invocationPath.ToArray());
                            throw new Refusal("lifecycle-opcode:throw", MetadataFailure(body, "opcode"));
                        }
                        var crossesFinally = regions.Any(region => region.Kind == "finally"
                            && offset >= region.Start && offset < region.End);
                        throw new StateTerminated(At(body, offset, invocation), crossesFinally
                            ? "uncaught-exception;finally-continuation-not-executed;catch-continuation-not-executed"
                            : "uncaught-exception;catch-continuation-not-executed");
                    }
                    if (op == "ret")
                    {
                        if (stack.Count != (meta.ReturnsValue ? 1 : 0)) throw new Refusal("lifecycle-return-stack");
                        var returnedValue = meta.ReturnsValue ? Pop() : Value.Unknown;
                        if (returnedValue.Kind == "local-address") throw new Refusal("lifecycle-address-escape");
                        return returnedValue;
                    }
                    OpcodeFailure = new(selectedRootMetadata!, At(body, offset, invocation), body.Observation.MetadataToken,
                        op, meta.Instructions.Count, invocationPath.ToArray());
                    throw new Refusal("lifecycle-opcode:" + op, MetadataFailure(body, "opcode"));
                }
                throw new Refusal("lifecycle-no-return");
                int Index(long offset)
                {
                    var index = Array.IndexOf(offsets, offset);
                    return index >= 0 ? index : throw new Refusal("lifecycle-handler-offset-invalid");
                }
            }
            finally { frameLocals.Remove(invocation); if (pathAdded) invocationPath.RemoveAt(invocationPath.Count - 1); active.Remove(body.Observation.MethodIdentity); }
        }

        private Value Framework(string api, Value receiver, Value[] args, Site site, Body body, int depth)
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
                    // Constructor-proven SqlParameter allocations join inspected custom
                    // parameter objects in the lifecycle collection; both freeze on Add.
                    // Constant strings (naming lists built in live bodies) join without
                    // freezing: their exact text is their whole provenance.
                    if (args[0].Kind == "string") { collection.Slots.Add(args[0]); return Int(collection.Slots.Count - 1); }
                    Get(args[0], "custom", "parameter").Frozen = true;
                    collection.Slots.Add(args[0]); return Int(collection.Slots.Count - 1);
                case "arraylist-enumerator":
                    collection = Get(receiver, "arraylist");
                    if (collection.Frozen) throw new Refusal("lifecycle-collection-enumerated-twice");
                    collection.Frozen = true;
                    foreach (var item in collection.Slots) if (item.Kind == "object") Get(item, "custom", "parameter").Frozen = true;
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
                    // GetObjectValue is an identity copy for references: any input,
                    // unbound integer or proven scalar passes through with origin.
                    if (args[0].Kind is not ("read" or "int" or "computed-int" or "string" or "null" or "opaque-initializer" or "computed-string" or "dbnull")
                        && !(args[0].Kind == "input" && args[0].DeclaredType != null)) throw new Refusal("lifecycle-object-copy-unresolved");
                    return args[0];
                case "type-handle":
                    if (args[0].Kind != "type-token") throw new Refusal("lifecycle-type-handle-unresolved");
                    return new("type", args[0].Text);
                case "merge":
                    // Catalogued for the object-path dispatcher but deliberately unmodeled
                    // here: merging is an output-copy-back-shaped claim, declared.
                    throw new Refusal("lifecycle-merge-unmodeled");
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
                    heap[parameter.Text].Slots.Add(args.Length > 2 && args[2].Kind == "computed-int" ? args[2] : Int(args.Length > 2 ? Number(args[2]) : 0));
                    return parameter;
                case "parameters-item":
                    // The attached parameter at an exact in-range index.
                    var indexedCommand = Get(Get(receiver, "parameters").Parent, "command");
                    var indexedSlot = Number(args[0]);
                    if (indexedSlot < 0 || indexedSlot >= indexedCommand.Slots.Count) throw new Refusal("lifecycle-array-index");
                    return indexedCommand.Slots[indexedSlot];
                case "parameter-name":
                    // The tracked parameter's constructor-proven name.
                    return new("string", Text(Get(receiver, "parameter").Command));
                case "parameter-get-value":
                    // Output copy-back: the stored value origin (input, opaque,
                    // computed, dbnull) with no runtime-value claim.
                    return Get(receiver, "parameter").Fields.GetValueOrDefault("parameter-value", Value.Unknown);
                case "parameter-value":
                    if (args[0].Kind == "info-function" || args[0].Kind == "object" && (infoMessageBoundary || heap[args[0].Text].Kind == "info-delegate")) throw new Refusal("lifecycle-info-parameter-reference-escape");
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
                    set.Slots.Add(table); RecordInfoFill(cmd.Parent, args[0], execution); return Int(0);
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
                case "command-timeout":
                    _ = Get(receiver, "command"); _ = Number(args[0]); return Value.Unknown;
                case "command-transaction":
                    _ = Get(receiver, "command");
                    if (args[0].Kind is not ("null" or "unknown")) throw new Refusal("lifecycle-transaction-operand");
                    return Value.Unknown;
                case "parameter-direction":
                    var directed = Get(receiver, "parameter"); _ = Number(args[0]);
                    if (directed.Attached) throw new Refusal("lifecycle-parameter-mutated-after-attachment");
                    directed.Fields["parameter-direction"] = args[0]; return Value.Unknown;
                case "parameter-source":
                    var sourced = New("parameter");
                    heap[sourced.Text].Command = new("string", Text(args[0]));
                    heap[sourced.Text].Type = Int(Number(args[1]));
                    heap[sourced.Text].Slots.Add(args[2].Kind == "computed-int" ? args[2] : Int(Number(args[2])));
                    _ = Text(args[3]); return sourced;
                case "table-mappings":
                    _ = Get(Get(receiver, "adapter").Parent, "command");
                    return View("table-mappings", receiver);
                case "table-mapping-add":
                    var mappings = Get(receiver, "table-mappings");
                    if (mappings.Slots.Count >= 16) throw new Refusal("lifecycle-table-mapping-limit");
                    _ = Text(args[0]); _ = Text(args[1]);
                    mappings.Slots.Add(new("string", Text(args[0]) + "->" + Text(args[1])));
                    return Value.Unknown;
                case "arraylist-clear":
                    var clearedCollection = Get(receiver, "arraylist");
                    if (clearedCollection.Frozen) throw new Refusal("lifecycle-collection-mutated-or-limit");
                    clearedCollection.Slots.Clear(); return Value.Unknown;
                case "arraylist-toarray":
                    var copiedCollection = Get(receiver, "arraylist");
                    _ = args[0].Kind == "type" ? args[0] : throw new Refusal("lifecycle-toarray-type");
                    var copiedArray = New("array");
                    heap[copiedArray.Text].Slots.AddRange(copiedCollection.Slots);
                    return copiedArray;
                case "object-type":
                    // A statically typed value's runtime type name is derivable: exact
                    // constants and typed inputs resolve concretely (collapsing the
                    // .NET 1 type-name checks); genuinely polymorphic values stay unknown.
                    if (receiver.Kind == "string") return new("runtime-type", "System.String");
                    if (receiver.Kind is ("int" or "computed-int")) return new("runtime-type", "System.Int32"); // a boxed Integer is always Int32
                    if (receiver.Kind == "dbnull") return new("runtime-type", "System.DBNull");
                    if (receiver.Kind == "input" && receiver.DeclaredType is not null
                        && (receiver.DeclaredType.EndsWith("names:6:String)", StringComparison.Ordinal)
                            || receiver.DeclaredType.EndsWith("names:5:Int32)", StringComparison.Ordinal)))
                        return new("runtime-type", receiver.DeclaredType.Contains("names:6:String", StringComparison.Ordinal) ? "System.String" : "System.Int32");
                    return Value.Unknown;
                case "reflection-method":
                    return Value.Unknown;
                case "arraylist-count":
                    // The tracked collection's exact count (indexed For loops).
                    return Int(Get(receiver, "arraylist").Slots.Count);
                case "arraylist-item":
                    // In-range exact slot read; out-of-range refuses.
                    var itemCollection = Get(receiver, "arraylist");
                    var itemIndex = Number(args[0]);
                    if (itemIndex < 0 || itemIndex >= itemCollection.Slots.Count) throw new Refusal("lifecycle-array-index");
                    return itemCollection.Slots[itemIndex];
                case "information-isnothing":
                    // Information.IsNothing(x) is the Is Nothing test: tracked
                    // allocations/strings/boxed value types are provably not nothing;
                    // typed inputs and unbound strings fork their nullness.
                    return TestNotNull(args[0], site);
                case "information-isdbnull":
                    // Information.IsDBNull(x): exact DBNull values are true; unbound
                    // values fork their DBNull-ness (a runtime fact).
                    if (args[0].Kind == "dbnull") return Int(1);
                    if (args[0].Kind is ("string" or "int")) return Int(0);
                    return new("string-compare-bool", "isdbnull:" + ProvenanceOf(args[0]));
                case "dbnull-value":
                    return new("dbnull", "dbnull-value");
                case "string-slice":
                    // Substring/Mid of a runtime string is unprovable content: an
                    // unbound string carrying the receiver's provenance (the COBOL
                    // period-slicing pattern: Mid("202604",5,2) style month/year
                    // extraction feeding CInt and parameter values).
                    return UnboundString("string-slice:" + ProvenanceOf(receiver));
                case "string-op-equality":
                    // C# binds string == to String.op_Equality: ordinal equality.
                    // Exact operands compare concretely; an unbound operand is a
                    // runtime hypothetical that only a bounded fork may resolve.
                    if (args.Length != 2) throw new Refusal("lifecycle-vb-compare-shape");
                    if (args[0].Kind == "string" && args[1].Kind == "string")
                        return Int(args[0].Text == args[1].Text ? 1 : 0);
                    return new("string-compare-bool", ProvenanceOf(args[0]) + "|" + (args[1].Kind == "string" ? args[1].Text : "?"));
                case "vb-compare-string":
                    // VB lowers string comparisons to Operators.CompareString; with an
                    // unbound operand the result is a runtime fact carried as a
                    // hypothetical that only a bounded fork may resolve. Identical
                    // exact operands are equal under any culture: concrete, no fork.
                    if (args.Length != 3 || args[2].Kind != "int") throw new Refusal("lifecycle-vb-compare-shape");
                    if (args[0].Kind == "string" && args[1].Kind == "string")
                    {
                        // Identical texts are equal under any culture or compare mode.
                        if (args[0].Text == args[1].Text) return Int(0);
                        // The ordinal shortcut is sound only under BINARY compare:
                        // Option Compare Text is culture- and case-sensitive-aware,
                        // so text mode stays a hypothetical.
                        if (args[2].Text == "0" && args[0].Text.All(char.IsAscii) && args[1].Text.All(char.IsAscii))
                            return Int(string.CompareOrdinal(args[0].Text, args[1].Text));
                    }
                    // The marker carries the compared constant: "x equals A" and
                    // "x equals B" are distinct runtime facts with distinct keys.
                    return new("string-compare", ProvenanceOf(args[0]) + "|" + (args[1].Kind == "string" ? args[1].Text : "?"));
                case "type-tostring":
                    // The runtime type's name is unprovable: an unbound string with
                    // no type-name claim (the .NET 1 GetType().ToString() pattern) —
                    // unless the type was derived statically, which renders exactly.
                    if (receiver.Kind == "runtime-type") return new("string", receiver.Text);
                    return UnboundString("type-name:" + ProvenanceOf(receiver));
                case "object-tostring":
                    // The virtual Object.ToString slot (C# binds .ToString() on typed
                    // results here): an unbound string, no representation claim —
                    // except a statically derived type (exact) or a string receiver
                    // (String.ToString is identity). Unbound results carry the
                    // RECEIVER's provenance so distinct values keep distinct facts.
                    if (receiver.Kind == "runtime-type") return new("string", receiver.Text);
                    if (receiver.Kind == "string") return receiver;
                    return UnboundString("object-tostring:" + ProvenanceOf(receiver));
                case "stack-ctor":
                    // Logging-only stack-name chain (the same exact signed identities the
                    // trace boundary admits) extended to non-callback lifecycle bodies
                    // under the external-trace flag; every step stays unbound.
                    if (externalTraceRoot is null) throw new Refusal("lifecycle-stack-chain-boundary");
                    return new("stack-trace", "");
                case "stack-frame":
                    if (externalTraceRoot is null || receiver.Kind != "stack-trace"
                        || args[0].Kind != "int" || Number(args[0]) is < 0 or > 31)
                        throw new Refusal("lifecycle-stack-chain-boundary");
                    return new("stack-frame", "");
                case "stack-method":
                    if (externalTraceRoot is null || receiver.Kind != "stack-frame")
                        throw new Refusal("lifecycle-stack-chain-boundary");
                    return new("stack-method", "");
                case "reflection-name":
                    return UnboundString(receiver.Kind == "stack-method" ? "stack-frame-method-name" : "reflection-method-name");
                case "exception-ctor":
                    // A framework exception constructor: on a tracked allocation it is
                    // the MyBase.New base-constructor call (constructor completion —
                    // framework internals unmodeled); as a newobj it produces the same
                    // untracked external-exception value the direct-throw adjacency
                    // produces, because every legal continuation is a store into a
                    // to-be-thrown object or the throw itself.
                    if (receiver.Kind == "object" && heap.TryGetValue(receiver.Text, out var exceptionSelf)
                        && exceptionSelf.Kind == "custom") return Value.Unknown;
                    return new("external-exception", "exception-ctor");
                case "exception-tostring":
                    // The exception's string form: unbound, no representation claim
                    // (the diagnostics/logging pattern on caught or stored exceptions).
                    return UnboundString("exception-tostring");
                case "exception-message":
                    return UnboundString("exception-message");
                case "exception-inner":
                    return Value.Unknown;
                case "string-concat":
                    // Exact for all-constant operands at any compiler-lowered arity (2-4);
                    // any unbound operand keeps the whole result unbound.
                    if (args.All(argument => argument.Kind == "string"))
                        return new("string", string.Concat(args.Select(argument => argument.Text)));
                    return UnboundString("string-concat");
                case "string-concat-array":
                    var concatArray = Get(args[0], "array");
                    if (concatArray.Slots.Count > 16) throw new Refusal("lifecycle-string-concat-limit");
                    return concatArray.Slots.All(slot => slot.Kind == "string")
                        ? new("string", string.Concat(concatArray.Slots.Select(slot => slot.Text)))
                        : UnboundString("string-concat-array");
                case "iif":
                    if (args[0].Kind == "state-test")
                        throw new Refusal("lifecycle-state-dependent-value");
                    var selected = Number(args[0]) != 0 ? args[1] : args[2];
                    return selected.Kind is ("string" or "int" or "computed-int" or "dbnull" or "null" or "unknown" or "input"
                        or "opaque-initializer" or "computed-string") ? selected
                        : throw new Refusal("lifecycle-iif-operand-untracked");
                case "to-integer":
                    if (args[0].Kind == "int") return args[0];
                    if (args[0].Kind == "input" && args[0].DeclaredType == "type(namespace:6:System|names:5:Int32)") return args[0];
                    // CInt over a runtime string is a culture-dependent parse: the
                    // value stays unproven (an unbound integer that parameter sizes
                    // may carry as unbound evidence, never as a number).
                    if (args[0].Kind is ("string" or "computed-string" or "input")) return new("computed-int", "cint:" + ProvenanceOf(args[0]));
                    throw new Refusal("lifecycle-conversion-unresolved");
                case "event-log":
                    // Root-body external logging terminal: only trace-scalar arguments,
                    // same unbound semantics as the web-trace chain, never in callbacks.
                    // Gated on the external-trace boundary being enabled for this extraction
                    // (externalTraceRoot is set only then); callbacks are never interpreted.
                    if (externalTraceRoot is null || !TraceScalarValue(args[0]) || !TraceScalarValue(args[1]))
                        throw new Refusal("lifecycle-event-log-boundary");
                    if (TraceBoundaries.Count >= 8) throw new Refusal("lifecycle-chain-limit");
                    TraceBoundaries.Add(new(ExternalTracePolicy,
                        [new(site, Hash("<event-log>")), new(site, Hash("<event-log>"))],
                        [TraceOrigin(args[0]), TraceOrigin(args[1])], null)
                        { ArgumentsPartial = args[0].Kind == "computed-string" || args[1].Kind == "computed-string",
                          ArgumentEffects = args.Any(argument => argument.Kind == "computed-string")
                              ? "partial-unbound-values;may-throw" : "scalar-values-only" });
                    return Value.Unknown;
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
