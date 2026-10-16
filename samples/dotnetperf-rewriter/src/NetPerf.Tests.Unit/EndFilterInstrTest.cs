using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Tests.Test_References;

namespace NetPerf.UnitTests.CILCorrectness {

	public partial class ILValidatorTest {

		[TestClass]
		public class EndFilterInstrTest {

			private Mono.Cecil.Cil.MethodBody GetMethod(string methodName) {
				return ILHelper.GetMethodBody("instr_endfilter.il.txt", methodName);
			}

			[TestMethod]
			public void VerifyEndFilter_EndFilterInstrNotInProtectedRegionFilterOrHandler_IsInvalid() {
				var method = GetMethod("NotInProtectedRegionFilterOrHandler");

				var result = ILValidator_Privates.VerifyEndFilter(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyEndFilter_EndFilterInstrWithinProtectedRegion_IsInvalid() {
				var method = GetMethod("WithinProtectedRegion");

				var result = ILValidator_Privates.VerifyEndFilter(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyEndFilter_EndFilterInstrWithinFilterCodeForFilterWhenLexicallyLast_IsValid() {
				var method = GetMethod("WithinFilterCodeForFilter");

				var result = ILValidator_Privates.VerifyEndFilter(method);

				Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyEndFilter_EndFilterInstrWithinFilterCodeForFilterWhenLexicallyFirst_IsInvalid() {
				var method = GetMethod("WithinFilterCodeForFilter");

				// have to move the endfilter instruction to the beginning of the
				// filter code, since ilasm won't compile IL if the endfilter instruction isn't
				// the lexically last instruction in the filter code
				var handler = method.ExceptionHandlers.First();
				var instr = method.Instructions.Where(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Endfilter).Single();

				// move the instruction out of the method
				instr.Previous.Next = instr.Next;
				handler.HandlerStart = instr.Previous;

				// and insert it into the beginning of the filter
				instr.Previous = handler.FilterStart.Previous;
				instr.Next = handler.FilterStart;
				handler.FilterStart.Previous = instr;

				handler.FilterStart = instr;

				var result = ILValidator_Privates.VerifyEndFilter(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyEndFilter_EndFilterInstrWithinFilterCodeForFilterWhenLexicallyMiddle_IsInvalid() {
				var method = GetMethod("WithinFilterCodeForFilter");

				// have to move the endfilter instruction to the middle of the
				// filter code, since ilasm won't compile IL if the endfilter instruction isn't
				// the lexically last instruction in the filter code
				var handler = method.ExceptionHandlers.First();
				var instr = method.Instructions.Where(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Endfilter).Single();

				// move the instruction out of the method
				instr.Previous.Next = instr.Next;
				handler.HandlerStart = instr.Previous;

				// and insert it after the beginning of the filter
				instr.Next = handler.FilterStart.Next;
				handler.FilterStart.Next.Previous = instr;
				instr.Previous = handler.FilterStart;
				handler.FilterStart.Next = instr;

				var result = ILValidator_Privates.VerifyEndFilter(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyEndFilter_EndFilterInstrWithinFilterHandler_IsInvalid() {
				var method = GetMethod("WithinFilterHandler");

				var result = ILValidator_Privates.VerifyEndFilter(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyEndFilter_EndFilterInstrWithinFaultHandler_IsInvalid() {
				var method = GetMethod("WithinFaultHandler");

				var result = ILValidator_Privates.VerifyEndFilter(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyEndFilter_EndFilterInstrWithinFinallyHandler_IsInvalid() {
				var method = GetMethod("WithinFinallyHandler");

				var result = ILValidator_Privates.VerifyEndFilter(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyEndFilter_EndFilterInstrWithinCatchhandler_IsInvalid() {
				var method = GetMethod("WithinCatchhandler");

				var result = ILValidator_Privates.VerifyEndFilter(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

		}
	}
}
