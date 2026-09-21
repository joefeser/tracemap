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
    internal const string PolicyVersion = "explicit-il-body-evidence.v1";
    internal const string IlLocationKind = "managed-il-v1";
    internal const string BodyLimitation = "IL body evidence proves only that the admitted assembly contains this exact bounded operand-aware instruction stream at this module-local method row; it does not prove execution, dispatch, reachability, behavior, source ownership, semantic equivalence, or rewrite preservation.";
    internal const string CallLimitation = "A call site records the static member reference encoded in this module's IL; it does not prove execution, virtual dispatch resolution, target presence, cross-assembly resolution, call-graph reachability, or rewrite equivalence.";
    internal const string GapLimitation = "This categorical gap reduces only the explicitly requested IL body/call lane; it does not prove absence and never alters source, compiled-metadata, or PDB evidence.";

    private static readonly object OpcodeTableGate = new();
    private static Dictionary<int, System.Reflection.Emit.OpCode>? singleByteOpcodes;
    private static Dictionary<int, System.Reflection.Emit.OpCode>? multiByteOpcodes;

    internal static IlBodyEvaluation Evaluate(
        ScanOptions options,
        CompiledInputEvaluation compiledEvaluation,
        CancellationToken cancellationToken = default)
    {
        if (!options.IlBodyEvidence)
            return IlBodyEvaluation.Disabled;

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
                var srm = ReadSystemReflectionMetadataBodies(bytes, limits, workBudget, cancellationToken);
                var cecil = ReadCecilBodies(bytes, limits, workBudget, cancellationToken);
                var disagreements = CompareBodies(cecil, srm);
                if (disagreements.Count > 0)
                {
                    evaluated.Add(InputGap(artifact, "IlReaderDisagreement"));
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
        var coverage = globalGapKinds.Count == 0
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
            coverage);
        var knownGaps = DistinctGapKinds(outcomes, globalGapKinds)
            .Select(value => $"IL body evidence coverage reduced: {value}.")
            .ToArray();
        return new IlBodyEvaluation(provenance, evaluated, knownGaps, admitted);
    }

    private static IEnumerable<string> DistinctGapKinds(IReadOnlyList<IlInputOutcome> outcomes, List<string> globalGapKinds) =>
        globalGapKinds.Concat(outcomes.SelectMany(item => item.GapKinds))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal);

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
        foreach (var input in evaluation.Inputs.OrderBy(item => item.Outcome.SafeLocator, StringComparer.Ordinal))
        {
            var outcome = input.Outcome;
            var common = CommonProperties(provenance, outcome);
            foreach (var gapKind in outcome.GapKinds.OrderBy(value => value, StringComparer.Ordinal))
                facts.Add(GapFact(manifest, outcome.SafeLocator, gapKind, common));
            if (outcome.Outcome != "admitted")
                continue;

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

                foreach (var call in body.Calls.OrderBy(item => item.Offset, Comparer<long>.Default))
                {
                    var callIdentity = $"{body.BodyIdentity}|call:{call.Opcode}:{call.Offset.ToString(CultureInfo.InvariantCulture)}:{call.TargetIdentity}";
                    facts.Add(FactFactory.Create(
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
                        })));
                }
            }
        }

        return facts;
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

    private static IlReaderResult ReadCecilBodies(
        byte[] bytes,
        IlBodyLimits limits,
        IlWorkBudget budget,
        CancellationToken cancellationToken)
    {
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
                bodies.Add(ReadCecilBody(method, assemblyIdentity, limits, budget));
            }
        }
        return new IlReaderResult(assemblyIdentity, module.Name, module.Mvid.ToString("D", CultureInfo.InvariantCulture), bodies);
    }

    private static IlBodyObservation ReadCecilBody(
        CecilMethodDefinition method,
        string assemblyIdentity,
        IlBodyLimits limits,
        IlWorkBudget budget)
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
        var calls = new List<IlCallObservation>();
        foreach (var instruction in body.Instructions)
        {
            if (instructions.Count >= limits.MaxInstructionsPerBody)
                throw new IlEvidenceException("IlInstructionLimitExceeded");
            if (!budget.TryConsume(1))
                throw new IlEvidenceException("IlTotalWorkLimitExceeded");
            var operand = CecilOperand(instruction, calls, budget, assemblyIdentity, limits);
            instructions.Add($"{instructions.Count.ToString(CultureInfo.InvariantCulture)}:{instruction.Offset.ToString("x", CultureInfo.InvariantCulture)}:{instruction.OpCode.Name}:{operand}");
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
        var localsSha256 = DigestLines(locals.Select(local => $"{local.Index.ToString(CultureInfo.InvariantCulture)}:{local.Type}"));
        var exceptionRegionsSha256 = DigestLines(handlers);
        var canonical = CanonicalBody(body.MaxStackSize, body.InitLocals, instructions.Count, instructionsSha256, locals.Length, localsSha256, handlers.Length, exceptionRegionsSha256);
        var bodySha256 = ManagedMetadataExtractor.Sha256(Encoding.UTF8.GetBytes(canonical));
        var bodyIdentity = $"{methodIdentity}|il-body:instructions:{instructions.Count.ToString(CultureInfo.InvariantCulture)}:sha256:{bodySha256}";
        if (bodyIdentity.Length > limits.MaxTextLength)
            throw new IlEvidenceException("IlTextLimitExceeded");
        return new IlBodyObservation(
            ManagedMetadataExtractor.Token(unchecked((int)method.MetadataToken.ToUInt32())),
            methodIdentity,
            instructions.Count,
            instructionsSha256,
            locals.Length,
            localsSha256,
            handlers.Length,
            exceptionRegionsSha256,
            body.MaxStackSize.ToString(CultureInfo.InvariantCulture),
            body.InitLocals,
            bodyIdentity,
            bodySha256,
            calls);
    }

    private static string CecilOperand(Instruction instruction, List<IlCallObservation> calls, IlWorkBudget budget, string selfAssemblyIdentity, IlBodyLimits limits)
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
                return $"i:{((sbyte)instruction.Operand!).ToString(CultureInfo.InvariantCulture)}";
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
                if (IsCallObservationOpcode(instruction.OpCode.Name))
                    calls.Add(new IlCallObservation(instruction.Offset, instruction.OpCode.Name, target.Kind, target.Token, target.Identity));
                return $"m:{target.Kind}:{target.Token}:{target.Identity}";
            case OperandType.InlineType:
                var typeIdentity = CecilTypeOperandIdentity((Mono.Cecil.TypeReference)instruction.Operand!);
                if (typeIdentity.Length > limits.MaxTextLength)
                    throw new IlEvidenceException("IlTextLimitExceeded");
                if (instruction.OpCode.Name == "constrained.")
                {
                    if (!budget.TryConsume(1))
                        throw new IlEvidenceException("IlTotalWorkLimitExceeded");
                    calls.Add(new IlCallObservation(
                        instruction.Offset,
                        instruction.OpCode.Name,
                        "constrainedtype",
                        "-",
                        typeIdentity));
                }
                return $"t:{typeIdentity}";
            case OperandType.InlineField or OperandType.InlineTok or OperandType.InlineSig:
                return $"tok:{RawCecilToken(instruction.Operand)}";
            default:
                throw new IlEvidenceException("IlOperandEncodingUnsupported");
        }
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
            reference.ExplicitThis);
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

    private static IlReaderResult ReadSystemReflectionMetadataBodies(
        byte[] bytes,
        IlBodyLimits limits,
        IlWorkBudget budget,
        CancellationToken cancellationToken)
    {
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
            bodies.Add(ReadSrmBody(pe, reader, provider, handle, method, assemblyIdentity, limits, budget));
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
        IlWorkBudget budget)
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
            if (first == 0xfe ? !multi.ContainsKey(il[position]) : !single.ContainsKey(first))
                throw new IlEvidenceException("MalformedIlBody");
            var opcode = first == 0xfe ? multi[il[position++]] : single[first];
            var operand = SrmOperand(reader, provider, il, ref position, offset, opcode, calls, branchTargets, budget, assemblyIdentity, limits);
            instructions.Add($"{instructions.Count.ToString(CultureInfo.InvariantCulture)}:{offset.ToString("x", CultureInfo.InvariantCulture)}:{opcode.Name!.ToString()}:{operand}");
        }
        if (branchTargets.Any(target => !instructionOffsets.Contains(target)))
            throw new IlEvidenceException("MalformedIlBody");

        var locals = body.LocalSignature.IsNil
            ? []
            : reader.GetStandaloneSignature(body.LocalSignature)
                .DecodeLocalSignature(provider, genericContext: null)
                .Select((type, index) => (Index: index, Type: type))
                .ToArray();
        if (locals.Length > limits.MaxLocalsPerBody)
            throw new IlEvidenceException("IlLocalLimitExceeded");
        if (!budget.TryConsume(locals.Length))
            throw new IlEvidenceException("IlTotalWorkLimitExceeded");
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
        var localsSha256 = DigestLines(locals.Select(local => $"{local.Index.ToString(CultureInfo.InvariantCulture)}:{local.Type}"));
        var exceptionRegionsSha256 = DigestLines(handlers);
        var canonical = CanonicalBody(body.MaxStack, body.LocalVariablesInitialized, instructions.Count, instructionsSha256, locals.Length, localsSha256, handlers.Length, exceptionRegionsSha256);
        var bodySha256 = ManagedMetadataExtractor.Sha256(Encoding.UTF8.GetBytes(canonical));
        var bodyIdentity = $"{methodIdentity}|il-body:instructions:{instructions.Count.ToString(CultureInfo.InvariantCulture)}:sha256:{bodySha256}";
        if (bodyIdentity.Length > limits.MaxTextLength)
            throw new IlEvidenceException("IlTextLimitExceeded");
        return new IlBodyObservation(
            ManagedMetadataExtractor.Token(MetadataTokens.GetToken(handle)),
            methodIdentity,
            instructions.Count,
            instructionsSha256,
            locals.Length,
            localsSha256,
            handlers.Length,
            exceptionRegionsSha256,
            body.MaxStack.ToString(CultureInfo.InvariantCulture),
            body.LocalVariablesInitialized,
            bodyIdentity,
            bodySha256,
            calls);
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
                return $"i:{ReadSByte(il, ref position).ToString(CultureInfo.InvariantCulture)}";
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
                    calls.Add(new IlCallObservation(offset, opcode.Name.ToString(), methodTarget.Kind, methodTarget.Token, methodTarget.Identity));
                return $"m:{methodTarget.Kind}:{methodTarget.Token}:{methodTarget.Identity}";
            case System.Reflection.Emit.OperandType.InlineType:
                var typeToken = ReadInt32(il, ref position);
                var typeIdentity = provider.GetTypeFromEntityHandle(MetadataTokens.EntityHandle(typeToken));
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
            case System.Reflection.Emit.OperandType.InlineField or System.Reflection.Emit.OperandType.InlineTok or System.Reflection.Emit.OperandType.InlineSig:
                var rawToken = ReadInt32(il, ref position);
                return $"tok:0x{rawToken:x8}";
            default:
                throw new IlEvidenceException("IlOperandEncodingUnsupported");
        }
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
                if (reference.Parent.Kind is not (HandleKind.TypeReference or HandleKind.TypeDefinition or HandleKind.TypeSpecification))
                    throw new IlEvidenceException("IlCallTargetIdentityUnavailable");
                var parent = provider.GetTypeFromEntityHandle(reference.Parent);
                var referenceDecoded = reference.DecodeMethodSignature(provider, genericContext: null);
                var referenceSignature = ManagedMetadataExtractor.MethodSignature(
                    referenceDecoded.ReturnType,
                    referenceDecoded.ParameterTypes,
                    referenceDecoded.GenericParameterCount,
                    referenceDecoded.Header.CallingConvention == SignatureCallingConvention.VarArgs ? "vararg" : "default",
                    referenceDecoded.Header.IsInstance,
                    (referenceDecoded.Header.RawValue & 0x40) != 0);
                return SrmBoundedTarget(
                    "memberref",
                    ManagedMetadataExtractor.Token(unchecked((uint)token)),
                    $"memberref|type:{parent}|member:{ManagedMetadataExtractor.EncodeIdentityComponent(reader.GetString(reference.Name))}|{referenceSignature}",
                    limits);
            default:
                throw new IlEvidenceException("IlCallTargetIdentityUnavailable");
        }
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

    private static string UserStringDigest(string value) =>
        ManagedMetadataExtractor.Sha256(Encoding.Unicode.GetBytes(value));

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

    private static void ValidateLimits(IlBodyLimits limits)
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
        public bool TryConsume(long units)
        {
            if (units > remaining)
                return false;
            remaining -= units;
            return true;
        }
    }
}
