// ADAPTED EXTRACTION for #856 stage 2c. The construction helpers below are
// extracted near-verbatim from the byte-pinned corpus sources:
//   samples/dotnetperf-rewriter/src/NetPerf.Tests.Unit/BranchesTest.cs
//     (GetSourceTestMethod, AddTargetAndSource)
//   samples/dotnetperf-rewriter/src/NetPerf.Tests.Unit/ProtectedBlocksTest.cs
//     (GetMethod, AddProtectedBlock)
// OracleAdaptationPinTests pins the extraction: every corpus line of the
// extracted regions must appear here verbatim, and every other line of this
// file is a documented adaptation. The corpus tree itself is never modified.
//
// Adaptation classes (everything not verbatim from the corpus):
//   - Era prototypes came from ILHelper.GetAssembly("branches.il.txt" /
//     "protectedblocks.il.txt"), which ran ilasm.exe at test time; ilasm is not
//     available (and is excluded from the corpus). The grids are rebuilt in
//     pure Cecil mirroring the committed fixtures: SourceTestsBase = 25 nop +
//     ret (branches.il.txt lines 35-64; the three ilasm labels carry no bytes),
//     ProtectedBlocks_Base = 50 nop + ret (protectedblocks.il.txt from line 57).
//   - Offsets are assigned positionally because the validator's region math
//     reads Instruction.Offset and the era values came from the ilasm round
//     trip (one-byte nops: instruction i sits at offset i).
//   - The prototype builders live on this class instead of cached static
//     _prototype fields with ILHelper.CloneMethod; each call builds a fresh
//     body, which is what the era clone achieved.
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NetPerf.Goldens;

internal static class GoldenBodyBuilders
{
	private const int SourceTestsBaseInstructionCount = 26;
	private const int ProtectedBlocksBaseInstructionCount = 51;

	internal static MethodBody SourceTestMethod(MethodDefinition host = null) {
		return BuildPrototype(SourceTestsBaseInstructionCount, host);
	}

	internal static void AddTargetAndSource(MethodBody body, OpCode sourceOp,
		int sourceIndx, ExceptionHandlerType sourceKind,
		int targetIndx, ExceptionHandlerType targetKind,
		int target2Indx = -1, ExceptionHandlerType target2Kind = ExceptionHandlerType.Finally) //
	{

		var target = body.Instructions[targetIndx];
		var source = target2Indx == -1
			? Instruction.Create(sourceOp, target)
			: Instruction.Create(sourceOp, new[] { target, body.Instructions[target2Indx] });
		source.Offset = body.Instructions[sourceIndx].Offset;

		body.GetILProcessor().Replace(body.Instructions[sourceIndx], source);

		var firstKind = sourceIndx >= 2 && sourceIndx < 11
			? sourceKind
			: (targetIndx >= 2 && targetIndx < 11
				? targetKind
				: target2Kind);

		var secondKind = sourceIndx >= 14 && sourceIndx < 23
			? sourceKind
			: (targetIndx >= 14 && targetIndx < 23
				? targetKind
				: target2Kind);

		body.ExceptionHandlers.Add(new ExceptionHandler(firstKind) {
			TryStart = body.Instructions[2],
			TryEnd = body.Instructions[5],
			FilterStart = body.Instructions[5],
			HandlerStart = body.Instructions[8],
			HandlerEnd = body.Instructions[11]
		});

		body.ExceptionHandlers.Add(new ExceptionHandler(secondKind) {
			TryStart = body.Instructions[14],
			TryEnd = body.Instructions[17],
			FilterStart = body.Instructions[17],
			HandlerStart = body.Instructions[20],
			HandlerEnd = body.Instructions[23]
		});
	}

	internal static MethodBody ProtectedBlocksMethod(MethodDefinition host = null) {
		return BuildPrototype(ProtectedBlocksBaseInstructionCount, host);
	}

	internal static void AddProtectedBlock(MethodBody body,
		ExceptionHandlerType handlerKind,
		int tryStart = -1, int tryEnd = -1,
		int filterStart = -1, int filterEnd = -1,
		int handlerStart = -1, int handlerEnd = -1) //
	{

		var instrCount = body.Instructions.Count;
		if (tryStart >= instrCount)
			throw new ArgumentOutOfRangeException("tryStart", "cannot be greater than the number of instructions (" + instrCount + ").");
		if (tryEnd >= instrCount)
			throw new ArgumentOutOfRangeException("tryEnd", "cannot be greater than the number of instructions (" + instrCount + ").");
		if (filterStart >= instrCount)
			throw new ArgumentOutOfRangeException("filterStart", "cannot be greater than the number of instructions (" + instrCount + ").");
		if (filterEnd >= instrCount)
			throw new ArgumentOutOfRangeException("filterEnd", "cannot be greater than the number of instructions (" + instrCount + ").");
		if (handlerStart >= instrCount)
			throw new ArgumentOutOfRangeException("handlerStart", "cannot be greater than the number of instructions (" + instrCount + ").");
		if (handlerEnd >= instrCount)
			throw new ArgumentOutOfRangeException("handlerEnd", "cannot be greater than the number of instructions (" + instrCount + ").");


		var region = new ExceptionHandler(handlerKind) {
			TryStart = tryStart >= 0 ? body.Instructions[tryStart] : null,
			TryEnd = tryEnd >= 0 ? body.Instructions[tryEnd] : null,
			FilterStart = filterStart >= 0 ? body.Instructions[filterStart] : null,
			HandlerStart = handlerStart >= 0 ? body.Instructions[handlerStart] : null,
			HandlerEnd = handlerEnd >= 0 ? body.Instructions[handlerEnd] : null
		};

		if (filterEnd > 0) {
			body.Instructions[filterEnd - 1].OpCode = OpCodes.Endfilter;
		}

		if ((handlerKind == ExceptionHandlerType.Finally || handlerKind == ExceptionHandlerType.Fault) && handlerEnd > 0) {
			body.Instructions[handlerEnd - 1].OpCode = OpCodes.Endfinally;
		}

		body.ExceptionHandlers.Add(region);
	}

	// ADAPTATION: replaces the era ILHelper/ilasm prototype path (see header).
	// One-byte instructions only, so instruction i sits at offset i — the same
	// offsets the era read back from the assembled fixture.
	private static MethodBody BuildPrototype(int instructionCount, MethodDefinition host) {
		var body = new MethodBody(host ?? new MethodDefinition("GoldenPrototype",
			MethodAttributes.Public | MethodAttributes.HideBySig, moduleTypeSystem.Void));
		for (var i = 0; i < instructionCount - 1; i++) {
			var nop = Instruction.Create(OpCodes.Nop);
			nop.Offset = i;
			body.Instructions.Add(nop);
		}
		var ret = Instruction.Create(OpCodes.Ret);
		ret.Offset = instructionCount - 1;
		body.Instructions.Add(ret);
		return body;
	}

	// ADAPTATION: the prototype methods need a type system for void/branch
	// construction; a single detached module hosts it (the era bodies hung off
	// real assemblies built by ilasm).
	private static readonly TypeSystem moduleTypeSystem = CreateTypeSystem();

	private static TypeSystem CreateTypeSystem() {
		var module = ModuleDefinition.CreateModule("GoldenPrototypes", new ModuleParameters { Kind = ModuleKind.Dll });
		return module.TypeSystem;
	}
}
