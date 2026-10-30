using System.Buffers.Binary;
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CecilMethodDefinition = Mono.Cecil.MethodDefinition;
using CecilTypeDefinition = Mono.Cecil.TypeDefinition;
using CecilGenericInstanceType = Mono.Cecil.GenericInstanceType;
using CecilCustomModifier = Mono.Cecil.IModifierType;
using CecilGenericParameter = Mono.Cecil.GenericParameter;
using CecilModuleDefinition = Mono.Cecil.ModuleDefinition;
using SrmMethodDefinition = System.Reflection.Metadata.MethodDefinition;

namespace TraceMap.Core;

/// <summary>
/// Bounded operand-aware IL method-body and direct-call evidence for admitted
/// managed assemblies. Mono.Cecil is never the sole oracle: every admitted
/// input's canonical body encodings are rebuilt independently from raw IL
/// bytes with System.Reflection.Metadata and compared exactly before any
/// positive fact is retained.
/// </summary>
internal static class IlBodyEvidenceExtractor
{
    internal const string SchemaVersion = "il-body-provenance.v1";
    internal const string PolicyVersion = "explicit-il-body-evidence.v28";
    internal const string IlLocationKind = "managed-il-v1";
    internal const string BodyLimitation = "IL body evidence proves only that the admitted assembly contains this exact bounded operand-aware instruction stream at this module-local method row; it does not prove execution, dispatch, reachability, behavior, source ownership, semantic equivalence, or rewrite preservation.";
    internal const string CallLimitation = "A call site records the static member reference or calli standalone signature encoded in this module's IL; a calli signature does not identify a target member. No call site proves execution, virtual dispatch resolution, target presence, cross-assembly resolution, call-graph reachability, or rewrite equivalence.";
    internal const string GapLimitation = "This categorical gap reduces only the explicitly requested IL body/call lane; it does not prove absence and never alters source, compiled-metadata, or PDB evidence.";

    private static readonly object OpcodeTableGate = new();
    private static Dictionary<int, System.Reflection.Emit.OpCode>? singleByteOpcodes;
    private static Dictionary<int, System.Reflection.Emit.OpCode>? multiByteOpcodes;

    internal static IlBodyEvaluation Evaluate(
        ScanOptions options,
        CompiledInputEvaluation compiledEvaluation,
        CancellationToken cancellationToken = default)
    {
        var rootSelections = IlLifecycleRootSelection.Parse(options.IlLifecycleRoots);
        if (options.IlCompiledScalarBoundary && (!options.IlOpaqueRootInitializers || rootSelections.Count == 0))
            throw new ArgumentException("compiled-scalar-boundary-requires-opaque-selected-roots");
        if (options.IlInfoMessageBoundary && rootSelections.Count == 0)
            throw new ArgumentException("info-message-boundary-requires-selected-roots");
        if (options.IlExternalTraceBoundary && rootSelections.Count == 0)
            throw new ArgumentException("external-trace-boundary-requires-selected-roots");
        if (options.IlOpaqueRootInitializers && rootSelections.Count == 0)
            throw new ArgumentException("opaque-root-initializers-require-selected-roots");
        if (!options.IlBodyEvidence)
        {
            if (rootSelections.Count > 0) throw new ArgumentException("il-lifecycle-root-requires-il-body-evidence");
            return IlBodyEvaluation.Disabled;
        }

        var limits = options.IlBodyLimits ?? new IlBodyLimits();
        ValidateLimits(limits);
        var compiledMaximumBytes = options.CompiledInputLimits?.MaxFileSizeBytes ?? new CompiledInputLimits().MaxFileSizeBytes;
        var generatorSha256 = GeneratorSha256();
        var admitted = compiledEvaluation.BindingArtifacts
            .Where(artifact => artifact.Outcome == "admitted")
            .OrderBy(artifact => artifact.SafeLocator, StringComparer.Ordinal)
            .ThenBy(artifact => artifact.FullPath, StringComparer.Ordinal)
            .ToArray();
        var expected = admitted.Select(artifact => new IlExpectedInput(artifact.SafeLocator, artifact.Role)).ToArray();
        var globalGapKinds = new List<string>();
        if (admitted.Length == 0)
            globalGapKinds.Add("IlCompiledEvidenceUnavailable");

        var evaluated = new List<EvaluatedIlInput>();
        if (globalGapKinds.Count > 0)
            evaluated.Add(new EvaluatedIlInput(new IlInputOutcome(
                "il-input-set",
                "none",
                "missing",
                "unknown",
                null,
                ManagedMetadataExtractor.CanonicalDigest(new { safeLocator = "il-input-set", globalGapKinds }),
                null,
                null,
                null,
                null,
                globalGapKinds.ToArray()), []));
        var workBudget = new IlWorkBudget(limits.MaxTotalWorkUnits);
        // Value derivation may fail independently; it must not spend the raw
        // body/call admission budget or retire independently decoded bodies.
        var srmValueBudget = new IlWorkBudget(limits.MaxTotalWorkUnits);
        var cecilValueBudget = new IlWorkBudget(limits.MaxTotalWorkUnits);
        var srmObjectBudget = new IlWorkBudget(rootSelections.Count > 0 ? 0 : Math.Min(limits.MaxTotalWorkUnits, 1_048_576));
        var cecilObjectBudget = new IlWorkBudget(rootSelections.Count > 0 ? 0 : Math.Min(limits.MaxTotalWorkUnits, 1_048_576));
        foreach (var artifact in admitted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (artifact.RawFileSha256 is null)
            {
                evaluated.Add(InputGap(artifact, "IlCompiledArtifactChangedOrUnreadable"));
                continue;
            }
            var bytes = PortablePdbExtractor.ReadVerifiedCompiledBytes(artifact.FullPath, artifact.RawFileSha256, compiledMaximumBytes, cancellationToken);
            if (bytes is null)
            {
                evaluated.Add(InputGap(artifact, "IlCompiledArtifactChangedOrUnreadable"));
                continue;
            }

            try
            {
                // The raw reader runs first so every bound (opcode table,
                // operand extent, switch table, string limit) is validated
                // before Mono.Cecil materializes the same operand.
                var srm = ReadSystemReflectionMetadataBodies(bytes, limits, workBudget, cancellationToken, valueBudget: srmValueBudget, objectBudget: srmObjectBudget);
                var cecil = ReadCecilBodies(bytes, limits, workBudget, cancellationToken, valueBudget: cecilValueBudget, objectBudget: cecilObjectBudget);
                var disagreements = CompareBodies(cecil, srm);
                cecil = cecil with { Bodies = AgreeValueFlows(cecil.Bodies, srm.Bodies) };
                if (disagreements.Count > 0)
                {
                    // Agreement is method-local once the assembly/module identity
                    // agrees. Preserve only bodies checked by both independent
                    // readers; the input and scan remain explicitly partial.
                    var agreedBodies = AgreedBodies(cecil, disagreements);
                    evaluated.Add(new EvaluatedIlInput(new IlInputOutcome(
                        artifact.SafeLocator,
                        artifact.Role,
                        "partial",
                        artifact.ProvenanceState,
                        artifact.RawFileSha256,
                        PrivacyProjectedDigest(artifact),
                        artifact.AssemblyIdentity,
                        cecil.ModuleName,
                        cecil.ModuleMvid,
                        artifact.ProvenanceBindingInputSha256,
                        ["IlReaderDisagreement"]), agreedBodies));
                    continue;
                }
                evaluated.Add(new EvaluatedIlInput(new IlInputOutcome(
                    artifact.SafeLocator,
                    artifact.Role,
                    "admitted",
                    artifact.ProvenanceState,
                    artifact.RawFileSha256,
                    PrivacyProjectedDigest(artifact),
                    artifact.AssemblyIdentity,
                    cecil.ModuleName,
                    cecil.ModuleMvid,
                    artifact.ProvenanceBindingInputSha256,
                    []), cecil.Bodies));
            }
            catch (IlEvidenceException exception)
            {
                evaluated.Add(InputGap(artifact, exception.GapKind));
            }
            catch (ManagedMetadataExtractor.ManagedInputException exception)
            {
                evaluated.Add(InputGap(artifact, exception.GapKind == "ManagedInputSignatureNestingLimitExceeded"
                    ? "IlSignatureNestingLimitExceeded"
                    : "IlMetadataIdentityUnavailable"));
            }
            catch (BadImageFormatException)
            {
                evaluated.Add(InputGap(artifact, "MalformedIlBody"));
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or OverflowException or IndexOutOfRangeException)
            {
                evaluated.Add(InputGap(artifact, "MalformedIlBody"));
            }
        }

        foreach (var selection in rootSelections)
            if (evaluated.SelectMany(input => input.Bodies.Where(body => input.Outcome.Outcome == "admitted"
                    && input.Outcome.GapKinds.Count == 0 && input.Outcome.RawFileSha256 == selection.BinarySha256
                    && body.MetadataToken == selection.MetadataToken)).Count() != 1)
                throw new ArgumentException("il-lifecycle-root-not-uniquely-admitted");
        var outcomes = evaluated.Select(item => item.Outcome)
            .OrderBy(item => item.SafeLocator, StringComparer.Ordinal)
            .ThenBy(item => item.Role, StringComparer.Ordinal)
            .ToArray();
        var boundedInputSha256 = ManagedMetadataExtractor.CanonicalDigest(new
        {
            schemaVersion = SchemaVersion,
            policyVersion = PolicyVersion,
            generatorSha256,
            extractorIdentities = new[] { ScannerVersions.IlBodyEvidenceExtractor, "system-reflection-metadata/10.0.0", "mono-cecil/0.11.6" },
            expectedInputs = expected,
            effectiveLimits = limits,
            lifecycleRootSelections = rootSelections,
            infoMessageBoundary = new { enabled = options.IlInfoMessageBoundary, policy = IlSqlLifecycleExtractor.InfoMessagePolicy, maxInstructions = 256, maxInspectionSteps = 4096, maxFields = 4, maxSubscriptions = 4, maxReceipts = 16, maxNamingReceipts = 8 },
            compiledScalarBoundary = new { enabled = options.IlCompiledScalarBoundary,
                policy = IlSqlLifecycleExtractor.CompiledScalarPolicy, maxInstructions = 128, maxHandlers = 4,
                maxCalls = 32, maxLocals = 32, maxStack = 16, maxSummaries = 4 },
            externalTraceBoundary = new { enabled = options.IlExternalTraceBoundary,
                policy = IlSqlLifecycleExtractor.ExternalTracePolicy, maxChains = 8, maxChainInstructions = 128, maxStoredLocals = 16, maxStackNameCalls = 32, maxFrameIndex = 31, maxExpressionStack = 16, maxStringComputations = 32, maxStringArrayElements = 16 },
            opaqueRootInitializers = new { enabled = options.IlOpaqueRootInitializers,
                policy = IlSqlLifecycleExtractor.OpaquePolicy, maxFields = 8, maxChainInstructions = 32, maxChainCalls = 8,
                maxCallArguments = 4, maxChainStack = 16 },
            lifecycleSliceProfile = new { schema = "il-sql-lifecycle-slice/1", maxRoots = 16,
                maxBodies = 128, maxInputBytes = 67_108_864, retainedCharactersPerReader = Math.Min(limits.MaxTotalWorkUnits, 1_048_576),
                decodeWorkAcrossRoots = limits.MaxTotalWorkUnits },
            controlFlowValueLimits = new { IlControlFlowValueExtractor.MaxInstructions, IlControlFlowValueExtractor.MaxSlots,
                IlControlFlowValueExtractor.MaxWorkUnits, IlControlFlowValueExtractor.MaxExceptionEntries,
                IlArgumentAlternatives.MaxAlternatives, IlArgumentAlternatives.MaxEncodedLength },
            valueDerivationAggregateWorkPerReader = limits.MaxTotalWorkUnits,
            maxRetainedReturnSites = IlCallValueExtractor.MaxRetainedReturnSites,
            sqlShapeLimits = new { IlSqlShapeExtractor.Schema, IlSqlShapeExtractor.MaxCalls,
                IlSqlShapeExtractor.MaxParameters, aggregateWork = limits.MaxTotalWorkUnits },
            sqlObjectLimits = new { IlSqlObjectPathExtractor.Schema, IlSqlObjectPathExtractor.MaxSteps,
                IlSqlObjectPathExtractor.MaxFrames, IlSqlObjectPathExtractor.MaxObjects, IlSqlObjectPathExtractor.MaxSites,
                maxIndexedBodies = 4096, maxInstructions = 256, maxFields = 64,
                retainedCharacterBudgetPerReader = Math.Min(limits.MaxTotalWorkUnits, 1_048_576),
                retentionProfile = "actual-retained-text.v3", independentObjectBodyAgreement = true,
                aggregateWork = limits.MaxTotalWorkUnits },
            sqlLifecycleLimits = new { IlSqlLifecycleExtractor.Schema, maxSteps = 8192, maxFrames = 24,
                maxObjects = 256, maxExecutions = 8, maxFailures = 256, maxIndexedBodies = 4096,
                maxInstructions = 256, maxConstructorOwnerViews = 24, maxConstructorOwnerRecords = 256,
                maxStatePaths = IlSqlLifecycleExtractor.MaxStatePaths, maxStateDecisionSites = IlSqlLifecycleExtractor.MaxStateSites, aggregateWork = limits.MaxTotalWorkUnits },
            outcomes = outcomes.Select(item => new
            {
                item.SafeLocator,
                item.Role,
                item.Outcome,
                item.ProvenanceState,
                item.RawFileSha256,
                item.PrivacyProjectedInputSha256,
                item.AssemblyIdentity,
                item.ModuleName,
                item.ModuleMvid,
                item.ProvenanceBindingInputSha256,
                item.GapKinds
            })
        });
        var coverage = rootSelections.Count == 0 && globalGapKinds.Count == 0
            && outcomes.Length > 0
            && outcomes.All(item => item.Outcome == "admitted" && item.GapKinds.Count == 0)
                ? "il-complete"
                : "il-partial";
        var provenance = new IlBodyProvenance(
            SchemaVersion,
            PolicyVersion,
            generatorSha256,
            [ScannerVersions.IlBodyEvidenceExtractor, "system-reflection-metadata/10.0.0", "mono-cecil/0.11.6"],
            expected,
            limits,
            outcomes,
            boundedInputSha256,
            "local-only",
            coverage)
        { AdmissionWork = CompiledAdmissionWorkUsage.Create("il-body", generatorSha256, boundedInputSha256,
            workBudget.Consumed, workBudget.RefusedRequests, limits.MaxTotalWorkUnits) };
        if (rootSelections.Count > 0) globalGapKinds.Add("IlLifecycleSelectedRootsOnly");
        var knownGaps = DistinctGapKinds(outcomes, globalGapKinds)
            .Select(value => $"IL body evidence coverage reduced: {value}.")
            .ToArray();
        return new IlBodyEvaluation(provenance, evaluated, knownGaps, admitted)
        { LifecycleRoots = rootSelections, OpaqueRootInitializers = options.IlOpaqueRootInitializers, ExternalTraceBoundary = options.IlExternalTraceBoundary, CompiledScalarBoundary = options.IlCompiledScalarBoundary, InfoMessageBoundary = options.IlInfoMessageBoundary, CompiledMaximumBytes = compiledMaximumBytes, SliceCancellationToken = cancellationToken };
    }

    private static IEnumerable<string> DistinctGapKinds(IReadOnlyList<IlInputOutcome> outcomes, List<string> globalGapKinds) =>
        globalGapKinds.Concat(outcomes.SelectMany(item => item.GapKinds))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal);

    internal const string CensusLimitation =
        "Observed helper-call evidence: the IL references the CompilerServices late-binding helpers; it does not prove the compiler generated the call or that the language compiled with late-binding semantics (direct helper calls and rewrite-inserted calls are indistinguishable), and it never establishes the assembly Option Strict setting. Observed IL call sites in admitted/partial bodies only; density is the late-bound share of observed calls, not of all code. "
        + "Late-bound means the call target encodes Microsoft.VisualBasic.CompilerServices LateBinding/NewLateBinding helper members; "
        + "other dynamic mechanisms (Reflection, DLR, delegates) are not counted. No current reducer or report consumes this census; high density is documented consumer guidance for downweighting source-lane call-graph claims, not implemented behavior. "
        + "The census never establishes runtime behavior, dispatch outcomes, or the assembly Option Strict setting.";

    // Exact declaring-type match over the call fact's encoded target identity,
    // parsed structurally. Every encoded string value is length-framed
    // (EncodeIdentityComponent), so values may legally contain '|' — a regex over
    // the raw text could be impersonated by a crafted metadata name embedding the
    // framing. The parser walks components honoring the frames, so text inside a
    // value can never cross a component boundary.
    //
    // Two encodings carry the declaring type:
    //   memberref|type:scope(...)type(namespace:<framed>|names:<framed>)|member:<framed>|...
    //     (helper referenced from another assembly)
    //   assembly:name:<framed>|...|type:namespace:<framed>|names:<framed>|arity:<n>|method:<framed>|...
    //     (the assembly defines the helper type itself — embedded or IL-merged
    //     CompilerServices helpers; generic instantiations embed the same element
    //     identity, so they are covered)
    // Only LateBinding/NewLateBinding in Microsoft.VisualBasic.CompilerServices
    // counts, with the member/method/constructor component anchored right after
    // the declaring type — signature type mentions are always later components.
    internal static bool IsLateBoundHelperTarget(string targetIdentity) =>
        IsMemberRefLateBindingHelper(targetIdentity) || IsBareFramedLateBindingHelper(targetIdentity);

    private const string LateBindingHelperNamespace = "Microsoft.VisualBasic.CompilerServices";

    private static bool IsMemberRefLateBindingHelper(string identity)
    {
        const string prefix = "memberref|type:scope(";
        if (!identity.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        // Walk the scope group's framed components until its closing paren.
        var position = prefix.Length;
        while (position < identity.Length && identity[position] != ')')
        {
            var separator = identity.IndexOf(':', position);
            if (separator < 0)
                return false;
            if (!TryReadFramedValue(identity, separator + 1, out _, out var scopeEnd))
                return false;
            position = scopeEnd < identity.Length && identity[scopeEnd] == '|' ? scopeEnd + 1 : scopeEnd;
        }
        if (position >= identity.Length || identity[position] != ')')
            return false;
        position++;
        if (!identity.AsSpan(position).StartsWith("type(namespace:", StringComparison.Ordinal))
            return false;
        // "type(namespace:" already ends with the namespace tag, so the helper
        // reads the framed value directly.
        position += "type(namespace:".Length;
        if (!TryReadHelperNamespaceAndNames(identity, ref position, namespaceTagPending: false))
            return false;
        return identity.AsSpan(position).StartsWith(")|member:", StringComparison.Ordinal);
    }

    private static bool IsBareFramedLateBindingHelper(string identity)
    {
        var position = 0;
        while (position < identity.Length)
        {
            var tagEnd = identity.IndexOf(':', position);
            if (tagEnd < 0)
                return false;
            var tag = identity.Substring(position, tagEnd - position);
            var valueStart = tagEnd + 1;
            if (!TryReadComponentValue(identity, valueStart, out var boundary))
                return false;

            if (tag == "type")
            {
                // The type component's value is multi-segment
                // (namespace:<framed>|names:<framed>), so its extent is parsed here,
                // not by the single-segment component reader. The helper type must
                // be followed by its arity component and then the
                // method/constructor anchor (constructors of the helper type count).
                var cursor = valueStart;
                if (!TryReadHelperNamespaceAndNames(identity, ref cursor, namespaceTagPending: true))
                    return false;
                if (!identity.AsSpan(cursor).StartsWith("|arity:", StringComparison.Ordinal))
                    return false;
                var afterArity = identity.IndexOf('|', cursor + "|arity:".Length);
                if (afterArity < 0)
                    return false;
                return NextComponentTagIs(identity, afterArity, "method", "constructor");
            }
            position = boundary >= identity.Length ? identity.Length : boundary + 1;
        }
        return false;
    }

    // Consumes "namespace:<framed CompilerServices>|names:<framed helper name>"
    // (with or without the leading separator) at cursor, leaving it on the
    // character that ends the names value. Nested types concatenate name
    // segments inside one names value and therefore cannot match.
    private static bool TryReadHelperNamespaceAndNames(string identity, ref int cursor, bool namespaceTagPending)
    {
        var position = cursor;
        if (namespaceTagPending)
        {
            if (position < identity.Length && identity[position] == '|')
                position++;
            if (!identity.AsSpan(position).StartsWith("namespace:", StringComparison.Ordinal))
                return false;
            position += "namespace:".Length;
        }
        if (!TryReadFramedValue(identity, position, out var namespaceValue, out var namespaceEnd)
            || !namespaceValue.SequenceEqual(LateBindingHelperNamespace))
            return false;
        position = namespaceEnd;
        if (!identity.AsSpan(position).StartsWith("|names:", StringComparison.Ordinal))
            return false;
        position += "|names:".Length;
        if (!TryReadFramedValue(identity, position, out var namesValue, out var namesEnd))
            return false;
        cursor = namesEnd;
        return namesValue.SequenceEqual("LateBinding") || namesValue.SequenceEqual("NewLateBinding");
    }

    private static bool NextComponentTagIs(string identity, int boundary, params string[] tags)
    {
        if (boundary + 1 >= identity.Length || identity[boundary] != '|')
            return false;
        var tagEnd = identity.IndexOf(':', boundary + 1);
        if (tagEnd < 0)
            return false;
        var tag = identity.AsSpan(boundary + 1, tagEnd - boundary - 1);
        foreach (var candidate in tags)
            if (tag.SequenceEqual(candidate))
                return true;
        return false;
    }


    // Reads a length-framed value whose value may contain '|'. Frames appear as
    // <len>:<value> possibly preceded by one alphabetic sub-tag
    // (method:8:LateCall, assembly:name:28:...): skip at most one non-numeric
    // segment before the digits. The declared length must land exactly on a
    // component boundary ('|' or a closing paren).
    private static bool TryReadFramedValue(string identity, int start, out ReadOnlySpan<char> value, out int end)
    {
        value = default;
        end = 0;
        var position = start;
        for (var segment = 0; segment < 2; segment++)
        {
            var colon = identity.IndexOf(':', position);
            if (colon < 0)
                return false;
            var head = identity.AsSpan(position, colon - position);
            if (head.Length > 0 && uint.TryParse(head, CultureInfo.InvariantCulture, out var length) && length <= int.MaxValue - colon)
            {
                var valueStart = colon + 1;
                if (valueStart + (int)length > identity.Length)
                    return false;
                var valueEnd = valueStart + (int)length;
                if (valueEnd != identity.Length && identity[valueEnd] != '|' && identity[valueEnd] != ')')
                    return false;
                value = identity.AsSpan(valueStart, (int)length);
                end = valueEnd;
                return true;
            }
            position = colon + 1;
        }
        return false;
    }

    // Reads one top-level component value: framed when the digits land on a
    // boundary (the encoder frames every string value), otherwise bare up to the
    // next separator.
    private static bool TryReadComponentValue(string identity, int start, out int boundary)
    {
        if (TryReadFramedValue(identity, start, out _, out var framedEnd))
        {
            boundary = framedEnd;
            return true;
        }
        var bareEnd = identity.IndexOf('|', start);
        boundary = bareEnd < 0 ? identity.Length : bareEnd;
        return true;
    }

    private static string CensusBucket(int lateBound, int observed) => lateBound == 0 ? "none"
        : observed == 0 ? "unknown"
        : (double)lateBound / observed <= 0.10 ? "low"
        : (double)lateBound / observed <= 0.50 ? "moderate"
        : "high";

    internal static IReadOnlyList<CodeFact> MaterializeFacts(
        ScanManifest manifest,
        IlBodyEvaluation evaluation,
        IReadOnlyList<CodeFact> compiledFacts,
        CancellationToken cancellationToken = default)
    {
        if (evaluation.Provenance is null)
            return [];
        var provenance = evaluation.Provenance;
        var compiledMethodsByLocatorAndToken = new Dictionary<(string AssemblyLocator, string MetadataToken), List<CodeFact>>();
        foreach (var fact in compiledFacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (fact.FactType != FactTypes.ManagedMethodDeclared
                || !fact.Properties.TryGetValue("metadataToken", out var token))
                continue;
            var key = (fact.Evidence.FilePath, token);
            if (!compiledMethodsByLocatorAndToken.TryGetValue(key, out var candidates))
                compiledMethodsByLocatorAndToken[key] = candidates = [];
            candidates.Add(fact);
        }

        var facts = new List<CodeFact>();
        // Independent bounded derivation phase over the admitted body/call
        // observations. Its aggregate bound uses the policy-bound work limit;
        // it never consumes or upgrades the body-admission receipt.
        var commandBudget = new IlWorkBudget(provenance.EffectiveLimits.MaxTotalWorkUnits);
        var sqlShapeBudget = new IlWorkBudget(provenance.EffectiveLimits.MaxTotalWorkUnits);
        foreach (var input in evaluation.Inputs.OrderBy(item => item.Outcome.SafeLocator, StringComparer.Ordinal))
        {
            var outcome = input.Outcome;
            var common = CommonProperties(provenance, outcome);
            foreach (var gapKind in outcome.GapKinds.OrderBy(value => value, StringComparer.Ordinal))
                facts.Add(GapFact(manifest, outcome.SafeLocator, gapKind, common));
            if (outcome.Outcome is not ("admitted" or "partial"))
                continue;

            // Per-assembly late-binding census (#854): observed IL call sites whose
            // target is the Visual Basic late-binding helper (LateBinding/NewLateBinding
            // in Microsoft.VisualBasic.CompilerServices) versus all observed calls. The
            // raw namespace and type names appear verbatim inside the encoded target
            // identity, so detection does not depend on identity length framing.
            var observedCalls = 0;
            var lateBoundCalls = 0;
            foreach (var body in input.Bodies)
            {
                foreach (var call in body.Calls)
                {
                    observedCalls++;
                    if (IsLateBoundHelperTarget(call.TargetIdentity))
                        lateBoundCalls++;
                }
            }
            facts.Add(FactFactory.Create(manifest,
                FactTypes.ManagedLateBindingCensus, RuleIds.DotNetIlLateBindingCensus,
                EvidenceTiers.Tier2Structural, IlEvidence(outcome.SafeLocator),
                targetSymbol: outcome.SafeLocator, contractElement: "il-late-binding-census",
                properties: CopyToSorted(common, new (string Key, string Value)[]
                {
                    ("observedIlCallCount", observedCalls.ToString(CultureInfo.InvariantCulture)),
                    ("lateBoundCallCount", lateBoundCalls.ToString(CultureInfo.InvariantCulture)),
                    ("lateBindingDensity", CensusBucket(lateBoundCalls, observedCalls)),
                    ("censusScope", outcome.Outcome == "admitted" ? "admitted-bodies" : "partial-bodies"),
                    ("limitation", CensusLimitation)
                })));

            foreach (var body in input.Bodies.OrderBy(item => item.MetadataToken, StringComparer.Ordinal))
            {
                var bodyProperties = CopyToSorted(common);
                bodyProperties["metadataToken"] = body.MetadataToken;
                bodyProperties["instructionCount"] = body.InstructionCount.ToString(CultureInfo.InvariantCulture);
                bodyProperties["ilBodySha256"] = body.BodySha256;
                bodyProperties["instructionsSha256"] = body.InstructionsSha256;
                bodyProperties["localCount"] = body.LocalCount.ToString(CultureInfo.InvariantCulture);
                bodyProperties["localsSha256"] = body.LocalsSha256;
                bodyProperties["exceptionRegionCount"] = body.ExceptionRegionCount.ToString(CultureInfo.InvariantCulture);
                bodyProperties["exceptionRegionsSha256"] = body.ExceptionRegionsSha256;
                bodyProperties["maxStack"] = body.MaxStack;
                bodyProperties["initLocals"] = body.InitLocals ? "true" : "false";
                bodyProperties["limitation"] = BodyLimitation;
                bodyProperties["objectBodyMetadataSchema"] = "il-object-body-retention/1";
                bodyProperties["objectBodyScope"] = evaluation.LifecycleRoots.Count > 0 ? "deferred-to-root-slice" : "corpus";
                bodyProperties["objectBodyState"] = body.ObjectBody is null ? "unavailable" : "retained";
                bodyProperties["objectBodyRetention"] = SqlServerSchemaEvidence.Json(body.ObjectBodyRetention);
                bodyProperties["objectBodyAgreement"] = SqlServerSchemaEvidence.Json(body.ObjectBodyAgreement);
                if (compiledMethodsByLocatorAndToken.TryGetValue((outcome.SafeLocator, body.MetadataToken), out var candidates)
                    && candidates.Count == 1)
                    bodyProperties["compiledFactId"] = candidates[0].FactId;
                var bodyFact = FactFactory.Create(
                    manifest,
                    FactTypes.ManagedIlBodyDeclared,
                    RuleIds.DotNetIlBody,
                    EvidenceTiers.Tier2Structural,
                    IlEvidence(outcome.SafeLocator),
                    targetSymbol: body.BodyIdentity,
                    contractElement: "il-method-body",
                    properties: bodyProperties);
                facts.Add(bodyFact);
                // One bounded summary per body. Null means unavailable (reader
                // disagreement or a work limit), never an empty/constant return.
                var returns = body.ValueFlow?.Returns;
                var returnsAvailable = returns is not null && returns.Count <= IlCallValueExtractor.MaxRetainedReturnSites;
                facts.Add(FactFactory.Create(manifest,
                    FactTypes.ManagedIlReturnValuesObserved, RuleIds.DotNetIlValues,
                    EvidenceTiers.Tier3SyntaxOrTextual, IlEvidence(outcome.SafeLocator),
                    targetSymbol: body.BodyIdentity, contractElement: "il-return-operand-origins",
                    properties: CopyToSorted(common, new (string Key, string Value)[]
                    {
                        ("ilBodyFactId", bodyFact.FactId),
                        ("valueSchema", "il-return-values.v1"),
                        ("valueState", returnsAvailable ? "return-operands-candidate" : "return-operands-unavailable"),
                        ("returnCount", (returns?.Count ?? -1).ToString(CultureInfo.InvariantCulture)),
                        ("returnFlowGaps", System.Text.Json.JsonSerializer.Serialize(body.ValueFlow?.Gaps ?? [])),
                        ("returnOrigins", System.Text.Json.JsonSerializer.Serialize(returnsAvailable
                            ? returns!.Select(value => value with { Origin = PublicOperand(value.Origin) }).ToArray() : [])),
                        ("limitation", "At most 256 reached return-site operands from independently agreed local value flow. Return operands are candidates, not executed return values. Consumers must verify the callee signature and exact producer call, join all return sites conservatively, and preserve unsupported flow, virtual dispatch and runtime-dependent values. " + IlCallValueExtractor.Limitation)
                    })));
                var valuesByOffset = body.ValueFlow?.Calls.ToDictionary(value => value.Offset);
                var callFactsByOffset = new Dictionary<long, CodeFact>();
                foreach (var call in body.Calls.OrderBy(item => item.Offset, Comparer<long>.Default))
                {
                    var callIdentity = $"{body.BodyIdentity}|call:{call.Opcode}:{call.Offset.ToString(CultureInfo.InvariantCulture)}:{call.TargetIdentity}";
                    var callFact = FactFactory.Create(
                        manifest,
                        FactTypes.ManagedIlCallObserved,
                        RuleIds.DotNetIlCall,
                        EvidenceTiers.Tier2Structural,
                        IlEvidence(outcome.SafeLocator),
                        targetSymbol: callIdentity,
                        contractElement: $"il-call:{call.Opcode}",
                        properties: CopyToSorted(common, new (string Key, string Value)[]
                        {
                            ("metadataToken", body.MetadataToken),
                            ("ilOffset", call.Offset.ToString(CultureInfo.InvariantCulture)),
                            ("opcode", call.Opcode),
                            ("referenceKind", call.ReferenceKind),
                            ("referenceToken", call.ReferenceToken),
                            ("targetIdentity", call.TargetIdentity),
                            ("ilBodyFactId", bodyFact.FactId),
                            ("limitation", CallLimitation)
                        }));
                    facts.Add(callFact);
                    callFactsByOffset.Add(call.Offset, callFact);
                    var values = valuesByOffset?.GetValueOrDefault(call.Offset);
                    if (values is not null)
                        facts.Add(FactFactory.Create(manifest,
                            FactTypes.ManagedIlCallValuesObserved, RuleIds.DotNetIlValues,
                            EvidenceTiers.Tier3SyntaxOrTextual, IlEvidence(outcome.SafeLocator),
                            targetSymbol: callIdentity, contractElement: "il-call-operand-origins",
                            properties: CopyToSorted(common, new (string Key, string Value)[]
                            {
                                ("metadataToken", body.MetadataToken),
                                ("ilOffset", call.Offset.ToString(CultureInfo.InvariantCulture)),
                                ("ilBodyFactId", bodyFact.FactId),
                                ("ilCallFactId", callFact.FactId),
                                ("valueSchema", IlCallValueExtractor.Schema),
                                ("valueState", values.State),
                                ("valueRegion", values.Region.ToString(CultureInfo.InvariantCulture)),
                                ("callHasThis", call.StackShape?.HasThis == true ? "true" : "false"),
                                ("callParameterCount", (call.StackShape?.ParameterCount ?? -1).ToString(CultureInfo.InvariantCulture)),
                                ("callShapeSupported", call.StackShape?.Supported == true ? "true" : "false"),
                                ("callByReferenceParameters", call.StackShape?.ByReferenceParameters ?? ""),
                                ("receiverOrigin", System.Text.Json.JsonSerializer.Serialize(PublicOperand(values.Receiver))),
                                ("argumentOrigins", System.Text.Json.JsonSerializer.Serialize(values.Arguments.Select((value, index) =>
                                    call.StackShape is { } shape && index < shape.ByReferenceParameters.Length && shape.ByReferenceParameters[index] == '1'
                                        ? new IlValueOrigin("unknown", "") : PublicOperand(value)))),
                                ("resultOrigin", System.Text.Json.JsonSerializer.Serialize(values.Result)),
                                ("limitation", IlCallValueExtractor.Limitation)
                            })));
                }
                foreach (var gap in (body.ValueFlow?.Gaps ?? []).Concat(
                    returns?.Count > IlCallValueExtractor.MaxRetainedReturnSites ? ["IlValueReturnSiteLimit"] : []))
                    facts.Add(FactFactory.Create(manifest, FactTypes.AnalysisGap, RuleIds.DotNetIlValues,
                        EvidenceTiers.Tier4Unknown, IlEvidence(outcome.SafeLocator),
                        targetSymbol: body.BodyIdentity, contractElement: gap,
                        properties: CopyToSorted(common, new (string Key, string Value)[]
                        {
                            ("metadataToken", body.MetadataToken), ("ilBodyFactId", bodyFact.FactId),
                            ("gapKind", gap), ("valueSchema", IlCallValueExtractor.Schema),
                            ("limitation", IlCallValueExtractor.Limitation)
                        })));
                var commandBindings = IlCommandBindingExtractor.Extract(body, commandBudget);
                foreach (var binding in commandBindings.Bindings)
                {
                    var endpoint = callFactsByOffset[binding.EndpointOffset];
                    var sites = binding.ConfigurationOffsets
                        .Concat(binding.AdapterBindingOffset is { } attachment ? [attachment] : [])
                        .Distinct().Order().Select(offset => callFactsByOffset[offset].FactId).ToArray();
                    facts.Add(FactFactory.Create(manifest, FactTypes.ManagedIlDatabaseCommandCandidate,
                        RuleIds.DotNetIlCommandBinding, EvidenceTiers.Tier3SyntaxOrTextual,
                        IlEvidence(outcome.SafeLocator), targetSymbol: endpoint.TargetSymbol,
                        contractElement: "il-database-command-candidate",
                        properties: CopyToSorted(common, new (string Key, string Value)[]
                        {
                            ("metadataToken", body.MetadataToken), ("ilBodyFactId", bodyFact.FactId),
                            ("ilCallFactId", endpoint.FactId), ("ilOffset", binding.EndpointOffset.ToString(CultureInfo.InvariantCulture)),
                            ("commandBindingSchema", IlCommandBindingExtractor.Schema),
                            ("commandBindingRegion", binding.Region.ToString(CultureInfo.InvariantCulture)),
                            ("commandReceiverOrigin", System.Text.Json.JsonSerializer.Serialize(binding.CommandReceiver)),
                            ("endpointReceiverOrigin", System.Text.Json.JsonSerializer.Serialize(binding.EndpointReceiver)),
                            ("commandTextOrigin", System.Text.Json.JsonSerializer.Serialize(binding.CommandText)),
                            ("commandTypeOrigin", System.Text.Json.JsonSerializer.Serialize(binding.CommandType)),
                            ("configurationCallFactIds", System.Text.Json.JsonSerializer.Serialize(sites)),
                            ("commandBindingMaxTrackedReceivers", IlCommandBindingExtractor.MaxTrackedReceivers.ToString(CultureInfo.InvariantCulture)),
                            ("commandBindingMaxBodyWorkUnits", IlCommandBindingExtractor.MaxWorkUnits.ToString(CultureInfo.InvariantCulture)),
                            ("commandBindingMaxAggregateWorkUnits", provenance.EffectiveLimits.MaxTotalWorkUnits.ToString(CultureInfo.InvariantCulture)),
                            ("limitation", IlCommandBindingExtractor.Limitation)
                        })));
                }
                foreach (var gap in commandBindings.Gaps)
                    facts.Add(FactFactory.Create(manifest, FactTypes.AnalysisGap, RuleIds.DotNetIlCommandBinding,
                        EvidenceTiers.Tier4Unknown, IlEvidence(outcome.SafeLocator),
                        targetSymbol: body.BodyIdentity, contractElement: gap,
                        properties: CopyToSorted(common, new (string Key, string Value)[]
                        {
                            ("metadataToken", body.MetadataToken), ("ilBodyFactId", bodyFact.FactId),
                            ("gapKind", gap), ("commandBindingSchema", IlCommandBindingExtractor.Schema),
                            ("limitation", IlCommandBindingExtractor.Limitation)
                        })));
                var sqlShapes = IlSqlShapeExtractor.Extract(body, commandBindings, sqlShapeBudget);
                foreach (var shape in sqlShapes.Observations)
                {
                    var endpoint = callFactsByOffset[shape.EndpointOffset];
                    var parameters = shape.Parameters.Select(parameter => new
                    {
                        parameter.Origin, parameter.Name, parameter.SqlDbType, parameter.Size,
                        constructorCallFactId = callFactsByOffset[parameter.ConstructorOffset].FactId,
                        attachmentCallFactId = callFactsByOffset[parameter.AttachmentOffset].FactId
                    }).ToArray();
                    facts.Add(FactFactory.Create(manifest, FactTypes.ManagedIlSqlShapeCandidate,
                        RuleIds.DotNetIlSqlShape, EvidenceTiers.Tier3SyntaxOrTextual,
                        IlEvidence(outcome.SafeLocator), targetSymbol: endpoint.TargetSymbol,
                        contractElement: "il-sql-shape-candidate",
                        properties: CopyToSorted(common, new (string Key, string Value)[]
                        {
                            ("metadataToken", body.MetadataToken), ("ilBodyFactId", bodyFact.FactId),
                            ("ilCallFactId", endpoint.FactId), ("sqlShapeSchema", IlSqlShapeExtractor.Schema),
                            ("dataSetOrigin", System.Text.Json.JsonSerializer.Serialize(shape.DataSetOrigin)),
                            ("returnOffset", shape.ReturnOffset.ToString(CultureInfo.InvariantCulture)),
                            ("parameterShapes", System.Text.Json.JsonSerializer.Serialize(parameters)),
                            ("limitation", IlSqlShapeExtractor.Limitation)
                        })));
                }
                foreach (var gap in sqlShapes.Gaps)
                    facts.Add(FactFactory.Create(manifest, FactTypes.AnalysisGap, RuleIds.DotNetIlSqlShape,
                        EvidenceTiers.Tier4Unknown, IlEvidence(outcome.SafeLocator),
                        targetSymbol: body.BodyIdentity, contractElement: gap,
                        properties: CopyToSorted(common, new (string Key, string Value)[]
                        {
                            ("metadataToken", body.MetadataToken), ("ilBodyFactId", bodyFact.FactId),
                            ("gapKind", gap), ("sqlShapeSchema", IlSqlShapeExtractor.Schema),
                            ("limitation", IlSqlShapeExtractor.Limitation)
                        })));
            }
        }

        if (evaluation.LifecycleRoots.Count == 0) EmitSqlObjectPaths(manifest, evaluation, provenance, facts);
        EmitSqlLifecycles(manifest, evaluation, provenance, facts);
        return facts;
    }

    private static void EmitSqlLifecycles(ScanManifest manifest, IlBodyEvaluation evaluation, IlBodyProvenance provenance, List<CodeFact> facts)
    {
        var bodies = evaluation.Inputs.OrderBy(input => input.Outcome.SafeLocator, StringComparer.Ordinal)
            .SelectMany(input => input.Bodies.Select(body => new IlSqlObjectPathExtractor.Body(body, input.Outcome))).ToArray();
        var bodyFacts = facts.Where(f => f.FactType == FactTypes.ManagedIlBodyDeclared)
            .GroupBy(f => (f.Properties["rawFileSha256"], f.TargetSymbol!)).ToDictionary(g => g.Key, g => g.First());
        var budget = new IlWorkBudget(provenance.EffectiveLimits.MaxTotalWorkUnits);
        var sliceDecodeBudget = new IlWorkBudget(provenance.EffectiveLimits.MaxTotalWorkUnits);
        var selected = evaluation.LifecycleRoots.Count > 0;
        var roots = selected ? evaluation.LifecycleRoots.Select(selection => bodies.Single(body =>
            body.Input.RawFileSha256 == selection.BinarySha256 && body.Observation.MetadataToken == selection.MetadataToken))
            : bodies.Where(body => IlSqlLifecycleExtractor.IsRoot(body.Observation));
        foreach (var originalRoot in roots)
        {
            using var slice = selected ? new IlSqlLifecycleSlice(evaluation, bodies, originalRoot, sliceDecodeBudget) : null;
            var root = slice?.Root ?? originalRoot;
            var scopedBodies = slice?.Bodies ?? bodies;
            var sliceReceipt = slice?.Receipt();
            var guarded = IlSqlLifecycleExtractor.IsGuardedRoot(root.Observation);
            foreach (var enabled in guarded ? new[] { false, true } : new[] { false })
            {
                var lifecycleSchema = IlSqlLifecycleExtractor.IsReadRoot(root.Observation)
                    ? IlSqlLifecycleExtractor.Schema : IlSqlLifecycleExtractor.EntrySchema;
                var result = slice?.Failure is { } sliceFailure
                    ? new IlSqlLifecycleExtractor.Result([], [], null, [sliceFailure])
                    : ScanProgressAmbient.Observe(ScanProgressStages.IlLifecycle, () => IlSqlLifecycleExtractor.Extract(root, scopedBodies, enabled, budget, evaluation.OpaqueRootInitializers, evaluation.ExternalTraceBoundary, evaluation.CompiledScalarBoundary, evaluation.InfoMessageBoundary));
                if (result.OpaqueInitializers is { Count: > 0 } || result.ExternalTraces is { Count: > 0 } || result.TraceStringComputations is { Count: > 0 }) lifecycleSchema = IlSqlLifecycleExtractor.OpaqueEntrySchema;
                if (result.InfoMessages is { Count: > 0 }) lifecycleSchema = IlSqlLifecycleExtractor.InfoMessageSchema;
                if (result.StateAlternatives is { Count: > 0 }) lifecycleSchema = IlSqlLifecycleExtractor.StateAlternativesSchema;
                var common = new Dictionary<string, string>(CommonProperties(provenance, root.Input), StringComparer.Ordinal);
                if (evaluation.InfoMessageBoundary) common["infoMessagePolicy"] = IlSqlLifecycleExtractor.InfoMessagePolicy;
                if (evaluation.CompiledScalarBoundary)
                    common["compiledScalarPolicy"] = IlSqlLifecycleExtractor.CompiledScalarPolicy;
                if (evaluation.ExternalTraceBoundary)
                    common["externalTracePolicy"] = IlSqlLifecycleExtractor.ExternalTracePolicy;
                if (evaluation.OpaqueRootInitializers)
                    common["opaqueInitializerPolicy"] = IlSqlLifecycleExtractor.OpaquePolicy;
                if (result.StateAlternatives is { Count: > 0 })
                    common["stateControlPolicy"] = IlSqlLifecycleExtractor.StateControlPolicy;
                if (sliceReceipt is not null)
                {
                    common["lifecycleSlice"] = SqlServerSchemaEvidence.Json(sliceReceipt);
                    common["lifecycleCoverage"] = "selected-root-slice-only";
                }
                var guard = guarded ? "argument:0=" + (enabled ? "true" : "false") : "unconditional";
                if (result.StateAlternatives is { Count: > 0 })
                {
                    // Bounded unbound-state alternatives: one candidate fact carries every
                    // explored path; no alternative is promoted to unconditional entry evidence.
                    var alternativeLinks = result.StateAlternatives.SelectMany(alternative => alternative.Completion?.Bodies ?? [])
                        .Select(site => new { site.Body, site.Binary, site.Invocation,
                            bodyFactId = bodyFacts[(site.Binary, site.Body)].FactId }).ToArray();
                    facts.Add(FactFactory.Create(manifest, FactTypes.ManagedIlSqlLifecycleCandidate,
                        RuleIds.DotNetIlSqlLifecycle, EvidenceTiers.Tier3SyntaxOrTextual, IlEvidence(root.Input.SafeLocator),
                        targetSymbol: root.Observation.BodyIdentity, contractElement: "il-sql-lifecycle:" + guard,
                        properties: CopyToSorted(common, new (string Key, string Value)[]
                        {
                            ("sqlLifecycleSchema", lifecycleSchema), ("guard", guard),
                            ("rootBodyFactId", bodyFacts[(root.Input.RawFileSha256!, root.Observation.BodyIdentity)].FactId),
                            ("lifecycle", SqlServerSchemaEvidence.Json(result)),
                            ("bodyLinks", SqlServerSchemaEvidence.Json(alternativeLinks)),
                            ("limitation", IlSqlLifecycleExtractor.Limitation)
                        })));
                }
                else if ((result.Returned is not null || result.Entry is not null) && result.Executions.Count > 0)
                {
                    var links = result.Bodies!.Select(site => new { site.Body, site.Binary, site.Invocation,
                        bodyFactId = bodyFacts[(site.Binary, site.Body)].FactId }).ToArray();
                    facts.Add(FactFactory.Create(manifest, result.Entry is null ? FactTypes.ManagedIlSqlLifecycleCandidate : FactTypes.ManagedIlSqlLifecycleEntryCandidate,
                        RuleIds.DotNetIlSqlLifecycle, EvidenceTiers.Tier3SyntaxOrTextual, IlEvidence(root.Input.SafeLocator),
                        targetSymbol: root.Observation.BodyIdentity, contractElement: "il-sql-lifecycle:" + guard,
                        properties: CopyToSorted(common, new (string Key, string Value)[]
                        {
                            ("sqlLifecycleSchema", lifecycleSchema), ("guard", guard),
                            ("rootBodyFactId", bodyFacts[(root.Input.RawFileSha256!, root.Observation.BodyIdentity)].FactId),
                            ("lifecycle", SqlServerSchemaEvidence.Json(result)),
                            ("bodyLinks", SqlServerSchemaEvidence.Json(links)),
                            ("limitation", IlSqlLifecycleExtractor.Limitation)
                        })));
                }
                foreach (var gap in result.Gaps)
                {
                    var gapProperties = CopyToSorted(common, new (string Key, string Value)[]
                    {
                        ("sqlLifecycleSchema", lifecycleSchema), ("guard", guard), ("gapKind", gap),
                        ("limitation", IlSqlLifecycleExtractor.Limitation)
                    });
                    if (result.InfoMessageFailure is not null) gapProperties["infoMessageFailure"] = SqlServerSchemaEvidence.Json(result.InfoMessageFailure);
                    if (result.OpaqueInitializerRejection is not null)
                        gapProperties["opaqueInitializerRejection"] = SqlServerSchemaEvidence.Json(result.OpaqueInitializerRejection);
                    if (result.FieldAccessFailure is not null)
                        gapProperties["fieldAccessFailure"] = SqlServerSchemaEvidence.Json(result.FieldAccessFailure);
                    if (result.ExternalTraceFailure is not null)
                        gapProperties["externalTraceFailure"] = SqlServerSchemaEvidence.Json(result.ExternalTraceFailure);
                    if (result.OpcodeFailure is not null)
                        gapProperties["opcodeFailure"] = SqlServerSchemaEvidence.Json(result.OpcodeFailure);
                    if (result.FieldOwnerFailure is not null)
                        gapProperties["fieldOwnerFailure"] = SqlServerSchemaEvidence.Json(result.FieldOwnerFailure);
                    if (result.MetadataFailure is not null)
                        gapProperties["bodyMetadataFailure"] = SqlServerSchemaEvidence.Json(result.MetadataFailure);
                    facts.Add(FactFactory.Create(manifest, FactTypes.AnalysisGap, RuleIds.DotNetIlSqlLifecycle,
                        EvidenceTiers.Tier4Unknown, IlEvidence(root.Input.SafeLocator), targetSymbol: root.Observation.BodyIdentity,
                        contractElement: "il-sql-lifecycle:" + guard + ":" + gap, properties: gapProperties));
                }
            }
        }
    }

    private static void EmitSqlObjectPaths(ScanManifest manifest, IlBodyEvaluation evaluation, IlBodyProvenance provenance, List<CodeFact> facts)
    {
        var bodies = evaluation.Inputs.OrderBy(input => input.Outcome.SafeLocator, StringComparer.Ordinal)
            .SelectMany(input => input.Bodies.Select(body => new IlSqlObjectPathExtractor.Body(body, input.Outcome))).ToArray();
        var bodyFacts = facts.Where(fact => fact.FactType == FactTypes.ManagedIlBodyDeclared)
            .GroupBy(fact => (fact.Properties["rawFileSha256"], fact.TargetSymbol!))
            .ToDictionary(group => group.Key, group => group.First());
        var budget = new IlWorkBudget(provenance.EffectiveLimits.MaxTotalWorkUnits);
        foreach (var root in bodies.Where(body => IlSqlObjectPathExtractor.IsRoot(body.Observation)))
        {
            var common = CommonProperties(provenance, root.Input);
            var result = IlSqlObjectPathExtractor.Extract(root, bodies, budget);
            foreach (var path in result.Paths)
            {
                var sites = path.Sites.Select(site => new
                {
                    site.BodyIdentity, site.BinarySha256, site.Offset, site.Frame, site.Action,
                    bodyFactId = bodyFacts[(site.BinarySha256, site.BodyIdentity)].FactId
                }).ToArray();
                facts.Add(FactFactory.Create(manifest, FactTypes.ManagedIlSqlObjectPathCandidate,
                    RuleIds.DotNetIlSqlObjectPath, EvidenceTiers.Tier3SyntaxOrTextual, IlEvidence(root.Input.SafeLocator),
                    targetSymbol: root.Observation.BodyIdentity, contractElement: "il-sql-object-path-candidate",
                    properties: CopyToSorted(common, new (string Key, string Value)[]
                    {
                        ("sqlObjectPathSchema", IlSqlObjectPathExtractor.Schema),
                        ("rootBodyFactId", bodyFacts[(root.Input.RawFileSha256!, root.Observation.BodyIdentity)].FactId),
                        ("executionIdentity", path.ExecutionIdentity),
                        ("commandText", System.Text.Json.JsonSerializer.Serialize(path.CommandText)),
                        ("commandType", System.Text.Json.JsonSerializer.Serialize(path.CommandType)),
                        ("parameterShapes", System.Text.Json.JsonSerializer.Serialize(path.Parameters)),
                        ("columnIdentity", System.Text.Json.JsonSerializer.Serialize(path.Column)),
                        ("filledDataSetIdentity", path.FilledDataSetIdentity), ("consumerDataSetIdentity", path.ConsumerDataSetIdentity),
                        ("consumerRelation", path.Relation), ("sites", System.Text.Json.JsonSerializer.Serialize(sites)),
                        ("limitation", IlSqlObjectPathExtractor.Limitation)
                    }.Concat(path.ConsumerTableIdentity is null ? [] : new (string Key, string Value)[]
                    {
                        ("consumerTableIdentity", path.ConsumerTableIdentity),
                        ("filledTableIdentity", path.FilledTableIdentity!)
                    }).ToArray())));
            }
            foreach (var gap in result.Gaps)
                facts.Add(FactFactory.Create(manifest, FactTypes.AnalysisGap, RuleIds.DotNetIlSqlObjectPath,
                    EvidenceTiers.Tier4Unknown, IlEvidence(root.Input.SafeLocator), targetSymbol: root.Observation.BodyIdentity,
                    contractElement: gap, properties: CopyToSorted(common, new (string Key, string Value)[]
                    {
                        ("sqlObjectPathSchema", IlSqlObjectPathExtractor.Schema), ("gapKind", gap),
                        ("limitation", IlSqlObjectPathExtractor.Limitation)
                    })));
        }
    }

    internal static List<string> CompareBodies(IlReaderResult cecil, IlReaderResult srm)
    {
        var disagreements = new List<string>();
        if (!string.Equals(cecil.AssemblyIdentity, srm.AssemblyIdentity, StringComparison.Ordinal)
            || !string.Equals(cecil.ModuleName, srm.ModuleName, StringComparison.Ordinal)
            || !string.Equals(cecil.ModuleMvid, srm.ModuleMvid, StringComparison.Ordinal))
            disagreements.Add("assembly");
        var left = cecil.Bodies.ToDictionary(item => item.MetadataToken, StringComparer.Ordinal);
        var right = srm.Bodies.ToDictionary(item => item.MetadataToken, StringComparer.Ordinal);
        foreach (var token in left.Keys.Concat(right.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!left.TryGetValue(token, out var cecilBody) || !right.TryGetValue(token, out var srmBody))
            {
                disagreements.Add(token);
                continue;
            }
            var callShapesAgree = cecilBody.Calls.Count == srmBody.Calls.Count
                && cecilBody.Calls.Zip(srmBody.Calls, (first, second) =>
                    first.Offset == second.Offset
                    && string.Equals(first.Opcode, second.Opcode, StringComparison.Ordinal)
                    && string.Equals(first.ReferenceKind, second.ReferenceKind, StringComparison.Ordinal)
                    && string.Equals(first.ReferenceToken, second.ReferenceToken, StringComparison.Ordinal)
                    && string.Equals(first.TargetIdentity, second.TargetIdentity, StringComparison.Ordinal)).All(equal => equal);
            var agrees = string.Equals(cecilBody.BodyIdentity, srmBody.BodyIdentity, StringComparison.Ordinal)
                && cecilBody.InstructionCount == srmBody.InstructionCount
                && string.Equals(cecilBody.InstructionsSha256, srmBody.InstructionsSha256, StringComparison.Ordinal)
                && cecilBody.LocalCount == srmBody.LocalCount
                && string.Equals(cecilBody.LocalsSha256, srmBody.LocalsSha256, StringComparison.Ordinal)
                && cecilBody.ExceptionRegionCount == srmBody.ExceptionRegionCount
                && string.Equals(cecilBody.ExceptionRegionsSha256, srmBody.ExceptionRegionsSha256, StringComparison.Ordinal)
                && string.Equals(cecilBody.MaxStack, srmBody.MaxStack, StringComparison.Ordinal)
                && cecilBody.InitLocals == srmBody.InitLocals
                && callShapesAgree;
            if (agrees)
                continue;
            disagreements.Add(token);
        }
        return disagreements;
    }

    internal static IReadOnlyList<IlBodyObservation> AgreedBodies(IlReaderResult cecil, IReadOnlyList<string> disagreements)
    {
        if (disagreements.Contains("assembly", StringComparer.Ordinal))
            return [];
        var disputed = disagreements.ToHashSet(StringComparer.Ordinal);
        return cecil.Bodies.Where(body => !disputed.Contains(body.MetadataToken)).ToArray();
    }

    internal static IReadOnlyList<IlBodyObservation> AgreeValueFlows(IReadOnlyList<IlBodyObservation> cecil,
        IReadOnlyList<IlBodyObservation> srm)
    {
        var byToken = srm.ToDictionary(body => body.MetadataToken, StringComparer.Ordinal);
        return cecil.Select(body =>
        {
            byToken.TryGetValue(body.MetadataToken, out var other);
            var raw = other is not null && RawBodiesAgree(body, other);
            var callShapes = other is not null && body.Calls.Count == other.Calls.Count
                && body.Calls.Zip(other.Calls, (left, right) => left.StackShape == right.StackShape).All(equal => equal);
            var values = raw && callShapes && System.Text.Json.JsonSerializer.Serialize(body.ValueFlow)
                == System.Text.Json.JsonSerializer.Serialize(other!.ValueFlow);
            // The object interpreter uses independently decoded instructions and call
            // shapes, not the generic operand-value summary. Its evidence is agreed
            // separately; a value-summary refusal must not erase an agreed body.
            var objects = raw && callShapes && body.ObjectBody is not null && other!.ObjectBody is not null
                && System.Text.Json.JsonSerializer.Serialize(body.ObjectBody)
                    == System.Text.Json.JsonSerializer.Serialize(other.ObjectBody);
            var reason = objects ? "dual-reader-agreed"
                : other is null ? "object-body-peer-missing"
                : !raw ? "object-body-raw-disagreement"
                : !callShapes ? "object-body-call-shape-disagreement"
                : body.ObjectBody is null ? body.ObjectBodyRetention?.Reason ?? "object-body-cecil-unavailable"
                : other.ObjectBody is null ? other.ObjectBodyRetention?.Reason ?? "object-body-srm-unavailable"
                : "object-body-reader-disagreement";
            var retention = (body.ObjectBody is not null && other?.ObjectBody is null
                ? other?.ObjectBodyRetention : body.ObjectBodyRetention)
                ?? new("unavailable", reason, -1, 0, 0, 0);
            return body with
            {
                ValueFlow = values ? body.ValueFlow : new([], ["IlValueReaderDisagreementOrLimit"]),
                ObjectBody = objects ? body.ObjectBody : null,
                ObjectBodyRetention = retention with { State = objects ? "retained" : "unavailable", Reason = reason },
                ObjectBodyAgreement = new(raw, callShapes, values, objects, body.ObjectBodyRetention, other?.ObjectBodyRetention)
            };
        }).ToArray();
    }

    internal static bool RawBodiesAgree(IlBodyObservation left, IlBodyObservation right)
        => left.BodyIdentity == right.BodyIdentity && left.MethodIdentity == right.MethodIdentity
            && left.InstructionCount == right.InstructionCount && left.InstructionsSha256 == right.InstructionsSha256
            && left.OpcodesSha256 == right.OpcodesSha256 && left.LocalCount == right.LocalCount
            && left.LocalsSha256 == right.LocalsSha256 && left.ExceptionRegionCount == right.ExceptionRegionCount
            && left.ExceptionRegionsSha256 == right.ExceptionRegionsSha256 && left.MaxStack == right.MaxStack
            && left.InitLocals == right.InitLocals && left.Calls.Count == right.Calls.Count
            && left.Calls.Zip(right.Calls, (a, b) => a.Offset == b.Offset && a.Opcode == b.Opcode
                && a.ReferenceKind == b.ReferenceKind && a.ReferenceToken == b.ReferenceToken
                && a.TargetIdentity == b.TargetIdentity).All(equal => equal);

    internal static IlObjectBody? RetainObjectBody(int instructionCount, int declaringFieldCount,
        Func<IlObjectBody> create, IlBodyLimits limits, IlWorkBudget budget, out IlObjectBodyRetention retention)
    {
        var before = budget.Consumed;
        var reason = instructionCount > 256 ? "object-body-instruction-limit"
            : declaringFieldCount > 64 ? "object-body-field-limit" : null;
        if (reason is not null)
        {
            retention = new("unavailable", reason, declaringFieldCount, 0, before, budget.Maximum);
            return null;
        }
        var candidate = create();
        // Charge the strings actually retained, including signature/owner/name and
        // private field identities. Do not reserve MaxTextLength for every declared
        // field on every method; public/static fields are not retained in this lane.
        var characters = candidate.Instructions.Sum(text => (long)text.Length)
            + (candidate.ExceptionRegions ?? []).Sum(text => (long)text.Length)
            + candidate.PrivateInstanceFields.Sum(text => (long)text.Length)
            + candidate.MemberReference.Length + candidate.OwnerType.Length + candidate.Name.Length + (candidate.BaseType?.Length ?? 0);
        if (candidate.MemberReference.Length > limits.MaxTextLength || candidate.OwnerType.Length > limits.MaxTextLength
            || candidate.Name.Length > limits.MaxTextLength || candidate.BaseType?.Length > limits.MaxTextLength || candidate.PrivateInstanceFields.Any(field => field.Length > limits.MaxTextLength))
            reason = "object-body-text-limit";
        else if (!budget.TryConsume(characters)) reason = "object-body-retention-budget";
        retention = new(reason is null ? "retained" : "unavailable", reason ?? "reader-retained",
            declaringFieldCount, characters, before, budget.Maximum);
        return reason is null ? candidate : null;
    }

    // Addresses are used internally for conservative object exposure, never
    // advertised as values or substituted for a caller's scalar argument.
    private static IlValueOrigin PublicOperand(IlValueOrigin value) => IlValueAddresses.IsAddress(value) || value.Kind == "address-unavailable" ? new("unknown", "") : value;

    // Internal so the rewrite lane can rebuild the same dual-reader contract
    // over its own paired inputs instead of duplicating IL decoding.
    internal static IlReaderResult ReadCecilBodies(
        byte[] bytes,
        IlBodyLimits limits,
        IlWorkBudget budget,
        CancellationToken cancellationToken,
        bool retainDiagnosticInstructions = false,
        IlWorkBudget? valueBudget = null, IlWorkBudget? objectBudget = null, IReadOnlySet<string>? selectedMethods = null)
    {
        valueBudget ??= new IlWorkBudget(limits.MaxTotalWorkUnits);
        objectBudget ??= new IlWorkBudget(Math.Min(limits.MaxTotalWorkUnits, 1_048_576));
        using var stream = new MemoryStream(bytes, writable: false);
        using var resolver = new ManagedMetadataExtractor.RejectingAssemblyResolver();
        using var module = CecilModuleDefinition.ReadModule(stream, new ReaderParameters
        {
            AssemblyResolver = resolver,
            InMemory = true,
            ReadSymbols = false,
            ReadingMode = ReadingMode.Deferred
        });
        if (module.Assembly is null)
            throw new IlEvidenceException("ManagedNetmoduleInputUnsupported");
        using var rawStream = new MemoryStream(bytes, writable: false);
        using var rawPe = new PEReader(rawStream, PEStreamOptions.LeaveOpen);
        var rawReader = rawPe.GetMetadataReader();
        var assemblyIdentity = CecilSelfAssemblyIdentity(module);
        var bodies = new List<IlBodyObservation>();
        var bodyCount = 0;
        foreach (var type in ManagedMetadataExtractor.FlattenTypes(module.Types))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var method in type.Methods)
            {
                if (!method.HasBody)
                    continue;
                bodyCount++;
                if (bodyCount > limits.MaxBodyCount)
                    throw new IlEvidenceException("IlBodyCountLimitExceeded");
                if (!budget.TryConsume(1))
                    throw new IlEvidenceException("IlTotalWorkLimitExceeded");
                if (selectedMethods is not null && !selectedMethods.Contains(ManagedMetadataExtractor.Token(method.MetadataToken.ToUInt32()))) continue;
                var rawMethod = rawReader.GetMethodDefinition(
                    MetadataTokens.MethodDefinitionHandle(checked((int)method.MetadataToken.RID)));
                var rawIl = rawPe.GetMethodBody(rawMethod.RelativeVirtualAddress).GetILBytes()
                    ?? throw new IlEvidenceException("MalformedIlBody");
                bodies.Add(ReadCecilBody(method, rawIl, assemblyIdentity, limits, budget, retainDiagnosticInstructions, valueBudget, objectBudget));
            }
        }
        return new IlReaderResult(assemblyIdentity, module.Name, module.Mvid.ToString("D", CultureInfo.InvariantCulture), bodies);
    }

    private static IlBodyObservation ReadCecilBody(
        CecilMethodDefinition method,
        byte[] rawIl,
        string assemblyIdentity,
        IlBodyLimits limits,
        IlWorkBudget budget,
        bool retainDiagnosticInstructions,
        IlWorkBudget valueBudget, IlWorkBudget objectBudget)
    {
        var body = method.Body;
        var memberKind = method.IsConstructor ? "constructor" : "method";
        var signature = ManagedMetadataExtractor.MethodSignature(
            ManagedMetadataExtractor.FormatType(method.ReturnType),
            method.Parameters.Select(parameter => ManagedMetadataExtractor.FormatType(parameter.ParameterType)),
            method.GenericParameters.Count,
            method.CallingConvention == MethodCallingConvention.VarArg ? "vararg" : "default",
            method.HasThis,
            method.ExplicitThis);
        var methodIdentity = $"{ManagedMetadataExtractor.TypeIdentity(assemblyIdentity, (CecilTypeDefinition)method.DeclaringType!)}|{memberKind}:{ManagedMetadataExtractor.EncodeIdentityComponent(method.Name)}|{signature}";
        if (methodIdentity.Length > limits.MaxTextLength)
            throw new IlEvidenceException("IlTextLimitExceeded");
        var instructions = new List<string>();
        var opcodes = new List<string>();
        var calls = new List<IlCallObservation>();
        foreach (var instruction in body.Instructions)
        {
            if (instructions.Count >= limits.MaxInstructionsPerBody)
                throw new IlEvidenceException("IlInstructionLimitExceeded");
            if (!budget.TryConsume(1))
                throw new IlEvidenceException("IlTotalWorkLimitExceeded");
            var opcodeName = CanonicalCecilOpcodeName(instruction, rawIl);
            var operand = CecilOperand(instruction, opcodeName, rawIl, calls, budget, assemblyIdentity, limits);
            instructions.Add($"{instructions.Count.ToString(CultureInfo.InvariantCulture)}:{instruction.Offset.ToString("x", CultureInfo.InvariantCulture)}:{opcodeName}:{operand}");
            opcodes.Add(opcodeName);
        }
        var locals = body.Variables
            .Select(variable => (variable.Index, Type: CecilLocalType(variable.VariableType)))
            .OrderBy(item => item.Index, Comparer<int>.Default)
            .ToArray();
        if (locals.Length > limits.MaxLocalsPerBody)
            throw new IlEvidenceException("IlLocalLimitExceeded");
        if (!budget.TryConsume(locals.Length))
            throw new IlEvidenceException("IlTotalWorkLimitExceeded");
        var handlers = body.ExceptionHandlers.Select(handler => CecilHandler(handler, body.CodeSize)).ToArray();
        if (handlers.Length > limits.MaxExceptionRegionsPerBody)
            throw new IlEvidenceException("IlExceptionRegionLimitExceeded");
        if (!budget.TryConsume(handlers.Length))
            throw new IlEvidenceException("IlTotalWorkLimitExceeded");
        var instructionsSha256 = DigestLines(instructions);
        var opcodesSha256 = DigestLines(opcodes);
        var localsSha256 = DigestLines(locals.Select(local => $"{local.Index.ToString(CultureInfo.InvariantCulture)}:{local.Type}"));
        var exceptionRegionsSha256 = DigestLines(handlers);
        var canonical = CanonicalBody(body.MaxStackSize, body.InitLocals, instructions.Count, instructionsSha256, locals.Length, localsSha256, handlers.Length, exceptionRegionsSha256);
        var bodySha256 = ManagedMetadataExtractor.Sha256(Encoding.UTF8.GetBytes(canonical));
        var bodyIdentity = $"{methodIdentity}|il-body:instructions:{instructions.Count.ToString(CultureInfo.InvariantCulture)}:sha256:{bodySha256}";
        if (bodyIdentity.Length > limits.MaxTextLength)
            throw new IlEvidenceException("IlTextLimitExceeded");
        var objectBody = RetainObjectBody(instructions.Count, method.DeclaringType.Fields.Count,
            () => new IlObjectBody(instructions, $"memberref|type:{ScopedNamedType(method.DeclaringType)}|member:{ManagedMetadataExtractor.EncodeIdentityComponent(method.Name)}|{signature}",
                    ScopedNamedType(method.DeclaringType), method.Name, method.Parameters.Count, method.HasThis,
                    method.ReturnType.FullName != "System.Void", method.IsVirtual,
                    method.HasGenericParameters || method.DeclaringType.HasGenericParameters,
                    method.DeclaringType.Fields.Where(field => field.IsPrivate && !field.IsStatic)
                        .Select(field => CecilFieldIdentity(field)["fielddef|".Length..]).Order(StringComparer.Ordinal).ToArray(), handlers,
                    method.DeclaringType.BaseType is { } baseType ? CecilTypeOperandIdentity(baseType) : null), limits, objectBudget, out var objectRetention);
        return new IlBodyObservation(
            ManagedMetadataExtractor.Token(unchecked((int)method.MetadataToken.ToUInt32())),
            methodIdentity,
            instructions.Count,
            instructionsSha256,
            opcodesSha256,
            locals.Length,
            localsSha256,
            handlers.Length,
            exceptionRegionsSha256,
            body.MaxStackSize.ToString(CultureInfo.InvariantCulture),
            body.InitLocals,
            bodyIdentity,
            bodySha256,
            calls,
            retainDiagnosticInstructions ? instructions : null,
            ExtractCallValues(instructions, calls, body.MaxStackSize, handlers, valueBudget),
            objectBody, objectRetention);
    }

    // Cecil and Reflection.Emit may use different display names for an opcode.
    // Use the raw-reader name only after confirming Cecil decoded the same
    // numeric opcode bytes at the same offset. A real opcode disagreement is
    // left intact for the dual-reader comparison to withhold that method.
    internal static string CanonicalCecilOpcodeName(Instruction instruction, byte[] rawIl)
    {
        var offset = instruction.Offset;
        if (offset < 0 || offset >= rawIl.Length)
            return instruction.OpCode.Name;
        var first = rawIl[offset];
        if (first == 0xfe)
        {
            if (offset + 1 >= rawIl.Length ||
                unchecked((ushort)instruction.OpCode.Value) != (ushort)(0xfe00 | rawIl[offset + 1]))
                return instruction.OpCode.Name;
            return MultiByteOpcodes().TryGetValue(rawIl[offset + 1], out var opcode)
                ? opcode.Name! : instruction.OpCode.Name;
        }
        if (unchecked((ushort)instruction.OpCode.Value) != first)
            return instruction.OpCode.Name;
        return SingleByteOpcodes().TryGetValue(first, out var single)
            ? single.Name! : instruction.OpCode.Name;
    }

    private static string CecilOperand(Instruction instruction, string opcodeName, byte[] rawIl, List<IlCallObservation> calls, IlWorkBudget budget, string selfAssemblyIdentity, IlBodyLimits limits)
    {
        switch (instruction.OpCode.OperandType)
        {
            case OperandType.InlineNone:
                return "-";
            case OperandType.ShortInlineBrTarget or OperandType.InlineBrTarget:
                return $"br:0x{((Instruction)instruction.Operand!).Offset:x}";
            case OperandType.InlineSwitch:
                var targets = (Instruction[])instruction.Operand!;
                if (!budget.TryConsume(targets.Length))
                    throw new IlEvidenceException("IlTotalWorkLimitExceeded");
                return $"sw:{targets.Length.ToString(CultureInfo.InvariantCulture)}[{string.Join(",", targets.Select(target => $"0x{target.Offset:x}"))}]";
            case OperandType.ShortInlineI:
                // ldc.i4.s is signed; prefix operands such as unaligned.
                // are stored by Cecil as unsigned bytes.
                var shortInteger = instruction.Operand switch
                {
                    sbyte signed => (int)signed,
                    byte unsigned => unsigned,
                    _ => throw new IlEvidenceException("MalformedIlBody")
                };
                return $"i:{shortInteger.ToString(CultureInfo.InvariantCulture)}";
            case OperandType.InlineI:
                return $"i:{((int)instruction.Operand!).ToString(CultureInfo.InvariantCulture)}";
            case OperandType.InlineI8:
                return $"i:{((long)instruction.Operand!).ToString(CultureInfo.InvariantCulture)}";
            case OperandType.ShortInlineR:
                return $"r:{BitConverter.SingleToInt32Bits((float)instruction.Operand!).ToString("x8", CultureInfo.InvariantCulture)}";
            case OperandType.InlineR:
                return $"r:{BitConverter.DoubleToInt64Bits((double)instruction.Operand!).ToString("x16", CultureInfo.InvariantCulture)}";
            case OperandType.InlineString:
                var text = (string)instruction.Operand!;
                if (text.Length > limits.MaxTextLength)
                    throw new IlEvidenceException("IlTextLimitExceeded");
                return $"str:{text.Length.ToString(CultureInfo.InvariantCulture)}:{UserStringDigest(text)}";
            case OperandType.ShortInlineVar or OperandType.InlineVar or OperandType.ShortInlineArg or OperandType.InlineArg:
                return $"v:{CecilVariableIndex(instruction.Operand!)}";
            case OperandType.InlineMethod:
                if (!budget.TryConsume(1))
                    throw new IlEvidenceException("IlTotalWorkLimitExceeded");
                var target = CecilMethodTarget((MethodReference)instruction.Operand!, selfAssemblyIdentity, limits);
                if (IsCallObservationOpcode(opcodeName))
                {
                    var reference = (MethodReference)instruction.Operand!;
                    calls.Add(new IlCallObservation(instruction.Offset, opcodeName, target.Kind, target.Token, target.Identity,
                        new(reference.Parameters.Count, reference.HasThis, reference.ReturnType.MetadataType != MetadataType.Void,
                            reference.CallingConvention != MethodCallingConvention.VarArg && !reference.ExplicitThis
                            && !reference.ReturnType.IsByReference,
                            string.Concat(reference.Parameters.Select(parameter => parameter.ParameterType.IsByReference ? '1' : '0')))));
                }
                return $"m:{target.Kind}:{target.Token}:{target.Identity}";
            case OperandType.InlineType:
                var typeIdentity = CecilTypeOperandIdentity((Mono.Cecil.TypeReference)instruction.Operand!);
                if (typeIdentity.Length > limits.MaxTextLength)
                    throw new IlEvidenceException("IlTextLimitExceeded");
                if (opcodeName == "constrained.")
                {
                    if (!budget.TryConsume(1))
                        throw new IlEvidenceException("IlTotalWorkLimitExceeded");
                    calls.Add(new IlCallObservation(
                        instruction.Offset,
                        opcodeName,
                        "constrainedtype",
                        "-",
                        typeIdentity));
                }
                return $"t:{typeIdentity}";
            case OperandType.InlineField:
                if (instruction.Operand is not FieldReference field)
                    throw new IlEvidenceException("IlOperandEncodingUnsupported");
                return BoundedOperand("field", RawCecilToken(field), CecilFieldIdentity(field), limits);
            case OperandType.InlineTok:
                return instruction.Operand switch
                {
                    Mono.Cecil.TypeReference type => BoundedOperand("type",
                        CecilInlineTypeToken(type, rawIl, instruction.Offset + instruction.OpCode.Size),
                        CecilTypeOperandIdentity(type), limits),
                    FieldReference tokenField => BoundedOperand("field", RawCecilToken(tokenField), CecilFieldIdentity(tokenField), limits),
                    MethodReference tokenMethod => BoundedOperand("method", RawCecilToken(tokenMethod), CecilMethodTarget(tokenMethod, selfAssemblyIdentity, limits).Identity, limits),
                    _ => throw new IlEvidenceException("IlOperandEncodingUnsupported")
                };
            case OperandType.InlineSig:
                if (instruction.Operand is not CallSite callSite)
                    throw new IlEvidenceException("IlOperandEncodingUnsupported");
                var sentinelCount = callSite.Parameters.Count(parameter => parameter.ParameterType is SentinelType);
                if (sentinelCount > 1 || sentinelCount == 1 && ((int)callSite.CallingConvention & 0x0f) != 5)
                    throw new IlEvidenceException("IlOperandEncodingUnsupported");
                var cecilSignature = CanonicalCallSite(
                    ((int)callSite.CallingConvention & 0x0f), callSite.HasThis, callSite.ExplicitThis,
                    callSite.Parameters.TakeWhile(parameter => parameter.ParameterType is not SentinelType).Count(),
                    ManagedMetadataExtractor.FormatType(callSite.ReturnType),
                    callSite.Parameters.Select(parameter => ManagedMetadataExtractor.FormatType(parameter.ParameterType)));
                if (cecilSignature.Length > limits.MaxTextLength)
                    throw new IlEvidenceException("IlTextLimitExceeded");
                if (opcodeName == "calli")
                    calls.Add(new IlCallObservation(instruction.Offset, "calli", "standalonesig",
                        RawCecilToken(callSite), cecilSignature));
                return $"sig:{RawCecilToken(callSite)}:{cecilSignature}";
            default:
                throw new IlEvidenceException("IlOperandEncodingUnsupported");
        }
    }

    internal static string ObjectMemberReference(CecilMethodDefinition method)
    {
        var signature = ManagedMetadataExtractor.MethodSignature(ManagedMetadataExtractor.FormatType(method.ReturnType),
            method.Parameters.Select(parameter => ManagedMetadataExtractor.FormatType(parameter.ParameterType)),
            method.GenericParameters.Count, method.CallingConvention == MethodCallingConvention.VarArg ? "vararg" : "default",
            method.HasThis, method.ExplicitThis);
        return $"memberref|type:{ScopedNamedType(method.DeclaringType)}|member:{ManagedMetadataExtractor.EncodeIdentityComponent(method.Name)}|{signature}";
    }

    private static (string Kind, string Token, string Identity) CecilMethodTarget(MethodReference reference, string selfAssemblyIdentity, IlBodyLimits limits)
    {
        if (reference is GenericInstanceMethod generic)
        {
            var element = CecilMethodTarget(generic.ElementMethod, selfAssemblyIdentity, limits);
            var arguments = string.Join(",", generic.GenericArguments.Select(argument => ManagedMetadataExtractor.FormatType(argument)));
            return BoundedTarget(
                "methodspec",
                ManagedMetadataExtractor.Token(generic.MetadataToken.ToUInt32()),
                $"{element.Identity}|gargs:<{arguments}>",
                limits);
        }
        var callingConvention = reference.CallingConvention == MethodCallingConvention.VarArg ? "vararg" : "default";
        if (reference is CecilMethodDefinition definition)
        {
            var memberKind = definition.IsConstructor ? "constructor" : "method";
            var signature = ManagedMetadataExtractor.MethodSignature(
                ManagedMetadataExtractor.FormatType(definition.ReturnType),
                definition.Parameters.Select(parameter => ManagedMetadataExtractor.FormatType(parameter.ParameterType)),
                definition.GenericParameters.Count,
                callingConvention,
                definition.HasThis,
                definition.ExplicitThis);
            return BoundedTarget(
                "methoddef",
                ManagedMetadataExtractor.Token(definition.MetadataToken.ToUInt32()),
                $"{ManagedMetadataExtractor.TypeIdentity(selfAssemblyIdentity, (CecilTypeDefinition)definition.DeclaringType!)}|{memberKind}:{ManagedMetadataExtractor.EncodeIdentityComponent(definition.Name)}|{signature}",
                limits);
        }
        var referenceSignature = ManagedMetadataExtractor.MethodSignature(
            ManagedMetadataExtractor.FormatType(reference.ReturnType),
            reference.Parameters.Select(parameter => ManagedMetadataExtractor.FormatType(parameter.ParameterType)),
            reference.GenericParameters.Count,
            callingConvention,
            reference.HasThis,
            reference.ExplicitThis)
            + (callingConvention == "vararg"
                ? $"|required:{reference.Parameters.TakeWhile(parameter => parameter.ParameterType is not SentinelType).Count().ToString(CultureInfo.InvariantCulture)}"
                : "");
        return BoundedTarget(
            "memberref",
            ManagedMetadataExtractor.Token(reference.MetadataToken.ToUInt32()),
            $"memberref|type:{CecilTypeOperandIdentity(reference.DeclaringType!)}|member:{ManagedMetadataExtractor.EncodeIdentityComponent(reference.Name)}|{referenceSignature}",
            limits);
    }

    private static (string Kind, string Token, string Identity) BoundedTarget(string kind, string token, string identity, IlBodyLimits limits)
    {
        if (identity.Length > limits.MaxTextLength)
            throw new IlEvidenceException("IlTextLimitExceeded");
        return (kind, token, identity);
    }

    /// <summary>
    /// Canonical identity for a direct metadata-token type operand. Named
    /// rows always keep their assembly scope so the encoding matches the
    /// System.Reflection.Metadata row-based reader exactly; only constructed
    /// shapes (generic instances, arrays, by-ref, pointers) use the shared
    /// signature formatter, whose blob decoding both readers agree on.
    /// </summary>
    private static string CecilTypeOperandIdentity(Mono.Cecil.TypeReference type) => type switch
    {
        CecilGenericInstanceType or Mono.Cecil.ArrayType or Mono.Cecil.ByReferenceType
            or Mono.Cecil.PointerType or Mono.Cecil.FunctionPointerType or CecilGenericParameter
            or CecilCustomModifier or Mono.Cecil.SentinelType or PinnedType
            => ManagedMetadataExtractor.FormatType(type),
        Mono.Cecil.TypeSpecification specification
            => CecilTypeOperandIdentity(specification.ElementType),
        _ => ScopedNamedType(type)
    };

    private static string ScopedNamedType(Mono.Cecil.TypeReference type)
    {
        var (ns, names) = ManagedMetadataExtractor.CecilTypeName(type);
        return "scope(" + ManagedMetadataExtractor.CecilAssemblyScope(type) + ")type("
            + ManagedMetadataExtractor.MetadataTypePath(ns, names) + ")";
    }

    private static string CecilSelfAssemblyIdentity(CecilModuleDefinition module)
    {
        var assemblyReferenceIdentity = ManagedMetadataExtractor.AssemblyReferenceIdentity(
            module.Assembly!.Name?.Name ?? "<netmodule>",
            module.Assembly.Name?.Version?.ToString() ?? "0.0.0.0",
            module.Assembly.Name?.Culture,
            module.Assembly.Name?.PublicKeyToken);
        return ManagedMetadataExtractor.AssemblyArtifactIdentity(
            assemblyReferenceIdentity, module.Name, ManagedMetadataExtractor.TargetFramework(module.Assembly));
    }

    private static string RawCecilToken(object? operand) => operand switch
    {
        IMetadataTokenProvider provider => ManagedMetadataExtractor.Token(provider.MetadataToken.ToUInt32()),
        _ => throw new IlEvidenceException("IlOperandEncodingUnsupported")
    };

    // Cecil may project an InlineTok TypeSpec onto a decoded type whose
    // MetadataToken names another table. Keep the token encoded in the IL;
    // SRM independently validates that row and the two decoded identities
    // must still agree before evidence is admitted.
    internal static string CecilInlineTypeToken(Mono.Cecil.TypeReference type, ReadOnlySpan<byte> il, int operandOffset)
    {
        if (operandOffset < 0 || operandOffset > il.Length - sizeof(int))
            throw new IlEvidenceException("MalformedIlBody");
        var raw = BinaryPrimitives.ReadUInt32LittleEndian(il.Slice(operandOffset, sizeof(int)));
        return raw >> 24 == 0x1b ? ManagedMetadataExtractor.Token(raw) : RawCecilToken(type);
    }

    private static string CecilFieldIdentity(FieldReference field) =>
        $"{(field is Mono.Cecil.FieldDefinition ? "fielddef" : "memberref")}|type:{CecilTypeOperandIdentity(field.DeclaringType)}|member:{ManagedMetadataExtractor.EncodeIdentityComponent(field.Name)}|{ManagedMetadataExtractor.FormatType(field.FieldType)}";

    private static string BoundedOperand(string kind, string token, string identity, IlBodyLimits limits)
    {
        if (identity.Length > limits.MaxTextLength)
            throw new IlEvidenceException("IlTextLimitExceeded");
        return $"tok:{kind}:{token}:{identity}";
    }

    private static string CecilVariableIndex(object operand) => operand switch
    {
        VariableDefinition variable => variable.Index.ToString(CultureInfo.InvariantCulture),
        // Cecil resolves argument operands to ParameterDefinition, whose index
        // excludes the implicit this slot that the raw IL argument index keeps.
        Mono.Cecil.ParameterDefinition parameter => parameter.Method?.HasThis == true
            ? (parameter.Index + 1).ToString(CultureInfo.InvariantCulture)
            : parameter.Index.ToString(CultureInfo.InvariantCulture),
        int value => value.ToString(CultureInfo.InvariantCulture),
        _ => throw new IlEvidenceException("IlOperandEncodingUnsupported:RawCecilToken:" + operand?.GetType().Name)
    };

    private static string CecilLocalType(Mono.Cecil.TypeReference type) => type is PinnedType pinned
        ? ManagedMetadataExtractor.FormatType(pinned.ElementType) + " pinned"
        : ManagedMetadataExtractor.FormatType(type);

    private static string CanonicalHandlerKind(ExceptionHandlerType kind) => kind switch
    {
        ExceptionHandlerType.Catch => "catch",
        ExceptionHandlerType.Filter => "filter",
        ExceptionHandlerType.Finally => "finally",
        ExceptionHandlerType.Fault => "fault",
        _ => throw new IlEvidenceException("IlExceptionRegionKindUnsupported")
    };

    private static string CecilHandler(ExceptionHandler handler, int codeSize)
    {
        if (handler.TryStart is null || handler.HandlerStart is null)
            throw new IlEvidenceException("MalformedIlBody");
        var tryEnd = handler.TryEnd?.Offset ?? codeSize;
        var handlerEnd = handler.HandlerEnd?.Offset ?? codeSize;
        if (tryEnd < handler.TryStart.Offset || tryEnd > codeSize
            || handlerEnd < handler.HandlerStart.Offset || handlerEnd > codeSize)
            throw new IlEvidenceException("MalformedIlBody");
        return $"kind:{CanonicalHandlerKind(handler.HandlerType)}"
            + $":try:{handler.TryStart.Offset:x}+{tryEnd - handler.TryStart.Offset:x}"
            + $":handler:{handler.HandlerStart.Offset:x}+{handlerEnd - handler.HandlerStart.Offset:x}"
            + $":filter:{(handler.FilterStart is { } filterStart ? filterStart.Offset.ToString("x", CultureInfo.InvariantCulture) : "-1")}"
            + $":catch:{(handler.CatchType is null ? "-" : CecilTypeOperandIdentity(handler.CatchType))}";
    }

    // Internal so the rewrite lane can independently verify each paired side
    // before any join is attempted; see ReadCecilBodies.
    internal static IlReaderResult ReadSystemReflectionMetadataBodies(
        byte[] bytes,
        IlBodyLimits limits,
        IlWorkBudget budget,
        CancellationToken cancellationToken,
        bool retainDiagnosticInstructions = false,
        IlWorkBudget? valueBudget = null, IlWorkBudget? objectBudget = null, IReadOnlySet<string>? selectedMethods = null)
    {
        valueBudget ??= new IlWorkBudget(limits.MaxTotalWorkUnits);
        objectBudget ??= new IlWorkBudget(Math.Min(limits.MaxTotalWorkUnits, 1_048_576));
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
        var reader = pe.GetMetadataReader();
        var assembly = reader.GetAssemblyDefinition();
        var publicKey = assembly.PublicKey.IsNil ? [] : reader.GetBlobBytes(assembly.PublicKey);
        var selfReference = ManagedMetadataExtractor.AssemblyReferenceIdentity(
            reader.GetString(assembly.Name),
            assembly.Version.ToString(),
            assembly.Culture.IsNil ? null : reader.GetString(assembly.Culture),
            PublicKeyTokenFromPublicKey(publicKey));
        var moduleDefinition = reader.GetModuleDefinition();
        var moduleName = reader.GetString(moduleDefinition.Name);
        var assemblyIdentity = ManagedMetadataExtractor.AssemblyArtifactIdentity(
            selfReference,
            moduleName,
            ManagedMetadataExtractor.TargetFramework(reader));
        var provider = new ManagedMetadataExtractor.MetadataTypeProvider(reader);
        var bodies = new List<IlBodyObservation>();
        var bodyCount = 0;
        foreach (var handle in reader.MethodDefinitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var method = reader.GetMethodDefinition(handle);
            if (method.RelativeVirtualAddress == 0)
                continue;
            bodyCount++;
            if (bodyCount > limits.MaxBodyCount)
                throw new IlEvidenceException("IlBodyCountLimitExceeded");
            if (!budget.TryConsume(1))
                throw new IlEvidenceException("IlTotalWorkLimitExceeded");
            if (selectedMethods is not null && !selectedMethods.Contains(ManagedMetadataExtractor.Token(MetadataTokens.GetToken(handle)))) continue;
            bodies.Add(ReadSrmBody(pe, reader, provider, handle, method, assemblyIdentity, limits, budget,
                retainDiagnosticInstructions, valueBudget, objectBudget));
        }
        return new IlReaderResult(assemblyIdentity, moduleName, reader.GetGuid(moduleDefinition.Mvid).ToString("D", CultureInfo.InvariantCulture), bodies);
    }

    private static IlBodyObservation ReadSrmBody(
        PEReader pe,
        MetadataReader reader,
        ManagedMetadataExtractor.MetadataTypeProvider provider,
        MethodDefinitionHandle handle,
        SrmMethodDefinition method,
        string assemblyIdentity,
        IlBodyLimits limits,
        IlWorkBudget budget,
        bool retainDiagnosticInstructions,
        IlWorkBudget valueBudget, IlWorkBudget objectBudget)
    {
        var body = pe.GetMethodBody(method.RelativeVirtualAddress);
        var il = body.GetILBytes() ?? throw new IlEvidenceException("MalformedIlBody");
        var name = reader.GetString(method.Name);
        var declaringTypeHandle = method.GetDeclaringType();
        var declaringIdentity = SrmTypeIdentity(reader, declaringTypeHandle, assemblyIdentity);
        var decoded = method.DecodeSignature(provider, genericContext: null);
        var memberKind = name is ".ctor" or ".cctor" ? "constructor" : "method";
        var signature = ManagedMetadataExtractor.MethodSignature(
            decoded.ReturnType,
            decoded.ParameterTypes,
            method.GetGenericParameters().Count,
            decoded.Header.CallingConvention == SignatureCallingConvention.VarArgs ? "vararg" : "default",
            decoded.Header.IsInstance,
            (decoded.Header.RawValue & 0x40) != 0);
        var methodIdentity = $"{declaringIdentity}|{memberKind}:{ManagedMetadataExtractor.EncodeIdentityComponent(name)}|{signature}";
        if (methodIdentity.Length > limits.MaxTextLength)
            throw new IlEvidenceException("IlTextLimitExceeded");

        var instructions = new List<string>();
        var opcodes = new List<string>();
        var calls = new List<IlCallObservation>();
        var instructionOffsets = new HashSet<int>();
        var branchTargets = new List<int>();
        var single = SingleByteOpcodes();
        var multi = MultiByteOpcodes();
        var position = 0;
        while (position < il.Length)
        {
            if (instructions.Count >= limits.MaxInstructionsPerBody)
                throw new IlEvidenceException("IlInstructionLimitExceeded");
            if (!budget.TryConsume(1))
                throw new IlEvidenceException("IlTotalWorkLimitExceeded");
            var offset = position;
            instructionOffsets.Add(offset);
            var first = il[position++];
            if (first == 0xfe) EnsureAvailable(il, position, 1);
            if (first == 0xfe ? !multi.ContainsKey(il[position]) : !single.ContainsKey(first))
                throw new IlEvidenceException("MalformedIlBody");
            var opcode = first == 0xfe ? multi[il[position++]] : single[first];
            var operand = SrmOperand(reader, provider, il, ref position, offset, opcode, calls, branchTargets, budget, assemblyIdentity, limits);
            instructions.Add($"{instructions.Count.ToString(CultureInfo.InvariantCulture)}:{offset.ToString("x", CultureInfo.InvariantCulture)}:{opcode.Name!.ToString()}:{operand}");
            opcodes.Add(opcode.Name!.ToString());
        }
        if (branchTargets.Any(target => !instructionOffsets.Contains(target)))
            throw new IlEvidenceException("MalformedIlBody");

        var declaredLocalCount = 0;
        if (!body.LocalSignature.IsNil)
        {
            var localSignature = reader.GetStandaloneSignature(body.LocalSignature);
            var blob = reader.GetBlobReader(localSignature.Signature);
            if (blob.ReadSignatureHeader().Kind != SignatureKind.LocalVariables)
                throw new IlEvidenceException("MalformedIlBody");
            declaredLocalCount = blob.ReadCompressedInteger();
        }
        if (declaredLocalCount < 0)
            throw new IlEvidenceException("MalformedIlBody");
        if (declaredLocalCount > limits.MaxLocalsPerBody)
            throw new IlEvidenceException("IlLocalLimitExceeded");
        if (!budget.TryConsume(declaredLocalCount))
            throw new IlEvidenceException("IlTotalWorkLimitExceeded");
        var locals = body.LocalSignature.IsNil
            ? []
            : reader.GetStandaloneSignature(body.LocalSignature)
                .DecodeLocalSignature(provider, genericContext: null)
                .Select((type, index) => (Index: index, Type: type))
                .ToArray();
        if (locals.Length != declaredLocalCount)
            throw new IlEvidenceException("MalformedIlBody");
        var handlers = body.ExceptionRegions
            .Select(region => $"kind:{region.Kind.ToString().ToLowerInvariant()}"
                + $":try:{region.TryOffset:x}+{region.TryLength:x}"
                + $":handler:{region.HandlerOffset:x}+{region.HandlerLength:x}"
                + $":filter:{(region.FilterOffset == -1 ? "-1" : region.FilterOffset.ToString("x", CultureInfo.InvariantCulture))}"
                + $":catch:{(region.CatchType.IsNil ? "-" : provider.GetTypeFromEntityHandle(region.CatchType))}")
            .ToArray();
        if (handlers.Length > limits.MaxExceptionRegionsPerBody)
            throw new IlEvidenceException("IlExceptionRegionLimitExceeded");
        if (!budget.TryConsume(handlers.Length))
            throw new IlEvidenceException("IlTotalWorkLimitExceeded");
        var instructionsSha256 = DigestLines(instructions);
        var opcodesSha256 = DigestLines(opcodes);
        var localsSha256 = DigestLines(locals.Select(local => $"{local.Index.ToString(CultureInfo.InvariantCulture)}:{local.Type}"));
        var exceptionRegionsSha256 = DigestLines(handlers);
        var canonical = CanonicalBody(body.MaxStack, body.LocalVariablesInitialized, instructions.Count, instructionsSha256, locals.Length, localsSha256, handlers.Length, exceptionRegionsSha256);
        var bodySha256 = ManagedMetadataExtractor.Sha256(Encoding.UTF8.GetBytes(canonical));
        var bodyIdentity = $"{methodIdentity}|il-body:instructions:{instructions.Count.ToString(CultureInfo.InvariantCulture)}:sha256:{bodySha256}";
        if (bodyIdentity.Length > limits.MaxTextLength)
            throw new IlEvidenceException("IlTextLimitExceeded");
        var objectBody = RetainObjectBody(instructions.Count, reader.GetTypeDefinition(declaringTypeHandle).GetFields().Count,
            () => new IlObjectBody(instructions, $"memberref|type:{provider.GetTypeFromEntityHandle(declaringTypeHandle)}|member:{ManagedMetadataExtractor.EncodeIdentityComponent(name)}|{signature}",
                    provider.GetTypeFromEntityHandle(declaringTypeHandle), name, decoded.ParameterTypes.Length,
                    decoded.Header.IsInstance, decoded.ReturnType != "type(namespace:6:System|names:4:Void)",
                    (method.Attributes & System.Reflection.MethodAttributes.Virtual) != 0,
                    method.GetGenericParameters().Count != 0 || reader.GetTypeDefinition(declaringTypeHandle).GetGenericParameters().Count != 0,
                    reader.GetTypeDefinition(declaringTypeHandle).GetFields().Where(fieldHandle =>
                    {
                        var attributes = reader.GetFieldDefinition(fieldHandle).Attributes;
                        return (attributes & System.Reflection.FieldAttributes.FieldAccessMask) == System.Reflection.FieldAttributes.Private
                            && (attributes & System.Reflection.FieldAttributes.Static) == 0;
                    }).Select(fieldHandle => SrmFieldIdentity(reader, provider, fieldHandle)["fielddef|".Length..])
                        .Order(StringComparer.Ordinal).ToArray(), handlers,
                    reader.GetTypeDefinition(declaringTypeHandle).BaseType.IsNil ? null
                        : provider.GetTypeFromEntityHandle(reader.GetTypeDefinition(declaringTypeHandle).BaseType)), limits, objectBudget, out var objectRetention);
        return new IlBodyObservation(
            ManagedMetadataExtractor.Token(MetadataTokens.GetToken(handle)),
            methodIdentity,
            instructions.Count,
            instructionsSha256,
            opcodesSha256,
            locals.Length,
            localsSha256,
            handlers.Length,
            exceptionRegionsSha256,
            body.MaxStack.ToString(CultureInfo.InvariantCulture),
            body.LocalVariablesInitialized,
            bodyIdentity,
            bodySha256,
            calls,
            retainDiagnosticInstructions ? instructions : null,
            ExtractCallValues(instructions, calls, body.MaxStack, handlers, valueBudget),
            objectBody, objectRetention);
    }

    private static (string Kind, string Token, string Identity) SrmBoundedTarget(string kind, string token, string identity, IlBodyLimits limits)
    {
        if (identity.Length > limits.MaxTextLength)
            throw new IlEvidenceException("IlTextLimitExceeded");
        return (kind, token, identity);
    }

    private static string SrmTypeIdentity(MetadataReader reader, TypeDefinitionHandle handle, string assemblyIdentity)
    {
        var (ns, names, arity) = ManagedMetadataExtractor.MetadataTypeName(reader, handle);
        return $"{assemblyIdentity}|type:{ManagedMetadataExtractor.MetadataTypePath(ns, names)}|arity:{arity.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string SrmOperand(
        MetadataReader reader,
        ManagedMetadataExtractor.MetadataTypeProvider provider,
        byte[] il,
        ref int position,
        int offset,
        System.Reflection.Emit.OpCode opcode,
        List<IlCallObservation> calls,
        List<int> branchTargets,
        IlWorkBudget budget,
        string assemblyIdentity,
        IlBodyLimits limits)
    {
        switch (opcode.OperandType)
        {
            case System.Reflection.Emit.OperandType.InlineNone:
                return "-";
            case System.Reflection.Emit.OperandType.ShortInlineBrTarget:
                var shortDelta = ReadSByte(il, ref position);
                return $"br:0x{RecordBranchTarget((long)position + shortDelta, il.Length, branchTargets):x}";
            case System.Reflection.Emit.OperandType.InlineBrTarget:
                var longDelta = ReadInt32(il, ref position);
                return $"br:0x{RecordBranchTarget((long)position + longDelta, il.Length, branchTargets):x}";
            case System.Reflection.Emit.OperandType.InlineSwitch:
                var count = ReadInt32(il, ref position);
                if (count < 0 || (long)position + checked((long)count * sizeof(int)) > il.Length)
                    throw new IlEvidenceException("MalformedIlBody");
                if (!budget.TryConsume(count))
                    throw new IlEvidenceException("IlTotalWorkLimitExceeded");
                var deltas = new int[count];
                for (var index = 0; index < count; index++)
                    deltas[index] = ReadInt32(il, ref position);
                // Every switch delta is relative to the shared offset
                // immediately after the complete jump table.
                var targets = new string[count];
                for (var index = 0; index < count; index++)
                {
                    var target = (long)position + deltas[index];
                    targets[index] = $"0x{RecordBranchTarget(target, il.Length, branchTargets):x}";
                }
                return $"sw:{count.ToString(CultureInfo.InvariantCulture)}[{string.Join(",", targets)}]";
            case System.Reflection.Emit.OperandType.ShortInlineI:
                var shortInteger = ReadSByte(il, ref position);
                var integer = opcode == System.Reflection.Emit.OpCodes.Ldc_I4_S
                    ? (int)shortInteger
                    : unchecked((byte)shortInteger);
                return $"i:{integer.ToString(CultureInfo.InvariantCulture)}";
            case System.Reflection.Emit.OperandType.InlineI:
                return $"i:{ReadInt32(il, ref position).ToString(CultureInfo.InvariantCulture)}";
            case System.Reflection.Emit.OperandType.InlineI8:
                return $"i:{ReadInt64(il, ref position).ToString(CultureInfo.InvariantCulture)}";
            case System.Reflection.Emit.OperandType.ShortInlineR:
                return $"r:{ReadInt32(il, ref position).ToString("x8", CultureInfo.InvariantCulture)}";
            case System.Reflection.Emit.OperandType.InlineR:
                return $"r:{ReadInt64(il, ref position).ToString("x16", CultureInfo.InvariantCulture)}";
            case System.Reflection.Emit.OperandType.InlineString:
                var stringToken = ReadInt32(il, ref position);
                if ((stringToken & 0xff000000) != 0x70000000)
                    throw new IlEvidenceException("MalformedIlBody");
                var text = reader.GetUserString(MetadataTokens.UserStringHandle(stringToken));
                if (text.Length > limits.MaxTextLength)
                    throw new IlEvidenceException("IlTextLimitExceeded");
                return $"str:{text.Length.ToString(CultureInfo.InvariantCulture)}:{UserStringDigest(text)}";
            case System.Reflection.Emit.OperandType.ShortInlineVar or System.Reflection.Emit.OperandType.InlineVar:
                var variable = opcode.OperandType == System.Reflection.Emit.OperandType.ShortInlineVar
                    ? il[position++]
                    : ReadUInt16(il, ref position);
                return $"v:{variable.ToString(CultureInfo.InvariantCulture)}";
            case System.Reflection.Emit.OperandType.InlineMethod:
                if (!budget.TryConsume(1))
                    throw new IlEvidenceException("IlTotalWorkLimitExceeded");
                var methodToken = ReadInt32(il, ref position);
                var methodTarget = SrmMethodTarget(reader, provider, methodToken, assemblyIdentity, limits);
                if (IsCallObservationOpcode(opcode.Name!.ToString()))
                    calls.Add(new IlCallObservation(offset, opcode.Name.ToString(), methodTarget.Kind, methodTarget.Token, methodTarget.Identity,
                        SrmCallStackShape(reader, provider, methodToken)));
                return $"m:{methodTarget.Kind}:{methodTarget.Token}:{methodTarget.Identity}";
            case System.Reflection.Emit.OperandType.InlineType:
                var typeToken = ReadInt32(il, ref position);
                var typeHandle = MetadataTokens.EntityHandle(typeToken);
                // Reject wrong-table tokens here so Mono.Cecil never casts them later.
                if (typeHandle.Kind is not (HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification))
                    throw new IlEvidenceException("IlOperandEncodingUnsupported");
                var typeIdentity = provider.GetTypeFromEntityHandle(typeHandle);
                if (typeIdentity.Length > limits.MaxTextLength)
                    throw new IlEvidenceException("IlTextLimitExceeded");
                if (opcode.Name!.ToString() == "constrained.")
                {
                    if (!budget.TryConsume(1))
                        throw new IlEvidenceException("IlTotalWorkLimitExceeded");
                    calls.Add(new IlCallObservation(
                        offset,
                        opcode.Name.ToString(),
                        "constrainedtype",
                        "-",
                        typeIdentity));
                }
                return $"t:{typeIdentity}";
            case System.Reflection.Emit.OperandType.InlineField or System.Reflection.Emit.OperandType.InlineTok:
                var rawToken = ReadInt32(il, ref position);
                ValidateMetadataOperand(reader, provider, rawToken, opcode.OperandType);
                return SrmMetadataOperand(reader, provider, rawToken, opcode.OperandType, assemblyIdentity, limits);
            case System.Reflection.Emit.OperandType.InlineSig:
                var signatureToken = ReadInt32(il, ref position);
                ValidateMetadataOperand(reader, provider, signatureToken, opcode.OperandType);
                var standalone = reader.GetStandaloneSignature((StandaloneSignatureHandle)MetadataTokens.EntityHandle(signatureToken));
                var decodedSignature = standalone.DecodeMethodSignature(provider, null);
                if (decodedSignature.GenericParameterCount != 0)
                    throw new IlEvidenceException("IlOperandEncodingUnsupported");
                var srmSignature = CanonicalCallSite(
                    (int)decodedSignature.Header.CallingConvention,
                    decodedSignature.Header.IsInstance,
                    (decodedSignature.Header.RawValue & 0x40) != 0,
                    decodedSignature.RequiredParameterCount,
                    decodedSignature.ReturnType,
                    decodedSignature.ParameterTypes);
                if (srmSignature.Length > limits.MaxTextLength)
                    throw new IlEvidenceException("IlTextLimitExceeded");
                if (opcode.Name!.ToString() == "calli")
                    calls.Add(new IlCallObservation(offset, "calli", "standalonesig",
                        ManagedMetadataExtractor.Token(unchecked((uint)signatureToken)), srmSignature));
                return $"sig:0x{signatureToken:x8}:{srmSignature}";
            default:
                throw new IlEvidenceException("IlOperandEncodingUnsupported");
        }
    }

    private static string CanonicalCallSite(int convention, bool hasThis, bool explicitThis,
        int requiredParameterCount, string returnType, IEnumerable<string> parameters)
    {
        var types = parameters.ToArray();
        if (requiredParameterCount < 0 || requiredParameterCount > types.Length)
            throw new IlEvidenceException("MalformedIlBody");
        return $"callconv:{convention.ToString(CultureInfo.InvariantCulture)}|hasThis:{hasThis.ToString().ToLowerInvariant()}|explicitThis:{explicitThis.ToString().ToLowerInvariant()}|required:{requiredParameterCount.ToString(CultureInfo.InvariantCulture)}|({string.Join(",", types)})->{returnType}";
    }

    private static string SrmMetadataOperand(MetadataReader reader,
        ManagedMetadataExtractor.MetadataTypeProvider provider, int token,
        System.Reflection.Emit.OperandType operandType, string assemblyIdentity, IlBodyLimits limits)
    {
        var handle = MetadataTokens.EntityHandle(token);
        var raw = ManagedMetadataExtractor.Token(unchecked((uint)token));
        var isField = handle.Kind == HandleKind.FieldDefinition
            || handle.Kind == HandleKind.MemberReference
                && reader.GetMemberReference((MemberReferenceHandle)handle).GetKind() == MemberReferenceKind.Field;
        if (operandType == System.Reflection.Emit.OperandType.InlineField || isField)
            return BoundedOperand("field", raw, SrmFieldIdentity(reader, provider, handle), limits);
        if (handle.Kind is HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification)
            return BoundedOperand("type", raw, provider.GetTypeFromEntityHandle(handle), limits);
        if (handle.Kind is HandleKind.MethodDefinition or HandleKind.MethodSpecification or HandleKind.MemberReference)
            return BoundedOperand("method", raw, SrmMethodTarget(reader, provider, token, assemblyIdentity, limits).Identity, limits);
        throw new IlEvidenceException("IlOperandEncodingUnsupported");
    }

    private static string SrmFieldIdentity(MetadataReader reader,
        ManagedMetadataExtractor.MetadataTypeProvider provider, EntityHandle handle)
    {
        if (handle.Kind == HandleKind.MemberReference)
        {
            var member = reader.GetMemberReference((MemberReferenceHandle)handle);
            if (member.Parent.Kind is not (HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification))
                throw new IlEvidenceException("IlOperandEncodingUnsupported");
            return $"memberref|type:{provider.GetTypeFromEntityHandle(member.Parent)}|member:{ManagedMetadataExtractor.EncodeIdentityComponent(reader.GetString(member.Name))}|{member.DecodeFieldSignature(provider, null)}";
        }
        if (handle.Kind == HandleKind.FieldDefinition)
        {
            var definitionHandle = (FieldDefinitionHandle)handle;
            var field = reader.GetFieldDefinition(definitionHandle);
            var typeHandle = field.GetDeclaringType();
            return $"fielddef|type:{provider.GetTypeFromEntityHandle(typeHandle)}|member:{ManagedMetadataExtractor.EncodeIdentityComponent(reader.GetString(field.Name))}|{field.DecodeSignature(provider, null)}";
        }
        throw new IlEvidenceException("IlOperandEncodingUnsupported");
    }

    // Raw token values remain part of the body digest, but a valid row and a
    // decodable signature are required before that digest is admitted. This
    // check runs in the independent SRM pass, before Cecil sees the input.
    private static void ValidateMetadataOperand(
        MetadataReader reader,
        ManagedMetadataExtractor.MetadataTypeProvider provider,
        int token,
        System.Reflection.Emit.OperandType operandType)
    {
        EntityHandle handle;
        try
        {
            handle = MetadataTokens.EntityHandle(token);
            switch (handle.Kind)
            {
                case HandleKind.FieldDefinition when operandType != System.Reflection.Emit.OperandType.InlineSig:
                    _ = reader.GetFieldDefinition((FieldDefinitionHandle)handle).DecodeSignature(provider, null);
                    return;
                case HandleKind.MemberReference when operandType != System.Reflection.Emit.OperandType.InlineSig:
                    var member = reader.GetMemberReference((MemberReferenceHandle)handle);
                    if (member.GetKind() == MemberReferenceKind.Field)
                    {
                        _ = member.DecodeFieldSignature(provider, null);
                        return;
                    }
                    if (operandType == System.Reflection.Emit.OperandType.InlineField)
                        break;
                    _ = member.DecodeMethodSignature(provider, null);
                    return;
                case HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification
                    when operandType == System.Reflection.Emit.OperandType.InlineTok:
                    _ = provider.GetTypeFromEntityHandle(handle);
                    return;
                case HandleKind.MethodDefinition when operandType == System.Reflection.Emit.OperandType.InlineTok:
                    _ = reader.GetMethodDefinition((MethodDefinitionHandle)handle).DecodeSignature(provider, null);
                    return;
                case HandleKind.MethodSpecification when operandType == System.Reflection.Emit.OperandType.InlineTok:
                    var specification = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                    _ = specification.DecodeSignature(provider, null);
                    return;
                case HandleKind.StandaloneSignature when operandType == System.Reflection.Emit.OperandType.InlineSig:
                    _ = reader.GetStandaloneSignature((StandaloneSignatureHandle)handle).DecodeMethodSignature(provider, null);
                    return;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or BadImageFormatException or InvalidOperationException
            or IndexOutOfRangeException or OverflowException)
        {
            throw new IlEvidenceException("MalformedIlBody");
        }
        throw new IlEvidenceException("IlOperandEncodingUnsupported");
    }

    private static (string Kind, string Token, string Identity) SrmMethodTarget(
        MetadataReader reader,
        ManagedMetadataExtractor.MetadataTypeProvider provider,
        int token,
        string assemblyIdentity,
        IlBodyLimits limits)
    {
        var handle = MetadataTokens.EntityHandle(token);
        switch (handle.Kind)
        {
            case HandleKind.MethodSpecification:
                var specification = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                var element = SrmMethodTarget(reader, provider, MetadataTokens.GetToken(specification.Method), assemblyIdentity, limits);
                var arguments = string.Join(",", specification.DecodeSignature(provider, null));
                return SrmBoundedTarget(
                    "methodspec",
                    ManagedMetadataExtractor.Token(unchecked((uint)token)),
                    $"{element.Identity}|gargs:<{arguments}>",
                    limits);
            case HandleKind.MethodDefinition:
                var definition = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                var name = reader.GetString(definition.Name);
                var declaring = definition.GetDeclaringType();
                var declaringIdentity = SrmTypeIdentity(reader, declaring, assemblyIdentity);
                var memberKind = name is ".ctor" or ".cctor" ? "constructor" : "method";
                var decoded = definition.DecodeSignature(provider, genericContext: null);
                var signature = ManagedMetadataExtractor.MethodSignature(
                    decoded.ReturnType,
                    decoded.ParameterTypes,
                    definition.GetGenericParameters().Count,
                    decoded.Header.CallingConvention == SignatureCallingConvention.VarArgs ? "vararg" : "default",
                    decoded.Header.IsInstance,
                    (decoded.Header.RawValue & 0x40) != 0);
                return SrmBoundedTarget(
                    "methoddef",
                    ManagedMetadataExtractor.Token(unchecked((uint)token)),
                    $"{declaringIdentity}|{memberKind}:{ManagedMetadataExtractor.EncodeIdentityComponent(name)}|{signature}",
                    limits);
            case HandleKind.MemberReference:
                var reference = reader.GetMemberReference((MemberReferenceHandle)handle);
                // ECMA-335 also permits a MethodDef parent for a vararg
                // MemberRef carrying optional call-site arguments. Resolve
                // only the declaring type encoded by that local MethodDef;
                // never infer a target from the display name.
                if (reference.Parent.Kind is not (HandleKind.TypeReference or HandleKind.TypeDefinition
                    or HandleKind.TypeSpecification or HandleKind.MethodDefinition))
                    throw new IlEvidenceException("IlCallTargetIdentityUnavailable");
                var parent = reference.Parent.Kind == HandleKind.MethodDefinition
                    ? provider.GetTypeFromEntityHandle(reader.GetMethodDefinition((MethodDefinitionHandle)reference.Parent).GetDeclaringType())
                    : provider.GetTypeFromEntityHandle(reference.Parent);
                var referenceDecoded = reference.DecodeMethodSignature(provider, genericContext: null);
                var referenceSignature = ManagedMetadataExtractor.MethodSignature(
                    referenceDecoded.ReturnType,
                    referenceDecoded.ParameterTypes,
                    referenceDecoded.GenericParameterCount,
                    referenceDecoded.Header.CallingConvention == SignatureCallingConvention.VarArgs ? "vararg" : "default",
                    referenceDecoded.Header.IsInstance,
                    (referenceDecoded.Header.RawValue & 0x40) != 0)
                    + (referenceDecoded.Header.CallingConvention == SignatureCallingConvention.VarArgs
                        ? $"|required:{referenceDecoded.RequiredParameterCount.ToString(CultureInfo.InvariantCulture)}"
                        : "");
                return SrmBoundedTarget(
                    "memberref",
                    ManagedMetadataExtractor.Token(unchecked((uint)token)),
                    $"memberref|type:{parent}|member:{ManagedMetadataExtractor.EncodeIdentityComponent(reader.GetString(reference.Name))}|{referenceSignature}",
                    limits);
            default:
                throw new IlEvidenceException("IlCallTargetIdentityUnavailable");
        }
    }

    private static IlValueFlowObservation ExtractCallValues(IReadOnlyList<string> instructions,
        IReadOnlyList<IlCallObservation> calls, int maxStack, IReadOnlyList<string> handlers, IlWorkBudget budget)
    {
        if (!budget.TryConsume(instructions.Count))
            return new([], ["IlValueWorkLimitExceeded"]);
        var boundaries = new HashSet<long>();
        var entries = new List<IlValueExceptionEntry>();
        var regions = new List<IlValueExceptionRegion>();
        foreach (var handler in handlers)
        {
            var parts = handler.Split(':');
            var handlerStart = long.Parse(parts[5].Split('+')[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            entries.Add(new(handlerStart, parts[1] is "catch" or "filter" ? 1 : 0));
            regions.Add(new(handlerStart, handlerStart + long.Parse(parts[5].Split('+')[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                parts[7] == "-1" ? null : long.Parse(parts[7], NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
            foreach (var index in new[] { 3, 5 })
            {
                var range = parts[index].Split('+');
                var start = long.Parse(range[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                boundaries.Add(start);
                boundaries.Add(start + long.Parse(range[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }
            if (parts[7] != "-1")
            {
                var filterStart = long.Parse(parts[7], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                boundaries.Add(filterStart); entries.Add(new(filterStart, 1));
            }
        }
        var flow = IlCallValueExtractor.Extract(instructions, calls, maxStack, handlers.Count != 0, boundaries, entries, regions);
        return budget.TryConsume(flow.WorkUnits) ? flow : new([], ["IlValueControlFlowAggregateWorkLimit"]);
    }

    private static IlCallStackShape SrmCallStackShape(MetadataReader reader,
        ManagedMetadataExtractor.MetadataTypeProvider provider, int token)
    {
        var handle = MetadataTokens.EntityHandle(token);
        if (handle.Kind == HandleKind.MethodSpecification)
            handle = reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method;
        var signature = handle.Kind switch
        {
            HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle)handle).DecodeSignature(provider, null),
            HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)handle).DecodeMethodSignature(provider, null),
            _ => throw new IlEvidenceException("IlCallTargetIdentityUnavailable")
        };
        return new(signature.ParameterTypes.Length, signature.Header.IsInstance,
            signature.ReturnType != "type(namespace:6:System|names:4:Void)",
            signature.Header.CallingConvention != SignatureCallingConvention.VarArgs
            && (signature.Header.RawValue & 0x40) == 0
            && !signature.ReturnType.Contains('&'),
            string.Concat(signature.ParameterTypes.Select(type => type.EndsWith('&') ? '1' : '0')));
    }

    private static Dictionary<int, System.Reflection.Emit.OpCode> SingleByteOpcodes()
    {
        lock (OpcodeTableGate)
        {
            singleByteOpcodes ??= BuildOpcodes(single: true);
            return singleByteOpcodes;
        }
    }

    private static Dictionary<int, System.Reflection.Emit.OpCode> MultiByteOpcodes()
    {
        lock (OpcodeTableGate)
        {
            multiByteOpcodes ??= BuildOpcodes(single: false);
            return multiByteOpcodes;
        }
    }

    private static Dictionary<int, System.Reflection.Emit.OpCode> BuildOpcodes(bool single)
    {
        var result = new Dictionary<int, System.Reflection.Emit.OpCode>();
        foreach (var field in typeof(System.Reflection.Emit.OpCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (field.FieldType != typeof(System.Reflection.Emit.OpCode))
                continue;
            var opcode = (System.Reflection.Emit.OpCode)field.GetValue(null)!;
            var value = opcode.Value & 0xffff;
            if (single == ((value & 0xff00) != 0xfe00))
                result[single ? value : value & 0xff] = opcode;
        }
        return result;
    }

    private static bool IsCallObservationOpcode(string opcode) => opcode is
        "call" or "callvirt" or "newobj" or "ldftn" or "ldvirtftn";

    private static int RecordBranchTarget(long target, int bodyLength, List<int> branchTargets)
    {
        if (target < 0 || target >= bodyLength)
            throw new IlEvidenceException("MalformedIlBody");
        var offset = (int)target;
        branchTargets.Add(offset);
        return offset;
    }

    private static sbyte ReadSByte(byte[] il, ref int position)
    {
        EnsureAvailable(il, position, 1);
        return unchecked((sbyte)il[position++]);
    }

    private static ushort ReadUInt16(byte[] il, ref int position)
    {
        EnsureAvailable(il, position, 2);
        var value = BinaryPrimitives.ReadUInt16LittleEndian(il.AsSpan(position, 2));
        position += 2;
        return value;
    }

    private static int ReadInt32(byte[] il, ref int position)
    {
        EnsureAvailable(il, position, 4);
        var value = BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(position, 4));
        position += 4;
        return value;
    }

    private static long ReadInt64(byte[] il, ref int position)
    {
        EnsureAvailable(il, position, 8);
        var value = BinaryPrimitives.ReadInt64LittleEndian(il.AsSpan(position, 8));
        position += 8;
        return value;
    }

    private static void EnsureAvailable(byte[] il, int position, int size)
    {
        if (position < 0 || position + size > il.Length)
            throw new IlEvidenceException("MalformedIlBody");
    }

    private static string UserStringDigest(string value)
    {
        // User strings contain UTF-16 code units, including unpaired
        // surrogates. Encoding.Unicode replaces those with U+FFFD and would
        // collapse distinct literal operands into the same body identity.
        var bytes = new byte[checked(value.Length * sizeof(char))];
        for (var index = 0; index < value.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * sizeof(char), sizeof(char)), value[index]);
        return ManagedMetadataExtractor.Sha256(bytes);
    }

    private static string DigestLines(IEnumerable<string> lines) =>
        ManagedMetadataExtractor.Sha256(Encoding.UTF8.GetBytes(string.Join(";\n", lines)));

    private static string CanonicalBody(
        int maxStack,
        bool initLocals,
        int instructionCount,
        string instructionsSha256,
        int localCount,
        string localsSha256,
        int handlerCount,
        string handlersSha256) =>
        "il-body-v1"
        + $"|maxstack:{maxStack.ToString(CultureInfo.InvariantCulture)}"
        + $"|initlocals:{(initLocals ? "true" : "false")}"
        + $"|instructions:{instructionCount.ToString(CultureInfo.InvariantCulture)}:{instructionsSha256}"
        + $"|locals:{localCount.ToString(CultureInfo.InvariantCulture)}:{localsSha256}"
        + $"|handlers:{handlerCount.ToString(CultureInfo.InvariantCulture)}:{handlersSha256}";

    private static byte[] PublicKeyTokenFromPublicKey(byte[] key)
    {
        if (key.Length == 0)
            return [];
        var hash = SHA1.HashData(key);
        return hash[^8..].Reverse().ToArray();
    }

    private static EvaluatedIlInput InputGap(CompiledInputBindingArtifact artifact, string gapKind) => new(
        new IlInputOutcome(
            artifact.SafeLocator,
            artifact.Role,
            GapOutcome(gapKind),
            artifact.ProvenanceState,
            artifact.RawFileSha256,
            PrivacyProjectedDigest(artifact, gapKind),
            artifact.AssemblyIdentity,
            null,
            null,
            artifact.ProvenanceBindingInputSha256,
            [gapKind]),
        []);

    private static string GapOutcome(string gapKind) => gapKind switch
    {
        "IlCompiledArtifactChangedOrUnreadable" => "unreadable",
        "MalformedIlBody" => "malformed",
        "IlReaderDisagreement" => "disputed",
        "IlCompiledEvidenceUnavailable" => "missing",
        "ManagedNetmoduleInputUnsupported" => "unsupported",
        "IlCallTargetIdentityUnavailable" or "IlOperandEncodingUnsupported" or "IlExceptionRegionKindUnsupported" => "unsupported",
        _ => "limit-exhausted"
    };

    private static string PrivacyProjectedDigest(CompiledInputBindingArtifact artifact, string? gapKind = null) =>
        ManagedMetadataExtractor.CanonicalDigest(new
        {
            artifact.SafeLocator,
            artifact.Role,
            Outcome = gapKind is null ? "admitted" : "gap",
            artifact.ProvenanceState,
            artifact.AssemblyIdentity,
            gapKinds = gapKind is null ? Array.Empty<string>() : new[] { gapKind }
        });

    private static SortedDictionary<string, string> CopyToSorted(IReadOnlyDictionary<string, string> source, params (string Key, string Value)[] extra)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in source)
            result[pair.Key] = pair.Value;
        foreach (var (key, value) in extra)
            result[key] = value;
        return result;
    }

    private static IReadOnlyDictionary<string, string> CommonProperties(IlBodyProvenance provenance, IlInputOutcome outcome)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["evidenceLocationKind"] = IlLocationKind,
            ["inputRole"] = outcome.Role,
            ["provenanceState"] = outcome.ProvenanceState,
            ["ilBoundedInputSha256"] = provenance.BoundedInputSha256,
            ["ilGeneratorSha256"] = provenance.GeneratorSha256,
            ["ilCoverage"] = provenance.CoverageState,
            ["ilInputOutcome"] = outcome.Outcome,
            ["artifactVisibility"] = provenance.ArtifactVisibility
        };
        if (outcome.AssemblyIdentity is not null)
            properties["assemblyIdentity"] = outcome.AssemblyIdentity;
        if (outcome.ModuleName is not null)
            properties["moduleName"] = outcome.ModuleName;
        if (outcome.ModuleMvid is not null)
            properties["moduleMvid"] = outcome.ModuleMvid;
        if (outcome.RawFileSha256 is not null)
            properties["rawFileSha256"] = outcome.RawFileSha256;
        if (outcome.ProvenanceBindingInputSha256 is not null)
            properties["provenanceBindingInputSha256"] = outcome.ProvenanceBindingInputSha256;
        return properties;
    }

    private static EvidenceSpan IlEvidence(string safeLocator) => new(
        safeLocator,
        1,
        1,
        null,
        nameof(IlBodyEvidenceExtractor),
        ScannerVersions.IlBodyEvidenceExtractor);

    private static CodeFact GapFact(ScanManifest manifest, string safeLocator, string gapKind, IReadOnlyDictionary<string, string> common)
    {
        var properties = new Dictionary<string, string>(common)
        {
            ["evidenceLocationKind"] = "managed-input-v1",
            ["gapKind"] = gapKind,
            ["limitation"] = GapLimitation
        };
        properties.Remove("assemblyIdentity");
        properties.Remove("moduleName");
        properties.Remove("moduleMvid");
        return FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.DotNetIlGap,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(safeLocator, 1, 1, null, nameof(IlBodyEvidenceExtractor), ScannerVersions.IlBodyEvidenceExtractor),
            contractElement: gapKind,
            properties: properties);
    }

    private static string GeneratorSha256()
    {
        var path = typeof(IlBodyEvidenceExtractor).Assembly.Location;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException("The exact IL body evidence generator bytes are unavailable.");
        return ManagedMetadataExtractor.Sha256(File.ReadAllBytes(path));
    }

    // Internal so the rewrite lane applies the identical validation to the
    // body limits it reuses before generating any provenance.
    internal static void ValidateLimits(IlBodyLimits limits)
    {
        if (limits.MaxBodyCount <= 0
            || limits.MaxInstructionsPerBody <= 0
            || limits.MaxLocalsPerBody <= 0
            || limits.MaxExceptionRegionsPerBody <= 0
            || limits.MaxTextLength <= 0
            || limits.MaxTotalWorkUnits <= 0)
            throw new ArgumentException("IL body evidence limits must all be positive.");
    }

    internal sealed record IlReaderResult(
        string AssemblyIdentity,
        string ModuleName,
        string ModuleMvid,
        IReadOnlyList<IlBodyObservation> Bodies);

    internal sealed class IlEvidenceException(string gapKind) : Exception(gapKind)
    {
        public string GapKind { get; } = gapKind;
    }

    internal sealed class IlWorkBudget(long maximum)
    {
        private long remaining = maximum;
        private readonly long initialMaximum = maximum;
        internal long Consumed => initialMaximum - remaining;
        internal long Maximum => initialMaximum;
        internal long RefusedRequests { get; private set; }
        public bool TryConsume(long units)
        {
            if (units < 0 || units > remaining)
            {
                RefusedRequests++;
                return false;
            }
            remaining -= units;
            return true;
        }
    }
}
