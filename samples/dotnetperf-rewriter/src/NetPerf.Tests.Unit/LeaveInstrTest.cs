using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Tests.Test_References;

namespace NetPerf.UnitTests.CILCorrectness {

	[CLSCompliant(false)]
	[TestClass]
	public class LeaveInstrTest : AbstractLeaveInstrTest {

		[ClassInitialize]
		public static void BeforeAllTests(TestContext data) {
			_testData = ILHelper.GetAssembly("instr_leave.il.txt");
		}

		[ClassCleanup]
		public static void AfterAllTests() {
			_testData = null;
		}

		protected override Mono.Cecil.Cil.MethodBody TransformBody(Mono.Cecil.Cil.MethodBody body) {
			// replace each leave_s instruction with a leave instruction
			foreach (var instr in body.Instructions.Where(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Leave_S)) {
				instr.OpCode = Mono.Cecil.Cil.OpCodes.Leave;

			}

			return body;
		}
	}

	[CLSCompliant(false)]
	[TestClass]
	public class LeaveShortInstrTest : AbstractLeaveInstrTest {

		[ClassInitialize]
		public static void BeforeAllTests(TestContext data) {
			_testData = ILHelper.GetAssembly("instr_leave.il.txt");
		}

		[ClassCleanup]
		public static void AfterAllTests() {
			_testData = null;
		}

		protected override Mono.Cecil.Cil.MethodBody TransformBody(Mono.Cecil.Cil.MethodBody body) {
			// replace each leave instruction with a leave_s instruction
			foreach (var instr in body.Instructions.Where(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Leave)) {
				instr.OpCode = Mono.Cecil.Cil.OpCodes.Leave_S;

			}

			return body;
		}

	}

	[CLSCompliant(false)]
	[TestClass]
	public abstract class AbstractLeaveInstrTest {

		protected static Mono.Cecil.AssemblyDefinition _testData;

		private Mono.Cecil.Cil.MethodBody GetMethod(string methodName) {
			return TransformBody(ILHelper.GetMethodBody(_testData, methodName));
		}

		protected virtual Mono.Cecil.Cil.MethodBody TransformBody(Mono.Cecil.Cil.MethodBody body) {
			return body;
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilter_TargetInSameFilter_IsValid() {
			var method = GetMethod("SourceWithinFilter_TargetInSameFilter");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilter_TargetNotInSameFilter_IsInvalid() {
			var method = GetMethod("SourceWithinFilter_TargetNotInSameFilter");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFaultHandler_TargetInSameFaultHandler_IsValid() {
			var method = GetMethod("SourceWithinFaultHandler_TargetInSameFaultHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFaultHandler_TargetNotInSameFaultHandler_IsInvalid() {
			var method = GetMethod("SourceWithinFaultHandler_TargetNotInSameFaultHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceInFinallyHandler_TargetInSameFinallyHandler_IsValid() {
			var method = GetMethod("SourceInFinallyHandler_TargetInSameFinallyHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceInFinallyHandler_TargetNotInSameFinallyHandler_IsInvalid() {
			var method = GetMethod("SourceInFinallyHandler_TargetNotInSameFinallyHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinSameTry_IsValid() {
			var method = GetMethod("SourceWithinTry_TargetWithinSameTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetIsFirstInstructionWithinNestedTry_IsValid() {
			var method = GetMethod("SourceWithinTry_TargetIsFirstInstructionWithinNestedTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetIsNotFirstInstructionWithinNestedTry_IsValid() {
			var method = GetMethod("SourceWithinTry_TargetIsNotFirstInstructionWithinNestedTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinCatchHandlerOfNestedTry_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinCatchHandlerOfNestedTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinFaultHandlerOfNestedTry_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinFaultHandlerOfNestedTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinEnclosingTry_IsValid() {
			var method = GetMethod("SourceWithinTry_TargetWithinEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinCatchHandlerOfSiblingProtectedBlock_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinCatchHandlerOfSiblingProtectedBlock");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinCatchHandler_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinCatchHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinFilterHandler_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinFilterHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinFaultHandler_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinFaultHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinCatchHandlerForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinCatchHandlerForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinHandlerForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinHandlerForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinFilterHandlerForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinFilterHandlerForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinFinallyHandlerForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinFinallyHandlerForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinFaulthandlerForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinFaulthandlerForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetIsFirstInstrOfDisjointTry_IsValid() {
			var method = GetMethod("SourceWithinTry_TargetIsFirstInstrOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetIsNotFirstInstrOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetIsNotFirstInstrOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinCatchHandlerOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinCatchHandlerOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinFilterHandlerOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinFilterHandlerOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinFinallyHandlerOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinFinallyHandlerOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinFaultHandlerOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinTry_TargetWithinFaultHandlerOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinTry_TargetWithinUnprotectedCode_IsValid() {
			var method = GetMethod("SourceWithinTry_TargetWithinUnprotectedCode");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetWithinSameCatchHandler_IsValid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetWithinSameCatchHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetNotInSameCatchHandler_IsInvalid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetNotInSameCatchHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetInAssociatedTry_IsValid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetInAssociatedTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetInTryNestedInAssociatedTry_IsInvalid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetInTryNestedInAssociatedTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetInEnclosingTry_IsValid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetInEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetInTryThatEnclosesTheEnclosingTry_IsValid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetInTryThatEnclosesTheEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetInCatchHandlerForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetInCatchHandlerForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetInFilterForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetInFilterForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetInFilterHandlerForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetInFilterHandlerForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetInFinallyHandlerForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetInFinallyHandlerForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetInFaultHandlerForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetInFaultHandlerForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetIsFirstInstrOfDisjointTry_IsValid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetIsFirstInstrOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetIsNotFirstInstrOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetIsNotFirstInstrOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetIsInCatchHandlerOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetIsInCatchHandlerOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetIsInFilterOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetIsInFilterOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetIsInFilterHandlerOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetIsInFilterHandlerOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetIsInFinallyHandlerOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetIsInFinallyHandlerOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetIsInFaultHandlerOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetIsInFaultHandlerOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinCatchHandler_TargetIsInUnprotectedCode_IsValid() {
			var method = GetMethod("SourceWithinCatchHandler_TargetIsInUnprotectedCode");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetWithinSameFilterHandler_IsValid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetWithinSameFilterHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetNotInSameFilterHandler_IsInvalid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetNotInSameFilterHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetInAssociatedTry_IsValid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetInAssociatedTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetInTryNestedInAssociatedTry_IsValid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetInTryNestedInAssociatedTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetInEnclosingTry_IsValid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetInEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetInTryThatEnclosesTheEnclosingTry_IsValid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetInTryThatEnclosesTheEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetInCatchHandlerForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetInCatchHandlerForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetInFilterForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetInFilterForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetInFilterHandlerForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetInFilterHandlerForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetInFinallyHandlerForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetInFinallyHandlerForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetInFaultHandlerForEnclosingTry_IsInvalid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetInFaultHandlerForEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetIsFirstInstrOfDisjointTry_IsValid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetIsFirstInstrOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetIsNotFirstInstrOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetIsNotFirstInstrOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetIsInCatchHandlerOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetIsInCatchHandlerOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetIsInFilterOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetIsInFilterOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetIsInFilterHandlerOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetIsInFilterHandlerOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetIsInFinallyHandlerOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetIsInFinallyHandlerOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetIsInFaultHandlerOfDisjointTry_IsInvalid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetIsInFaultHandlerOfDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_SourceWithinFilterHandler_TargetIsInUnprotectedCode_IsValid() {
			var method = GetMethod("SourceWithinFilterHandler_TargetIsInUnprotectedCode");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetWithinCatchHandler_SourceInSameCatchHandler_IsValid() {
			var method = GetMethod("TargetWithinCatchHandler_SourceInSameCatchHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetWithinCatchHandler_SourceNotInSameCatchHandler_IsInvalid() {
			var method = GetMethod("TargetWithinCatchHandler_SourceNotInSameCatchHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetWithinFilter_SourceInSameFilter_IsValid() {
			var method = GetMethod("TargetWithinFilter_SourceInSameFilter");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetWithinFilter_SourceNotInSameFilter_IsInvalid() {
			var method = GetMethod("TargetWithinFilter_SourceNotInSameFilter");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetInFilterHandler_SourceInSameFilterHandler_IsValid() {
			var method = GetMethod("TargetInFilterHandler_SourceInSameFilterHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetInFilterHandler_SourceNotInSameFilterHandler_IsInvalid() {
			var method = GetMethod("TargetInFilterHandler_SourceNotInSameFilterHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetInFinallyHandler_SourceInSameFinallyHandler_IsValid() {
			var method = GetMethod("TargetInFinallyHandler_SourceInSameFinallyHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetInFinallyHandler_SourceNotInSameFinallyHandler_IsInvalid() {
			var method = GetMethod("TargetInFinallyHandler_SourceNotInSameFinallyHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetInFaultHandler_SourceInSameFaultHandler_IsValid() {
			var method = GetMethod("TargetInFaultHandler_SourceInSameFaultHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetInFaultHandler_SourceNotInSameFaultHandler_IsInvalid() {
			var method = GetMethod("TargetInFaultHandler_SourceNotInSameFaultHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetIsNotFirstInstrInTry_SourceInSameTry_IsValid() {
			var method = GetMethod("TargetIsNotFirstInstrInTry_SourceInSameTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetIsNotFirstInstrInTry_SourceInDisjointTry_IsInvalid() {
			var method = GetMethod("TargetIsNotFirstInstrInTry_SourceInDisjointTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetIsNotFirstInstrInTry_SourceInNestedTry_IsValid() {
			var method = GetMethod("TargetIsNotFirstInstrInTry_SourceInNestedTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetIsNotFirstInstrInTry_SourceInEnclosingTry_IsInvalid() {
			var method = GetMethod("TargetIsNotFirstInstrInTry_SourceInEnclosingTry");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetIsNotFirstInstrInTry_SourceInAssociatedCatchHandler_IsValid() {
			var method = GetMethod("TargetIsNotFirstInstrInTry_SourceInAssociatedCatchHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetIsNotFirstInstrInTry_SourceInAssociatedFilter_IsInvalid() {
			var method = GetMethod("TargetIsNotFirstInstrInTry_SourceInAssociatedFilter");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetIsNotFirstInstrInTry_SourceInAssociatedFilterHandler_IsValid() {
			var method = GetMethod("TargetIsNotFirstInstrInTry_SourceInAssociatedFilterHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsTrue(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetIsNotFirstInstrInTry_SourceInAssociatedFinallyHandler_IsInvalid() {
			var method = GetMethod("TargetIsNotFirstInstrInTry_SourceInAssociatedFinallyHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetIsNotFirstInstrInTry_SourceInAssociatedFaultHandler_IsInvalid() {
			var method = GetMethod("TargetIsNotFirstInstrInTry_SourceInAssociatedFaultHandler");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}

		[TestMethod]
		public void VerifyLeave_TargetIsNotFirstInstrInTry_SourceInUnprotectedCode_IsInvalid() {
			var method = GetMethod("TargetIsNotFirstInstrInTry_SourceInUnprotectedCode");

			var result = ILValidator_Privates.VerifyLeave(method);

			Assert.IsFalse(result.IsValid, string.Join(" ", result.ErrorMessages));
		}
	}


}
