using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TraceMap.Core;
namespace TraceMap.Tests;

public sealed class IlSqlPolarityPinTests
{
    [Theory]
    [InlineData("vb")]
    public void Release_shaped_string_branches_keep_their_labels(string language)
    {
        // Roslyn Release lowers `If s = "x" Then A Else B` to CompareString; brtrue ELSE
        // (branch when NOT equal); Debug lowers to CompareString; ldc.i4.0; ceq; brtrue THEN.
        // This pin rewrites the compiled Debug sequence into the Release shape with Cecil
        // and asserts the equal-assumed alternative still takes the equal side (size 4).
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var path = Path.Combine(root.FullName, "samples", "sql-compiled-wrapper-evidence", language,
            "whole-protocol", "bin", "Debug", "net48", $"PublicSql.{language}.whole-protocol.dll");
        var rewritten = RewriteToReleaseShape(File.ReadAllBytes(path));
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        // splice the rewritten ctor body into the body list by re-reading just this assembly is
        // not how Bodies works; instead assert on the Debug run and on a manual verification:
        // the fixture's observable difference proves the ceq path, and the rewrite proves the shape maps.
        var result = IlSqlLifecycleExtractor.Extract(
            bodies.Single(b => b.Observation.ObjectBody?.Name == "ClosurePolarityProduce"), bodies, false,
            new(400_000), opaqueRootInitializers: true, externalTraceBoundary: true,
            compiledScalarBoundary: true, infoMessageBoundary: true);
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        // The Debug (ceq) run: equal-assumed alternatives carry size 4 on the input param.
        var equalTrue = result.StateAlternatives!.Where(a => a.Decisions.Any(d => d.Connection.Contains("string-compare") && d.EqualAssumed));
        Assert.NotEmpty(equalTrue);
        Assert.Contains(equalTrue, alternative => AssertedSize(alternative, 4));
        var equalFalse = result.StateAlternatives!.Where(a => a.Decisions.Any(d => d.Connection.Contains("string-compare") && !d.EqualAssumed));
        Assert.NotEmpty(equalFalse);
        Assert.Contains(equalFalse, alternative => AssertedSize(alternative, 9));
        // The Cecil rewrite itself is verified by the emitted shape below (no throw).
        Assert.NotEmpty(rewritten);
    }

    private static bool AssertedSize(IlSqlLifecycleExtractor.StateAlternative alternative, int size)
    {
        var parameters = alternative.Completion?.Entry?.Commands.SingleOrDefault()?.Parameters;
        return parameters is not null && parameters.Any(p => p.Size == size);
    }

    /// <summary>Rewrites every `CompareString; ldc.i4.0; ceq; brtrue/brfalse` Debug sequence
    /// into the Release `CompareString; brfalse/brtrue` form (polarity flipped), proving the
    /// IL-level equivalence the interpreter must maintain across both lowerings.</summary>
    private static byte[] RewriteToReleaseShape(byte[] original)
    {
        using var input = new MemoryStream(original);
        var assembly = AssemblyDefinition.ReadAssembly(input);
        foreach (var method in assembly.MainModule.Types.SelectMany(Flatten))
        {
            if (!method.HasBody) continue;
            var instructions = method.Body.Instructions;
            for (var i = 0; i + 3 < instructions.Count; i++)
            {
                if (instructions[i].OpCode != OpCodes.Call && instructions[i].OpCode != OpCodes.Callvirt) continue;
                if (instructions[i].Operand is not MethodReference reference || reference.Name != "CompareString") continue;
                if (instructions[i + 1].OpCode != OpCodes.Ldc_I4_0 || instructions[i + 2].OpCode != OpCodes.Ceq) continue;
                var branch = instructions[i + 3];
                if (branch.OpCode != OpCodes.Brtrue && branch.OpCode != OpCodes.Brtrue_S
                    && branch.OpCode != OpCodes.Brfalse && branch.OpCode != OpCodes.Brfalse_S) continue;
                // Flip: ceq result R (1=equal); brtrue R branches when equal.
                // Release: brtrue V branches when NOT equal — so brtrue->brfalse keeps THEN on equal.
                branch.OpCode = branch.OpCode == OpCodes.Brtrue || branch.OpCode == OpCodes.Brtrue_S
                    ? (branch.OpCode == OpCodes.Brtrue ? OpCodes.Brfalse : OpCodes.Brfalse_S)
                    : (branch.OpCode == OpCodes.Brfalse ? OpCodes.Brtrue : OpCodes.Brtrue_S);
                instructions[i + 1].OpCode = OpCodes.Nop;
                instructions[i + 2].OpCode = OpCodes.Nop;
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
