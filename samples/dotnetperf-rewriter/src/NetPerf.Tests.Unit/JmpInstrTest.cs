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
		public class JmpInstrTest {

			protected static Mono.Cecil.AssemblyDefinition _testData;

			[ClassInitialize]
			public static void BeforeAllTests(TestContext data) {
				_testData = ILHelper.GetAssembly("instr_jmp.il.txt");
			}

			[ClassCleanup]
			public static void AfterAllTests() {
				_testData = null;
			}

			private Mono.Cecil.Cil.MethodBody GetMethod(string methodName) {
				return ILHelper.GetMethodBody(_testData, methodName);
			}

			[TestMethod]
			public void VerifyJmp_JmpInstrNotInProtectedRegionOrFilterOrHandler_IsValid() {
				var method = GetMethod("NotInProtectedRegionFilterOrHandler");

				var result = ILValidator_Privates.VerifyJmp(method);

				Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyJmp_JmpInstrWithinProtectedRegion_IsInvalid() {
				var method = GetMethod("WithinProtectedRegion");

				var result = ILValidator_Privates.VerifyJmp(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyJmp_JmpInstrWithinFilterCodeForFilter_IsInvalid() {
				var method = GetMethod("WithinFilterCodeForFilter");

				var result = ILValidator_Privates.VerifyJmp(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyJmp_JmpInstrWithinFilterHandler_IsInvalid() {
				var method = GetMethod("WithinFilterHandler");

				var result = ILValidator_Privates.VerifyJmp(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyJmp_JmpInstrWithinFaultHandler_IsInvalid() {
				var method = GetMethod("WithinFaultHandler");

				var result = ILValidator_Privates.VerifyJmp(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyJmp_JmpInstrWithinFinallyHandler_IsInvalid() {
				var method = GetMethod("WithinFinallyHandler");

				var result = ILValidator_Privates.VerifyJmp(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyJmp_JmpInstrWithinCatchhandler_IsInvalid() {
				var method = GetMethod("WithinCatchhandler");

				var result = ILValidator_Privates.VerifyJmp(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
			}
		}
	}
}
