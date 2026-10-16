using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Tests.Test_References;

namespace NetPerf.UnitTests.CILCorrectness {

	public partial class ILValidatorTest {

		[CLSCompliant(false)]
		[TestClass]
		public class RethrowInstrTest {

			protected static Mono.Cecil.AssemblyDefinition _testData;

			[ClassInitialize]
			public static void BeforeAllTests(TestContext data) {
				_testData = ILHelper.GetAssembly("instr_rethrow.il.txt");
			}

			[ClassCleanup]
			public static void AfterAllTests() {
				_testData = null;
			}

			private Mono.Cecil.Cil.MethodBody GetMethod(string methodName) {
				return ILHelper.GetMethodBody(_testData, methodName);
			}

			[TestMethod]
			public void VerifyRethrow_InstrInsideTopLevelCatch_NoError() {
				var method = GetMethod("ImmediatlyInsideCatchHandler");

				var result = ILValidator_Privates.VerifyRethrow(method);

				Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyRethrow_InstrNestedInsideCatchHandler_NoError() {
				var method = GetMethod("NestedInsideCatchHandler");

				var result = ILValidator_Privates.VerifyRethrow(method);

				Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyRethrow_InstrInsideNestedCatchHandler_NoError() {
				var method = GetMethod("InsideNestedCatchHandler");

				var result = ILValidator_Privates.VerifyRethrow(method);

				Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyRethrow_InstrInsideFinallyHandler_Error() {
				var method = GetMethod("InsideFinallyHandler");

				var result = ILValidator_Privates.VerifyRethrow(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyRethrow_InstrInsideFaultHandler_Error() {
				var method = GetMethod("InsideFaultHandler");

				var result = ILValidator_Privates.VerifyRethrow(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyRethrow_InstrInsideFilterCodeForFilterHandler_Error() {
				var method = GetMethod("InsideFilterCodeForFilterHandler");

				var result = ILValidator_Privates.VerifyRethrow(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyRethrow_InstrInsideFilterHandler_Error() {
				var method = GetMethod("InsideFilterHandler");

				var result = ILValidator_Privates.VerifyRethrow(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyRethrow_InstrInsideProtectedRegion_Error() {
				var method = GetMethod("InsideProtectedRegion");

				var result = ILValidator_Privates.VerifyRethrow(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyRethrow_InstrNotInsideAnyHandlerOrFilterOrProtectedRegion_Error() {
				var method = GetMethod("NotInsideAnyHandler");

				var result = ILValidator_Privates.VerifyRethrow(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}
		}
	}
}
