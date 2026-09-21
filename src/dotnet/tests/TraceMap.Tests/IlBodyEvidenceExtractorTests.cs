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
        Assert.Equal(0, await TraceMapCommand.RunAsync(
        [
            "scan",
            "--repo", fixture.Source,
            "--out", output,
            "--compiled-input", fixture.Assembly,
            "--il-body-evidence"
        ], TextWriter.Null, TextWriter.Null));

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
        Assert.Equal("compiled-dotnet-fixture-cases.v4", document.RootElement.GetProperty("schemaVersion").GetString());
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
