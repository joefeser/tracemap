using System.Globalization;
using System.Reflection.Metadata;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CecilAssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using CecilMethodDefinition = Mono.Cecil.MethodDefinition;
using CecilTypeDefinition = Mono.Cecil.TypeDefinition;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

/// <summary>
/// Public ECMA-335 control-flow and exception-handling rewrite-suite cases
/// (#766, Task 10 fifth slice). The before sides are the deterministic
/// compiler-produced CompiledEvidence.CSharp.ControlFlow fixture assembly;
/// every after side is a deterministic Mono.Cecil-generated mutation or a
/// bounded byte patch produced inside this suite, never a hand-written or
/// machine-generated binary from outside the repository. Each test pins one
/// catalog case ID from fixture-cases.json v7.
/// </summary>
public sealed class IlRewriteControlFlowEvidenceExtractorTests
{
    private const string FixtureType = "TraceMap.CompiledFixtures.CSharp.Il.IlRewriteControlFlowShapes";

    [Fact]
    public void Branch_retarget_keeps_the_opcode_stream_and_classifies_operand_only_change()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "branch");
        MutateBranchRetarget(before, after);

        var result = Scan(PairOptions(before, after, temp));

        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = EdgeFact(result, "LoopWithBranches");
        Assert.Equal(EvidenceTiers.Tier2Structural, edge.EvidenceTier);
        Assert.Equal(RuleIds.DotNetIlRewrite, edge.RuleId);
        Assert.Equal("operand-only-change", edge.Properties["relationshipKind"]);
        Assert.Equal("true", edge.Properties["opcodeSequencePreserved"]);
        Assert.Equal("false", edge.Properties["tokenRetargeted"]);
        Assert.Equal("0", edge.Properties["callRetargetCount"]);
        Assert.NotEqual(edge.Properties["beforeIlBodySha256"], edge.Properties["afterIlBodySha256"]);
        Assert.Contains(":after:sha256:", edge.TargetSymbol, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
        // Untouched control-flow siblings prove the join survived the whole
        // compiler-produced assembly, not just the mutated method.
        Assert.Equal("unchanged", EdgeFact(result, "DenseSwitch").Properties["relationshipKind"]);
        Assert.Equal("unchanged", EdgeFact(result, "NestedTryRegions").Properties["relationshipKind"]);
    }

    [Fact]
    public void Switch_jump_table_permutation_classifies_operand_only_change()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "switch");
        MutateSwitchTargets(before, after);

        var result = Scan(PairOptions(before, after, temp));

        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = EdgeFact(result, "DenseSwitch");
        Assert.Equal("operand-only-change", edge.Properties["relationshipKind"]);
        Assert.Equal("true", edge.Properties["opcodeSequencePreserved"]);
        Assert.Equal("0", edge.Properties["callRetargetCount"]);
        Assert.NotEqual(edge.Properties["beforeIlBodySha256"], edge.Properties["afterIlBodySha256"]);
        Assert.Equal("unchanged", EdgeFact(result, "LoopWithBranches").Properties["relationshipKind"]);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    [Fact]
    public void Leave_retarget_classifies_operand_only_change()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "leave");
        MutateLeaveTarget(before, after);

        var result = Scan(PairOptions(before, after, temp));

        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = EdgeFact(result, "TryCatchFinallyWithLeave");
        Assert.Equal("operand-only-change", edge.Properties["relationshipKind"]);
        Assert.Equal("true", edge.Properties["opcodeSequencePreserved"]);
        Assert.Equal("0", edge.Properties["callRetargetCount"]);
        Assert.NotEqual(edge.Properties["beforeIlBodySha256"], edge.Properties["afterIlBodySha256"]);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    [Fact]
    public void Nested_region_rebinding_classifies_body_structure_change()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "eh-rebind");
        MutateNestedTryRebinding(before, after);

        var result = Scan(PairOptions(before, after, temp));

        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = EdgeFact(result, "NestedTryRegions");
        Assert.Equal("body-structure-change", edge.Properties["relationshipKind"]);
        Assert.Equal("true", edge.Properties["opcodeSequencePreserved"]);
        Assert.Equal("0", edge.Properties["callRetargetCount"]);
        Assert.NotEqual(edge.Properties["beforeIlBodySha256"], edge.Properties["afterIlBodySha256"]);
        Assert.Equal("unchanged", EdgeFact(result, "TryCatchFinallyWithLeave").Properties["relationshipKind"]);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    [Fact]
    public void Handler_kind_change_catch_to_fault_classifies_body_structure_change()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "eh-kind");
        MutateHandlerKind(before, after);

        var result = Scan(PairOptions(before, after, temp));

        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = EdgeFact(result, "NestedTryRegions");
        Assert.Equal("body-structure-change", edge.Properties["relationshipKind"]);
        Assert.Equal("true", edge.Properties["opcodeSequencePreserved"]);
        Assert.Equal("0", edge.Properties["callRetargetCount"]);
        Assert.NotEqual(edge.Properties["beforeIlBodySha256"], edge.Properties["afterIlBodySha256"]);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    [Fact]
    public void Max_stack_only_header_change_classifies_body_structure_change()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "maxstack");
        BumpRecordedMaxStack(after, "LocalsAndMaxStack");

        var result = Scan(PairOptions(before, after, temp));

        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = EdgeFact(result, "LocalsAndMaxStack");
        Assert.Equal("body-structure-change", edge.Properties["relationshipKind"]);
        Assert.Equal("true", edge.Properties["opcodeSequencePreserved"]);
        Assert.Equal("0", edge.Properties["callRetargetCount"]);
        Assert.NotEqual(edge.Properties["beforeIlBodySha256"], edge.Properties["afterIlBodySha256"]);
        // Every other body is byte-identical, so the whole assembly joins.
        Assert.Equal("unchanged", EdgeFact(result, "StackShape").Properties["relationshipKind"]);
        Assert.Equal("unchanged", EdgeFact(result, "LoopWithBranches").Properties["relationshipKind"]);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    [Fact]
    public void Stack_neutral_dup_pop_insertion_classifies_instruction_stream_change()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "stack-neutral");
        MutateStackNeutralInsertion(before, after);

        var result = Scan(PairOptions(before, after, temp));

        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = EdgeFact(result, "StackShape");
        Assert.Equal("instruction-stream-change", edge.Properties["relationshipKind"]);
        Assert.Equal("false", edge.Properties["opcodeSequencePreserved"]);
        Assert.Equal("0", edge.Properties["callRetargetCount"]);
        Assert.NotEqual(edge.Properties["beforeIlBodySha256"], edge.Properties["afterIlBodySha256"]);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    [Fact]
    public void Stack_reshaping_insertion_classifies_instruction_stream_change()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "stack-reshape");
        MutateStackReshapingInsertion(before, after);

        var result = Scan(PairOptions(before, after, temp));

        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = EdgeFact(result, "StackShape");
        Assert.Equal("instruction-stream-change", edge.Properties["relationshipKind"]);
        Assert.Equal("false", edge.Properties["opcodeSequencePreserved"]);
        Assert.Equal("0", edge.Properties["callRetargetCount"]);
        Assert.NotEqual(edge.Properties["beforeIlBodySha256"], edge.Properties["afterIlBodySha256"]);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    [Fact]
    public void Control_flow_edges_never_claim_runtime_equivalence_or_execution()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "branch");
        MutateBranchRetarget(before, after);

        var result = Scan(PairOptions(before, after, temp));

        Assert.All(result.Facts.Where(fact => fact.RuleId == RuleIds.DotNetIlRewrite), fact =>
        {
            var limitation = fact.Properties.GetValueOrDefault("limitation");
            Assert.False(string.IsNullOrWhiteSpace(limitation));
            Assert.Contains("does not prove semantic equivalence", limitation, StringComparison.Ordinal);
            Assert.Contains("behavior preservation", limitation, StringComparison.Ordinal);
            Assert.Equal(ScannerVersions.IlRewriteEvidenceExtractor, fact.Evidence.ExtractorVersion);
        });
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.ManagedIlCallRetargetObserved);
    }

    [Fact]
    public void Out_of_range_branch_delta_withholds_the_whole_pair()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "hostile-branch");
        CorruptBranchDelta(after, "LoopWithBranches");

        var evaluation = IlRewriteEvidenceExtractor.Evaluate(PairOptions(before, after, temp));

        Assert.Equal("rewrite-partial", evaluation.Provenance!.CoverageState);
        var pair = Assert.Single(evaluation.Pairs);
        Assert.Empty(pair.Edges);
        Assert.Equal("malformed", pair.Outcome.Outcome);
        var failure = Assert.Single(pair.SideFailures);
        Assert.Equal("after", failure.Side);
        Assert.Equal("IlRewriteMalformedInput", failure.GapKind);
        Assert.Equal("MalformedIlBody", failure.Cause);
    }

    [Fact]
    public void Oversized_switch_table_count_withholds_the_whole_pair()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "hostile-switch");
        CorruptSwitchCount(after, "DenseSwitch");

        var evaluation = IlRewriteEvidenceExtractor.Evaluate(PairOptions(before, after, temp));

        Assert.Equal("rewrite-partial", evaluation.Provenance!.CoverageState);
        var pair = Assert.Single(evaluation.Pairs);
        Assert.Empty(pair.Edges);
        Assert.Equal("malformed", pair.Outcome.Outcome);
        var failure = Assert.Single(pair.SideFailures);
        Assert.Equal("after", failure.Side);
        Assert.Equal("IlRewriteMalformedInput", failure.GapKind);
        Assert.Equal("MalformedIlBody", failure.Cause);
    }

    [Fact]
    public void Exception_region_limit_exhaustion_withholds_the_pair_with_a_rule_backed_gap()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "limit");
        var result = Scan(PairOptions(before, after, temp, new IlBodyLimits(MaxExceptionRegionsPerBody: 1)));

        Assert.Equal("rewrite-partial", result.Manifest.IlRewriteProvenance!.CoverageState);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.ManagedIlRewriteObserved);
        var gaps = result.Facts.Where(fact => fact.RuleId == RuleIds.DotNetIlRewriteGap).ToArray();
        Assert.Equal(2, gaps.Length);
        Assert.All(gaps, gap =>
        {
            Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
            Assert.Equal("IlRewriteExceptionRegionLimitExceeded", gap.Properties["gapKind"]);
            Assert.Equal("IlExceptionRegionLimitExceeded", gap.Properties["cause"]);
            Assert.False(string.IsNullOrWhiteSpace(gap.Properties.GetValueOrDefault("limitation")));
        });
        Assert.Equal(["after", "before"], gaps.Select(gap => gap.Properties["side"]).OrderBy(side => side, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Malformed_control_flow_gaps_are_evidenced_as_analysis_gap_facts()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "hostile-branch");
        CorruptBranchDelta(after, "LoopWithBranches");

        var result = Scan(PairOptions(before, after, temp));

        var gap = Assert.Single(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
        Assert.Equal(FactTypes.AnalysisGap, gap.FactType);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal("IlRewriteMalformedInput", gap.Properties["gapKind"]);
        Assert.Equal("after", gap.Properties["side"]);
        Assert.False(string.IsNullOrWhiteSpace(gap.Properties.GetValueOrDefault("limitation")));
    }

    [Fact]
    public void Fixture_catalog_records_control_flow_rewrite_cases_gaps_and_non_claims()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepoRoot(), "samples", "compiled-dotnet-evidence", "fixture-cases.json")));
        Assert.Equal("compiled-dotnet-fixture-cases.v9", document.RootElement.GetProperty("schemaVersion").GetString());
        var cases = document.RootElement.GetProperty("ilRewriteCases").EnumerateArray().ToArray();
        Assert.True(cases.Length >= 20);
        Assert.Equal(cases.Length, cases.Select(item => item.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.All(cases, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("shape").GetString()));
            Assert.NotEmpty(item.GetProperty("expectedRuleIds").EnumerateArray());
            Assert.Contains(item.GetProperty("expectedTier").GetString(), new[] { EvidenceTiers.Tier2Structural, EvidenceTiers.Tier4Unknown });
            Assert.NotEmpty(item.GetProperty("nonClaims").EnumerateArray());
        });
        var ids = cases.Select(item => item.GetProperty("id").GetString()).ToArray();
        foreach (var id in new[]
                 {
                     "CS-ILRW-CFLOW-010", "CS-ILRW-CFLOW-011", "CS-ILRW-CFLOW-012",
                     "CS-ILRW-CFLOW-013", "CS-ILRW-CFLOW-014", "CS-ILRW-CFLOW-015",
                     "CS-ILRW-CFLOW-016", "CS-ILRW-CFLOW-017", "ILRW-CFLOW-HOSTILE-018",
                     "ILRW-CFLOW-HOSTILE-019", "ILRW-CFLOW-LIMIT-020",
                 })
            Assert.Contains(id, ids);
    }

    [Fact]
    public void Repeat_control_flow_scans_are_deterministic_and_privacy_projected()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "determinism");
        MutateBranchRetarget(before, after);
        var first = Scan(PairOptions(before, after, temp));
        var second = ScanEngine.Scan(PairOptions(before, after, temp));

        Assert.Equal(
            JsonSerializer.Serialize(first.Facts),
            JsonSerializer.Serialize(second.Facts));
        Assert.Equal(
            JsonSerializer.Serialize(first.Manifest.IlRewriteProvenance),
            JsonSerializer.Serialize(second.Manifest.IlRewriteProvenance));
        var serialized = JsonSerializer.Serialize(first);
        Assert.DoesNotContain(temp.Path, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFullPath(RepoRoot()), serialized, StringComparison.Ordinal);
        Assert.All(first.Facts.Where(fact => fact.RuleId is RuleIds.DotNetIlRewrite or RuleIds.DotNetIlRewriteGap), fact =>
        {
            Assert.Matches("^[0-9a-f]{64}$", fact.Properties.GetValueOrDefault("ilRewriteBoundedInputSha256"));
            Assert.Matches("^[0-9a-f]{64}$", fact.Properties.GetValueOrDefault("ilRewriteGeneratorSha256"));
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("limitation")));
            Assert.Equal(ScannerVersions.IlRewriteEvidenceExtractor, fact.Evidence.ExtractorVersion);
        });
    }

    [Fact]
    public async Task Control_flow_rewrite_evidence_persists_through_every_required_artifact()
    {
        using var temp = new TempDirectory();
        var (before, after) = FixturePair(temp, "artifacts");
        MutateSwitchTargets(before, after);
        var output = Path.Combine(temp.Path, "cli-out");
        var cliError = new StringWriter();
        var cliExit = await TraceMapCommand.RunAsync(
        [
            "scan",
            "--repo", RepoRoot(),
            "--out", output,
            "--il-rewrite-evidence",
            "--il-rewrite-before", before,
            "--il-rewrite-after", after
        ], TextWriter.Null, cliError);
        Assert.True(cliExit == 0, $"CLI scan failed with exit {cliExit}: {cliError}");

        Assert.True(File.Exists(Path.Combine(output, "facts.ndjson")));
        Assert.True(File.Exists(Path.Combine(output, "index.sqlite")));
        Assert.True(File.Exists(Path.Combine(output, "report.md")));
        Assert.True(File.Exists(Path.Combine(output, "scan-manifest.json")));
        var ndjson = File.ReadAllText(Path.Combine(output, "facts.ndjson"));
        Assert.Contains("dotnet.compiled.il-rewrite.v1", ndjson, StringComparison.Ordinal);
        Assert.Contains("operand-only-change", ndjson, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet.compiled.il-rewrite-gap", ndjson, StringComparison.Ordinal);
        using var connection = new SqliteConnection($"Data Source={Path.Combine(output, "index.sqlite")}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "select count(*) from facts where rule_id like 'dotnet.compiled.il-rewrite%'";
        var rows = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        Assert.True(rows > 0);
        var manifestText = File.ReadAllText(Path.Combine(output, "scan-manifest.json"));
        Assert.Contains("ilRewriteProvenance", manifestText, StringComparison.Ordinal);
        Assert.Contains("## Compiled .NET IL Rewrite Evidence", File.ReadAllText(Path.Combine(output, "report.md")), StringComparison.Ordinal);
    }

    private static (string Before, string After) FixturePair(TempDirectory temp, string role)
    {
        var before = Path.Combine(temp.Path, $"ControlFlow.{role}.before.dll");
        var after = Path.Combine(temp.Path, $"ControlFlow.{role}.after.dll");
        File.Copy(ControlFlowFixture(), before);
        File.Copy(ControlFlowFixture(), after);
        return (before, after);
    }

    private static string ControlFlowFixture()
    {
        // The fixture builds per-configuration like every sibling compiled
        // fixture; probe Debug then Release so `dotnet test -c Release`
        // still locates the assembly.
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var candidate = Path.Combine(
                FindRepoRoot(),
                "samples", "compiled-dotnet-evidence", "csharp", "bin", configuration, "net10.0",
                "CompiledEvidence.CSharp.ControlFlow.dll");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            "CompiledEvidence.CSharp.ControlFlow.dll was not found in bin/Debug/net10.0 or bin/Release/net10.0 under samples/compiled-dotnet-evidence/csharp; build the fixture project first.");
    }

    private static ScanOptions PairOptions(string before, string after, TempDirectory temp, IlBodyLimits? bodyLimits = null) => new(
        RepoRoot(),
        TempOutput(temp),
        IlRewriteEvidence: true,
        IlRewriteBeforePaths: [before],
        IlRewriteAfterPaths: [after],
        IlBodyLimits: bodyLimits);

    private static CodeFact EdgeFact(ScanResult result, string methodName)
    {
        var matches = result.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlRewriteObserved
            && fact.Properties.GetValueOrDefault("methodIdentity")!.Contains(
                $"method:{methodName.Length.ToString(CultureInfo.InvariantCulture)}:{methodName}|",
                StringComparison.Ordinal)).ToArray();
        return Assert.Single(matches);
    }

    private static ScanResult Scan(ScanOptions options) => ScanEngine.Scan(options);

    // Scan outputs live beneath the test's disposable TempDirectory so every
    // artifact tree is removed when the test ends; the random leaf keeps two
    // scans of one test from sharing an output directory.
    private static string TempOutput(TempDirectory temp) =>
        Path.Combine(temp.Path, $"scan-{Path.GetRandomFileName()}", "out");

    private static string RepoRoot() => Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", "csharp");

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

    private static void MutateControlFlow(string before, string after, Action<CecilTypeDefinition> mutate)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(before);
        var type = (CecilTypeDefinition)assembly.MainModule.GetType(FixtureType)!;
        mutate(type);
        assembly.Write(after);
    }

    private static CecilMethodDefinition FindMethod(CecilTypeDefinition type, string name) =>
        type.Methods.Single(method => method.Name == name);

    // CS-ILRW-CFLOW-010: retarget the method's first branch instruction (the
    // loop-entry forward branch) to the next instruction boundary after its
    // original target. The opcode stream, every other operand, and the
    // non-instruction body structure stay identical, so only the
    // operand-aware digests change.
    private static void MutateBranchRetarget(string before, string after) => MutateControlFlow(before, after, type =>
    {
        var branch = FindMethod(type, "LoopWithBranches").Body.Instructions
            .First(instruction => instruction.OpCode.OperandType is OperandType.ShortInlineBrTarget or OperandType.InlineBrTarget);
        var original = (Instruction)branch.Operand!;
        Assert.NotNull(original.Next);
        branch.Operand = original.Next;
    });

    // CS-ILRW-CFLOW-011: permute the switch jump table by swapping the first
    // and last target entries. The fixed-size table keeps every instruction
    // offset stable, so the change is operand-only.
    private static void MutateSwitchTargets(string before, string after) => MutateControlFlow(before, after, type =>
    {
        var instruction = FindMethod(type, "DenseSwitch").Body.Instructions
            .Single(item => item.OpCode == OpCodes.Switch);
        var targets = (Instruction[])instruction.Operand!;
        Assert.True(targets.Length >= 2);
        (targets[0], targets[^1]) = (targets[^1], targets[0]);
        instruction.Operand = targets;
    });

    // CS-ILRW-CFLOW-012: retarget the try region's leave from the shared
    // post-handler offset to the post-region code used by the outermost
    // leave. Both targets sit outside every exception region.
    private static void MutateLeaveTarget(string before, string after) => MutateControlFlow(before, after, type =>
    {
        var leaves = FindMethod(type, "TryCatchFinallyWithLeave").Body.Instructions
            .Where(instruction => instruction.OpCode.Name.StartsWith("leave", StringComparison.Ordinal))
            .ToArray();
        Assert.True(leaves.Length >= 2);
        var sharedTarget = (Instruction)leaves[^1].Operand!;
        Assert.NotEqual((Instruction)leaves[0].Operand!, sharedTarget);
        leaves[0].Operand = sharedTarget;
    });

    // CS-ILRW-CFLOW-013: rebind the nested catch's try start to the outer
    // finally's try start. The regions stay properly nested and no
    // instruction changes, so only the exception-region digest changes.
    private static void MutateNestedTryRebinding(string before, string after) => MutateControlFlow(before, after, type =>
    {
        var handlers = FindMethod(type, "NestedTryRegions").Body.ExceptionHandlers;
        var finallyHandler = Assert.Single(handlers, handler => handler.HandlerType == ExceptionHandlerType.Finally);
        var catchHandler = Assert.Single(handlers, handler => handler.HandlerType == ExceptionHandlerType.Catch);
        Assert.NotEqual(finallyHandler.TryStart, catchHandler.TryStart);
        catchHandler.TryStart = finallyHandler.TryStart;
    });

    // CS-ILRW-CFLOW-014: turn the nested catch handler into a fault handler
    // over identical region extents with no catch-type token.
    private static void MutateHandlerKind(string before, string after) => MutateControlFlow(before, after, type =>
    {
        var catchHandler = Assert.Single(
            FindMethod(type, "NestedTryRegions").Body.ExceptionHandlers,
            handler => handler.HandlerType == ExceptionHandlerType.Catch);
        catchHandler.HandlerType = ExceptionHandlerType.Fault;
        catchHandler.CatchType = null;
    });

    // CS-ILRW-CFLOW-016: insert a dup/pop pair after the first instruction.
    // The pair is transiently deeper but net stack-neutral; the opcode stream
    // changes, so the edge may make no per-instruction claim.
    private static void MutateStackNeutralInsertion(string before, string after) => MutateControlFlow(before, after, type =>
    {
        var processor = FindMethod(type, "StackShape").Body.GetILProcessor();
        var first = processor.Body.Instructions[0];
        var duplicate = Instruction.Create(OpCodes.Dup);
        var pop = Instruction.Create(OpCodes.Pop);
        processor.InsertAfter(first, duplicate);
        processor.InsertAfter(duplicate, pop);
    });

    // CS-ILRW-CFLOW-017: insert a balanced constant/add pair before the
    // existing add so the evaluation-stack depth profile changes while the
    // body stays well-formed decodable IL (the stack is empty at ret).
    private static void MutateStackReshapingInsertion(string before, string after) => MutateControlFlow(before, after, type =>
    {
        var processor = FindMethod(type, "StackShape").Body.GetILProcessor();
        var add = processor.Body.Instructions.Single(instruction => instruction.OpCode == OpCodes.Add);
        processor.InsertBefore(add, Instruction.Create(OpCodes.Add));
        processor.InsertBefore(add, Instruction.Create(OpCodes.Ldc_I4_1));
    });

    // CS-ILRW-CFLOW-015: bump the recorded max-stack in the fat method-body
    // header. Instructions, locals, exception regions, and init-locals stay
    // byte-identical, so only the recorded header field changes.
    private static void BumpRecordedMaxStack(string path, string methodName)
    {
        var bytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if (reader.GetString(method.Name) != methodName || method.RelativeVirtualAddress == 0)
                continue;
            var bodyOffset = FileOffset(pe, method.RelativeVirtualAddress);
            if ((bytes[bodyOffset] & 0x03) != 0x03)
                throw new InvalidOperationException($"{methodName} does not use a fat method-body header.");
            bytes[bodyOffset + 2]++;
            File.WriteAllBytes(path, bytes);
            return;
        }

        throw new InvalidOperationException($"{methodName} body not found.");
    }

    // ILRW-CFLOW-HOSTILE-018: set the method's first short-branch delta to
    // 0x7f so the resolved target falls past the method-body extent and both
    // readers must fail closed instead of decoding past the corrupted stream.
    private static void CorruptBranchDelta(string path, string methodName)
    {
        var instruction = ReadInstruction(path, methodName,
            item => item.OpCode.OperandType == OperandType.ShortInlineBrTarget);
        PatchInstructionOperand(path, methodName, instruction, operandOffset: 1, value: 0x7f);
    }

    // ILRW-CFLOW-HOSTILE-019: set the low byte of the switch count (one byte
    // past the 0x45 opcode) to 0x7f so the declared jump table overruns the
    // bounded body extent.
    private static void CorruptSwitchCount(string path, string methodName)
    {
        var instruction = ReadInstruction(path, methodName, item => item.OpCode == OpCodes.Switch);
        PatchInstructionOperand(path, methodName, instruction, operandOffset: 1, value: 0x7f);
    }

    private static Instruction ReadInstruction(string path, string methodName, Func<Instruction, bool> predicate)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(path);
        var type = (CecilTypeDefinition)assembly.MainModule.GetType(FixtureType)!;
        // First-in-order keeps the selection deterministic when a body has
        // several instructions of the same family.
        return FindMethod(type, methodName).Body.Instructions.First(predicate);
    }

    private static void PatchInstructionOperand(string path, string methodName, Instruction instruction, int operandOffset, byte value)
    {
        var bytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if (reader.GetString(method.Name) != methodName || method.RelativeVirtualAddress == 0)
                continue;
            var bodyOffset = FileOffset(pe, method.RelativeVirtualAddress);
            var headerSize = (bytes[bodyOffset] & 0x03) == 0x02 ? 1 : 12;
            bytes[bodyOffset + headerSize + instruction.Offset + operandOffset] = value;
            File.WriteAllBytes(path, bytes);
            return;
        }

        throw new InvalidOperationException($"{methodName} body not found.");
    }

    private static int FileOffset(System.Reflection.PortableExecutable.PEReader pe, int rva)
    {
        foreach (var section in pe.PEHeaders.SectionHeaders)
        {
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + Math.Max(section.VirtualSize, section.SizeOfRawData))
                return section.PointerToRawData + (rva - section.VirtualAddress);
        }

        throw new InvalidOperationException("RVA outside any section.");
    }
}
