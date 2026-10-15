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
		public class EndFinallyTest {

			protected static Mono.Cecil.AssemblyDefinition _testData;

			[ClassInitialize]
			public static void BeforeAllTests(TestContext data) {
				_testData = ILHelper.GetAssembly("instr_endfinally.il.txt");
			}

			[ClassCleanup]
			public static void AfterAllTests() {
				_testData = null;
			}

			private Mono.Cecil.Cil.MethodBody GetMethod(string methodName) {
				return ILHelper.GetMethodBody(_testData, methodName);
			}

			[TestMethod]
			public void VerifyEndFinally_InsideFinallyOrFaultHandlerWhenLexicallyLast_IsValid() {
				var method = GetMethod("InsideFinallyOrFaultHandler_LexicallyLast");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyEndFinally_InsideFinallyOrFaultHandlerWhenLexicallyMiddle_IsValid() {
				var method = GetMethod("InsideFinallyOrFaultHandler_LexicallyMiddle");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyEndFinally_InsideFinallyOrFaultHandlerWhenLexicallyFirst_IsValid() {
				var method = GetMethod("InsideFinallyOrFaultHandler_LexicallyFirst");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyEndFinally_NestedFinallyOrFaultHandlerInsideProtectedRegion_IsValid() {
				var method = GetMethod("NestedFinallyOrFaultHandlerInsideProtectedRegion");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyEndFinally_NestedFinallyorFaultInsideCatchHandler_IsValid() {
				var method = GetMethod("NestedFinallyorFaultInsideCatchHandler");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyEndFinally_NestedFinallyOrFaultInsideFilterHandler_IsValid() {
				var method = GetMethod("NestedFinallyOrFaultInsideFilterHandler");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
			}

			[TestMethod]
			public void VerifyEndFinally_InsideProtectedRegion_IsInvalid() {
				var method = GetMethod("InsideProtectedRegion");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
				// make sure both the endfinally and endfault violations were caught
				Assert.AreEqual(2, result.ErrorMessages.Count);
			}

			[TestMethod]
			public void VerifyEndFinally_InsideCatchHandler_IsInvalid() {
				var method = GetMethod("InsideCatchHandler");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
				// make sure both the endfinally and endfault violations were caught
				Assert.AreEqual(2, result.ErrorMessages.Count);
			}

			[TestMethod]
			public void VerifyEndFinally_InsideFilterCodeForFilter_IsInvalid() {
				var method = GetMethod("InsideFilterCodeForFilter");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
				// make sure both the endfinally and endfault violations were caught
				Assert.AreEqual(2, result.ErrorMessages.Count);
			}

			[TestMethod]
			public void VerifyEndFinally_InsideFilterHandler_IsInvalid() {
				var method = GetMethod("InsideFilterHandler");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
				// make sure both the endfinally and endfault violations were caught
				Assert.AreEqual(2, result.ErrorMessages.Count);
			}

			[TestMethod]
			public void VerifyEndFinally_InsideUnprotectedCode_IsInvalid() {
				var method = GetMethod("InsideUnprotectedCode");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
				// make sure both the endfinally and endfault violations were caught
				Assert.AreEqual(2, result.ErrorMessages.Count);
			}

			[TestMethod]
			public void VerifyEndFinally_InsideNestedProtectedRegion_IsInvalid() {
				var method = GetMethod("InsideNestedProtectedRegion");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
				// make sure both the endfinally and endfault violations were caught
				Assert.AreEqual(2, result.ErrorMessages.Count);
			}

			[TestMethod]
			public void VerifyEndFinally_InsideNestedCatchHandler_IsInvalid() {
				var method = GetMethod("InsideNestedCatchHandler");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
				// make sure both the endfinally and endfault violations were caught
				Assert.AreEqual(2, result.ErrorMessages.Count);
			}

			[TestMethod]
			public void VerifyEndFinally_InsideNestedFilterCodeForFilterHandler_IsInvalid() {
				var method = GetMethod("InsideNestedFilterCodeForFilterHandler");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
				// make sure both the endfinally and endfault violations were caught
				Assert.AreEqual(2, result.ErrorMessages.Count);
			}

			[TestMethod]
			public void VerifyEndFinally_InsideNestedFilterHandler_IsInvalid() {
				var method = GetMethod("InsideNestedFilterHandler");

				var result = ILValidator_Privates.VerifyEndFinally(method);

				Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
				// make sure both the endfinally and endfault violations were caught
				Assert.AreEqual(2, result.ErrorMessages.Count);
			}

		}

	}
}
