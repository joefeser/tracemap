using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TraceMap.Core;
namespace TraceMap.Tests;

public sealed class IlSqlNullFoldPinTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Release_shaped_null_tests_fold_both_null_operands(string language)
    {
        // v40-001: the private binary's .NET 1.x-era vbc lowers `If x Is Nothing`
        // to the cgt.un/ldnull reference test; this SDK's Debug lowering stores a
        // ceq temp instead (Equal folds null against null there). The pin rewrites
        // every `…; ldnull; ceq; stloc; ldloc; br*` null test into the Release-era
        // shape (ceq→cgt.un, the temp nopped, branch polarity flipped) and walks
        // it: the forwarded null-origin array folds deterministically (both
        // operands proven null, never a fork) and both fork paths complete.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, RewriteNullTestsToReleaseShape);
        var result = IlSqlLifecycleExtractor.Extract(
            bodies.Single(b => b.Observation.ObjectBody?.Name == "ClosureNullFoldProduce"), bodies, false,
            new(400_000), opaqueRootInitializers: true, externalTraceBoundary: true,
            compiledScalarBoundary: true, infoMessageBoundary: true);
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.StateAlternatives);
        Assert.NotEmpty(result.StateAlternatives!);
        var parameterCounts = result.StateAlternatives!.Where(a => a.Completion?.Entry is not null)
            .Select(a => Assert.Single(a.Completion!.Entry!.Commands).Parameters.Count).ToList();
        Assert.Contains(1, parameterCounts);
        Assert.Contains(0, parameterCounts);
    }

    /// <summary>Rewrites every `…; ldnull; ceq; stloc…; ldloc…; brtrue/brfalse` null test
    /// into the Release-era shape: ceq becomes cgt.un (truth inverted), the stored
    /// temp becomes nops, and the branch polarity flips — the equivalence the
    /// interpreter must maintain across both lowerings of the null guard.</summary>
    private static byte[] RewriteNullTestsToReleaseShape(byte[] original)
    {
        using var input = new MemoryStream(original);
        var assembly = AssemblyDefinition.ReadAssembly(input);
        foreach (var method in assembly.MainModule.Types.SelectMany(Flatten))
        {
            if (!method.HasBody) continue;
            var instructions = method.Body.Instructions;
            for (var i = 1; i + 3 < instructions.Count; i++)
            {
                if (instructions[i].OpCode != OpCodes.Ceq) continue;
                if (instructions[i - 1].OpCode != OpCodes.Ldnull) continue;
                if (!instructions[i + 1].OpCode.Name.StartsWith("stloc", StringComparison.Ordinal)) continue;
                if (!instructions[i + 2].OpCode.Name.StartsWith("ldloc", StringComparison.Ordinal)) continue;
                var branch = instructions[i + 3];
                if (branch.OpCode != OpCodes.Brtrue && branch.OpCode != OpCodes.Brtrue_S
                    && branch.OpCode != OpCodes.Brfalse && branch.OpCode != OpCodes.Brfalse_S) continue;
                instructions[i].OpCode = OpCodes.Cgt_Un;
                instructions[i + 1].OpCode = OpCodes.Nop;
                instructions[i + 2].OpCode = OpCodes.Nop;
                branch.OpCode = branch.OpCode == OpCodes.Brtrue || branch.OpCode == OpCodes.Brtrue_S
                    ? (branch.OpCode == OpCodes.Brtrue ? OpCodes.Brfalse : OpCodes.Brfalse_S)
                    : (branch.OpCode == OpCodes.Brfalse ? OpCodes.Brtrue : OpCodes.Brtrue_S);
            }
        }
        using var output = new MemoryStream();
        assembly.Write(output);
        return output.ToArray();
    }

    private static IEnumerable<MethodDefinition> Flatten(TypeDefinition type)
    {
        foreach (var method in type.Methods) yield return method;
        foreach (var nested in type.NestedTypes)
            foreach (var method in Flatten(nested)) yield return method;
    }
}
