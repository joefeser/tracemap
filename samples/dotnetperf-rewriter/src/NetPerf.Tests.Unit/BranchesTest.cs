using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.UnitTests;
using Mono.Cecil.Cil;

namespace NetPerf.Tests {

	public partial class ILValidatorTest {

		[TestClass]
		public partial class BranchesTest {

			static MethodBody _prototype;

			[ClassInitialize]
			public static void Initialize(TestContext context) {
				_prototype = ILHelper.GetMethodBody(ILHelper.GetAssembly("branches.il.txt"), "SourceTestsBase");
			}

			[ClassCleanup]
			public static void Teardown() {
				_prototype = null;
			}


			private static Mono.Cecil.Cil.MethodBody GetSourceTestMethod() {
				return ILHelper.CloneMethod(_prototype);
			}

			private void AddTargetAndSource(MethodBody body, OpCode sourceOp,
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
		}
	}
}
