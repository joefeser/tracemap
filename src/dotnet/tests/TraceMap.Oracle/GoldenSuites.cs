// #856 stage 2c: parses the corpus's machine-generated validator suites into
// expectation data and replays them. The suites are byte-pinned corpus files:
//   src/NetPerf.Tests.Unit/SwitchInstrTest.cs          (generated, VerifyBranches)
//   src/NetPerf.Tests.Unit/SingleTargetBranchesTest.cs (generated, VerifyBranches)
//   src/NetPerf.Tests.Unit/ProtectedBlocksTest.cs      (VerifyProtectedBlocks)
// The generated shape is regular: construct a body from a nop-grid prototype,
// mutate it via AddTargetAndSource/AddProtectedBlock (or the two helpers that
// wrap AddProtectedBlock), verify with a specific private era verifier, and
// assert the verdict plus, for failures, an exact error-message prefix/suffix.
// Anything that does not match the regular shape lands in Irregular with a
// reason — never silently dropped. LeaveInstrTest is NOT parsed: its bodies are
// hand-authored ilasm text with no construction helper (deferred; ilasm is not
// available cross-platform and is excluded from the corpus).
using System.Reflection;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NetPerf.Goldens;

internal sealed record BranchesGolden(
    string Suite, string TestName, string Verify,
    string SourceOpcode, int SourceIndex, string SourceKind,
    int TargetIndex, string TargetKind, int Target2Index, string Target2Kind,
    bool ExpectedValid, string ExpectedErrorSuffix)
{
    public string Category => $"{Suite}|{(ExpectedValid ? "pass" : "fail")}|{SourceOpcode}|{SourceKind},{TargetKind},{Target2Kind}";
}

internal readonly record struct ProtectedBlockSpec(
    string Kind, int TryStart, int TryEnd, int FilterStart, int FilterEnd, int HandlerStart, int HandlerEnd);

internal sealed record ProtectedBlocksGolden(
    string TestName, string Verify, bool ExpectedValid,
    string ErrorStartsWith, string ErrorEndsWith,
    IReadOnlyList<ProtectedBlockSpec> Blocks,
    IReadOnlyList<(int Index, string OpCode)> OpcodeWrites);

internal sealed record GoldenSuites(
    IReadOnlyList<BranchesGolden> Branches,
    IReadOnlyList<ProtectedBlocksGolden> ProtectedBlocks,
    IReadOnlyList<(string Suite, string TestName, string Reason)> Irregular,
    IReadOnlyList<string> EraNoOpTests)
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, GoldenSuites> _parsed = new();

    public static GoldenSuites Parse(string corpusRoot)
    {
        return _parsed.GetOrAdd(corpusRoot, static root =>
        {
            var branches = new List<BranchesGolden>();
            var blocks = new List<ProtectedBlocksGolden>();
            var irregular = new List<(string, string, string)>();
            var noOps = new List<string>();

            ParseBranchesSuite(root, "SwitchInstrTest.cs", branches, irregular);
            ParseBranchesSuite(root, "SingleTargetBranchesTest.cs", branches, irregular);
            ParseProtectedBlocksSuite(root, blocks, irregular, noOps);

            return new GoldenSuites(branches, blocks, irregular, noOps);
        });
    }

    private static void ParseBranchesSuite(string corpusRoot, string suiteFile,
        List<BranchesGolden> into, List<(string, string, string)> irregular)
    {
        var text = File.ReadAllText(Path.Combine(corpusRoot, "src", "NetPerf.Tests.Unit", suiteFile));
        foreach (var (name, body) in MethodBodies(text))
        {
            var flat = Regex.Replace(body, @"\s+", " ");
            var addMatch = Regex.Match(flat,
                @"AddTargetAndSource\(body, OpCodes\.(\w+), (-?\d+), ExceptionHandlerType\.(\w+), (-?\d+), ExceptionHandlerType\.(\w+)(?:, (-?\d+), ExceptionHandlerType\.(\w+))?\)");
            var verifyMatch = Regex.Match(flat, @"ILValidator_Privates\.Verify(\w+)\(body\)");
            var isValidAssert = Regex.Match(flat, @"Assert\.Is(True|False)\(result\.IsValid");
            var suffixAssert = Regex.Match(flat, @"StringAssert\.EndsWith\(result\.ErrorMessages\[0\], ""((?:[^""\\]|\\.)*)""\)");

            if (!addMatch.Success || !verifyMatch.Success || !isValidAssert.Success)
            {
                irregular.Add((suiteFile, name, "unrecognized AddTargetAndSource/verify/assert shape"));
                continue;
            }

            var expectedValid = isValidAssert.Groups[1].Value == "True";
            string suffix = null;
            if (suffixAssert.Success)
            {
                if (expectedValid)
                {
                    irregular.Add((suiteFile, name, "pass case carries an error-suffix assert"));
                    continue;
                }
                suffix = suffixAssert.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
            }
            else if (!expectedValid)
            {
                irregular.Add((suiteFile, name, "fail case without an error-suffix assert"));
                continue;
            }

            into.Add(new BranchesGolden(
                suiteFile, name, verifyMatch.Groups[1].Value,
                addMatch.Groups[1].Value,
                int.Parse(addMatch.Groups[2].Value), addMatch.Groups[3].Value,
                int.Parse(addMatch.Groups[4].Value), addMatch.Groups[5].Value,
                addMatch.Groups[6].Success ? int.Parse(addMatch.Groups[6].Value) : -1,
                addMatch.Groups[7].Success ? addMatch.Groups[7].Value : "Finally",
                expectedValid, suffix));
        }
    }

    private static void ParseProtectedBlocksSuite(string corpusRoot,
        List<ProtectedBlocksGolden> into, List<(string, string, string)> irregular, List<string> noOps)
    {
        const string suiteFile = "ProtectedBlocksTest.cs";
        var text = File.ReadAllText(Path.Combine(corpusRoot, "src", "NetPerf.Tests.Unit", suiteFile));
        foreach (var (name, body) in MethodBodies(text))
        {
            if (name == "ProtectedBlockHelper" || name == "DualRegionTestHelper" || name == "AddProtectedBlock" || name == "GetMethod")
                continue; // helper definitions, not goldens (pinned verbatim in GoldenBodyBuilders)

            var flat = Regex.Replace(body, @"\s+", " ");
            var verifyMatch = Regex.Match(flat, @"ILValidator_Privates\.Verify(\w+)\(body\)");
            var isValidAssert = Regex.Match(flat, @"Assert\.Is(True|False)\(result\.IsValid");
            var startsAssert = Regex.Match(flat, @"StringAssert\.StartsWith\(result\.ErrorMessages\[0\], ""((?:[^""\\]|\\.)*)""\)");
            var endsAssert = Regex.Match(flat, @"StringAssert\.EndsWith\(result\.ErrorMessages\[0\], ""((?:[^""\\]|\\.)*)""\)");
            string Unescape(Match m) => m.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");

            // Direct-construction form: one or more AddProtectedBlock calls, verdict from asserts.
            var direct = Regex.Matches(flat,
                @"AddProtectedBlock\(body, ExceptionHandlerType\.(\w+)((?:, \w+: -?\d+)*?)\)");
            if (direct.Count > 0 && verifyMatch.Success && isValidAssert.Success)
            {
                var blocks = direct.Select(ParseBlock).ToList();
                into.Add(new ProtectedBlocksGolden(name, verifyMatch.Groups[1].Value,
                    isValidAssert.Groups[1].Value == "True",
                    startsAssert.Success ? Unescape(startsAssert) : null,
                    endsAssert.Success ? Unescape(endsAssert) : null,
                    blocks, []));
                continue;
            }

            // Helper-mediated forms: the era helpers fix the expectations for failures.
            var single = Regex.Match(flat,
                @"ProtectedBlockHelper\((true|false), ExceptionHandlerType\.(\w+)((?:, \w+: -?\d+)*?)\)");
            if (single.Success)
            {
                var expectedValid = single.Groups[1].Value == "true";
                into.Add(new ProtectedBlocksGolden(name, "ProtectedBlocks", expectedValid,
                    expectedValid ? null : "The protected block",
                    expectedValid ? null : "regions are not pairwise disjoint.",
                    // The era ProtectedBlockHelper constructs a Filter handler
                    // regardless of its handlerKind argument; mirror that.
                    [ParseBlock("Filter", single.Groups[3].Value)], []));
                continue;
            }

            var dual = Regex.Match(flat,
                @"DualRegionTestHelper\((true|false), ExceptionHandlerType\.(\w+), (-?\d+), (-?\d+), (-?\d+), (-?\d+), (-?\d+), (-?\d+), ExceptionHandlerType\.(\w+), (-?\d+), (-?\d+), (-?\d+), (-?\d+), (-?\d+), (-?\d+)\)");
            if (dual.Success)
            {
                var expectedValid = dual.Groups[1].Value == "true";
                int G(int i) => int.Parse(dual.Groups[i].Value);
                into.Add(new ProtectedBlocksGolden(name, "ProtectedBlocks", expectedValid,
                    expectedValid ? null : "The exception entry pair",
                    expectedValid ? null : "neither nest, nor are disjoint, nor mutually protect.",
                    [
                        new ProtectedBlockSpec(dual.Groups[2].Value, G(3), G(4), G(5), G(6), G(7), G(8)),
                        new ProtectedBlockSpec(dual.Groups[9].Value, G(10), G(11), G(12), G(13), G(14), G(15)),
                    ], []));
                continue;
            }

            // Hand-authored inline form (two era tests): a single literal
            // ExceptionHandler plus explicit opcode writes, e.g. placing endfilter
            // inside the filter region.
            var inline = Regex.Match(flat,
                @"new ExceptionHandler\(ExceptionHandlerType\.(\w+)\) \{ TryStart = body\.Instructions\[(\d+)\], TryEnd = body\.Instructions\[(\d+)\], FilterStart = body\.Instructions\[(\d+)\], HandlerStart = body\.Instructions\[(\d+)\], HandlerEnd = body\.Instructions\[(\d+)\] \}");
            var opcodeWrites = Regex.Matches(flat, @"body\.Instructions\[(\d+)\]\.OpCode = OpCodes\.(\w+)")
                .Select(match => (int.Parse(match.Groups[1].Value), match.Groups[2].Value))
                .ToList();
            if (inline.Success && opcodeWrites.Count > 0 && verifyMatch.Success && isValidAssert.Success)
            {
                int I(int group) => int.Parse(inline.Groups[group].Value);
                into.Add(new ProtectedBlocksGolden(name, verifyMatch.Groups[1].Value,
                    isValidAssert.Groups[1].Value == "True",
                    startsAssert.Success ? Unescape(startsAssert) : null,
                    endsAssert.Success ? Unescape(endsAssert) : null,
                    [new ProtectedBlockSpec(inline.Groups[1].Value, I(2), I(3), I(4), -1, I(5), I(6))],
                    opcodeWrites));
                continue;
            }

            // A handful of era tests are intentionally empty (their bodies carry
            // only a comment explaining the case is covered elsewhere): counted
            // as no-ops, never replayed, never silently dropped.
            var onlyComments = body.Split('\n').All(line => string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//", StringComparison.Ordinal));
            if (onlyComments)
            {
                noOps.Add($"{suiteFile}|{name}");
                continue;
            }

            irregular.Add((suiteFile, name, "unrecognized protected-blocks construction"));
        }
    }

    private static ProtectedBlockSpec ParseBlock(Match addMatch) =>
        ParseBlock(addMatch.Groups[1].Value, addMatch.Groups[2].Value);

    private static ProtectedBlockSpec ParseBlock(string kind, string namedArgs)
    {
        var values = new Dictionary<string, int>();
        foreach (Match arg in Regex.Matches(namedArgs, @"(\w+): (-?\d+)"))
            values[arg.Groups[1].Value] = int.Parse(arg.Groups[2].Value);
        int Get(string name) => values.TryGetValue(name, out var value) ? value : -1;
        return new ProtectedBlockSpec(kind, Get("tryStart"), Get("tryEnd"), Get("filterStart"), Get("filterEnd"), Get("handlerStart"), Get("handlerEnd"));
    }

    // Generated test methods open with "public void Name() {" and close at the
    // first line that is exactly three tabs and a closing brace.
    private static IEnumerable<(string Name, string Body)> MethodBodies(string text)
    {
        const string openPrefix = "\t\t\tpublic void ";
        string currentName = null;
        var body = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            var clean = line.TrimEnd('\r');
            if (currentName is null)
            {
                if (clean.StartsWith(openPrefix, StringComparison.Ordinal) && clean.EndsWith("() {", StringComparison.Ordinal))
                    currentName = clean.Substring(openPrefix.Length, clean.Length - openPrefix.Length - "() {".Length);
                continue;
            }
            if (clean == "\t\t\t}")
            {
                yield return (currentName, string.Join('\n', body));
                currentName = null;
                body.Clear();
                continue;
            }
            body.Add(clean);
        }
    }
}

// Replays a parsed golden through the era construction helpers and the matching
// private era verifier, reached by reflection exactly the way the corpus's own
// ILValidator_Privates wrappers did.
internal static class GoldenReplay
{
    private static readonly Dictionary<string, OpCode> Opcodes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .ToDictionary(field => field.Name, field => (OpCode)field.GetValue(null)!);

    private static readonly Dictionary<string, MethodInfo> Verifiers = typeof(ILValidator)
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Where(method => method.GetParameters().Length == 1
            && method.GetParameters()[0].ParameterType == typeof(Mono.Cecil.Cil.MethodBody))
        .ToDictionary(method => method.Name, method => method);

    private static readonly Dictionary<string, ExceptionHandlerType> HandlerKinds = Enum.GetValues<ExceptionHandlerType>()
        .Cast<ExceptionHandlerType>()
        .Select(kind => (Name: kind.ToString(), Value: kind))
        .ToDictionary(pair => pair.Name, pair => pair.Value);

    public static Mono.Cecil.Cil.MethodBody BuildBody(BranchesGolden golden, MethodDefinition host = null)
    {
        var body = GoldenBodyBuilders.SourceTestMethod(host);
        GoldenBodyBuilders.AddTargetAndSource(body,
            Opcodes[golden.SourceOpcode],
            golden.SourceIndex, HandlerKinds[golden.SourceKind],
            golden.TargetIndex, HandlerKinds[golden.TargetKind],
            golden.Target2Index, HandlerKinds[golden.Target2Kind]);
        return body;
    }

    public static ILValidator.VerifyResult Replay(BranchesGolden golden) =>
        Invoke(golden.Verify, BuildBody(golden));

    public static ILValidator.VerifyResult Replay(ProtectedBlocksGolden golden)
    {
        var body = GoldenBodyBuilders.ProtectedBlocksMethod(); // replay stays on the detached prototype host
        foreach (var block in golden.Blocks)
            GoldenBodyBuilders.AddProtectedBlock(body, HandlerKinds[block.Kind],
                block.TryStart, block.TryEnd, block.FilterStart, block.FilterEnd, block.HandlerStart, block.HandlerEnd);
        foreach (var (index, opcode) in golden.OpcodeWrites)
            body.Instructions[index].OpCode = Opcodes[opcode];
        return Invoke(golden.Verify, body);
    }

    private static ILValidator.VerifyResult Invoke(string verify, Mono.Cecil.Cil.MethodBody body) =>
        (ILValidator.VerifyResult)Verifiers["Verify" + verify].Invoke(null, [body])!;
}
