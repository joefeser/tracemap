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
		public class RetInstrTest {

			protected static Mono.Cecil.AssemblyDefinition _testData;

			[ClassInitialize]
			public static void BeforeAllTests(TestContext data) {
				_testData = ILHelper.GetAssembly("instr_ret.il.txt");
			}

			[ClassCleanup]
			public static void AfterAllTests() {
				_testData = null;
			}

			private Mono.Cecil.Cil.MethodBody GetMethod(string methodName) {
				return ILHelper.GetMethodBody(_testData, methodName);
			}

			[TestMethod]
			public void VerifyRet_RetInstrNotInProtectedRegionOrFilterOrHandler_IsValid() {
				var method = GetMethod("NotInProtectedRegionFilterOrHandler");

				var result = ILValidator_Privates.VerifyRet(method);

				Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyRet_RetInstrWithinProtectedRegion_IsInvalid() {
				var method = GetMethod("WithinProtectedRegion");

				var result = ILValidator_Privates.VerifyRet(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyRet_RetInstrWithinFilterCodeForFilter_IsInvalid() {
				var method = GetMethod("WithinFilterCodeForFilter");

				var result = ILValidator_Privates.VerifyRet(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyRet_RetInstrWithinFilterHandler_IsInvalid() {
				var method = GetMethod("WithinFilterHandler");

				var result = ILValidator_Privates.VerifyRet(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyRet_RetInstrWithinFaultHandler_IsInvalid() {
				var method = GetMethod("WithinFaultHandler");

				var result = ILValidator_Privates.VerifyRet(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyRet_RetInstrWithinFinallyHandler_IsInvalid() {
				var method = GetMethod("WithinFinallyHandler");

				var result = ILValidator_Privates.VerifyRet(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyRet_RetInstrWithinCatchhandler_IsInvalid() {
				var method = GetMethod("WithinCatchhandler");

				var result = ILValidator_Privates.VerifyRet(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}
		}
	}
}
