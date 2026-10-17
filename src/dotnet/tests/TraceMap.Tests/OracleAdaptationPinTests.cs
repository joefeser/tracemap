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
                "32|			Contract.Requires<ArgumentNullException>(assembly != null, \"assembly\");",
                "33|			Contract.Requires<ArgumentException>(assemblySymbols == null || assemblySymbols.IsAbsolute);",
                "36|				var assemDef = AssemblyDefinition.ReadAssembly(assembly.FullPath, readParams);",
                "61|			Contract.Requires<ArgumentNullException>(assembly != null, \"assembly\");",
                "62|			Contract.Requires<ArgumentNullException>(factory != null, \"factory\");",
                "63|			Contract.Requires<ArgumentNullException>(factory.MessageController != null, \"factory.MessageController\");",
                "64|			Contract.Requires<ArgumentException>(assembly.IsAbsolute);",
                "65|			Contract.Requires<ArgumentException>(assemblySymbols == null || assemblySymbols.IsAbsolute);",
                "66|			Contract.Requires<ArgumentException>(keyFile == null || keyFile.IsAbsolute);",
                "75|				var assemDef = AssemblyDefinition.ReadAssembly(assembly.FullPath, readParams);",
                "131|			var readParams = new ReaderParameters(ReadingMode.Deferred) {",
                "143|				SymbolStream = new MemoryStream(),",
                "145|				SymbolWriterProvider = new Mono.Cecil.Pdb.PdbWriterProvider()",
                "354|					.Where(i => i.SequencePoint != null)",
                "355|					.Select(i => i.SequencePoint.Document)",
                "378|			if (!def.HasBody || def.Body.Instructions.IsEmpty()) {",
                "426|						return utf8.GetBytes(((VariableReference)instr.Operand).Name);",
                "534|				if (instrController.WantsSeqPointEndMessages && ShouldInstrumentSequencePoint(instr)) {",
                "536|					var point = instr.SequencePoint;",
                "736|					if (instr.SequencePoint != null) {",
                "737|						var point = instr.SequencePoint;",
                "745|							ShouldInstrumentSequencePoint(instr) ? \"(instrument)\" : \"(don't instrument)\");",
                "758|		private static bool ShouldInstrumentSequencePoint(Instruction current) {",
                "763|			if (current == null || current.SequencePoint == null)",
                "769|			if (current.SequencePoint.StartLine == 0xFEEFEE)",
                "860|				Contract.Requires<ArgumentNullException>(module != null, \"moduleToAddReferencesTo\");",
            ],
            [
                "1|// ADAPTED COPY of samples/dotnetperf-rewriter/src/NetPerf/ILInstrumenter.cs at",
                "2|// pinned corpus SHA db8c3359 (PROVENANCE.md). Adaptations for modern Mono.Cecil",
                "3|// are enumerated and pinned by OracleAdaptationPinTests; the corpus tree itself",
                "4|// is never modified.",
                "20|	// #856 stage 2b ADAPTATION of the byte-pinned corpus source (see",
                "21|	// OracleAdaptationPinTests for the exact diff pin): modern Cecil moved",
                "22|	// sequence points from Instruction to MethodDefinition.DebugInformation;",
                "23|	// this bridge routes the era reads.",
                "24|	internal static class SeqPointBridge",
                "25|	{",
                "26|		internal static SequencePoint SeqPoint(MethodBody body, Instruction instruction)",
                "27|			=> body.Method.DebugInformation.GetSequencePoint(instruction);",
                "28|	}",
                "30|",
                "46|			if (!(assembly != null)) throw new ArgumentNullException(\"assembly\");",
                "47|			if (!(assemblySymbols == null || assemblySymbols.IsAbsolute)) throw new ArgumentException(nameof(ArgumentException));",
                "50|				var assemDef = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(assembly.FullPath)), readParams); // ADAPTATION: in-memory read; modern Cecil patches bodies from the stream during same-path writes",
                "75|			if (!(assembly != null)) throw new ArgumentNullException(\"assembly\");",
                "76|			if (!(factory != null)) throw new ArgumentNullException(\"factory\");",
                "77|			if (!(factory.MessageController != null)) throw new ArgumentNullException(\"factory.MessageController\");",
                "78|			if (!(assembly.IsAbsolute)) throw new ArgumentException(nameof(ArgumentException));",
                "79|			if (!(assemblySymbols == null || assemblySymbols.IsAbsolute)) throw new ArgumentException(nameof(ArgumentException));",
                "80|			if (!(keyFile == null || keyFile.IsAbsolute)) throw new ArgumentException(nameof(ArgumentException));",
                "89|				var assemDef = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(assembly.FullPath)), readParams); // ADAPTATION: in-memory read; modern Cecil patches bodies from the stream during same-path writes",
                "145|			var readParams = new ReaderParameters(ReadingMode.Immediate) { // ADAPTATION: deferred + same-path write corrupts output in modern Cecil",
                "156|			// ADAPTATION: era behavior set a PdbWriterProvider unconditionally, which",
                "157|			// corrupts output when no symbols were read (oracle path passes none).",
                "159|				SymbolStream = symbolsAvailable ? new MemoryStream() : null,",
                "161|				SymbolWriterProvider = symbolsAvailable ? new Mono.Cecil.Pdb.PdbWriterProvider() : null",
                "370|					.Where(i => SeqPointBridge.SeqPoint(method.Body, i) != null)",
                "371|					.Select(i => SeqPointBridge.SeqPoint(method.Body, i).Document)",
                "394|			if (!def.HasBody || def.Body.Instructions.Count == 0) {",
                "442|						return utf8.GetBytes(((VariableDefinition)instr.Operand).Index.ToString(System.Globalization.CultureInfo.InvariantCulture));",
                "550|				if (instrController.WantsSeqPointEndMessages && ShouldInstrumentSequencePoint(body, instr)) {",
                "552|					var point = SeqPointBridge.SeqPoint(body, instr);",
                "752|					if (SeqPointBridge.SeqPoint(body, instr) != null) {",
                "753|						var point = SeqPointBridge.SeqPoint(body, instr);",
                "761|							ShouldInstrumentSequencePoint(body, instr) ? \"(instrument)\" : \"(don't instrument)\");",
                "774|		private static bool ShouldInstrumentSequencePoint(MethodBody body, Instruction current) {",
                "779|			if (current == null || SeqPointBridge.SeqPoint(body, current) == null)",
                "785|			if (SeqPointBridge.SeqPoint(body, current).StartLine == 0xFEEFEE)",
                "876|				if (!(module != null)) throw new ArgumentNullException(\"moduleToAddReferencesTo\");",
            ]
        ),
        new AdaptedFilePin(
            "src/NetPerf/ILValidator.cs",
            "tests/TraceMap.Oracle/Adapted/ILValidator.cs",
            "50f83bfa0801161bb50d66630af9f7a6c4fe11c1bae023a80701e0a12b9e4371",
            [
                "75|			return new VerifyResult();",
                "135|					if (handler.HandlerType == ExceptionHandlerType.Filter && IsInstructionBetween(instr, handler.FilterStart, handler.FilterEnd.Next)) {",
                "141|							handler.FilterEnd.Offset));",
                "175|						&& IsInstructionBetween(instr, handler.FilterStart, handler.FilterEnd)",
                "194|						if (handler.FilterEnd.Previous != instr) {",
                "199|								handler.FilterEnd.Offset));",
                "240|									 && IsInstructionBetween(instr, handler.FilterStart, handler.FilterEnd),",
                "242|								 end = handler.FilterEnd,",
                "302|										&& IsInstructionBetween(source, handler.FilterStart, handler.FilterEnd)",
                "303|										&& !IsInstructionBetween(target, handler.FilterStart, handler.FilterEnd))",
                "429|						&& IsInstructionBetween(target, handler.FilterStart, handler.FilterEnd)",
                "430|						&& !IsInstructionBetween(source, handler.FilterStart, handler.FilterEnd)) {",
                "437|							 handler.FilterEnd.Offset,",
                "498|				return new VerifyResult();",
                "543|					  where h.FilterEnd != null && h.FilterEnd != h.HandlerStart",
                "560|					  where h.FilterEnd != null",
                "561|						&& h.FilterEnd.Previous.OpCode != OpCodes.Endfilter",
                "567|					handler.FilterEnd.Offset));",
                "578|					PreviousInstr(entry.FilterEnd),",
                "590|						entry.FilterEnd == null ? (int?)null : entry.FilterEnd.Offset,",
                "632|									PreviousInstr(left.FilterEnd),",
                "638|									PreviousInstr(right.FilterEnd),",
                "653|								PreviousInstr(left.FilterEnd),",
                "657|								PreviousInstr(right.FilterEnd),",
                "678|						&& (h.FilterStart != null || h.FilterEnd != null)",
                "733|				.SelectMany(eh => new[] { eh.TryEnd, eh.FilterEnd, eh.HandlerEnd });",
                "742|				.Where(instr => !InstrsThatDontFallThrough.Contains(i => i == instr.OpCode));",
                "782|					new { instr=i, name=\"filter\", start=eh.FilterStart, end = eh.FilterEnd, targets=getInstrTargets(i)},",
                "835|					new { instr=i, name=\"filter\", start=eh.FilterStart, end = eh.FilterEnd, targets=getInstrTargets(i)},",
                "945|				new { isFilter=true, start=nullableOffset(left.FilterStart), end=nullableOffset(left.FilterEnd)},",
                "951|				new { isFilter=true, start=nullableOffset(right.FilterStart), end=nullableOffset(right.FilterEnd)},",
            ],
            [
                "1|// ADAPTED COPY of samples/dotnetperf-rewriter/src/NetPerf/ILValidator.cs at",
                "2|// pinned corpus SHA db8c3359 (PROVENANCE.md). Adaptations for modern Mono.Cecil",
                "3|// (FilterEnd removal, guard nullability) are enumerated and pinned by",
                "4|// OracleAdaptationPinTests; the corpus tree itself is never modified.",
                "79|			return new VerifyResult(null);",
                "139|					if (handler.HandlerType == ExceptionHandlerType.Filter && IsInstructionBetween(instr, handler.FilterStart, handler.HandlerStart.Next)) {",
                "145|							handler.HandlerStart.Offset));",
                "179|						&& IsInstructionBetween(instr, handler.FilterStart, handler.HandlerStart)",
                "198|						if (handler.HandlerStart.Previous != instr) {",
                "203|								handler.HandlerStart.Offset));",
                "244|									 && IsInstructionBetween(instr, handler.FilterStart, handler.HandlerStart),",
                "246|								 end = handler.HandlerStart,",
                "306|										&& IsInstructionBetween(source, handler.FilterStart, handler.HandlerStart)",
                "307|										&& !IsInstructionBetween(target, handler.FilterStart, handler.HandlerStart))",
                "433|						&& IsInstructionBetween(target, handler.FilterStart, handler.HandlerStart)",
                "434|						&& !IsInstructionBetween(source, handler.FilterStart, handler.HandlerStart)) {",
                "441|							 handler.HandlerStart.Offset,",
                "502|				return new VerifyResult(null);",
                "546|			// ADAPTATION (#856): the era guard FilterEnd != HandlerStart checked that the",
                "547|			// parsed filter region ends exactly where the handler begins. Modern Cecil has",
                "548|			// no FilterEnd — the filter region is defined as [FilterStart, HandlerStart) —",
                "549|			// so the non-adjacency condition is structurally false and this rule cannot fire.",
                "551|					  where false",
                "567|			// ADAPTATION (#856): the era guard FilterEnd != null meant \"this entry has a",
                "568|			// filter\"; mechanically rewriting it to HandlerStart != null would select",
                "569|			// every handler and then dereference a null FilterStart in the error path.",
                "570|			// FilterStart != null is the modern-Cecil expression of \"has a filter\", and",
                "571|			// the filter's last instruction is HandlerStart.Previous.",
                "573|					  where h.FilterStart != null",
                "574|						&& h.HandlerStart.Previous?.OpCode != OpCodes.Endfilter",
                "580|					handler.HandlerStart.Offset));",
                "585|			// ADAPTATION (#856): era PreviousInstr(FilterEnd) yielded a complete null pair for",
                "586|			// entries without a filter (PreviousInstr(null) == null); the modern filter end is",
                "587|			// HandlerStart, which is never null, so the has-a-filter nullability must be carried",
                "588|			// by FilterStart to keep the \"no filter\" region a complete null pair.",
                "595|					PreviousInstr(entry.FilterStart == null ? null : entry.HandlerStart),",
                "607|						entry.HandlerStart == null ? (int?)null : entry.HandlerStart.Offset,",
                "649|									PreviousInstr(left.FilterStart == null ? null : left.HandlerStart),",
                "655|									PreviousInstr(right.FilterStart == null ? null : right.HandlerStart),",
                "670|								PreviousInstr(left.FilterStart == null ? null : left.HandlerStart),",
                "674|								PreviousInstr(right.FilterStart == null ? null : right.HandlerStart),",
                "693|			// ADAPTATION (#856): the era condition FilterStart != null || FilterEnd != null",
                "694|			// meant \"has a filter\" twice; mechanically rewriting the second arm to",
                "695|			// HandlerStart != null would mark every handler as having a filter. Both era",
                "696|			// arms collapse to FilterStart != null on modern Cecil.",
                "699|						&& h.FilterStart != null",
                "754|				// ADAPTATION (#856): era FilterEnd was null for entries without a filter; the",
                "755|				// modern filter end is HandlerStart, which is never null, so the has-a-filter",
                "756|				// nullability is carried by FilterStart (a non-filter HandlerStart.Previous",
                "757|				// would be null when the handler begins the method).",
                "758|				.SelectMany(eh => new[] { eh.TryEnd, eh.FilterStart == null ? null : eh.HandlerStart, eh.HandlerEnd });",
                "767|				.Where(instr => !InstrsThatDontFallThrough.Any(i => i == instr.OpCode));",
                "807|					new { instr=i, name=\"filter\", start=eh.FilterStart, end = eh.HandlerStart, targets=getInstrTargets(i)},",
                "860|					new { instr=i, name=\"filter\", start=eh.FilterStart, end = eh.HandlerStart, targets=getInstrTargets(i)},",
                "970|				new { isFilter=true, start=nullableOffset(left.FilterStart), end=nullableOffset(left.FilterStart == null ? null : left.HandlerStart)},",
                "976|				new { isFilter=true, start=nullableOffset(right.FilterStart), end=nullableOffset(right.FilterStart == null ? null : right.HandlerStart)},",
            ]
        )
    ];


    // #856 stage 2c extraction pin: GoldenBodyBuilders.cs extracts method bodies
    // from corpus test sources rather than copying whole files. The pin keeps
    // extraction drift impossible two ways: every non-empty adapted line
    // (indentation-insensitive — the corpus nests its methods two classes deep)
    // must be verbatim corpus content or a pinned adaptation line, and every
    // line of the pinned corpus regions must still be present in the adapted
    // file, so a deleted extraction line fails loudly.
    private sealed record ExtractionSource(string CorpusRelativePath, string CorpusSha256, (int Start, int End)[] Regions);

    private sealed record ExtractionPin(string AdaptedRelativePath, ExtractionSource[] Sources, string[] AdaptedOnlyLines);

    private static readonly ExtractionPin[] ExtractionPins =
    [
        new ExtractionPin(
            "tests/TraceMap.Oracle/Adapted/GoldenBodyBuilders.cs",
            [
                new ExtractionSource(
                    "src/NetPerf.Tests.Unit/BranchesTest.cs",
                    "b5ac63dc45acaefa79bf017d54ce059d901f28b8d2344129aa4728dd59af594f",
                    [(34, 74)]),
                new ExtractionSource(
                    "src/NetPerf.Tests.Unit/ProtectedBlocksTest.cs",
                    "7c019a66358cd09d134585373715d1c3c8c788fa3ea60151fe5602e9f4fb8f8d",
                    [(82, 120)]),
            ],
            [
                "1|// ADAPTED EXTRACTION for #856 stage 2c. The construction helpers below are",
                "2|// extracted near-verbatim from the byte-pinned corpus sources:",
                "3|//   samples/dotnetperf-rewriter/src/NetPerf.Tests.Unit/BranchesTest.cs",
                "4|//     (GetSourceTestMethod, AddTargetAndSource)",
                "5|//   samples/dotnetperf-rewriter/src/NetPerf.Tests.Unit/ProtectedBlocksTest.cs",
                "6|//     (GetMethod, AddProtectedBlock)",
                "7|// OracleAdaptationPinTests pins the extraction: every corpus line of the",
                "8|// extracted regions must appear here verbatim, and every other line of this",
                "9|// file is a documented adaptation. The corpus tree itself is never modified.",
                "11|// Adaptation classes (everything not verbatim from the corpus):",
                "12|//   - Era prototypes came from ILHelper.GetAssembly(\"branches.il.txt\" /",
                "13|//     \"protectedblocks.il.txt\"), which ran ilasm.exe at test time; ilasm is not",
                "14|//     available (and is excluded from the corpus). The grids are rebuilt in",
                "15|//     pure Cecil mirroring the committed fixtures: SourceTestsBase = 25 nop +",
                "16|//     ret (branches.il.txt lines 35-64; the three ilasm labels carry no bytes),",
                "17|//     ProtectedBlocks_Base = 50 nop + ret (protectedblocks.il.txt from line 57).",
                "18|//   - Offsets are assigned positionally because the validator's region math",
                "19|//     reads Instruction.Offset and the era values came from the ilasm round",
                "20|//     trip (one-byte nops: instruction i sits at offset i).",
                "21|//   - The prototype builders live on this class instead of cached static",
                "22|//     _prototype fields with ILHelper.CloneMethod; each call builds a fresh",
                "23|//     body, which is what the era clone achieved.",
                "24|using Mono.Cecil;",
                "27|namespace NetPerf.Goldens;",
                "29|internal static class GoldenBodyBuilders",
                "31|private const int SourceTestsBaseInstructionCount = 26;",
                "32|private const int ProtectedBlocksBaseInstructionCount = 51;",
                "34|internal static MethodBody SourceTestMethod(MethodDefinition host = null) {",
                "35|return BuildPrototype(SourceTestsBaseInstructionCount, host);",
                "38|internal static void AddTargetAndSource(MethodBody body, OpCode sourceOp,",
                "81|internal static MethodBody ProtectedBlocksMethod(MethodDefinition host = null) {",
                "82|return BuildPrototype(ProtectedBlocksBaseInstructionCount, host);",
                "85|internal static void AddProtectedBlock(MethodBody body,",
                "126|// ADAPTATION: replaces the era ILHelper/ilasm prototype path (see header).",
                "127|// One-byte instructions only, so instruction i sits at offset i — the same",
                "128|// offsets the era read back from the assembled fixture.",
                "129|private static MethodBody BuildPrototype(int instructionCount, MethodDefinition host) {",
                "130|var body = new MethodBody(host ?? new MethodDefinition(\"GoldenPrototype\",",
                "131|MethodAttributes.Public | MethodAttributes.HideBySig, moduleTypeSystem.Void));",
                "132|for (var i = 0; i < instructionCount - 1; i++) {",
                "133|var nop = Instruction.Create(OpCodes.Nop);",
                "134|nop.Offset = i;",
                "135|body.Instructions.Add(nop);",
                "137|var ret = Instruction.Create(OpCodes.Ret);",
                "138|ret.Offset = instructionCount - 1;",
                "139|body.Instructions.Add(ret);",
                "140|return body;",
                "143|// ADAPTATION: the prototype methods need a type system for void/branch",
                "144|// construction; a single detached module hosts it (the era bodies hung off",
                "145|// real assemblies built by ilasm).",
                "146|private static readonly TypeSystem moduleTypeSystem = CreateTypeSystem();",
                "148|private static TypeSystem CreateTypeSystem() {",
                "149|var module = ModuleDefinition.CreateModule(\"GoldenPrototypes\", new ModuleParameters { Kind = ModuleKind.Dll });",
                "150|return module.TypeSystem;",
            ]),
    ];

    [Fact]
    public void Extracted_golden_builders_match_the_corpus_exactly_outside_the_pinned_adaptations()
    {
        foreach (var pin in ExtractionPins)
        {
            var adapted = ReadNormalizedLines(Path.Combine(RepoRoot(), pin.AdaptedRelativePath));
            var corpusLines = pin.Sources
                .SelectMany(source => ReadNormalizedLines(Path.Combine(CorpusRoot(), source.CorpusRelativePath)))
                .Select(StripIndentation)
                .Where(line => line.Length > 0)
                .ToHashSet();

            var actualAdaptedOnly = adapted
                .Select((line, index) => (Line: index + 1, Stripped: StripIndentation(line)))
                .Where(entry => entry.Stripped.Length > 0 && !corpusLines.Contains(entry.Stripped))
                .Select(entry => $"{entry.Line}|{entry.Stripped}")
                .ToArray();
            if (!actualAdaptedOnly.SequenceEqual(pin.AdaptedOnlyLines))
                throw new XunitException(
                    $"{pin.AdaptedRelativePath} drifted from the pinned extraction: lines outside the corpus " +
                    "sources must be exactly the pinned adaptation list:\n" +
                    $"actual ({actualAdaptedOnly.Length} lines):\n{string.Join("\n", actualAdaptedOnly)}");

            foreach (var source in pin.Sources)
            {
                // Each pinned region must appear as an ORDERED CONTIGUOUS run in the
                // adapted file (indentation-insensitive): set membership alone would
                // let reordered or duplicated region lines corrupt the oracle while
                // the pin stays green.
                var regionLines = ReadNormalizedLines(Path.Combine(CorpusRoot(), source.CorpusRelativePath));
                var adaptedStripped = adapted.Select(StripIndentation).ToArray();
                foreach (var (start, end) in source.Regions)
                {
                    var expected = Enumerable.Range(start, end - start + 1)
                        .Select(line => StripIndentation(regionLines[line - 1]))
                        .ToArray();
                    var found = false;
                    for (var offset = 0; offset <= adaptedStripped.Length - expected.Length && !found; offset++)
                    {
                        var matches = true;
                        for (var i = 0; i < expected.Length; i++)
                            if (adaptedStripped[offset + i] != expected[i])
                            {
                                matches = false;
                                break;
                            }
                        found = matches;
                    }
                    Assert.True(found,
                        $"extracted region {source.CorpusRelativePath}:{start}-{end} is not present in order " +
                        $"in {pin.AdaptedRelativePath}; if the extraction moved, regenerate the pin consciously");
                }
            }
        }
    }

    [Fact]
    public void Pinned_extraction_corpus_sources_match_their_sha256()
    {
        foreach (var pin in ExtractionPins)
            foreach (var source in pin.Sources)
            {
                var hash = Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(Path.Combine(CorpusRoot(), source.CorpusRelativePath)))).ToLowerInvariant();
                Assert.Equal(source.CorpusSha256, hash);
            }
    }

    private static string StripIndentation(string line) => line.TrimStart('\t', ' ');

    [Fact]
    public void Adapted_files_differ_from_the_corpus_by_exactly_the_pinned_adaptation_set()
    {
        foreach (var pin in Pins)
        {
            var corpus = ReadNormalizedLines(Path.Combine(CorpusRoot(), pin.CorpusRelativePath));
            var adapted = ReadNormalizedLines(Path.Combine(RepoRoot(), pin.AdaptedRelativePath));
            var (corpusOnly, adaptedOnly) = DiffLines(corpus, adapted);

            if (!corpusOnly.Select(l => $"{l.Line}|{l.Text}").SequenceEqual(pin.CorpusOnlyLines)
                || !adaptedOnly.Select(l => $"{l.Line}|{l.Text}").SequenceEqual(pin.AdaptedOnlyLines))
                throw new XunitException(
                    $"{pin.AdaptedRelativePath} no longer differs from the corpus by exactly the pinned adaptation set " +
                    "(line numbers are 1-based corpus/adapted positions). Regenerate the pin below (and update the " +
                    "adaptation-class docs in this file and the adapted-file headers) only for an intended adaptation:" +
                    $"\n\nCorpusOnlyLines =\n{FormatAsArrayLiteral(corpusOnly)}\nAdaptedOnlyLines =\n{FormatAsArrayLiteral(adaptedOnly)}");
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

    // Order- and position-sensitive line diff: an LCS walk emitting corpus-only
    // lines as Removed and adapted-only lines as Added, each carrying its 1-based
    // line number in the respective file, so a relocated edit fails the pin even
    // when its content is unchanged (e.g. moved between duplicate lines).
    private static (List<(int Line, string Text)> Removed, List<(int Line, string Text)> Added) DiffLines(string[] corpus, string[] adapted)
    {
        var suffix = new int[corpus.Length + 1, adapted.Length + 1];
        for (var i = corpus.Length - 1; i >= 0; i--)
            for (var j = adapted.Length - 1; j >= 0; j--)
                suffix[i, j] = corpus[i] == adapted[j]
                    ? suffix[i + 1, j + 1] + 1
                    : Math.Max(suffix[i + 1, j], suffix[i, j + 1]);

        var removed = new List<(int Line, string Text)>();
        var added = new List<(int Line, string Text)>();
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
                removed.Add((a + 1, corpus[a]));
                a++;
            }
            else
            {
                added.Add((b + 1, adapted[b]));
                b++;
            }
        }

        while (a < corpus.Length) removed.Add((a + 1, corpus[a++]));
        while (b < adapted.Length) added.Add((b + 1, adapted[b++]));
        return (removed, added);
    }

    private static string FormatAsArrayLiteral(IReadOnlyList<(int Line, string Text)> lines) =>
        string.Join(Environment.NewLine, lines.Select(line => $"\t\t\t\t\"{line.Line}|{Escape(line.Text)}\","));

    private static string Escape(string line) =>
        line.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
