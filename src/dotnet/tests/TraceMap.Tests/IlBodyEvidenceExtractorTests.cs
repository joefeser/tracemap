using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mono.Cecil;
using CecilMethodDefinition = Mono.Cecil.MethodDefinition;
using CecilModuleDefinition = Mono.Cecil.ModuleDefinition;
using CecilTypeDefinition = Mono.Cecil.TypeDefinition;
using CecilAssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlBodyEvidenceExtractorTests
{
    [Fact]
    public void Admitted_fixture_emits_operand_aware_bodies_calls_and_provenance()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            IlBodyEvidence: true));

        var provenance = result.Manifest.IlBodyProvenance;
        Assert.NotNull(provenance);
        Assert.Equal(IlBodyEvidenceExtractor.SchemaVersion, provenance.SchemaVersion);
        Assert.Equal("il-complete", provenance.CoverageState);
        Assert.Equal("local-only", provenance.ArtifactVisibility);
        Assert.All(provenance.Outcomes, outcome =>
        {
            Assert.Equal("admitted", outcome.Outcome);
            Assert.Empty(outcome.GapKinds);
        });
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlGap);
        var body = result.Facts.First(fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
            && fact.Properties.GetValueOrDefault("metadataToken") == "0x06000005");
        Assert.Equal(EvidenceTiers.Tier2Structural, body.EvidenceTier);
        Assert.All(result.Facts.Where(fact => fact.RuleId is RuleIds.DotNetIlBody or RuleIds.DotNetIlCall), fact =>
        {
            Assert.Equal("managed-il-v1", fact.Properties.GetValueOrDefault("evidenceLocationKind"));
            Assert.Equal("1", fact.Evidence.StartLine.ToString());
            Assert.Null(fact.Evidence.SnippetHash);
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("ilBoundedInputSha256")));
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("ilGeneratorSha256")));
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("rawFileSha256")));
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("limitation")));
            Assert.Equal(ScannerVersions.IlBodyEvidenceExtractor, fact.Evidence.ExtractorVersion);
        });
        var byId = result.Facts.ToDictionary(fact => fact.FactId, StringComparer.Ordinal);
        Assert.All(result.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlBodyDeclared), fact =>
        {
            if (fact.Properties.TryGetValue("compiledFactId", out var compiledFactId))
                Assert.Equal(FactTypes.ManagedMethodDeclared, byId[compiledFactId].FactType);
        });
        Assert.All(result.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlCallObserved), fact =>
            Assert.Equal(FactTypes.ManagedIlBodyDeclared, byId[fact.Properties["ilBodyFactId"]].FactType));
        var serialized = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(Path.GetFullPath(fixture.Source), serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("il-alpha", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("il-beta", serialized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CallMemberAlpha", "CallMemberBeta", false)]
    [InlineData("StringAlpha", "StringBeta", false)]
    [InlineData("ConstAlpha", "ConstBeta", false)]
    [InlineData("SwitchOrderAlpha", "SwitchOrderBeta", false)]
    [InlineData("Twice", "Twice", true)]
    public void Same_opcode_sequences_keep_distinct_identities_unless_only_the_signature_differs(
        string first,
        string second,
        bool expectEqualBodyDigest)
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            IlBodyEvidence: true));

        var shapes = OpcodeSequences(fixture.Assembly, first, second);
        if (first != second)
        {
            Assert.Equal(shapes.FirstOpcodes, shapes.SecondOpcodes);
            if (first is "SwitchOrderAlpha" or "LoopTripAlpha")
            {
                // The fixture proves operand-only differences: identical opcode
                // streams whose branch targets, member operands, or constants
                // differ. Assert the streams stay instruction-count identical.
                Assert.Equal(shapes.FirstOpcodes.Count, shapes.SecondOpcodes.Count);
            }
        }
        else
        {
            // Overloads with identical bodies: same digest, different identity.
            Assert.Equal(shapes.FirstOpcodes, shapes.SecondOpcodes);
        }

        var firstBody = first == "Twice"
            ? BodyFact(result, first, "(type(namespace:6:System|names:5:Int32))")
            : BodyFact(result, first);
        var secondBody = second == first
            ? BodyFact(result, second, "(type(namespace:6:System|names:5:Int64))")
            : BodyFact(result, second);
        Assert.NotEqual(firstBody.TargetSymbol, secondBody.TargetSymbol);
        if (expectEqualBodyDigest)
        {
            Assert.Equal(firstBody.Properties["ilBodySha256"], secondBody.Properties["ilBodySha256"]);
            Assert.Equal(firstBody.Properties["instructionsSha256"], secondBody.Properties["instructionsSha256"]);
        }
        else
        {
            Assert.NotEqual(firstBody.Properties["ilBodySha256"], secondBody.Properties["ilBodySha256"]);
            Assert.Equal(
                firstBody.Properties["instructionsSha256"] == secondBody.Properties["instructionsSha256"],
                first == second);
        }
    }

    [Fact]
    public void Direct_call_kinds_carry_exact_target_identities_tokens_and_offsets()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            IlBodyEvidence: true));

        var calls = result.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlCallObserved).ToArray();
        Assert.Contains(calls, call => call.Properties.GetValueOrDefault("opcode") == "call"
            && call.Properties.GetValueOrDefault("referenceKind") == "methoddef"
            && call.Properties["referenceToken"].StartsWith("0x06", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Properties.GetValueOrDefault("opcode") == "callvirt"
            && call.Properties.GetValueOrDefault("referenceKind") == "memberref"
            && call.Properties["targetIdentity"].Contains("scope(assembly:", StringComparison.Ordinal)
            && call.Properties["targetIdentity"].Contains("member:3:Add|", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Properties.GetValueOrDefault("opcode") == "newobj"
            && call.Properties["targetIdentity"].Contains("member:5:.ctor|", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Properties.GetValueOrDefault("opcode") == "ldftn"
            && call.Properties.GetValueOrDefault("referenceKind") == "methodspec"
            && call.Properties["referenceToken"].StartsWith("0x2b", StringComparison.Ordinal)
            && call.Properties["targetIdentity"].Contains("|gargs:<", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Properties.GetValueOrDefault("opcode") == "call"
            && call.Properties["targetIdentity"].Contains("method:4:Echo|", StringComparison.Ordinal));
        Assert.All(calls, call =>
        {
            Assert.InRange(int.Parse(call.Properties["ilOffset"], CultureInfo.InvariantCulture), 0, int.MaxValue);
            Assert.False(string.IsNullOrWhiteSpace(call.Properties.GetValueOrDefault("targetIdentity")));
            Assert.False(string.IsNullOrWhiteSpace(call.Properties.GetValueOrDefault("referenceToken")));
        });
        var interfaceCall = calls.Single(call => call.Properties.GetValueOrDefault("opcode") == "callvirt"
            && call.Properties["targetIdentity"].Contains("names:10:ICallShape", StringComparison.Ordinal));
        Assert.Contains("method:5:Apply|", interfaceCall.Properties["targetIdentity"], StringComparison.Ordinal);
        var constrainedCall = calls.Single(call => call.Properties.GetValueOrDefault("opcode") == "constrained.");
        Assert.Equal("constrainedtype", constrainedCall.Properties["referenceKind"]);
        Assert.Equal("-", constrainedCall.Properties["referenceToken"]);
        Assert.False(string.IsNullOrWhiteSpace(constrainedCall.Properties["targetIdentity"]));
    }

    [Fact]
    public void Exception_regions_and_locals_are_committed_with_counts_and_digests()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            IlBodyEvidence: true));

        var body = BodyFact(result, "ExceptionRegionShapes");
        Assert.Equal("3", body.Properties["exceptionRegionCount"]);
        Assert.True(int.Parse(body.Properties["localCount"], CultureInfo.InvariantCulture) > 0);
        Assert.Equal(64, body.Properties["localsSha256"]!.Length);
        Assert.Equal(64, body.Properties["exceptionRegionsSha256"]!.Length);
        Assert.Contains("|il-body:instructions:", body.TargetSymbol, StringComparison.Ordinal);
        Assert.Contains(":sha256:", body.TargetSymbol, StringComparison.Ordinal);
    }

    [Fact]
    public void Multi_target_switch_opcode_produces_complete_evidence_without_disagreement()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            IlBodyEvidence: true));

        // The dense fixture case set compiles to a genuine five-target switch
        // opcode whose deltas are all relative to the shared post-table base.
        var switchBody = BodyFact(result, "SwitchTable");
        Assert.True(int.Parse(switchBody.Properties["instructionCount"], CultureInfo.InvariantCulture) > 10);
        var provenance = result.Manifest.IlBodyProvenance;
        Assert.NotNull(provenance);
        Assert.Equal("il-complete", provenance.CoverageState);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlGap
            && fact.Properties.GetValueOrDefault("gapKind") == "IlReaderDisagreement");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved);
    }

    [Fact]
    public void Hostile_switch_table_fails_closed()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var truncated = new TempDirectory();
        using var oversized = new TempDirectory();
        var truncatedPath = Path.Combine(truncated.Path, "CompiledEvidence.CSharp.dll");
        File.WriteAllBytes(truncatedPath, CorruptSwitchTable(fixture.Assembly, declareCount: 0x00ff_0000));
        var oversizedPath = Path.Combine(oversized.Path, "CompiledEvidence.CSharp.dll");
        File.WriteAllBytes(oversizedPath, CorruptSwitchTable(fixture.Assembly, declareCount: 0x0fff_fff0));
        foreach (var hostile in new[] { truncatedPath, oversizedPath })
        {
            var result = Scan(new ScanOptions(
                fixture.Source,
                TempOutput(),
                CompiledInputPaths: [hostile],
                IlBodyEvidence: true));
            var provenance = result.Manifest.IlBodyProvenance;
            Assert.NotNull(provenance);
            Assert.Equal("il-partial", provenance.CoverageState);
            Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlGap
                && fact.Properties.GetValueOrDefault("gapKind") is "MalformedIlBody" or "IlReaderDisagreement");
            Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlBodyDeclared or FactTypes.ManagedIlCallObserved);
        }
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)127)]
    public void Branch_to_non_instruction_or_outside_body_fails_closed(byte delta)
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var temp = new TempDirectory();
        var assemblyPath = Path.Combine(temp.Path, "BranchAndJmp.dll");
        WriteBranchAndJmpAssembly(assemblyPath);
        var bytes = File.ReadAllBytes(assemblyPath);
        RewriteShortBranchDelta(bytes, "Branch", delta);
        File.WriteAllBytes(assemblyPath, bytes);

        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [assemblyPath],
            IlBodyEvidence: true));

        Assert.Equal("il-partial", result.Manifest.IlBodyProvenance!.CoverageState);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlGap
            && fact.Properties.GetValueOrDefault("gapKind") == "MalformedIlBody");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlBodyDeclared or FactTypes.ManagedIlCallObserved);
    }

    [Fact]
    public void Jmp_operand_is_hashed_but_not_reported_as_a_direct_call()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var temp = new TempDirectory();
        var assemblyPath = Path.Combine(temp.Path, "BranchAndJmp.dll");
        WriteBranchAndJmpAssembly(assemblyPath);

        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [assemblyPath],
            IlBodyEvidence: true));

        Assert.Equal("il-complete", result.Manifest.IlBodyProvenance!.CoverageState);
        var jump = BodyFact(result, "Jump");
        var jumpOther = BodyFact(result, "JumpOther");
        Assert.NotEqual(jump.Properties["ilBodySha256"], jumpOther.Properties["ilBodySha256"]);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved
            && fact.Properties.GetValueOrDefault("ilBodyFactId") is var bodyId
            && (bodyId == jump.FactId || bodyId == jumpOther.FactId));
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlGap);
    }

    [Fact]
    public void Handler_ending_at_code_size_has_complete_evidence()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var temp = new TempDirectory();
        var assemblyPath = Path.Combine(temp.Path, "TerminalHandler.dll");
        WriteTerminalHandlerAssembly(assemblyPath);

        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [assemblyPath],
            IlBodyEvidence: true));

        Assert.Equal("il-complete", result.Manifest.IlBodyProvenance!.CoverageState);
        Assert.Equal("1", BodyFact(result, "TerminalHandler").Properties["exceptionRegionCount"]);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlGap);
    }

    [Fact]
    public void Deep_standalone_local_signature_becomes_a_partial_il_gap()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var temp = new TempDirectory();
        var assemblyPath = Path.Combine(temp.Path, "DeepLocal.dll");
        WriteDeepLocalAssembly(assemblyPath);

        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [assemblyPath],
            IlBodyEvidence: true));

        Assert.Equal("il-partial", result.Manifest.IlBodyProvenance!.CoverageState);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlGap
            && fact.Properties.GetValueOrDefault("gapKind") == "IlSignatureNestingLimitExceeded");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlBodyDeclared or FactTypes.ManagedIlCallObserved);
    }

    [Fact]
    public void Declared_local_count_over_limit_becomes_a_partial_il_gap()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var temp = new TempDirectory();
        var assemblyPath = Path.Combine(temp.Path, "ManyLocals.dll");
        WriteManyLocalsAssembly(assemblyPath);

        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [assemblyPath],
            IlBodyEvidence: true,
            IlBodyLimits: new IlBodyLimits(MaxLocalsPerBody: 4)));

        Assert.Equal("il-partial", result.Manifest.IlBodyProvenance!.CoverageState);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlGap
            && fact.Properties.GetValueOrDefault("gapKind") == "IlLocalLimitExceeded");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlBodyDeclared or FactTypes.ManagedIlCallObserved);
    }

    [Fact]
    public void Oversized_user_string_fails_closed_to_the_text_limit_gap()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            IlBodyEvidence: true,
            IlBodyLimits: new IlBodyLimits(MaxTextLength: 4)));

        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlGap
            && fact.Properties.GetValueOrDefault("gapKind") == "IlTextLimitExceeded");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlBodyDeclared or FactTypes.ManagedIlCallObserved);
        Assert.Contains(result.Manifest.KnownGaps, gap => gap.StartsWith("IL body evidence coverage reduced: IlTextLimitExceeded", StringComparison.Ordinal));
    }

    [Fact]
    public void Same_trivial_body_in_two_assemblies_keeps_distinct_identities()
    {
        var csharp = Fixture("csharp", "CompiledEvidence.CSharp");
        var vb = Fixture("vb", "CompiledEvidence.VisualBasic");
        var result = Scan(new ScanOptions(
            csharp.Source,
            TempOutput(),
            CompiledInputPaths: [csharp.Assembly],
            CompiledDependencyPaths: [vb.Assembly],
            IlBodyEvidence: true));

        var csharpIdentity = BodyFact(result, "IlIdentity", assemblyIdentity: "CompiledEvidence.CSharp").TargetSymbol!;
        var vbIdentity = BodyFact(result, "IlIdentity", assemblyIdentity: "CompiledEvidence.VisualBasic").TargetSymbol!;
        Assert.Contains("name:23:CompiledEvidence.CSharp", csharpIdentity, StringComparison.Ordinal);
        Assert.Contains("name:28:CompiledEvidence.VisualBasic", vbIdentity, StringComparison.Ordinal);
        Assert.NotEqual(csharpIdentity, vbIdentity);
    }

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp")]
    [InlineData("vb", "CompiledEvidence.VisualBasic")]
    [InlineData("fsharp", "CompiledEvidence.FSharp")]
    public void Language_matrix_admits_bodies_and_calls(string language, string assemblyName)
    {
        var fixture = Fixture(language, assemblyName);
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            IlBodyEvidence: true));

        Assert.NotNull(result.Manifest.IlBodyProvenance);
        Assert.Equal("il-complete", result.Manifest.IlBodyProvenance.CoverageState);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared);
        if (language is "csharp" or "vb")
            Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved);
    }

    [Fact]
    public void Disabled_lane_emits_no_il_facts_and_changes_no_manifest_section()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly]));

        Assert.Null(result.Manifest.IlBodyProvenance);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId is RuleIds.DotNetIlBody or RuleIds.DotNetIlCall or RuleIds.DotNetIlGap);
        Assert.DoesNotContain(result.Manifest.KnownGaps, gap => gap.StartsWith("IL body evidence coverage reduced:", StringComparison.Ordinal));
    }

    [Fact]
    public void Requested_lane_without_admitted_compiled_input_emits_rule_backed_gap()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            IlBodyEvidence: true));

        var provenance = result.Manifest.IlBodyProvenance;
        Assert.NotNull(provenance);
        Assert.Equal("il-partial", provenance.CoverageState);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlGap
            && fact.Properties.GetValueOrDefault("gapKind") == "IlCompiledEvidenceUnavailable"
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlBodyDeclared or FactTypes.ManagedIlCallObserved);
    }

    [Fact]
    public void Changed_assembly_after_admission_fails_closed_without_positive_il_facts()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var realBytes = File.ReadAllBytes(fixture.Assembly);
        var changedBytes = realBytes.ToArray();
        changedBytes[changedBytes.Length - 1] ^= 0xff;
        using var temp = new TempDirectory();
        var changedPath = Path.Combine(temp.Path, "CompiledEvidence.CSharp.dll");
        File.WriteAllBytes(changedPath, changedBytes);
        var evaluation = new CompiledInputEvaluation(
            null,
            [],
            [],
            [new CompiledInputBindingArtifact(
                changedPath,
                "changed-locator",
                "primary",
                "admitted",
                "unbound",
                ManagedMetadataExtractor.Sha256(realBytes),
                "assembly",
                "digest")]);
        var outcome = IlBodyEvidenceExtractor.Evaluate(
            new ScanOptions(fixture.Source, TempOutput(), IlBodyEvidence: true),
            evaluation);

        Assert.NotNull(outcome.Provenance);
        Assert.Equal("il-partial", outcome.Provenance.CoverageState);
        var gap = Assert.Single(outcome.Provenance.Outcomes);
        Assert.Equal("unreadable", gap.Outcome);
        Assert.Contains("IlCompiledArtifactChangedOrUnreadable", gap.GapKinds);
        Assert.Empty(outcome.Inputs.SelectMany(input => input.Bodies));
    }

    [Fact]
    public void Corrupted_method_body_il_fails_closed_to_rule_backed_gap()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var temp = new TempDirectory();
        var corruptedPath = Path.Combine(temp.Path, "CompiledEvidence.CSharp.dll");
        File.WriteAllBytes(corruptedPath, CorruptFirstMethodBody(fixture.Assembly));
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [corruptedPath],
            IlBodyEvidence: true));

        var provenance = result.Manifest.IlBodyProvenance;
        Assert.NotNull(provenance);
        Assert.Equal("il-partial", provenance.CoverageState);
        var gap = Assert.Single(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlGap
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown
            && !string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("limitation")));
        Assert.False(string.IsNullOrWhiteSpace(gap.Properties.GetValueOrDefault("gapKind")));
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlBodyDeclared or FactTypes.ManagedIlCallObserved);
        Assert.Contains(result.Manifest.KnownGaps, gap => gap.StartsWith("IL body evidence coverage reduced:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1, "IlInstructionLimitExceeded")]
    [InlineData(2_000_000, "IlBodyCountLimitExceeded")]
    public void Limit_exhaustion_withholds_the_input_and_emits_rule_backed_gap(int limitValue, string expectedGap)
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var limits = limitValue == 1
            ? new IlBodyLimits(MaxInstructionsPerBody: limitValue)
            : new IlBodyLimits(MaxBodyCount: limitValue == 2_000_000 ? 1 : limitValue);
        if (expectedGap == "IlBodyCountLimitExceeded")
            limits = new IlBodyLimits(MaxBodyCount: 1);
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            IlBodyEvidence: true,
            IlBodyLimits: limits));

        var provenance = result.Manifest.IlBodyProvenance;
        Assert.NotNull(provenance);
        Assert.Equal("il-partial", provenance.CoverageState);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlGap
            && fact.Properties.GetValueOrDefault("gapKind") == expectedGap);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlBodyDeclared or FactTypes.ManagedIlCallObserved);
    }

    [Fact]
    public void Total_work_limit_exhaustion_emits_rule_backed_gap()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            IlBodyEvidence: true,
            IlBodyLimits: new IlBodyLimits(MaxTotalWorkUnits: 8)));

        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlGap
            && fact.Properties.GetValueOrDefault("gapKind") == "IlTotalWorkLimitExceeded");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlBodyDeclared or FactTypes.ManagedIlCallObserved);
    }

    [Fact]
    public void Invalid_limits_are_rejected()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        Assert.Throws<ArgumentException>(() => Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            IlBodyEvidence: true,
            IlBodyLimits: new IlBodyLimits(MaxBodyCount: 0))));
    }

    [Fact]
    public void Unbound_provenance_still_records_operand_aware_bodies()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var result = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            IlBodyEvidence: true));

        var provenance = result.Manifest.IlBodyProvenance;
        Assert.NotNull(provenance);
        Assert.All(provenance.Outcomes, outcome => Assert.Equal("unbound", outcome.ProvenanceState));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
            && fact.Properties.GetValueOrDefault("provenanceState") == "unbound");
    }

    [Fact]
    public void Repeat_scans_are_byte_identical_across_artifacts()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var firstOut = Directory.CreateTempSubdirectory("tracemap-il-repeat-").FullName;
        var secondOut = Directory.CreateTempSubdirectory("tracemap-il-repeat-").FullName;
        var options = new ScanOptions(
            fixture.Source,
            Path.Combine(firstOut, "out"),
            CompiledInputPaths: [fixture.Assembly],
            IlBodyEvidence: true);
        var first = ScanEngine.Scan(options);
        var second = ScanEngine.Scan(options with { OutputPath = Path.Combine(secondOut, "out") });

        Assert.Equal(
            JsonSerializer.Serialize(first.Facts),
            JsonSerializer.Serialize(second.Facts));
        Assert.Equal(
            JsonSerializer.Serialize(first.Manifest.IlBodyProvenance),
            JsonSerializer.Serialize(second.Manifest.IlBodyProvenance));
        Assert.Equal(
            first.Facts.Count(fact => fact.RuleId is RuleIds.DotNetIlBody or RuleIds.DotNetIlCall),
            second.Facts.Count(fact => fact.RuleId is RuleIds.DotNetIlBody or RuleIds.DotNetIlCall));
    }

    [Fact]
    public async Task Il_evidence_persists_through_every_required_artifact()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        var output = Path.Combine(Directory.CreateTempSubdirectory("tracemap-il-artifacts-").FullName, "out");
        var cliError = new StringWriter();
        var cliExit = await TraceMapCommand.RunAsync(
        [
            "scan",
            "--repo", fixture.Source,
            "--out", output,
            "--compiled-input", fixture.Assembly,
            "--il-body-evidence"
        ], TextWriter.Null, cliError);
        Assert.True(cliExit == 0, $"CLI scan failed with exit {cliExit}: {cliError}");

        Assert.True(File.Exists(Path.Combine(output, "facts.ndjson")));
        Assert.True(File.Exists(Path.Combine(output, "index.sqlite")));
        Assert.True(File.Exists(Path.Combine(output, "report.md")));
        Assert.True(File.Exists(Path.Combine(output, "scan-manifest.json")));
        var ndjson = File.ReadAllText(Path.Combine(output, "facts.ndjson"));
        Assert.Contains("dotnet.compiled.il-body.v1", ndjson, StringComparison.Ordinal);
        Assert.Contains("dotnet.compiled.il-call.v1", ndjson, StringComparison.Ordinal);
        using var connection = new SqliteConnection($"Data Source={Path.Combine(output, "index.sqlite")}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "select count(*) from facts where rule_id like 'dotnet.compiled.il-%'";
        var rows = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        Assert.True(rows > 0);
        var manifestText = File.ReadAllText(Path.Combine(output, "scan-manifest.json"));
        Assert.Contains("ilBodyProvenance", manifestText, StringComparison.Ordinal);
        Assert.Contains("## Compiled .NET IL Body Evidence", File.ReadAllText(Path.Combine(output, "report.md")), StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_bodies_detects_reader_disagreement_and_agreement()
    {
        var body = new IlBodyObservation(
            "0x06000001",
            "identity",
            1,
            "aaaa",
            "eeee",
            0,
            "bbbb",
            0,
            "cccc",
            "8",
            false,
            "identity|il-body:instructions:1:sha256:dddd",
            "dddd",
            [new IlCallObservation(0, "call", "methoddef", "0x06000002", "target")]);
        var cecil = new IlBodyEvidenceExtractor.IlReaderResult("assembly", "module", "mvid", [body]);
        Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(cecil, new IlBodyEvidenceExtractor.IlReaderResult("assembly", "module", "mvid", [body])));
        var changedCall = body with { Calls = [new IlCallObservation(0, "call", "methoddef", "0x06000002", "different-target")] };
        Assert.NotEmpty(IlBodyEvidenceExtractor.CompareBodies(cecil, new IlBodyEvidenceExtractor.IlReaderResult("assembly", "module", "mvid", [changedCall])));
        var missingBody = new IlBodyEvidenceExtractor.IlReaderResult("assembly", "module", "mvid", []);
        Assert.NotEmpty(IlBodyEvidenceExtractor.CompareBodies(cecil, missingBody));
        var changedAssembly = new IlBodyEvidenceExtractor.IlReaderResult("other", "module", "mvid", [body]);
        Assert.Contains("assembly", IlBodyEvidenceExtractor.CompareBodies(cecil, changedAssembly));
    }

    [Fact]
    public void Fixture_catalog_records_stable_il_cases_gaps_and_non_claims()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepoRoot(), "samples", "compiled-dotnet-evidence", "fixture-cases.json")));
        Assert.Equal("compiled-dotnet-fixture-cases.v6", document.RootElement.GetProperty("schemaVersion").GetString());
        var cases = document.RootElement.GetProperty("ilCases").EnumerateArray().ToArray();
        Assert.True(cases.Length >= 11);
        Assert.Equal(cases.Length, cases.Select(item => item.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.All(cases, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("shape").GetString()));
            Assert.NotEmpty(item.GetProperty("expectedRuleIds").EnumerateArray());
            Assert.Contains(item.GetProperty("expectedTier").GetString(), new[] { EvidenceTiers.Tier2Structural, EvidenceTiers.Tier4Unknown });
            Assert.NotEmpty(item.GetProperty("nonClaims").EnumerateArray());
        });
        Assert.Contains(cases, item => item.GetProperty("id").GetString() == "CS-IL-OPERAND-001");
        Assert.Contains(cases, item => item.GetProperty("id").GetString() == "CS-IL-BRANCH-005");
        Assert.Contains(cases, item => item.GetProperty("id").GetString() == "IL-HOSTILE-001");
    }

    [Fact]
    public void Rule_catalog_documents_the_il_rules_with_limitations()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        Assert.Contains("- id: dotnet.compiled.il-body.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("- id: dotnet.compiled.il-call.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("- id: dotnet.compiled.il-gap.v1", catalog, StringComparison.Ordinal);
        var bodyRule = catalog[catalog.IndexOf("- id: dotnet.compiled.il-body.v1", StringComparison.Ordinal)..];
        Assert.Contains("Mono.Cecil is not the sole oracle", bodyRule, StringComparison.Ordinal);
        Assert.Contains("rewrite equivalence is a separate deferred contract", bodyRule, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)4)]
    public void Unaligned_prefix_keeps_complete_body_evidence(byte alignment)
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "Unaligned.dll");
        using (var assembly = CecilAssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("Unaligned", new Version(1, 0)), "Unaligned", ModuleKind.Dll))
        {
            var module = assembly.MainModule;
            var type = new CecilTypeDefinition("Fixture", "IlShapes", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
            module.Types.Add(type);
            var method = new CecilMethodDefinition("Read", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Int32);
            method.Parameters.Add(new ParameterDefinition(new ByReferenceType(module.TypeSystem.Int32)));
            method.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ldarg_0));
            method.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Unaligned, alignment));
            method.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ldind_I4));
            method.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
            type.Methods.Add(method);
            var signedConstant = new CecilMethodDefinition("SignedConstant", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Int32);
            signedConstant.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_S, (sbyte)-7));
            signedConstant.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
            type.Methods.Add(signedConstant);
            assembly.Write(path);
        }

        var result = Scan(new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [path], IlBodyEvidence: true));

        Assert.Equal("il-complete", result.Manifest.IlBodyProvenance!.CoverageState);
        Assert.Equal("4", BodyFact(result, "Read").Properties["instructionCount"]);
        Assert.Equal("2", BodyFact(result, "SignedConstant").Properties["instructionCount"]);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlGap);
    }

    [Theory]
    [InlineData(0xd800, 0xd801)]
    [InlineData(0xdc00, 0xdc01)]
    [InlineData(0xd800, 0xfffd)]
    public void Distinct_utf16_code_units_keep_distinct_string_operand_digests(int firstCodeUnit, int secondCodeUnit)
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "CompiledEvidence.CSharp.dll");
        var original = File.ReadAllBytes(fixture.Assembly);
        var literal = System.Text.Encoding.Unicode.GetBytes("il-alpha");
        var literalOffset = original.AsSpan().IndexOf(literal);
        Assert.True(literalOffset >= 0);
        Assert.Equal(-1, original.AsSpan(literalOffset + literal.Length).IndexOf(literal));

        ScanResult ScanWithCodeUnit(int codeUnit)
        {
            var bytes = original.ToArray();
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(literalOffset, 2), (ushort)codeUnit);
            // ECMA-335 #US terminal flag: a code unit has a nonzero high byte.
            bytes[literalOffset + literal.Length] = 1;
            File.WriteAllBytes(path, bytes);
            return Scan(new ScanOptions(fixture.Source, TempOutput(), CompiledInputPaths: [path], IlBodyEvidence: true));
        }

        var first = ScanWithCodeUnit(firstCodeUnit);
        var second = ScanWithCodeUnit(secondCodeUnit);
        Assert.Equal("il-complete", first.Manifest.IlBodyProvenance!.CoverageState);
        Assert.Equal("il-complete", second.Manifest.IlBodyProvenance!.CoverageState);
        Assert.NotEqual(BodyFact(first, "StringAlpha").Properties["instructionsSha256"],
            BodyFact(second, "StringAlpha").Properties["instructionsSha256"]);
        Assert.NotEqual(BodyFact(first, "StringAlpha").TargetSymbol, BodyFact(second, "StringAlpha").TargetSymbol);
        Assert.Equal(BodyFact(first, "StringBeta").TargetSymbol, BodyFact(second, "StringBeta").TargetSymbol);
    }

    private static CodeFact BodyFact(ScanResult result, string methodName, string? signatureFragment = null, string? assemblyIdentity = null)
    {
        var matches = result.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
            && fact.TargetSymbol!.Contains($"method:{methodName.Length.ToString(CultureInfo.InvariantCulture)}:{methodName}|", StringComparison.Ordinal)
            && (signatureFragment is null || fact.TargetSymbol.Contains(signatureFragment, StringComparison.Ordinal))
            && (assemblyIdentity is null || fact.Properties.GetValueOrDefault("assemblyIdentity")!.Contains(assemblyIdentity, StringComparison.Ordinal))).ToArray();
        return Assert.Single(matches);
    }

    private static (List<string> FirstOpcodes, List<string> SecondOpcodes) OpcodeSequences(string assembly, string first, string second)
    {
        using var loaded = CecilAssemblyDefinition.ReadAssembly(assembly);
        var methods = AllMethods(loaded.MainModule.Types)
            .GroupBy(method => method.Name)
            .ToDictionary(group => group.Key, group => group.First());
        return (Opcodes(methods[first]), Opcodes(methods[second]));
    }

    private static IEnumerable<CecilMethodDefinition> AllMethods(IEnumerable<CecilTypeDefinition> types)
    {
        foreach (var type in types)
        foreach (var method in type.Methods)
            yield return method;
    }

    private static List<string> Opcodes(CecilMethodDefinition method) => method.Body.Instructions.Select(instruction => instruction.OpCode.Name.ToString()).ToList();

    private static byte[] CorruptFirstMethodBody(string assembly)
    {
        var bytes = File.ReadAllBytes(assembly);
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if (method.RelativeVirtualAddress == 0)
                continue;
            var body = pe.GetMethodBody(method.RelativeVirtualAddress);
            var rva = method.RelativeVirtualAddress;
            // Tiny bodies: IL starts at rva + 1; fat bodies: rva + 12. Use the
            // reader-reported IL size to land inside either header shape.
            var headerSize = (bytes[FileOffset(pe, rva)] & 0x03) == 0x02 ? 1 : 12;
            var ilOffset = FileOffset(pe, rva) + headerSize;
            // 0x0f is an unassigned single-byte opcode; any reader must fail
            // closed instead of decoding past the corrupted stream.
            bytes[ilOffset] = 0xee;
            return bytes;
        }
        throw new InvalidOperationException("No method body found to corrupt.");
    }

    private static byte[] CorruptSwitchTable(string assembly, int declareCount)
    {
        var bytes = File.ReadAllBytes(assembly);
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if (method.RelativeVirtualAddress == 0 || reader.GetString(method.Name) != "SwitchTable")
                continue;
            var ilStart = FileOffset(pe, method.RelativeVirtualAddress) + ((bytes[FileOffset(pe, method.RelativeVirtualAddress)] & 0x03) == 0x02 ? 1 : 12);
            var position = ilStart;
            // Skip nops/locals prologue opcodes until the switch opcode (0x45).
            while (bytes[position] != 0x45)
                position += bytes[position] == 0x00 ? 1 : 2;
            var countOffset = position + 1;
            bytes[countOffset] = (byte)(declareCount & 0xff);
            bytes[countOffset + 1] = (byte)((declareCount >> 8) & 0xff);
            bytes[countOffset + 2] = (byte)((declareCount >> 16) & 0xff);
            bytes[countOffset + 3] = (byte)((declareCount >> 24) & 0xff);
            return bytes;
        }
        throw new InvalidOperationException("SwitchTable body not found.");
    }

    private static void WriteBranchAndJmpAssembly(string path)
    {
        using var assembly = CecilAssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("BranchAndJmp", new Version(1, 0)),
            "BranchAndJmp",
            ModuleKind.Dll);
        var module = assembly.MainModule;
        var type = new CecilTypeDefinition("Fixture", "IlShapes", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        var target = new CecilMethodDefinition("Target", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        target.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
        type.Methods.Add(target);
        var jump = new CecilMethodDefinition("Jump", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        jump.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Jmp, target));
        type.Methods.Add(jump);
        var otherTarget = new CecilMethodDefinition("OtherTarget", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        otherTarget.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
        type.Methods.Add(otherTarget);
        var jumpOther = new CecilMethodDefinition("JumpOther", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        jumpOther.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Jmp, otherTarget));
        type.Methods.Add(jumpOther);
        var branch = new CecilMethodDefinition("Branch", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        var ret = Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret);
        branch.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Br_S, ret));
        branch.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4, 123456));
        branch.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Pop));
        branch.Body.Instructions.Add(ret);
        type.Methods.Add(branch);
        assembly.Write(path);
    }

    private static void WriteTerminalHandlerAssembly(string path)
    {
        using var assembly = CecilAssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("TerminalHandler", new Version(1, 0)),
            "TerminalHandler",
            ModuleKind.Dll);
        var module = assembly.MainModule;
        var type = new CecilTypeDefinition("Fixture", "IlShapes", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        var method = new CecilMethodDefinition("TerminalHandler", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        var tryStart = Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ldnull);
        var throwInstruction = Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Throw);
        var handlerStart = Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Pop);
        method.Body.Instructions.Add(tryStart);
        method.Body.Instructions.Add(throwInstruction);
        method.Body.Instructions.Add(handlerStart);
        method.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
        method.Body.ExceptionHandlers.Add(new Mono.Cecil.Cil.ExceptionHandler(Mono.Cecil.Cil.ExceptionHandlerType.Catch)
        {
            TryStart = tryStart,
            TryEnd = handlerStart,
            HandlerStart = handlerStart,
            HandlerEnd = null,
            CatchType = module.ImportReference(typeof(Exception))
        });
        type.Methods.Add(method);
        assembly.Write(path);
    }

    private static void WriteDeepLocalAssembly(string path)
    {
        using var assembly = CecilAssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("DeepLocal", new Version(1, 0)),
            "DeepLocal",
            ModuleKind.Dll);
        var module = assembly.MainModule;
        var type = new CecilTypeDefinition("Fixture", "IlShapes", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        var method = new CecilMethodDefinition("DeepLocal", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        Mono.Cecil.TypeReference localType = module.TypeSystem.Int32;
        for (var index = 0; index <= ManagedMetadataExtractor.MaximumSignatureTypeNesting; index++)
            localType = new Mono.Cecil.ArrayType(localType);
        method.Body.InitLocals = true;
        method.Body.Variables.Add(new Mono.Cecil.Cil.VariableDefinition(localType));
        method.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
        type.Methods.Add(method);
        assembly.Write(path);
    }

    private static void WriteManyLocalsAssembly(string path)
    {
        using var assembly = CecilAssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("ManyLocals", new Version(1, 0)),
            "ManyLocals",
            ModuleKind.Dll);
        var module = assembly.MainModule;
        var type = new CecilTypeDefinition("Fixture", "IlShapes", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        var method = new CecilMethodDefinition("ManyLocals", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);
        method.Body.InitLocals = true;
        for (var index = 0; index < 128; index++)
            method.Body.Variables.Add(new Mono.Cecil.Cil.VariableDefinition(module.TypeSystem.Int32));
        method.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
        type.Methods.Add(method);
        assembly.Write(path);
    }

    private static void RewriteShortBranchDelta(byte[] bytes, string methodName, byte delta)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if (reader.GetString(method.Name) != methodName)
                continue;
            var bodyOffset = FileOffset(pe, method.RelativeVirtualAddress);
            var headerSize = (bytes[bodyOffset] & 0x03) == 0x02 ? 1 : 12;
            var ilOffset = bodyOffset + headerSize;
            Assert.Equal((byte)Mono.Cecil.Cil.OpCodes.Br_S.Value, bytes[ilOffset]);
            bytes[ilOffset + 1] = delta;
            return;
        }
        throw new InvalidOperationException($"{methodName} body not found.");
    }

    private static int FileOffset(PEReader pe, int rva)
    {
        foreach (var section in pe.PEHeaders.SectionHeaders)
        {
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + Math.Max(section.VirtualSize, section.SizeOfRawData))
                return section.PointerToRawData + (rva - section.VirtualAddress);
        }
        throw new InvalidOperationException("RVA outside any section.");
    }

    private static ScanResult Scan(ScanOptions options) => ScanEngine.Scan(options);

    private static string TempOutput() => Path.Combine(Directory.CreateTempSubdirectory("tracemap-il-output-").FullName, "out");

    private static (string Source, string Assembly) Fixture(string language, string assemblyName)
    {
        var source = Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", language);
        return (source, Path.Combine(source, "bin", "Debug", "net10.0", assemblyName + ".dll"));
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
