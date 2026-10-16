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
		public class FallThroughTest {

			private class Pair {
				public OpCode Opcode {
					get;
					private set;
				}

				public ILValidator.VerifyResult Result {
					get;
					private set;
				}

				public Pair(OpCode code, ILValidator.VerifyResult result) {
					Opcode = code;
					Result = result;
				}
			}

			private static OpCode[] _instrWithoutFallthrough;
			private static OpCode[] _allInstructions;
			private static Mono.Cecil.AssemblyDefinition _testData;
			private static MethodBody _prototype;

			[ClassInitialize]
			public static void BeforeAllTests(TestContext context) {
				_testData = ILHelper.GetAssembly("fallthrough.il.txt");
				_prototype = ILHelper.GetMethodBody(_testData, "EmptyMethod");
				_instrWithoutFallthrough = new[] { OpCodes.Br, OpCodes.Br_S,
					OpCodes.Endfilter, OpCodes.Endfinally,
					OpCodes.Leave, OpCodes.Leave_S, OpCodes.Ret, 
					OpCodes.Rethrow, OpCodes.Throw };

				_allInstructions = typeof(OpCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
					.Select(fi => (OpCode)fi.GetValue(null))
					.ToArray();
			}

			[ClassCleanup]
			public static void AfterAllTests() {
				_testData = null;
				_prototype = null;
			}

			private Mono.Cecil.Cil.MethodBody GetMethod() {
				return ILHelper.CloneMethod(_prototype);
			}

			private Mono.Cecil.Cil.MethodBody GetMethod(string methodName) {
				return ILHelper.GetMethodBody(_testData, methodName);
			}

			private void SingleRegionHelper(bool shouldPass,
				ExceptionHandlerType handlerKind,
				int tryStart, int tryEnd, int filterStart, int filterEnd, int handlerStart, int handlerEnd,
				string startsWith = null, string endsWith = null) //
			{
				var body = GetMethod();

				AddProtectedBlock(body, ExceptionHandlerType.Filter,
					tryStart: tryStart, tryEnd: tryEnd,
					filterStart: filterStart, filterEnd: filterEnd,
					handlerStart: handlerStart, handlerEnd: handlerEnd);

				var result = NetPerf.Tests.Test_References.ILValidator_Privates.VerifyFallThrough(body);

				if (shouldPass) {
					Assert.IsTrue(result.IsValid);
				}
				else {
					Assert.IsFalse(result.IsValid);
					if (!string.IsNullOrEmpty(startsWith)) {
						StringAssert.StartsWith(result.ErrorMessages[0], startsWith);
					}
					if (!string.IsNullOrEmpty(endsWith)) {
						StringAssert.EndsWith(result.ErrorMessages[0], endsWith);
					}
				}
			}

			private void AddProtectedBlock(MethodBody body,
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

				if (tryEnd > 0 && (tryEnd + 1) < body.Instructions.Count
					&& !(body.Instructions[tryEnd - 1].OpCode == OpCodes.Leave || body.Instructions[tryEnd - 1].OpCode == OpCodes.Leave_S)) //
				{
					body.Instructions[tryEnd - 1].OpCode = OpCodes.Leave;
				}

				if (filterEnd > 0) {
					body.Instructions[filterEnd - 1].OpCode = OpCodes.Endfilter;
				}

				if (handlerEnd > 0 && (handlerEnd + 1) < body.Instructions.Count
					&& !(body.Instructions[handlerEnd - 1].OpCode == OpCodes.Leave || body.Instructions[handlerEnd - 1].OpCode == OpCodes.Leave_S)) //
				{
					body.Instructions[handlerEnd - 1].OpCode = OpCodes.Leave;
				}

				if ((handlerKind == ExceptionHandlerType.Finally || handlerKind == ExceptionHandlerType.Fault) && handlerEnd > 0) {
					body.Instructions[handlerEnd - 1].OpCode = OpCodes.Endfinally;
				}

				body.ExceptionHandlers.Add(region);
			}

			private string CheckResult(bool shouldBeValid, string startsWith, string endsWith, Pair result) {
				var doesntStartWith = (!string.IsNullOrEmpty(startsWith) && (result.Result.ErrorMessages.Count < 1 || !result.Result.ErrorMessages[0].StartsWith(startsWith)));
				var doesntEndWith = (!string.IsNullOrEmpty(endsWith) && (result.Result.ErrorMessages.Count < 1 || !result.Result.ErrorMessages[0].EndsWith(endsWith)));

				var failed = result.Result.IsValid != shouldBeValid || doesntEndWith || doesntStartWith;


				if (failed) {
					var errorMsg = string.Empty;
					if (doesntStartWith) {
						errorMsg += string.Format("{0}: '{1}' does not begin with '{2}'",
							result.Opcode.Name,
							!result.Result.IsValid ? result.Result.ErrorMessages[0] : string.Empty,
							startsWith);
					}

					if (doesntEndWith) {
						errorMsg += Environment.NewLine + string.Format("{0}: '{1}' does not end with '{2}'",
							result.Opcode.Name,
							!result.Result.IsValid ? result.Result.ErrorMessages[0] : string.Empty,
							endsWith);
					}

					if (!doesntEndWith && !doesntStartWith)
						return string.Format("{0}: {1}",
							result.Opcode.Name,
							!result.Result.IsValid ? result.Result.ErrorMessages[0] : string.Empty);

					return errorMsg;
				}

				return null;
			}

			//OVerview of all the rules:
			//Entry to filters or handlers can only be accomplished through the CLI exception system; that is, it is not valid 
			//for control to fall through into such blocks. This means (1) filters and handlers cannot appear at the beginning of a 
			//method, or (2) immediately following any instruction that can cause control flow to fall through. 
			//[Note: Conditional branches can have multiple effects on control flow. Since one of the possible effects is to 
			//allow control flow to fall through, a filter or handler cannot appear immediately following a conditional branch. 
			//end note] 
			// (ignored) Entry to protected blocks can be accomplished by fall-through, at which time the evaluation stack shall be 
			//empty.
			//(3) Exit from protected blocks, filters, or handlers cannot be accomplished via fall through.

			[TestMethod]
			public void FallThrough_AllConditionsSatisified_Passes() {
				var body = GetMethod("ValidMethod");

				var result = NetPerf.Tests.Test_References.ILValidator_Privates.VerifyFallThrough(body);

				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			// filters and handlers cannot appear
			// (1) at the beginning of a method

			[TestMethod]
			public void AtBeginningOfMethod_FilterIsPresent_Fails() {
				SingleRegionHelper(false,
					ExceptionHandlerType.Filter, 10, 14, 0, 5, 5, 8,
					"A handler or filter is at the beginning of the method.");
			}

			[TestMethod]
			public void AtBeginningOfMethod_CatchHandlerIsPresent_Fails() {
				SingleRegionHelper(false,
					ExceptionHandlerType.Catch, 10, 14, -1, -1, 0, 8,
					"A handler or filter is at the beginning of the method.");


			}

			[TestMethod]
			public void AtBeginningOfMethod_FinallyHandlerIsPresent_Fails() {
				SingleRegionHelper(false,
					ExceptionHandlerType.Finally, 10, 14, -1, -1, 0, 8,
					"A handler or filter is at the beginning of the method.");
			}

			[TestMethod]
			public void AtBeginningOfMethod_FaultHandlerIsPresent_Fails() {
				SingleRegionHelper(false,
					ExceptionHandlerType.Fault, 10, 14, -1, -1, 0, 8,
					"A handler or filter is at the beginning of the method.");
			}

			// filters and handlers cannot appear
			// (2) immediately following any instruction that can cause control flow to fall through. 

			private void EnterFiltersAndHandlersHelper(bool shouldBeValid, IEnumerable<OpCode> testOpCodes, ExceptionHandlerType kind, string startsWith = null, string endsWith = null) {
				var results = new List<Pair>();

				foreach (var opcode in testOpCodes) {
					var body = GetMethod();

					AddProtectedBlock(body,
						kind, 12, 15, kind == ExceptionHandlerType.Filter ? 3 : -1, kind == ExceptionHandlerType.Filter ? 7 : -1, 7, 10);

					body.Instructions[kind == ExceptionHandlerType.Filter ? 2 : 6].OpCode = opcode;

					var result = NetPerf.Tests.Test_References.ILValidator_Privates.VerifyFallThrough(body);
					results.Add(new Pair(opcode, result));
				}

				var errorMessages = results
					.Select(r => CheckResult(shouldBeValid, startsWith, endsWith, r))
					.Where(m => m != null)
					.ToList();

				if (errorMessages.Count < 1) {
					Assert.IsTrue(true);
				}
				else {
					var errorMsg = "The following test cases failed: " + Environment.NewLine + string.Join(Environment.NewLine, errorMessages);
					Assert.Fail(errorMsg);
				}
			}

			[TestMethod]
			public void EnterFiltersAndHandlers_EnteringFilterWithNonFallThroughInstruction_Passes() {
				EnterFiltersAndHandlersHelper(true, _instrWithoutFallthrough, ExceptionHandlerType.Filter);
			}

			[TestMethod]
			public void EnterFiltersAndHandlers_EnteringFilterWithFallThroughInstruction_Fails() {
				EnterFiltersAndHandlersHelper(false, _allInstructions.Except(_instrWithoutFallthrough), ExceptionHandlerType.Filter,
					"The filter or handler that begins at",
					"is incorrectly preceeded by an instruction that could let control flow fall through.");
			}

			[TestMethod]
			public void EnterFiltersAndHandlers_EnteringCatchHandlerWithNonFallThroughInstruction_Passes() {
				EnterFiltersAndHandlersHelper(true, _instrWithoutFallthrough, ExceptionHandlerType.Catch);
			}

			[TestMethod]
			public void EnterFiltersAndHandlers_EnteringCatchHandlerWithFallThroughInstruction_Fails() {
				EnterFiltersAndHandlersHelper(false, _allInstructions.Except(_instrWithoutFallthrough), ExceptionHandlerType.Catch,
					"The filter or handler that begins at",
					"is incorrectly preceeded by an instruction that could let control flow fall through.");
			}

			[TestMethod]
			public void EnterFiltersAndHandlers_EnteringFinallyHandlerWithNonFallThroughInstruction_Passes() {
				EnterFiltersAndHandlersHelper(true, _instrWithoutFallthrough, ExceptionHandlerType.Finally);
			}

			[TestMethod]
			public void EnterFiltersAndHandlers_EnteringFinallyHandlerWithFallThroughInstruction_Fails() {
				EnterFiltersAndHandlersHelper(false, _allInstructions.Except(_instrWithoutFallthrough), ExceptionHandlerType.Finally,
					"The filter or handler that begins at",
					"is incorrectly preceeded by an instruction that could let control flow fall through.");
			}

			[TestMethod]
			public void EnterFiltersAndHandlers_EnteringFaultHandlerWithNonFallThroughInstruction_Passes() {
				EnterFiltersAndHandlersHelper(true, _instrWithoutFallthrough, ExceptionHandlerType.Fault);
			}

			[TestMethod]
			public void EnterFiltersAndHandlers_EnteringFaultHandlerWithFallThroughInstruction_Fails() {
				EnterFiltersAndHandlersHelper(false, _allInstructions.Except(_instrWithoutFallthrough), ExceptionHandlerType.Fault,
					"The filter or handler that begins at",
					"is incorrectly preceeded by an instruction that could let control flow fall through.");
			}

			//(3) Exit from protected blocks, filters, or handlers cannot be accomplished via fall through.

			private void ExitRegionHelper(bool shouldBeValid, IEnumerable<OpCode> testOpCodes, ExceptionHandlerType kind,
				bool isProtectedBlock, string startsWith = null, string endsWith = null, bool isFilter = false)//
			{
				var results = new List<Pair>();

				foreach (var opcode in testOpCodes) {
					var body = GetMethod();

					AddProtectedBlock(body,
						kind, 14, 17, kind == ExceptionHandlerType.Filter ? 3 : -1, kind == ExceptionHandlerType.Filter ? 9 : -1, 9, 12);

					body.Instructions[8].OpCode = OpCodes.Leave;
					body.Instructions[13].OpCode = OpCodes.Leave;

					if (kind == ExceptionHandlerType.Filter) {
						body.Instructions[2].OpCode = OpCodes.Leave;
					}

					if (isProtectedBlock) {
						body.Instructions[16].OpCode = opcode;
					}
					else if (isFilter) {
						body.Instructions[8].OpCode = opcode;
					}
					else {
						body.Instructions[11].OpCode = opcode;
					}

					var result = NetPerf.Tests.Test_References.ILValidator_Privates.VerifyFallThrough(body);
					results.Add(new Pair(opcode, result));
				}

				var errorMessages = results
					.Select(r => CheckResult(shouldBeValid, startsWith, endsWith, r))
					.Where(m => m != null)
					.ToList();

				if (errorMessages.Count < 1) {
					Assert.IsTrue(true);
				}
				else {
					var errorMsg = "The following test cases failed: " + Environment.NewLine + string.Join(Environment.NewLine, errorMessages);
					Assert.Fail(errorMsg);
				}
			}

			[TestMethod]
			public void ExitFromRegion_ExitingProtectedBlockWithNonFallThroughInstruction_Passes() {
				ExitRegionHelper(true, _instrWithoutFallthrough, ExceptionHandlerType.Finally, true);
			}

			[TestMethod]
			public void ExitFromRegion_ExitingProtectedBlockWithFallThroughInstruction_Fails() {
				ExitRegionHelper(false, _allInstructions.Except(_instrWithoutFallthrough), ExceptionHandlerType.Finally, true,
					"The protected block, filter, or handler which ends at",
					"does not end with an instruction that prevents fall through.");
			}

			[TestMethod]
			public void ExitFromRegion_ExitingFinallyHandlerWithNonFallThroughInstruction_Passes() {
				ExitRegionHelper(true, _instrWithoutFallthrough, ExceptionHandlerType.Finally, false);
			}

			[TestMethod]
			public void ExitFromRegion_ExitingFinallyHandlerWithFallThroughInstruction_Fails() {
				ExitRegionHelper(false, _allInstructions.Except(_instrWithoutFallthrough), ExceptionHandlerType.Finally, false,
					"The protected block, filter, or handler which ends at",
					"does not end with an instruction that prevents fall through.");
			}

			[TestMethod]
			public void ExitFromRegion_ExitingFaultHandlerWithNonFallThroughInstruction_Passes() {
				ExitRegionHelper(true, _instrWithoutFallthrough, ExceptionHandlerType.Fault, false);
			}

			[TestMethod]
			public void ExitFromRegion_ExitingFaultHandlerWithFallThroughInstruction_Fails() {
				ExitRegionHelper(false, _allInstructions.Except(_instrWithoutFallthrough), ExceptionHandlerType.Fault, false,
					"The protected block, filter, or handler which ends at",
					"does not end with an instruction that prevents fall through.");
			}

			[TestMethod]
			public void ExitFromRegion_ExitingCatchHandlerWithNonFallThroughInstruction_Passes() {
				ExitRegionHelper(true, _instrWithoutFallthrough, ExceptionHandlerType.Catch, false);
			}

			[TestMethod]
			public void ExitFromRegion_ExitingCatchHandlerWithFallThroughInstruction_Fails() {
				ExitRegionHelper(false, _allInstructions.Except(_instrWithoutFallthrough), ExceptionHandlerType.Catch, false,
					"The protected block, filter, or handler which ends at",
					"does not end with an instruction that prevents fall through.");
			}

			[TestMethod]
			public void ExitFromRegion_ExitingFilterHandlerWithNonFallThroughInstruction_Passes() {
				ExitRegionHelper(true, _instrWithoutFallthrough, ExceptionHandlerType.Filter, false);
			}

			[TestMethod]
			public void ExitFromRegion_ExitingFilterHandlerWithFallThroughInstruction_Fails() {
				ExitRegionHelper(false, _allInstructions.Except(_instrWithoutFallthrough), ExceptionHandlerType.Filter, false,
					"The protected block, filter, or handler which ends at",
					"does not end with an instruction that prevents fall through.");
			}

			[TestMethod]
			public void ExitFromRegion_ExitingFilterWithNonFallThroughInstruction_Passes() {
				ExitRegionHelper(true, _instrWithoutFallthrough, ExceptionHandlerType.Filter, false, isFilter: true);
			}

			[TestMethod]
			public void ExitFromRegion_ExitingFilterWithFallThroughInstruction_Fails() {
				ExitRegionHelper(false, _allInstructions.Except(_instrWithoutFallthrough), ExceptionHandlerType.Filter, false,
					"The filter or handler that begins at",
					"is incorrectly preceeded by an instruction that could let control flow fall through.",
					true);
			}
		}
	}
}
