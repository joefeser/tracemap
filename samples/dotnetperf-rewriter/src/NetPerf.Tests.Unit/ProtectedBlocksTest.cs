using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.UnitTests;
using Mono.Cecil.Cil;
using NetPerf.Tests.Test_References;

namespace NetPerf.Tests {

	public partial class ILValidatorTest {

		[TestClass]
		public class ProtectedBlocksTest {

			private static MethodBody _prototype;

			[ClassInitialize]
			public static void BeforeAllTests(TestContext context) {
				var assembly = ILHelper.GetAssembly("protectedblocks.il.txt");
				_prototype = ILHelper.GetMethodBody(assembly, "ProtectedBlocks_Base");
			}

			[ClassCleanup]
			public static void AfterAllTests() {
				_prototype = null;
			}

			private Mono.Cecil.Cil.MethodBody GetMethod() {
				return ILHelper.CloneMethod(_prototype);
			}

			private void ProtectedBlockHelper(bool shouldPass, ExceptionHandlerType handlerKind,
				int tryStart = -1, int tryEnd = -1,
				int filterStart = -1, int filterEnd = -1,
				int handlerStart = -1, int handlerEnd = -1) //
			{
				var body = GetMethod();

				AddProtectedBlock(body, ExceptionHandlerType.Filter,
					tryStart: tryStart, tryEnd: tryEnd,
					filterStart: filterStart, filterEnd: filterEnd,
					handlerStart: handlerStart, handlerEnd: handlerEnd);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);

				if (shouldPass) {
					Assert.IsTrue(result.IsValid);
				}
				else {

					Assert.IsFalse(result.IsValid);
					StringAssert.StartsWith(result.ErrorMessages[0], "The protected block");
					StringAssert.EndsWith(result.ErrorMessages[0], "regions are not pairwise disjoint.");
				}
			}

			private void DualRegionTestHelper(bool shouldpass,
				ExceptionHandlerType oKind, int ots, int ote, int ofs, int ofe, int ohs, int ohe,
				ExceptionHandlerType iKind, int its, int ite, int ifs, int ife, int ihs, int ihe) //
			{
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, oKind, tryStart: ots, tryEnd: ote, filterStart: ofs, filterEnd: ofe, handlerStart: ohs, handlerEnd: ohe);
				// and this is the contained one
				AddProtectedBlock(body, iKind, tryStart: its, tryEnd: ite, filterStart: ifs, filterEnd: ife, handlerStart: ihs, handlerEnd: ihe);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);

				if (shouldpass) {
					Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				}
				else {
					Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
					StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
					StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
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

				if (filterEnd > 0) {
					body.Instructions[filterEnd - 1].OpCode = OpCodes.Endfilter;
				}

				if ((handlerKind == ExceptionHandlerType.Finally || handlerKind == ExceptionHandlerType.Fault) && handlerEnd > 0) {
					body.Instructions[handlerEnd - 1].OpCode = OpCodes.Endfinally;
				}

				body.ExceptionHandlers.Add(region);
			}

			// Every method can have associated with it a set of exception entries, called the exception set. Each exception 
			// entry consists of 
			// *  Optional: a type token (the type of exception to be handled) or filterstart (the address of the first 
			//    instruction of the user-supplied filter code)  
			// *  Required: protected block  
			// *  Required: handler region. There are four kinds of handler regions: catch handlers, filtered 
			//    handlers, finally handlers, and fault handlers. (A filtered handler is the code that runs if the filter 
			//    evaluates to true.)
			[TestMethod]
			public void ProtectedBlockIsRequired_NoProtectedBlock_Fails() {
				var body = GetMethod();

				AddProtectedBlock(body, ExceptionHandlerType.Finally, handlerStart: 2, handlerEnd: 5);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);

				Assert.IsFalse(result.IsValid);
				StringAssert.StartsWith(result.ErrorMessages[0], "Invalid exception handler");
			}

			[TestMethod]
			public void HandlerRegionIsRequired_NoHandlerRegion_Fails() {
				var body = GetMethod();

				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 2, tryEnd: 5);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);

				Assert.IsFalse(result.IsValid);
				StringAssert.StartsWith(result.ErrorMessages[0], "Invalid exception handler");
			}

			[TestMethod]
			public void HandlerRegionIsRequired_CatchHandler_Passes() {
				var body = GetMethod();

				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 2, tryEnd: 5, handlerStart: 5, handlerEnd: 7);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);

				Assert.IsTrue(result.IsValid);
			}

			[TestMethod]
			public void HandlerRegionIsRequired_FinallyHandler_Passes() {
				var body = GetMethod();

				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 2, tryEnd: 5, handlerStart: 5, handlerEnd: 7);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);

				Assert.IsTrue(result.IsValid);
			}

			[TestMethod]
			public void HandlerRegionIsRequired_FilterHandler_Passes() {
				var body = GetMethod();

				AddProtectedBlock(body, ExceptionHandlerType.Filter,
					tryStart: 2, tryEnd: 5,
					filterStart: 5, filterEnd: 7,
					handlerStart: 7, handlerEnd: 9);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);

				Assert.IsTrue(result.IsValid);
			}

			[TestMethod]
			public void HandlerRegionIsRequired_FaultHandler_Passes() {
				var body = GetMethod();

				AddProtectedBlock(body, ExceptionHandlerType.Fault,
					tryStart: 2, tryEnd: 5,
					handlerStart: 5, handlerEnd: 7);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);

				Assert.IsTrue(result.IsValid);
			}

			// If an exception entry contains a filterstart, then filterstart strictly precedes handlerstart. 
			[TestMethod]
			public void FilterStartAfterHandlerStart_Fails() {
				var body = GetMethod();

				AddProtectedBlock(body, ExceptionHandlerType.Filter,
					tryStart: 2, tryEnd: 5,
					filterStart: 7, filterEnd: 9,
					handlerStart: 5, handlerEnd: 7);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);

				Assert.IsFalse(result.IsValid);
				StringAssert.StartsWith(result.ErrorMessages[0], "The filterstart");
			}

			[TestMethod]
			public void FilterStartBeforeHandlerStart_Passes() {
				var body = GetMethod();

				AddProtectedBlock(body, ExceptionHandlerType.Filter,
					tryStart: 2, tryEnd: 5,
					filterStart: 5, filterEnd: 7,
					handlerStart: 7, handlerEnd: 9);

				body.Instructions[6].OpCode = OpCodes.Endfilter;
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);

				Assert.IsTrue(result.IsValid);
			}

			// The filter starts at the instruction specified by filterstart and 
			// contains all instructions up to (but not including) that specified by handlerstart.
			[TestMethod]
			public void FilterEndSameAsHandlerStart_Passes() {
				// NOTE: the code for this test is functionally the same as the code in these tests
				var body = GetMethod();

				AddProtectedBlock(body, ExceptionHandlerType.Filter,
					tryStart: 2, tryEnd: 5,
					filterStart: 5, filterEnd: 7,
					handlerStart: 7, handlerEnd: 9);

				body.Instructions[6].OpCode = OpCodes.Endfilter;
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);

				Assert.IsTrue(result.IsValid);
			}

			// The lexically last instruction in the filter must be endfilter.
			[TestMethod]
			public void FirstInstructionInFilterIsEndFilter_Fails() {
				var body = GetMethod();

				var handler = new ExceptionHandler(ExceptionHandlerType.Filter) {
					TryStart = body.Instructions[2],
					TryEnd = body.Instructions[5],
					FilterStart = body.Instructions[5],
					HandlerStart = body.Instructions[9],
					HandlerEnd = body.Instructions[11]
				};

				body.Instructions[5].OpCode = OpCodes.Endfilter;
				body.ExceptionHandlers.Add(handler);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);

				Assert.IsFalse(result.IsValid);
				StringAssert.StartsWith(result.ErrorMessages[0], "The last instruction in filter");
			}

			[TestMethod]
			public void MiddleInstructionInFilterIsEndFilter_Fails() {
				var body = GetMethod();

				var handler = new ExceptionHandler(ExceptionHandlerType.Filter) {
					TryStart = body.Instructions[2],
					TryEnd = body.Instructions[5],
					FilterStart = body.Instructions[5],
					HandlerStart = body.Instructions[9],
					HandlerEnd = body.Instructions[11]
				};

				body.Instructions[7].OpCode = OpCodes.Endfilter;
				body.ExceptionHandlers.Add(handler);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);

				Assert.IsFalse(result.IsValid);
				StringAssert.StartsWith(result.ErrorMessages[0], "The last instruction in filter");
			}

			[TestMethod]
			public void LastInstructionInFilterIsEndFilter_Passes() {
				// the code for this test is functionally the same as the code in these tests
				//
				// FilterStartBeforeHandlerStart_Passes()
				// HandlerRegionIsRequired_FilterHandler_Passes()
			}

			// No two regions (protected block, filter, handler region) of a single exception entry
			// may overlap with one another.
			// Note: there are no success tests for this requirement because they would be the
			// same as FilterStartBeforeHandlerStart_Passes(). rather, each test includes all
			// three regions of an exception entry, but only the two under test overlap.
			// in addition, the kind of handler region is varied between tests to make sure that
			// the kind of handler region does not affect the validity of these tests
			[TestMethod]
			public void ProtectedBlockAndFilterOverlap_AtStartOfProtectedBlock_Fails() {
				// a distinct test for this functionality is not needed.
				// because of the ordering of the validation checks in VerifyProtectedBlocks(),
				// the FilterEndBeforeHandlerStart_Fails() will catch this test's scenario
				// before the validation checks for this situtation are run
			}

			[TestMethod]
			public void ProtectedBlockAndFilterOverlap_AtEndOfProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Filter,
					tryStart: 2, tryEnd: 5,
					filterStart: 4, filterEnd: 11,
					handlerStart: 11, handlerEnd: 13);
			}

			[TestMethod]
			public void ProtectedBlockAndFilterOverlap_ContainedWithinProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Filter,
					tryStart: 2, tryEnd: 13,
					filterStart: 5, filterEnd: 11,
					handlerStart: 11, handlerEnd: 13);
			}

			[TestMethod]
			public void ProtectedBlockAndFilterOverlap_CoincidentWithProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Filter,
					tryStart: 2, tryEnd: 5,
					filterStart: 2, filterEnd: 5,
					handlerStart: 5, handlerEnd: 13);
			}

			[TestMethod]
			public void ProtectedBlockAndCatchHandlerOverlap_AtStartOfProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Catch,
					tryStart: 5, tryEnd: 7,
					handlerStart: 3, handlerEnd: 6);
			}

			[TestMethod]
			public void ProtectedBlockAndCatchHandlerOverlap_AtEndOfProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Catch,
					tryStart: 2, tryEnd: 5,
					handlerStart: 4, handlerEnd: 7);
			}

			[TestMethod]
			public void ProtectedBlockAndCatchHandlerOverlap_ContainedWithinProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Catch,
					tryStart: 2, tryEnd: 11,
					handlerStart: 5, handlerEnd: 7);
			}

			[TestMethod]
			public void ProtectedBlockAndCatchHandlerOverlap_CoincidentWithProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Catch,
					tryStart: 2, tryEnd: 5,
					handlerStart: 2, handlerEnd: 5);
			}

			[TestMethod]
			public void ProtectedBlockAndCatchHandlerAdjacent_ProtectedBlockPreceeds_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Catch,
					tryStart: 2, tryEnd: 5,
					handlerStart: 5, handlerEnd: 11);
			}

			[TestMethod]
			public void ProtectedBlockAndCatchHandlerAdjacent_ProtectedBlockFollows_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Catch,
					tryStart: 11, tryEnd: 15,
					handlerStart: 5, handlerEnd: 11);
			}

			[TestMethod]
			public void ProtectedBlockAndCatchHandlerNotAdjacent_ProtectedBlockPreceeds_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Catch,
					tryStart: 2, tryEnd: 5,
					handlerStart: 9, handlerEnd: 11);
			}

			[TestMethod]
			public void ProtectedBlockAndCatchHandlerNotAdjacent_ProtectedBlockFollows_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Catch,
					tryStart: 9, tryEnd: 11,
					handlerStart: 5, handlerEnd: 7);
			}

			[TestMethod]
			public void ProtectedBlockAndFaultHandlerOverlap_AtStartOfProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Fault, tryStart: 5, tryEnd: 7, handlerStart: 3, handlerEnd: 6);
			}
			[TestMethod]
			public void ProtectedBlockAndFaultHandlerOverlap_AtEndOfProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Fault, tryStart: 2, tryEnd: 5, handlerStart: 4, handlerEnd: 7);
			}
			[TestMethod]
			public void ProtectedBlockAndFaultHandlerOverlap_ContainedWithinProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Fault, tryStart: 2, tryEnd: 11, handlerStart: 5, handlerEnd: 7);
			}
			[TestMethod]
			public void ProtectedBlockAndFaultHandlerOverlap_CoincidentWithProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Fault, tryStart: 2, tryEnd: 5, handlerStart: 2, handlerEnd: 5);
			}
			[TestMethod]
			public void ProtectedBlockAndFaultHandlerAdjacent_ProtectedBlockPreceeds_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Fault, tryStart: 2, tryEnd: 5, handlerStart: 5, handlerEnd: 11);
			}
			[TestMethod]
			public void ProtectedBlockAndFaultHandlerAdjacent_ProtectedBlockFollows_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Fault, tryStart: 11, tryEnd: 15, handlerStart: 5, handlerEnd: 11);
			}
			[TestMethod]
			public void ProtectedBlockAndFaultHandlerNotAdjacent_ProtectedBlockPreceeds_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Fault, tryStart: 2, tryEnd: 5, handlerStart: 9, handlerEnd: 11);
			}
			[TestMethod]
			public void ProtectedBlockAndFaultHandlerNotAdjacent_ProtectedBlockFollows_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Fault, tryStart: 9, tryEnd: 11, handlerStart: 5, handlerEnd: 7);
			}
			[TestMethod]
			public void ProtectedBlockAndFinallyHandlerOverlap_AtStartOfProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 7, handlerStart: 3, handlerEnd: 6);
			}
			[TestMethod]
			public void ProtectedBlockAndFinallyHandlerOverlap_AtEndOfProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Finally, tryStart: 2, tryEnd: 5, handlerStart: 4, handlerEnd: 7);
			}
			[TestMethod]
			public void ProtectedBlockAndFinallyHandlerOverlap_ContainedWithinProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Finally, tryStart: 2, tryEnd: 11, handlerStart: 5, handlerEnd: 7);
			}
			[TestMethod]
			public void ProtectedBlockAndFinallyHandlerOverlap_CoincidentWithProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Finally, tryStart: 2, tryEnd: 5, handlerStart: 2, handlerEnd: 5);
			}
			[TestMethod]
			public void ProtectedBlockAndFinallyHandlerAdjacent_ProtectedBlockPreceeds_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Finally, tryStart: 2, tryEnd: 5, handlerStart: 5, handlerEnd: 11);
			}
			[TestMethod]
			public void ProtectedBlockAndFinallyHandlerAdjacent_ProtectedBlockFollows_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Finally, tryStart: 11, tryEnd: 15, handlerStart: 5, handlerEnd: 11);
			}
			[TestMethod]
			public void ProtectedBlockAndFinallyHandlerNotAdjacent_ProtectedBlockPreceeds_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Finally, tryStart: 2, tryEnd: 5, handlerStart: 9, handlerEnd: 11);
			}
			[TestMethod]
			public void ProtectedBlockAndFinallyHandlerNotAdjacent_ProtectedBlockFollows_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Finally, tryStart: 9, tryEnd: 11, handlerStart: 5, handlerEnd: 7);
			}

			[TestMethod]
			public void ProtectedBlockAndFilterHandlerOverlap_AtStartOfProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Filter,
					tryStart: 5, tryEnd: 11,
					filterStart: 1, filterEnd: 3,
					handlerStart: 3, handlerEnd: 6);
			}

			[TestMethod]
			public void ProtectedBlockAndFilterHandlerOverlap_AtEndOfProtectedBlock_Fails() {
				// Note: the ProtectedBlockAndFilterHandlerOverlap_*() tests are not needed
				// because of the rule that states the filter code must be before the filter handler code
			}

			[TestMethod]
			public void ProtectedBlockAndFilterHandlerOverlap_ContainedWithinProtectedBlock_Fails() {
				// Note: the ProtectedBlockAndFilterHandlerOverlap_*() tests are not needed
				// because of the rule that states the filter code must be before the filter handler code
			}

			[TestMethod]
			public void ProtectedBlockAndFilterHandlerOverlap_CoincidentWithProtectedBlock_Fails() {
				ProtectedBlockHelper(false, ExceptionHandlerType.Filter,
					tryStart: 3, tryEnd: 6,
					filterStart: 1, filterEnd: 3,
					handlerStart: 3, handlerEnd: 6);
			}

			[TestMethod]
			public void ProtectedBlockAndFilterHandlerAdjacent_ProtectedBlockPreceeds_Passes() {
				// Note: this test is not needed
				// because of the rule that states the filter code must be before the filter handler code
			}

			[TestMethod]
			public void ProtectedBlockAndFilterHandlerAdjacent_ProtectedBlockFollows_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Filter,
					tryStart: 6, tryEnd: 8,
					filterStart: 1, filterEnd: 3,
					handlerStart: 3, handlerEnd: 6);
			}
			[TestMethod]
			public void ProtectedBlockAndFilterHandlerNotAdjacent_ProtectedBlockPreceeds_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Filter,
					tryStart: 3, tryEnd: 4,
					filterStart: 7, filterEnd: 9,
					handlerStart: 9, handlerEnd: 11);
			}
			[TestMethod]
			public void ProtectedBlockAndFilterHandlerNotAdjacent_ProtectedBlockFollows_Passes() {
				ProtectedBlockHelper(true, ExceptionHandlerType.Filter,
					tryStart: 8, tryEnd: 12,
					filterStart: 1, filterEnd: 3,
					handlerStart: 3, handlerEnd: 6);
			}

			// For every pair of exception entries in an exception set, one of the following must be true: 
			// *  They nest: all three regions of one entry shall be within a single region of the other entry, with 
			// the further restriction that the enclosing region shall not be a filter. [Note: Functions called from 
			// within a filter can contain exception handling. end note] 
			// *  They are disjoint: all six regions of the two entries are pairwise-disjoint (no addresses overlap).  
			// *  They mutually protect: the protected blocks are the same and the other regions are pairwise-
			// disjoint. In this case, all handlers shall be either catch handlers or filtered handlers. The 
			// precedence of the handler regions is determined by their ordering in the Exception Handler Table (Partition II).

			// regions nest - pass 
			// -- region within try (starts coincident, ends coincident, neither coincident)
			// -- region within handler (catch, finally, fault, filter)
			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsCatchContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsCatchContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsCatchContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsCatchContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsCatchContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsCatchContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsCatchContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 3, filterStart: 3, filterEnd: 5, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsCatchContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 1, tryEnd: 3, filterStart: 3, filterEnd: 5, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsCatchContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 1, tryEnd: 3, filterStart: 3, filterEnd: 5, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsCatchContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsCatchContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsCatchContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsFaultContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsFaultContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsFaultContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsFaultContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsFaultContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsFaultContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsFaultContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 3, filterStart: 3, filterEnd: 5, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsFaultContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 1, tryEnd: 3, filterStart: 3, filterEnd: 5, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsFaultContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 1, tryEnd: 3, filterStart: 3, filterEnd: 5, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsFaultContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsFaultContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsFaultContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsFilterContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 10, filterStart: 10, filterEnd: 13, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsFilterContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 10, filterStart: 10, filterEnd: 13, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsFilterContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 10, filterStart: 10, filterEnd: 13, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsFilterContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 10, filterStart: 10, filterEnd: 13, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsFilterContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 10, filterStart: 10, filterEnd: 13, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsFilterContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 10, filterStart: 10, filterEnd: 13, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsFilterContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 10, filterStart: 10, filterEnd: 13, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 3, filterStart: 3, filterEnd: 5, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsFilterContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 10, filterStart: 10, filterEnd: 13, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 1, tryEnd: 3, filterStart: 3, filterEnd: 5, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsFilterContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 10, filterStart: 10, filterEnd: 13, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 1, tryEnd: 3, filterStart: 3, filterEnd: 5, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsFilterContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 10, filterStart: 10, filterEnd: 13, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsFilterContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 10, filterStart: 10, filterEnd: 13, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsFilterContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 10, filterStart: 10, filterEnd: 13, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsFinallyContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsFinallyContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsFinallyContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsFinallyContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsFinallyContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsFinallyContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsFinallyContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 3, filterStart: 3, filterEnd: 5, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsFinallyContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 1, tryEnd: 3, filterStart: 3, filterEnd: 5, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsFinallyContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 1, tryEnd: 3, filterStart: 3, filterEnd: 5, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_StartsCoincident_ContainingIsFinallyContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_EndsCoincident_ContainingIsFinallyContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 10);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinProtectedBlock_NeitherCoincident_ContainingIsFinallyContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 10, handlerStart: 13, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 1, tryEnd: 3, handlerStart: 5, handlerEnd: 8);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			//

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsCatchContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 4, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsCatchContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsCatchContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsCatchContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 4, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsCatchContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsCatchContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsCatchContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 4, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsCatchContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 5, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsCatchContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 5, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsCatchContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 4, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsCatchContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsCatchContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsFaultContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 4, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsFaultContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsFaultContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsFaultContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 4, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsFaultContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsFaultContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsFaultContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 4, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsFaultContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 5, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsFaultContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 5, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsFaultContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 4, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsFaultContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsFaultContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsFilterContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 4, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 4, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsFilterContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 4, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsFilterContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 4, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsFilterContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 4, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 4, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsFilterContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 4, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsFilterContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 4, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsFilterContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 4, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 4, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsFilterContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 4, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 5, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsFilterContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 4, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 5, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsFilterContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 4, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 4, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsFilterContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 4, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsFilterContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 4, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsFinallyContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 4, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsFinallyContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsFinallyContainedIsCatch_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsFinallyContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 4, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsFinallyContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsFinallyContainedIsFault_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsFinallyContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 4, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsFinallyContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 5, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsFinallyContainedIsFilter_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 5, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_StartsCoincident_ContainingIsFinallyContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 4, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_EndsCoincident_ContainingIsFinallyContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 16);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			[TestMethod]
			public void NestedRegions_WithinHandler_NeitherCoincident_ContainingIsFinallyContainedIsFinally_Passes() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 2, handlerStart: 4, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
			}

			// regions nest - failures
			// -- region within filter code
			// -- try partially in different region
			// -- try wholly in different region
			// -- filter partially in different region -- dont' need, it implies the handler is in a different region
			// -- filter wholly in different region -- don't need, it implies the handler is in a different region
			// -- handler (catch, finally, fault, filter) partially in different region
			// -- handler (catch, finally, fault, filter) wholly in different region

			// -- the region is within filter code -- fails

			[TestMethod]
			public void NestedRegions_WithinFilter_StartsCoincident_ContainingIsFilterContainedIsCatch_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 14, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 2, tryEnd: 7, handlerStart: 9, handlerEnd: 12);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_WithinFilter_EndsCoincident_ContainingIsFilterContainedIsCatch_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 14, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 3, tryEnd: 7, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_WithinFilter_NeitherCoincident_ContainingIsFilterContainedIsCatch_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 14, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 3, tryEnd: 7, handlerStart: 9, handlerEnd: 12);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_WithinFilter_StartsCoincident_ContainingIsFilterContainedIsFault_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 14, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 2, tryEnd: 7, handlerStart: 9, handlerEnd: 12);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_WithinFilter_EndsCoincident_ContainingIsFilterContainedIsFault_Fails() {
				var body = GetMethod();
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 3, tryEnd: 7, handlerStart: 9, handlerEnd: 14);

				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 14, handlerStart: 14, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_WithinFilter_NeitherCoincident_ContainingIsFilterContainedIsFault_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 14, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 3, tryEnd: 7, handlerStart: 9, handlerEnd: 12);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_WithinFilter_StartsCoincident_ContainingIsFilterContainedIsFilter_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 14, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 2, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 12);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_WithinFilter_EndsCoincident_ContainingIsFilterContainedIsFilter_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 14, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 3, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 14);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_WithinFilter_NeitherCoincident_ContainingIsFilterContainedIsFilter_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 14, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 3, tryEnd: 7, filterStart: 7, filterEnd: 9, handlerStart: 9, handlerEnd: 12);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_WithinFilter_StartsCoincident_ContainingIsFilterContainedIsFinally_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 14, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 2, tryEnd: 7, handlerStart: 9, handlerEnd: 12);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_WithinFilter_EndsCoincident_ContainingIsFilterContainedIsFinally_Fails() {
				var body = GetMethod();

				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 3, tryEnd: 7, handlerStart: 9, handlerEnd: 14);

				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 14, handlerStart: 14, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_WithinFilter_NeitherCoincident_ContainingIsFilterContainedIsFinally_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 2, filterStart: 2, filterEnd: 14, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 3, tryEnd: 7, handlerStart: 9, handlerEnd: 12);
				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			// -- try partially in different region - fails

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockBeginsBeforeContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 2, tryEnd: 9, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 1, tryEnd: 7, handlerStart: 9, handlerEnd: 12);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockEndsAfterContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 2, tryEnd: 9, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 4, tryEnd: 10, handlerStart: 10, handlerEnd: 12);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockBeginsBeforeContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 2, tryEnd: 7, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 4, handlerEnd: 6);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockBeginsBeforeContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 2, tryEnd: 7, filterStart: 7, filterEnd: 14, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 4, handlerEnd: 6);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockBeginsBeforeContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 2, tryEnd: 7, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 4, handlerEnd: 6);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockBeginsBeforeContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 2, tryEnd: 7, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 4, handlerEnd: 6);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockEndsAfterContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 3, handlerStart: 7, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 12, tryEnd: 17, handlerStart: 8, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockEndsAfterContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 3, filterStart: 3, filterEnd: 7, handlerStart: 7, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 12, tryEnd: 17, handlerStart: 8, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockEndsAfterContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 3, handlerStart: 7, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 12, tryEnd: 17, handlerStart: 8, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockEndsAfterContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 3, handlerStart: 7, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 12, tryEnd: 17, handlerStart: 8, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			//
			// -- try wholly in different region -- fails
			//

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockAfterContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 6, handlerStart: 10, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 7, tryEnd: 9, handlerStart: 1, handlerEnd: 5);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockAfterContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 12);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 13, tryEnd: 16, handlerStart: 8, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockAfterContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 12);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 13, tryEnd: 16, handlerStart: 8, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockAfterContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 12);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 13, tryEnd: 16, handlerStart: 8, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockAfterContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 4, filterEnd: 7, handlerStart: 7, handlerEnd: 12);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 13, tryEnd: 16, handlerStart: 8, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockBeforeContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 6, tryEnd: 10, handlerStart: 10, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 1, tryEnd: 5, handlerStart: 7, handlerEnd: 9);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockBeforeContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 8, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 7, handlerStart: 10, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockBeforeContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 8, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 7, handlerStart: 10, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockBeforeContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 8, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 7, handlerStart: 10, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedProtectedBlockBeforeContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 9, filterEnd: 11, handlerStart: 11, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 7, handlerStart: 12, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			//
			// -- handler (catch, finally, fault, filter) partially in different region - fails
			//

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerEndsAfterContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 9, handlerStart: 12, handlerEnd: 15);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 1, tryEnd: 3, handlerStart: 7, handlerEnd: 11);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerEndsAfterContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 9, handlerStart: 12, handlerEnd: 15);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 1, tryEnd: 3, handlerStart: 7, handlerEnd: 11);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerEndsAfterContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 9, handlerStart: 12, handlerEnd: 15);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 1, tryEnd: 3, handlerStart: 7, handlerEnd: 11);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerEndsAfterContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 9, handlerStart: 12, handlerEnd: 15);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 1, tryEnd: 3, filterStart: 3, filterEnd: 7, handlerStart: 7, handlerEnd: 11);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerStartsBeforeContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 12, handlerStart: 12, handlerEnd: 15);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 9, tryEnd: 11, handlerStart: 3, handlerEnd: 8);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerStartsBeforeContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 12, handlerStart: 12, handlerEnd: 15);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 9, tryEnd: 11, handlerStart: 3, handlerEnd: 8);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerStartsBeforeContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 12, handlerStart: 12, handlerEnd: 15);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 9, tryEnd: 11, handlerStart: 3, handlerEnd: 8);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerStartsBeforeContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 5, tryEnd: 12, handlerStart: 12, handlerEnd: 15);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 9, tryEnd: 11, filterStart: 0, filterEnd: 3, handlerStart: 3, handlerEnd: 8);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerEndsAfterContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 8, tryEnd: 10, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerEndsAfterContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 8, tryEnd: 10, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerEndsAfterContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 8, tryEnd: 10, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerEndsAfterContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 8, tryEnd: 10, filterStart: 10, filterEnd: 12, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerEndsAfterContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 8, tryEnd: 10, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerEndsAfterContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 8, tryEnd: 10, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerEndsAfterContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 8, tryEnd: 10, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerEndsAfterContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 8, tryEnd: 10, filterStart: 10, filterEnd: 12, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerEndsAfterContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 8, tryEnd: 10, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerEndsAfterContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 8, tryEnd: 10, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerEndsAfterContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 8, tryEnd: 10, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerEndsAfterContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 8, tryEnd: 10, filterStart: 10, filterEnd: 12, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerEndsAfterContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 4, filterEnd: 7, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 8, tryEnd: 10, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerEndsAfterContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 4, filterEnd: 7, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 8, tryEnd: 10, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerEndsAfterContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 4, filterEnd: 7, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 8, tryEnd: 10, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerEndsAfterContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 4, filterEnd: 7, handlerStart: 7, handlerEnd: 13);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 8, tryEnd: 10, filterStart: 10, filterEnd: 12, handlerStart: 12, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerStartsBeforeContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 16, tryEnd: 18, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerStartsBeforeContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 16, tryEnd: 18, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerStartsBeforeContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 16, tryEnd: 18, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerStartsBeforeContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 16, tryEnd: 18, filterStart: 5, filterEnd: 7, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerStartsBeforeContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 16, tryEnd: 18, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerStartsBeforeContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 16, tryEnd: 18, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerStartsBeforeContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 16, tryEnd: 18, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerStartsBeforeContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 16, tryEnd: 18, filterStart: 5, filterEnd: 7, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerStartsBeforeContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 16, tryEnd: 18, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerStartsBeforeContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 16, tryEnd: 18, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerStartsBeforeContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 16, tryEnd: 18, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerStartsBeforeContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 16, tryEnd: 18, filterStart: 5, filterEnd: 7, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerStartsBeforeContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 12, filterEnd: 14, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 16, tryEnd: 18, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerStartsBeforeContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 12, filterEnd: 14, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 16, tryEnd: 18, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerStartsBeforeContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 12, filterEnd: 14, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 16, tryEnd: 18, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerStartsBeforeContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 12, filterEnd: 14, handlerStart: 14, handlerEnd: 19);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 16, tryEnd: 18, filterStart: 5, filterEnd: 7, handlerStart: 7, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}



			//
			// -- handler (catch, finally, fault, filter) wholly in different region
			//

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerAfterContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 6, handlerStart: 9, handlerEnd: 11);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 1, tryEnd: 5, handlerStart: 12, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerAfterContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 6, handlerStart: 9, handlerEnd: 11);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 1, tryEnd: 5, handlerStart: 12, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerAfterContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 6, handlerStart: 9, handlerEnd: 11);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 1, tryEnd: 5, handlerStart: 12, handlerEnd: 15);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerAfterContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 6, handlerStart: 9, handlerEnd: 11);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 1, tryEnd: 5, filterStart: 12, filterEnd: 14, handlerStart: 14, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerBeforeContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 6, tryEnd: 12, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 7, tryEnd: 11, handlerStart: 0, handlerEnd: 5);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerBeforeContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 6, tryEnd: 12, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 7, tryEnd: 11, handlerStart: 0, handlerEnd: 5);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerBeforeContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 6, tryEnd: 12, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 7, tryEnd: 11, handlerStart: 0, handlerEnd: 5);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerBeforeContainingProtectedBlock_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 6, tryEnd: 12, handlerStart: 14, handlerEnd: 16);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 7, tryEnd: 11, filterStart: 0, filterEnd: 3, handlerStart: 3, handlerEnd: 5);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerAfterContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 7, tryEnd: 9, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerAfterContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 7, tryEnd: 9, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerAfterContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 7, tryEnd: 9, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerAfterContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 7, tryEnd: 9, filterStart: 11, filterEnd: 13, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerAfterContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 7, tryEnd: 9, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerAfterContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 7, tryEnd: 9, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerAfterContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 7, tryEnd: 9, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerAfterContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 7, tryEnd: 9, filterStart: 11, filterEnd: 13, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerAfterContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 7, tryEnd: 9, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerAfterContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 7, tryEnd: 9, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerAfterContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 7, tryEnd: 9, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerAfterContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 7, tryEnd: 9, filterStart: 11, filterEnd: 13, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerAfterContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 4, filterEnd: 6, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 7, tryEnd: 9, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerAfterContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 4, filterEnd: 6, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 7, tryEnd: 9, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerAfterContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 4, filterEnd: 6, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 7, tryEnd: 9, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerAfterContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 4, filterEnd: 6, handlerStart: 6, handlerEnd: 10);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 7, tryEnd: 9, filterStart: 11, filterEnd: 13, handlerStart: 13, handlerEnd: 16);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerBeforeContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 15, tryEnd: 17, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerBeforeContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 15, tryEnd: 17, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerBeforeContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 15, tryEnd: 17, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerBeforeContainingCatchHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 15, tryEnd: 17, filterStart: 5, filterEnd: 7, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerBeforeContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 15, tryEnd: 17, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerBeforeContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 15, tryEnd: 17, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerBeforeContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 15, tryEnd: 17, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerBeforeContainingFaultHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 15, tryEnd: 17, filterStart: 5, filterEnd: 7, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerBeforeContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 15, tryEnd: 17, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerBeforeContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 15, tryEnd: 17, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerBeforeContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 15, tryEnd: 17, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerBeforeContainingFinallyHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 0, tryEnd: 4, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 15, tryEnd: 17, filterStart: 5, filterEnd: 7, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedCatchHandlerBeforeContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 12, filterEnd: 14, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Catch, tryStart: 15, tryEnd: 17, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFaultHandlerBeforeContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 12, filterEnd: 14, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Fault, tryStart: 15, tryEnd: 17, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFinallyHandlerBeforeContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 12, filterEnd: 14, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Finally, tryStart: 15, tryEnd: 17, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}

			[TestMethod]
			public void NestedRegions_ContainedFilterHandlerBeforeContainingFilterHandler_Fails() {
				var body = GetMethod();
				// this is the containing one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 0, tryEnd: 4, filterStart: 12, filterEnd: 14, handlerStart: 14, handlerEnd: 18);
				// and this is the contained one
				AddProtectedBlock(body, ExceptionHandlerType.Filter, tryStart: 15, tryEnd: 17, filterStart: 5, filterEnd: 7, handlerStart: 7, handlerEnd: 10);

				var result = ILValidator_Privates.VerifyProtectedBlocks(body);
				Assert.IsFalse(result.IsValid, string.Join(Environment.NewLine, result.ErrorMessages));
				StringAssert.StartsWith(result.ErrorMessages[0], "The exception entry pair");
				StringAssert.EndsWith(result.ErrorMessages[0], "neither nest, nor are disjoint, nor mutually protect.");
			}


			// regions are disjoint - pass
			// the tests in this region were created by taking all location combinations of 2 exception entries,
			// and combining that with all combinations of the handler type

			#region valid disjoint region tests

			[TestMethod]
			public void DisjointRegions_Permutation_1_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_2_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_3_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_4_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_5_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_6_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_7_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_8_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_9_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_10_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_11_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_12_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_13_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_14_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_15_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_16_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_17_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_18_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_19_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_20_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_21_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_22_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_23_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_24_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_25_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_26_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_27_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_28_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_29_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_30_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_31_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_32_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_33_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_34_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_35_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_36_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_37_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_38_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_39_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_40_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_41_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_42_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_43_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_44_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_45_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_46_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_47_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_48_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_49_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_50_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_51_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_52_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_53_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_54_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_55_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_56_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_57_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_58_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_59_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_60_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_61_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_62_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_63_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_64_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_65_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_66_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_67_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_68_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_69_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_70_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_71_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_72_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_73_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_74_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_75_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_76_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_77_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_78_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_79_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_80_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_81_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_82_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_83_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_84_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_85_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_86_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_87_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_88_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_89_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_90_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_91_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_92_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_93_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_94_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_95_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_96_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_97_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_98_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_99_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_100_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_101_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_102_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_103_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_104_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_105_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_106_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_107_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_108_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_109_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_110_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_111_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_112_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_113_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_114_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_115_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_116_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_117_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_118_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_119_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_120_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_121_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_122_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_123_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_124_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_125_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_126_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_127_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_128_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_129_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_130_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_131_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_132_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_133_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_134_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_135_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_136_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_137_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_138_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_139_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_140_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_141_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_142_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_143_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_144_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_145_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_146_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_147_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_148_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_149_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_150_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_151_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_152_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_153_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_154_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_155_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_156_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_157_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_158_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_159_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_160_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_161_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_162_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_163_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_164_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_165_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_166_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_167_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_168_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_169_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_170_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_171_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_172_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_173_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_174_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_175_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_176_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_177_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_178_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_179_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_180_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_181_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_182_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_183_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_184_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_185_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_186_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_187_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_188_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_189_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_190_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_191_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_192_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_193_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_194_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_195_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_196_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_197_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_198_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_199_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_200_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_201_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_202_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_203_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_204_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_205_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_206_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 7, 9, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_207_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_208_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_209_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_210_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_211_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_212_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_213_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_214_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_215_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_216_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_217_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_218_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_219_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_220_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_221_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_222_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_223_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_224_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_225_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_226_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_227_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_228_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_229_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_230_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_231_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_232_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_233_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_234_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_235_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_236_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_237_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_238_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 2, 4, 15, 17, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_239_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_240_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 17, 19);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_241_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_242_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_243_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_244_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_245_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_246_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_247_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_248_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_249_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_250_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_251_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_252_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_253_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_254_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_255_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_256_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_257_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_258_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_259_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_260_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_261_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_262_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_263_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_264_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_265_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_266_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_267_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_268_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_269_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_270_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_271_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_272_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_273_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_274_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_275_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_276_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_277_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_278_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_279_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_280_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 12, 14, 15, 17, 17, 19,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_281_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_282_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_283_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_284_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_285_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_286_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_287_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_288_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 17, 19,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_289_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_290_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_291_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_292_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_293_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_294_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_295_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_296_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_297_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_298_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_299_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_300_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_301_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_302_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 7, 9, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_303_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_304_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_305_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_306_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_307_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_308_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_309_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_310_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_311_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_312_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 0, 2, 2, 4,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_313_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_314_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_315_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_316_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_317_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_318_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Filter, 12, 14, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_319_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_320_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 2, 4,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_321_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_322_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_323_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_324_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_325_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_326_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_327_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_328_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_329_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_330_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_331_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_332_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_333_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_334_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 2, 4, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_335_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_336_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_337_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_338_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_339_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_340_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_341_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_342_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_343_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_344_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 5, 7, 7, 9,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_345_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_346_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_347_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_348_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_349_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Catch, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_350_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Filter, 12, 14, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_351_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_352_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 7, 9,
					ExceptionHandlerType.Fault, 12, 14, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_353_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_354_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_355_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_356_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_357_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_358_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_359_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_360_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_361_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_362_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_363_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_364_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_365_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_366_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 2, 4, 5, 7, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_367_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_368_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 2, 4, -1, -1, 7, 9);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_369_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_370_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_371_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_372_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_373_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_374_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_375_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_376_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 17, 19, 10, 12, 12, 14,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_377_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_378_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_379_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_380_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Finally, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_381_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Catch, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_382_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Filter, 7, 9, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_383_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Finally, 7, 9, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_Permutation_384_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Fault, 17, 19, -1, -1, 12, 14,
					ExceptionHandlerType.Fault, 7, 9, -1, -1, 2, 4);
			}



			#endregion

			// regions are disjoint - failures
			// -- trys overlap each other and catch, filter, finally, or fault handler

			#region invalid disjoint region tests

			[TestMethod]
			public void DisjointRegions_ProtectedBlockStartsBeforeOtherProtectedBlock_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Finally, 3, 7, -1, -1, 13, 17,
					ExceptionHandlerType.Finally, 2, 4, -1, -1, 21, 23);
			}

			[TestMethod]
			public void DisjointRegions_CatchStartsBeforeOtherProtectedBlock_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Finally, 3, 7, -1, -1, 13, 17,
					ExceptionHandlerType.Catch, 21, 23, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_FilterStartsBeforeOtherProtectedBlock_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Finally, 3, 7, -1, -1, 13, 17,
					ExceptionHandlerType.Filter, 21, 23, 0, 2, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_FinallyStartsBeforeOtherProtectedBlock_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Finally, 3, 7, -1, -1, 13, 17,
					ExceptionHandlerType.Finally, 21, 23, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_FaultStartsBeforeOtherProtectedBlock_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Finally, 3, 7, -1, -1, 13, 17,
					ExceptionHandlerType.Fault, 21, 23, -1, -1, 2, 4);
			}

			[TestMethod]
			public void DisjointRegions_ProtectedBlockStartsBeforeOtherHandler_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Filter, 3, 7, 9, 13, 13, 17,
					ExceptionHandlerType.Finally, 12, 14, -1, -1, 21, 23);
			}

			[TestMethod]
			public void DisjointRegions_CatchStartsBeforeOtherHandler_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Filter, 3, 7, 9, 13, 13, 17,
					ExceptionHandlerType.Catch, 21, 23, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_FilterStartsBeforeOtherHandler_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Filter, 3, 7, 9, 13, 13, 17,
					ExceptionHandlerType.Filter, 21, 23, 10, 12, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_FinallyStartsBeforeOtherHandler_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Filter, 3, 7, 9, 13, 13, 17,
					ExceptionHandlerType.Finally, 21, 23, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_FaultStartsBeforeOtherHandler_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Filter, 3, 7, 9, 13, 13, 17,
					ExceptionHandlerType.Fault, 21, 23, -1, -1, 12, 14);
			}

			[TestMethod]
			public void DisjointRegions_ProtectedBlockEndsAfterOtherProtectedBlock_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Finally, 3, 7, -1, -1, 13, 17,
					ExceptionHandlerType.Finally, 6, 8, -1, -1, 21, 23);
			}

			[TestMethod]
			public void DisjointRegions_CatchEndsAfterOtherProtectedBlock_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Finally, 3, 7, -1, -1, 13, 17,
					ExceptionHandlerType.Catch, 21, 23, -1, -1, 6, 8);
			}

			[TestMethod]
			public void DisjointRegions_FilterEndsAfterOtherProtectedBlock_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Finally, 3, 7, -1, -1, 13, 17,
					ExceptionHandlerType.Filter, 21, 23, 4, 6, 6, 8);
			}

			[TestMethod]
			public void DisjointRegions_FinallyEndsAfterOtherProtectedBlock_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Finally, 3, 7, -1, -1, 13, 17,
					ExceptionHandlerType.Finally, 21, 23, -1, -1, 6, 8);
			}

			[TestMethod]
			public void DisjointRegions_FaultEndsAfterOtherProtectedBlock_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Finally, 3, 7, -1, -1, 13, 17,
					ExceptionHandlerType.Fault, 21, 23, -1, -1, 6, 8);
			}

			[TestMethod]
			public void DisjointRegions_ProtectedBlockEndsAfterOtherHandler_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Filter, 3, 7, 9, 13, 13, 17,
					ExceptionHandlerType.Finally, 16, 18, -1, -1, 21, 23);
			}

			[TestMethod]
			public void DisjointRegions_CatchEndsAfterOtherHandler_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Filter, 3, 7, 9, 13, 13, 17,
					ExceptionHandlerType.Catch, 21, 23, -1, -1, 16, 18);
			}

			[TestMethod]
			public void DisjointRegions_FilterEndsAfterOtherHandler_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Filter, 3, 7, 9, 13, 13, 17,
					ExceptionHandlerType.Filter, 21, 23, 14, 16, 16, 18);
			}

			[TestMethod]
			public void DisjointRegions_FinallyEndsAfterOtherHandler_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Filter, 3, 7, 9, 13, 13, 17,
					ExceptionHandlerType.Finally, 21, 23, -1, -1, 16, 18);
			}

			[TestMethod]
			public void DisjointRegions_FaultEndsAfterOtherHandler_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Filter, 3, 7, 9, 13, 13, 17,
					ExceptionHandlerType.Fault, 21, 23, -1, -1, 16, 18);
			}




			#endregion

			// regions mutually protect - pass
			// -- trys coincident and catch handler
			// -- trys coincident and filter handler

			[TestMethod]
			public void MutuallyProtect_CatchHandlers_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 0, 4, -1, -1, 13, 17,
					ExceptionHandlerType.Catch, 0, 4, -1, -1, 21, 23);
			}

			[TestMethod]
			public void MututallyProtect_FilterHandlers_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Filter, 0, 4, 10, 13, 13, 17,
					ExceptionHandlerType.Filter, 0, 4, 19, 21, 21, 23);
			}

			[TestMethod]
			public void MututallyProtect_CatchAndFilterHandler_Passes() {
				DualRegionTestHelper(true,
					ExceptionHandlerType.Catch, 0, 4, -1, -1, 13, 17,
					ExceptionHandlerType.Filter, 0, 4, 19, 21, 21, 23);
			}

			// regions mututally protect - failures
			// -- trys coincident and finally handler
			// -- trys coincident and fault handler

			[TestMethod]
			public void MututallyProtect_FinallyHandler_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Catch, 0, 4, -1, -1, 13, 17,
					ExceptionHandlerType.Finally, 0, 4, -1, -1, 21, 23);
			}

			[TestMethod]
			public void MutuallyProtect_FaultHandler_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Fault, 0, 4, -1, -1, 13, 17,
					ExceptionHandlerType.Catch, 0, 4, -1, -1, 21, 23);
			}

			[TestMethod]
			public void MututallyProtect_HandlersCoincident_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Catch, 0, 4, -1, -1, 13, 17,
					ExceptionHandlerType.Catch, 0, 4, -1, -1, 13, 17);
			}

			[TestMethod]
			public void MututallyProtect_HandlersOverlap_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Catch, 0, 4, -1, -1, 13, 17,
					ExceptionHandlerType.Finally, 0, 4, -1, -1, 15, 23);
			}

			[TestMethod]
			public void MututallyProtect_HandlersNested_Fails() {
				DualRegionTestHelper(false,
					ExceptionHandlerType.Catch, 0, 4, -1, -1, 13, 17,
					ExceptionHandlerType.Finally, 0, 4, -1, -1, 14, 16);
			}


		}
	}
}
