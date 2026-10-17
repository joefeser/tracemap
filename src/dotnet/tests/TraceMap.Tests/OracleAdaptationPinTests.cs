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
                "754|				.SelectMany(eh => new[] { eh.TryEnd, eh.HandlerStart, eh.HandlerEnd });",
                "763|				.Where(instr => !InstrsThatDontFallThrough.Any(i => i == instr.OpCode));",
                "803|					new { instr=i, name=\"filter\", start=eh.FilterStart, end = eh.HandlerStart, targets=getInstrTargets(i)},",
                "856|					new { instr=i, name=\"filter\", start=eh.FilterStart, end = eh.HandlerStart, targets=getInstrTargets(i)},",
                "966|				new { isFilter=true, start=nullableOffset(left.FilterStart), end=nullableOffset(left.FilterStart == null ? null : left.HandlerStart)},",
                "972|				new { isFilter=true, start=nullableOffset(right.FilterStart), end=nullableOffset(right.FilterStart == null ? null : right.HandlerStart)},",
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
