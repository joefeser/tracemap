using System.Security.Cryptography;
using Xunit.Sdk;

namespace TraceMap.Tests;

// #856 stage 2b: the era sources in src/dotnet/tests/TraceMap.Oracle/Adapted/ are
// adapted copies of the byte-pinned dotnetperf corpus. This pin makes adaptation
// drift impossible: each adapted file is diffed (order-sensitive LCS over
// CR/BOM/trailing-whitespace-normalized lines) against its corpus source, and the
// differing lines must be exactly the pinned sets below. A new adaptation, a
// reverted adaptation, or a re-vendored corpus (SHA-256 pinned in the other test)
// all fail here until the sets are consciously regenerated.
//
// Adaptation classes currently pinned (details inline at each site in the
// adapted files):
//   ILInstrumenter — SeqPointBridge (modern Cecil moved sequence points to
//     MethodDefinition.DebugInformation); in-memory assembly reads + Immediate
//     reading mode (same-path writes corrupt output patched from the read
//     stream in modern Cecil); symbols written only when symbols were read;
//     generic Contract.Requires/Ensures rewritten to plain checks (the Code
//     Contracts rewriter is dead tooling; the generic forms throw at runtime on
//     modern .NET); era collection extensions inlined.
//   ILValidator — FilterEnd removed from modern Cecil; the filter region is
//     [FilterStart, HandlerStart), so FilterEnd translates to HandlerStart while
//     the era "has a filter" nullability is carried by FilterStart; struct
//     fossil: `new VerifyResult()` on an all-optional-params ctor is spelled
//     `new VerifyResult(null)` so the ctor always runs.
public sealed class OracleAdaptationPinTests
{
    private sealed record AdaptedFilePin(
        string CorpusRelativePath,
        string AdaptedRelativePath,
        string CorpusSha256,
        string[] CorpusOnlyLines,
        string[] AdaptedOnlyLines);

    private static readonly AdaptedFilePin[] Pins =
    [
        new AdaptedFilePin(
            "src/NetPerf/ILInstrumenter.cs",
            "tests/TraceMap.Oracle/Adapted/ILInstrumenter.cs",
            "17233629e5de39fb043eb7aed87388df65d8283240df573416d84becc880363e",
            [
                "\t\t\tContract.Requires<ArgumentNullException>(assembly != null, \"assembly\");",
                "\t\t\tContract.Requires<ArgumentException>(assemblySymbols == null || assemblySymbols.IsAbsolute);",
                "\t\t\t\tvar assemDef = AssemblyDefinition.ReadAssembly(assembly.FullPath, readParams);",
                "\t\t\tContract.Requires<ArgumentNullException>(assembly != null, \"assembly\");",
                "\t\t\tContract.Requires<ArgumentNullException>(factory != null, \"factory\");",
                "\t\t\tContract.Requires<ArgumentNullException>(factory.MessageController != null, \"factory.MessageController\");",
                "\t\t\tContract.Requires<ArgumentException>(assembly.IsAbsolute);",
                "\t\t\tContract.Requires<ArgumentException>(assemblySymbols == null || assemblySymbols.IsAbsolute);",
                "\t\t\tContract.Requires<ArgumentException>(keyFile == null || keyFile.IsAbsolute);",
                "\t\t\t\tvar assemDef = AssemblyDefinition.ReadAssembly(assembly.FullPath, readParams);",
                "\t\t\tvar readParams = new ReaderParameters(ReadingMode.Deferred) {",
                "\t\t\t\tSymbolStream = new MemoryStream(),",
                "\t\t\t\tSymbolWriterProvider = new Mono.Cecil.Pdb.PdbWriterProvider()",
                "\t\t\t\t\t.Where(i => i.SequencePoint != null)",
                "\t\t\t\t\t.Select(i => i.SequencePoint.Document)",
                "\t\t\tif (!def.HasBody || def.Body.Instructions.IsEmpty()) {",
                "\t\t\t\t\t\treturn utf8.GetBytes(((VariableReference)instr.Operand).Name);",
                "\t\t\t\tif (instrController.WantsSeqPointEndMessages && ShouldInstrumentSequencePoint(instr)) {",
                "\t\t\t\t\tvar point = instr.SequencePoint;",
                "\t\t\t\t\tif (instr.SequencePoint != null) {",
                "\t\t\t\t\t\tvar point = instr.SequencePoint;",
                "\t\t\t\t\t\t\tShouldInstrumentSequencePoint(instr) ? \"(instrument)\" : \"(don't instrument)\");",
                "\t\tprivate static bool ShouldInstrumentSequencePoint(Instruction current) {",
                "\t\t\tif (current == null || current.SequencePoint == null)",
                "\t\t\tif (current.SequencePoint.StartLine == 0xFEEFEE)",
                "\t\t\t\tContract.Requires<ArgumentNullException>(module != null, \"moduleToAddReferencesTo\");",
            ],
            [
                "// ADAPTED COPY of samples/dotnetperf-rewriter/src/NetPerf/ILInstrumenter.cs at",
                "// pinned corpus SHA db8c3359 (PROVENANCE.md). Adaptations for modern Mono.Cecil",
                "// are enumerated and pinned by OracleAdaptationPinTests; the corpus tree itself",
                "// is never modified.",
                "\t// #856 stage 2b ADAPTATION of the byte-pinned corpus source (see",
                "\t// OracleAdaptationPinTests for the exact diff pin): modern Cecil moved",
                "\t// sequence points from Instruction to MethodDefinition.DebugInformation;",
                "\t// this bridge routes the era reads.",
                "\tinternal static class SeqPointBridge",
                "\t{",
                "\t\tinternal static SequencePoint SeqPoint(MethodBody body, Instruction instruction)",
                "\t\t\t=> body.Method.DebugInformation.GetSequencePoint(instruction);",
                "\t}",
                "",
                "\t\t\tif (!(assembly != null)) throw new ArgumentNullException(\"assembly\");",
                "\t\t\tif (!(assemblySymbols == null || assemblySymbols.IsAbsolute)) throw new ArgumentException(nameof(ArgumentException));",
                "\t\t\t\tvar assemDef = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(assembly.FullPath)), readParams); // ADAPTATION: in-memory read; modern Cecil patches bodies from the stream during same-path writes",
                "\t\t\tif (!(assembly != null)) throw new ArgumentNullException(\"assembly\");",
                "\t\t\tif (!(factory != null)) throw new ArgumentNullException(\"factory\");",
                "\t\t\tif (!(factory.MessageController != null)) throw new ArgumentNullException(\"factory.MessageController\");",
                "\t\t\tif (!(assembly.IsAbsolute)) throw new ArgumentException(nameof(ArgumentException));",
                "\t\t\tif (!(assemblySymbols == null || assemblySymbols.IsAbsolute)) throw new ArgumentException(nameof(ArgumentException));",
                "\t\t\tif (!(keyFile == null || keyFile.IsAbsolute)) throw new ArgumentException(nameof(ArgumentException));",
                "\t\t\t\tvar assemDef = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(assembly.FullPath)), readParams); // ADAPTATION: in-memory read; modern Cecil patches bodies from the stream during same-path writes",
                "\t\t\tvar readParams = new ReaderParameters(ReadingMode.Immediate) { // ADAPTATION: deferred + same-path write corrupts output in modern Cecil",
                "\t\t\t// ADAPTATION: era behavior set a PdbWriterProvider unconditionally, which",
                "\t\t\t// corrupts output when no symbols were read (oracle path passes none).",
                "\t\t\t\tSymbolStream = symbolsAvailable ? new MemoryStream() : null,",
                "\t\t\t\tSymbolWriterProvider = symbolsAvailable ? new Mono.Cecil.Pdb.PdbWriterProvider() : null",
                "\t\t\t\t\t.Where(i => SeqPointBridge.SeqPoint(method.Body, i) != null)",
                "\t\t\t\t\t.Select(i => SeqPointBridge.SeqPoint(method.Body, i).Document)",
                "\t\t\tif (!def.HasBody || def.Body.Instructions.Count == 0) {",
                "\t\t\t\t\t\treturn utf8.GetBytes(((VariableDefinition)instr.Operand).Index.ToString(System.Globalization.CultureInfo.InvariantCulture));",
                "\t\t\t\tif (instrController.WantsSeqPointEndMessages && ShouldInstrumentSequencePoint(body, instr)) {",
                "\t\t\t\t\tvar point = SeqPointBridge.SeqPoint(body, instr);",
                "\t\t\t\t\tif (SeqPointBridge.SeqPoint(body, instr) != null) {",
                "\t\t\t\t\t\tvar point = SeqPointBridge.SeqPoint(body, instr);",
                "\t\t\t\t\t\t\tShouldInstrumentSequencePoint(body, instr) ? \"(instrument)\" : \"(don't instrument)\");",
                "\t\tprivate static bool ShouldInstrumentSequencePoint(MethodBody body, Instruction current) {",
                "\t\t\tif (current == null || SeqPointBridge.SeqPoint(body, current) == null)",
                "\t\t\tif (SeqPointBridge.SeqPoint(body, current).StartLine == 0xFEEFEE)",
                "\t\t\t\tif (!(module != null)) throw new ArgumentNullException(\"moduleToAddReferencesTo\");",
            ]
        ),
        new AdaptedFilePin(
            "src/NetPerf/ILValidator.cs",
            "tests/TraceMap.Oracle/Adapted/ILValidator.cs",
            "50f83bfa0801161bb50d66630af9f7a6c4fe11c1bae023a80701e0a12b9e4371",
            [
                "\t\t\treturn new VerifyResult();",
                "\t\t\t\t\tif (handler.HandlerType == ExceptionHandlerType.Filter && IsInstructionBetween(instr, handler.FilterStart, handler.FilterEnd.Next)) {",
                "\t\t\t\t\t\t\thandler.FilterEnd.Offset));",
                "\t\t\t\t\t\t&& IsInstructionBetween(instr, handler.FilterStart, handler.FilterEnd)",
                "\t\t\t\t\t\tif (handler.FilterEnd.Previous != instr) {",
                "\t\t\t\t\t\t\t\thandler.FilterEnd.Offset));",
                "\t\t\t\t\t\t\t\t\t && IsInstructionBetween(instr, handler.FilterStart, handler.FilterEnd),",
                "\t\t\t\t\t\t\t\t end = handler.FilterEnd,",
                "\t\t\t\t\t\t\t\t\t\t&& IsInstructionBetween(source, handler.FilterStart, handler.FilterEnd)",
                "\t\t\t\t\t\t\t\t\t\t&& !IsInstructionBetween(target, handler.FilterStart, handler.FilterEnd))",
                "\t\t\t\t\t\t&& IsInstructionBetween(target, handler.FilterStart, handler.FilterEnd)",
                "\t\t\t\t\t\t&& !IsInstructionBetween(source, handler.FilterStart, handler.FilterEnd)) {",
                "\t\t\t\t\t\t\t handler.FilterEnd.Offset,",
                "\t\t\t\treturn new VerifyResult();",
                "\t\t\t\t\t  where h.FilterEnd != null && h.FilterEnd != h.HandlerStart",
                "\t\t\t\t\t  where h.FilterEnd != null",
                "\t\t\t\t\t\t&& h.FilterEnd.Previous.OpCode != OpCodes.Endfilter",
                "\t\t\t\t\thandler.FilterEnd.Offset));",
                "\t\t\t\t\tPreviousInstr(entry.FilterEnd),",
                "\t\t\t\t\t\tentry.FilterEnd == null ? (int?)null : entry.FilterEnd.Offset,",
                "\t\t\t\t\t\t\t\t\tPreviousInstr(left.FilterEnd),",
                "\t\t\t\t\t\t\t\t\tPreviousInstr(right.FilterEnd),",
                "\t\t\t\t\t\t\t\tPreviousInstr(left.FilterEnd),",
                "\t\t\t\t\t\t\t\tPreviousInstr(right.FilterEnd),",
                "\t\t\t\t\t\t&& (h.FilterStart != null || h.FilterEnd != null)",
                "\t\t\t\t.SelectMany(eh => new[] { eh.TryEnd, eh.FilterEnd, eh.HandlerEnd });",
                "\t\t\t\t.Where(instr => !InstrsThatDontFallThrough.Contains(i => i == instr.OpCode));",
                "\t\t\t\t\tnew { instr=i, name=\"filter\", start=eh.FilterStart, end = eh.FilterEnd, targets=getInstrTargets(i)},",
                "\t\t\t\t\tnew { instr=i, name=\"filter\", start=eh.FilterStart, end = eh.FilterEnd, targets=getInstrTargets(i)},",
                "\t\t\t\tnew { isFilter=true, start=nullableOffset(left.FilterStart), end=nullableOffset(left.FilterEnd)},",
                "\t\t\t\tnew { isFilter=true, start=nullableOffset(right.FilterStart), end=nullableOffset(right.FilterEnd)},",
            ],
            [
                "// ADAPTED COPY of samples/dotnetperf-rewriter/src/NetPerf/ILValidator.cs at",
                "// pinned corpus SHA db8c3359 (PROVENANCE.md). Adaptations for modern Mono.Cecil",
                "// (FilterEnd removal, guard nullability) are enumerated and pinned by",
                "// OracleAdaptationPinTests; the corpus tree itself is never modified.",
                "\t\t\treturn new VerifyResult(null);",
                "\t\t\t\t\tif (handler.HandlerType == ExceptionHandlerType.Filter && IsInstructionBetween(instr, handler.FilterStart, handler.HandlerStart.Next)) {",
                "\t\t\t\t\t\t\thandler.HandlerStart.Offset));",
                "\t\t\t\t\t\t&& IsInstructionBetween(instr, handler.FilterStart, handler.HandlerStart)",
                "\t\t\t\t\t\tif (handler.HandlerStart.Previous != instr) {",
                "\t\t\t\t\t\t\t\thandler.HandlerStart.Offset));",
                "\t\t\t\t\t\t\t\t\t && IsInstructionBetween(instr, handler.FilterStart, handler.HandlerStart),",
                "\t\t\t\t\t\t\t\t end = handler.HandlerStart,",
                "\t\t\t\t\t\t\t\t\t\t&& IsInstructionBetween(source, handler.FilterStart, handler.HandlerStart)",
                "\t\t\t\t\t\t\t\t\t\t&& !IsInstructionBetween(target, handler.FilterStart, handler.HandlerStart))",
                "\t\t\t\t\t\t&& IsInstructionBetween(target, handler.FilterStart, handler.HandlerStart)",
                "\t\t\t\t\t\t&& !IsInstructionBetween(source, handler.FilterStart, handler.HandlerStart)) {",
                "\t\t\t\t\t\t\t handler.HandlerStart.Offset,",
                "\t\t\t\treturn new VerifyResult(null);",
                "\t\t\t// ADAPTATION (#856): the era guard FilterEnd != HandlerStart checked that the",
                "\t\t\t// parsed filter region ends exactly where the handler begins. Modern Cecil has",
                "\t\t\t// no FilterEnd — the filter region is defined as [FilterStart, HandlerStart) —",
                "\t\t\t// so the non-adjacency condition is structurally false and this rule cannot fire.",
                "\t\t\t\t\t  where false",
                "\t\t\t// ADAPTATION (#856): the era guard FilterEnd != null meant \"this entry has a",
                "\t\t\t// filter\"; mechanically rewriting it to HandlerStart != null would select",
                "\t\t\t// every handler and then dereference a null FilterStart in the error path.",
                "\t\t\t// FilterStart != null is the modern-Cecil expression of \"has a filter\", and",
                "\t\t\t// the filter's last instruction is HandlerStart.Previous.",
                "\t\t\t\t\t  where h.FilterStart != null",
                "\t\t\t\t\t\t&& h.HandlerStart.Previous?.OpCode != OpCodes.Endfilter",
                "\t\t\t\t\thandler.HandlerStart.Offset));",
                "\t\t\t// ADAPTATION (#856): era PreviousInstr(FilterEnd) yielded a complete null pair for",
                "\t\t\t// entries without a filter (PreviousInstr(null) == null); the modern filter end is",
                "\t\t\t// HandlerStart, which is never null, so the has-a-filter nullability must be carried",
                "\t\t\t// by FilterStart to keep the \"no filter\" region a complete null pair.",
                "\t\t\t\t\tPreviousInstr(entry.FilterStart == null ? null : entry.HandlerStart),",
                "\t\t\t\t\t\tentry.HandlerStart == null ? (int?)null : entry.HandlerStart.Offset,",
                "\t\t\t\t\t\t\t\t\tPreviousInstr(left.FilterStart == null ? null : left.HandlerStart),",
                "\t\t\t\t\t\t\t\t\tPreviousInstr(right.FilterStart == null ? null : right.HandlerStart),",
                "\t\t\t\t\t\t\t\tPreviousInstr(left.FilterStart == null ? null : left.HandlerStart),",
                "\t\t\t\t\t\t\t\tPreviousInstr(right.FilterStart == null ? null : right.HandlerStart),",
                "\t\t\t// ADAPTATION (#856): the era condition FilterStart != null || FilterEnd != null",
                "\t\t\t// meant \"has a filter\" twice; mechanically rewriting the second arm to",
                "\t\t\t// HandlerStart != null would mark every handler as having a filter. Both era",
                "\t\t\t// arms collapse to FilterStart != null on modern Cecil.",
                "\t\t\t\t\t\t&& h.FilterStart != null",
                "\t\t\t\t.SelectMany(eh => new[] { eh.TryEnd, eh.HandlerStart, eh.HandlerEnd });",
                "\t\t\t\t.Where(instr => !InstrsThatDontFallThrough.Any(i => i == instr.OpCode));",
                "\t\t\t\t\tnew { instr=i, name=\"filter\", start=eh.FilterStart, end = eh.HandlerStart, targets=getInstrTargets(i)},",
                "\t\t\t\t\tnew { instr=i, name=\"filter\", start=eh.FilterStart, end = eh.HandlerStart, targets=getInstrTargets(i)},",
                "\t\t\t\tnew { isFilter=true, start=nullableOffset(left.FilterStart), end=nullableOffset(left.FilterStart == null ? null : left.HandlerStart)},",
                "\t\t\t\tnew { isFilter=true, start=nullableOffset(right.FilterStart), end=nullableOffset(right.FilterStart == null ? null : right.HandlerStart)},",
            ]
        )
    ];

    [Fact]
    public void Adapted_files_differ_from_the_corpus_by_exactly_the_pinned_adaptation_set()
    {
        foreach (var pin in Pins)
        {
            var corpus = ReadNormalizedLines(Path.Combine(CorpusRoot(), pin.CorpusRelativePath));
            var adapted = ReadNormalizedLines(Path.Combine(RepoRoot(), pin.AdaptedRelativePath));
            var (corpusOnly, adaptedOnly) = DiffLines(corpus, adapted);

            if (!corpusOnly.SequenceEqual(pin.CorpusOnlyLines) || !adaptedOnly.SequenceEqual(pin.AdaptedOnlyLines))
                throw new XunitException(
                    $"{pin.AdaptedRelativePath} no longer differs from the corpus by exactly the pinned adaptation set. " +
                    "Regenerate the pin below (and update the adaptation-class docs in this file and the adapted-file headers) " +
                    $"only for an intended adaptation:\n\nCorpusOnlyLines =\n{FormatAsArrayLiteral(corpusOnly)}\nAdaptedOnlyLines =\n{FormatAsArrayLiteral(adaptedOnly)}");
        }
    }

    [Fact]
    public void Pinned_corpus_sources_match_their_sha256()
    {
        foreach (var pin in Pins)
        {
            var hash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(Path.Combine(CorpusRoot(), pin.CorpusRelativePath)))).ToLowerInvariant();
            Assert.Equal(pin.CorpusSha256, hash);
        }
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TraceMap.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("repository root unavailable");
    }

    private static string CorpusRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "samples", "dotnetperf-rewriter")))
                return Path.Combine(directory.FullName, "samples", "dotnetperf-rewriter");
        throw new DirectoryNotFoundException("dotnetperf corpus unavailable");
    }

    private static string[] ReadNormalizedLines(string path) =>
        File.ReadAllLines(path).Select(line => line.Replace("\ufeff", "").TrimEnd()).ToArray();

    // Order-sensitive line diff: an LCS walk emitting corpus-only lines as
    // Removed and adapted-only lines as Added, both in document order, so the
    // pinned arrays read like the era diff itself.
    private static (List<string> Removed, List<string> Added) DiffLines(string[] corpus, string[] adapted)
    {
        var suffix = new int[corpus.Length + 1, adapted.Length + 1];
        for (var i = corpus.Length - 1; i >= 0; i--)
            for (var j = adapted.Length - 1; j >= 0; j--)
                suffix[i, j] = corpus[i] == adapted[j]
                    ? suffix[i + 1, j + 1] + 1
                    : Math.Max(suffix[i + 1, j], suffix[i, j + 1]);

        var removed = new List<string>();
        var added = new List<string>();
        int a = 0, b = 0;
        while (a < corpus.Length && b < adapted.Length)
        {
            if (corpus[a] == adapted[b])
            {
                a++;
                b++;
            }
            else if (suffix[a + 1, b] >= suffix[a, b + 1])
            {
                removed.Add(corpus[a]);
                a++;
            }
            else
            {
                added.Add(adapted[b]);
                b++;
            }
        }

        while (a < corpus.Length) removed.Add(corpus[a++]);
        while (b < adapted.Length) added.Add(adapted[b++]);
        return (removed, added);
    }

    private static string FormatAsArrayLiteral(IReadOnlyList<string> lines) =>
        string.Join(Environment.NewLine, lines.Select(line => $"\t\t\t\"{line.Replace("\\", "\\\\").Replace("\"", "\\\"")}\","));
}
