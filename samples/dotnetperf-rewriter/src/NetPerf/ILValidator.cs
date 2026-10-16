using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;
using Mono.Cecil;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;

namespace NetPerf {
	internal static class ILValidator {

		public struct VerifyResult {
			public readonly bool IsValid;
			public readonly ReadOnlyCollection<string> ErrorMessages;

			public VerifyResult(List<string> errors = null) {
				ErrorMessages = errors == null ? new List<string>().AsReadOnly() : errors.AsReadOnly();
				IsValid = ErrorMessages.Count == 0;
			}
		}

		public static VerifyResult VerifyMethod(MethodDefinition methodDef) {
			var allErrorMessages = new List<string>()
				.Concat(VerifyThrow(methodDef.Body).ErrorMessages)
				.Concat(VerifyRethrow(methodDef.Body).ErrorMessages)
				.Concat(VerifyRet(methodDef.Body).ErrorMessages)
				.Concat(VerifyJmp(methodDef.Body).ErrorMessages)
				.Concat(VerifyEndFilter(methodDef.Body).ErrorMessages)
				.Concat(VerifyEndFinally(methodDef.Body).ErrorMessages)
				.Concat(VerifyLeave(methodDef.Body).ErrorMessages)
				.Concat(VerifyProtectedBlocks(methodDef.Body).ErrorMessages)
				.Concat(VerifyFallThrough(methodDef.Body).ErrorMessages)
				.Concat(VerifyBranches(methodDef.Body).ErrorMessages);


			return new VerifyResult(allErrorMessages.ToList());
		}

		private static bool IsInstructionBetween(Instruction instr, Instruction inclusiveStart, Instruction exclusiveEnd) {
			Contract.Requires(instr != null);

			// a null inclusiveStart means search from the end to the beginning of the method
			// a null exclusiveEnd means search from the inclusiveStart to the end of the method
			if (inclusiveStart == null && exclusiveEnd == null)
				return true;

			if (inclusiveStart == exclusiveEnd)
				return inclusiveStart == instr;

			var getNext = inclusiveStart == null
				? new Func<Instruction, Instruction>(i => i.Previous)
				: new Func<Instruction, Instruction>(i => i.Next);

			var curInstr = inclusiveStart == null
				? exclusiveEnd
				: inclusiveStart;

			var endInstr = exclusiveEnd == null
				? null
				: exclusiveEnd.Previous;

			while (!(curInstr == instr || curInstr == endInstr || curInstr == null)) {
				curInstr = getNext(curInstr);
			}

			return instr == curInstr;
		}

		// protected region (exception handling) rules

		private static VerifyResult VerifyThrow(MethodBody body) {
			// there are no restrictions on the source instruction
			// and there is no target instruction
			return new VerifyResult();
		}

		private static VerifyResult VerifyRethrow(MethodBody body) {

			var errors = new List<string>();
			// find all of the rethrow instructions in the method
			var instrs = from i in body.Instructions
						 where i.OpCode == OpCodes.Rethrow
						 select i;

			foreach (var instr in instrs) {
				// the instruction must be within a catch handler.
				// the handler doesn't have to be the innermost one

				var withinBlocks = from handler in body.ExceptionHandlers
								   where handler.HandlerType == ExceptionHandlerType.Catch
										 && IsInstructionBetween(instr, handler.HandlerStart, handler.HandlerEnd)
								   select handler;

				if (withinBlocks.IsEmpty()) {
					errors.Add(string.Format(
						"rethrow instruction [{0:X4}] is not within a catch handler",
						instr.Offset));
				}
			}


			return new VerifyResult(errors);
		}

		private static VerifyResult VerifyRet(MethodBody body) {
			var errors = VerifyInstrNotInProtectedRegionOrFilterOrHandler(OpCodes.Ret, body);
			return new VerifyResult(errors);
		}

		private static VerifyResult VerifyJmp(MethodBody body) {
			var errors = VerifyInstrNotInProtectedRegionOrFilterOrHandler(OpCodes.Jmp, body);
			return new VerifyResult(errors);
		}

		private static List<string> VerifyInstrNotInProtectedRegionOrFilterOrHandler(OpCode opCode, MethodBody body) {
			var errors = new List<string>();

			var instrs = from i in body.Instructions
						 where i.OpCode == opCode
						 select i;

			foreach (var instr in instrs) {
				// cannot be enclosed in a protected block, filter, or handler
				foreach (var handler in body.ExceptionHandlers) {
					if (IsInstructionBetween(instr, handler.TryStart, handler.TryEnd)) {
						errors.Add(string.Format(
							"{0} instr [{1:X4}] should not be within protected block [{2:X4, 3:X4}]",
							opCode.Name,
							instr.Offset,
							handler.TryStart.Offset,
							handler.TryEnd.Offset));
					}

					if (handler.HandlerType == ExceptionHandlerType.Filter && IsInstructionBetween(instr, handler.FilterStart, handler.FilterEnd.Next)) {
						errors.Add(string.Format(
							"{0} instr [{1:X4}] should not be within filter [{2:X4, 3:X4}]",
							opCode.Name,
							instr.Offset,
							handler.FilterStart.Offset,
							handler.FilterEnd.Offset));
					}

					if (IsInstructionBetween(instr, handler.HandlerStart, handler.HandlerEnd)) {
						errors.Add(string.Format(
								"{0} instr [{1:X4}] should not be within handler [{2:X4, 3:X4}]",
								opCode.Name,
								instr.Offset,
								handler.HandlerStart.Offset,
								handler.HandlerEnd.Offset));
					}

				}

			}

			return errors;
		}

		private static VerifyResult VerifyEndFilter(MethodBody body) {

			var errors = new List<string>();

			var instrs = from i in body.Instructions
						 where i.OpCode == OpCodes.Endfilter
						 select i;

			foreach (var instr in instrs) {
				// Shall appear as the lexically last instruction in the filter.
				// 1) must appear in a filter block (implicitly, can only appear in a single block)
				// 2) must be the last instruction in the filter block

				var x = from handler in body.ExceptionHandlers
						where handler.HandlerType == ExceptionHandlerType.Filter
						&& IsInstructionBetween(instr, handler.FilterStart, handler.FilterEnd)
						select handler;

				if (x.IsEmpty()) {
					errors.Add(string.Format(
						"endfilter instr [{0:X4}] is not within a filter block",
						instr.Offset));
				}
				else {
					if (x.Count() > 1) {
						// this case should never happen unless the method
						// fails several other checks, but in the unlikely
						// event it does happen, we don't want to throw an exception
						errors.Add(string.Format(
								"endfilter instr [{0:X4}] is within more than one filter block",
								instr.Offset));
					}
					else {
						var handler = x.First();
						if (handler.FilterEnd.Previous != instr) {
							errors.Add(string.Format(
								"endfilter instr [{0:X4}] is not the lexically last instruction in filter [{1:X4}, {2:X4}]",
								instr.Offset,
								handler.FilterStart.Offset,
								handler.FilterEnd.Offset));
						}
					}
				}
			}

			return new VerifyResult(errors);
		}

		private static VerifyResult VerifyEndFinally(MethodBody body) {

			var errors = new List<string>();

			var instrs = from i in body.Instructions
						 where i.OpCode == OpCodes.Endfinally
						 select i;


			foreach (var instr in instrs) {
				// The innermost enclosing protected block, filter, or handler shall be a finally or fault handler 
				// 1) has to be immediatly within a finally or fault handler

				// follow the numbers!
				// (1) track every protected region, filter, or handler that the instruction could be in,
				//     and whether the region is a valid location for the instruction
				// (2) flatten the resulting array so it can be processed easier
				// (3) get rid of evey region that the instruction isn't in
				// (4) order the result set so that the narrowest region (the most nested region)
				//     comes back first
				var withinHandlers =
					(from array in
						 (from handler in body.ExceptionHandlers // (1)
						  select new[] {
							  new {
								 within = IsInstructionBetween(instr, handler.TryStart, handler.TryEnd),
								 start = handler.TryStart,
								 end = handler.TryEnd,
								 isValidLoc = false
							 },
							 new {
								 within = handler.HandlerType == ExceptionHandlerType.Filter
									 && IsInstructionBetween(instr, handler.FilterStart, handler.FilterEnd),
								 start = handler.FilterStart,
								 end = handler.FilterEnd,
								 isValidLoc = false
							 },
							 new {
								 within = IsInstructionBetween(instr, handler.HandlerStart, handler.HandlerEnd),
								 start = handler.HandlerStart,
								 end = handler.HandlerEnd,
								 isValidLoc = handler.HandlerType == ExceptionHandlerType.Finally
									 || handler.HandlerType == ExceptionHandlerType.Fault
							 }
						  })
					 from a in array // (2)
					 where a.within // (3)
					 orderby a.start.Offset descending, a.end.Offset ascending // (4)
					 select a).ToList();

				if (withinHandlers.IsEmpty()) {
					// the instruction is also not within a filter or a protected region,
					// but since those are invalid regions anyway, we don't want to
					// imply that they are valid locations by mentioning them in the error message
					errors.Add(string.Format(
							"endfinally instruction [{0:X4}] is not within a handler",
							instr.Offset));
				}
				else {
					var innermost = withinHandlers.First();
					if (!innermost.isValidLoc) {
						errors.Add(string.Format(
							"endfinally instruction [{0:X4}] is not within an innermost finally or fault handler",
							instr.Offset));

					}
				}

			}

			return new VerifyResult(errors);
		}


		private static VerifyResult VerifyLeave(MethodBody body) {

			var errors = new List<string>();

			var instrs = from i in body.Instructions
						 where i.OpCode == OpCodes.Leave || i.OpCode == OpCodes.Leave_S
						 select i;

			foreach (var source in instrs) {
				var target = (Instruction)source.Operand;

				// 1.  If the source is within a filter, fault handler, or finally handler, the target shall be within the same 
				// filter, fault handler, or finally handler.
				var withinHandlers = from handler in body.ExceptionHandlers
									 where
									 (handler.HandlerType != ExceptionHandlerType.Catch && handler.HandlerType != ExceptionHandlerType.Filter
										&& IsInstructionBetween(source, handler.HandlerStart, handler.HandlerEnd)
										&& !IsInstructionBetween(target, handler.HandlerStart, handler.HandlerEnd))
									 ||
									 (handler.HandlerType == ExceptionHandlerType.Filter
										&& IsInstructionBetween(source, handler.FilterStart, handler.FilterEnd)
										&& !IsInstructionBetween(target, handler.FilterStart, handler.FilterEnd))
									 select handler;

				foreach (var handler in withinHandlers) {
					errors.Add(string.Format(
						"{0} instruction [{1:X4}] is within a filter, finally or fault handler, but its target [{2:X4}] is not",
						source.OpCode.Name,
						source.Offset,
						target.Offset));
				}


				//2.  If the source is within a protected block, the target shall be within the same protected block, 
				//within an enclosing protected block, the first instruction of a disjoint protected block, or not 
				//within any protected block.
				var targetWithinBlocks = (from handler in body.ExceptionHandlers
										  where IsInstructionBetween(target, handler.TryStart, handler.TryEnd)
										  select handler).ToList();
				foreach (var handler in body.ExceptionHandlers.Where(h => IsInstructionBetween(source, h.TryStart, h.TryEnd))) {

					// is the target not within any protected block?
					var isTargetValid = false
						|| targetWithinBlocks.IsEmpty();

					// is the target in the same (including nested) protected blocks?
					isTargetValid = isTargetValid
						|| IsInstructionBetween(target, handler.TryStart, handler.TryEnd);

					// is the target within an enclosing protected block?
					var enclosingBlocks = from hh in targetWithinBlocks
										  where IsNestedWithin(
										  hh.TryStart.Offset,
										  hh.TryEnd.Offset,
										  handler.TryStart.Offset,
										  handler.TryEnd.Offset)
										  select hh;
					isTargetValid = isTargetValid
						|| (!IsInstructionBetween(target, handler.TryStart, handler.TryEnd)
							&& enclosingBlocks.Any(b => IsInstructionBetween(target, b.TryStart, b.TryEnd)));

					// is the target the first instruction of a disjoint protected block?
					var disjointBlocks = from hh in targetWithinBlocks
										 where AreDisjoint(
										 handler.TryStart.Offset,
										 handler.TryEnd.Offset,
										 hh.TryStart.Offset,
										 hh.TryEnd.Offset)
										 select hh;
					isTargetValid = isTargetValid
						|| disjointBlocks.Any(b => b.TryStart == target);

					if (!isTargetValid) {
						errors.Add(string.Format(
							"{0} instruction [{1:X4}] is within a protected region, but its target [{2:X4}] " +
							"is not within the same region, a disjoint region, an enclosing region, or no region at all",
							source.OpCode.Name,
							source.Offset,
							target.Offset));
					}
				}

				//3.  If the source is within a catch handler or filtered handler, the target shall be within the same catch 
				//handler or filtered handler, within the associated protected block, within a protected block that 
				//encloses the catch handler or filtered handler, the first instruction of a disjoint protected block, or 
				//not within any protected block.

				withinHandlers = from handler in body.ExceptionHandlers
								 where (handler.HandlerType == ExceptionHandlerType.Catch
										|| handler.HandlerType == ExceptionHandlerType.Filter)
									&& IsInstructionBetween(source, handler.HandlerStart, handler.HandlerEnd)
								 select handler;

				foreach (var handler in withinHandlers) {
					// is the target not within any protected blocks?
					var isTargetValid = false
						|| targetWithinBlocks.IsEmpty();

					// is the target within the same catch or filter handler?
					isTargetValid = isTargetValid
						|| IsInstructionBetween(target, handler.HandlerStart, handler.HandlerEnd);

					// is the target within the associated protected block inclusing a block nested within that one)?
					isTargetValid = isTargetValid
						|| IsInstructionBetween(target, handler.TryStart, handler.TryEnd);

					// is the target within an enclosing protected block?
					var enclosingBlocks = from hh in targetWithinBlocks
										  where IsNestedWithin(
											  hh.TryStart.Offset,
											  hh.TryEnd.Offset,
											  handler.TryStart.Offset,
											  handler.TryEnd.Offset)
										  select hh;
					isTargetValid = isTargetValid
						|| enclosingBlocks.IsNotEmpty();

					// is the target the first instruction of a disjoint protected block
					var disjointBlocks = from hh in targetWithinBlocks
										 where AreDisjoint(
											handler.TryStart.Offset,
											handler.TryEnd.Offset,
											hh.TryStart.Offset,
											hh.TryEnd.Offset)
										 select hh;
					isTargetValid = isTargetValid
						|| disjointBlocks.Any(b => b.TryStart == target);

					if (!isTargetValid) {
						errors.Add(string.Format(
							"{0} instruction [{1:X4}] is within a catch or filtered handler, but its target [{2:X4}] " +
							"is not within the same handler, or within the associated protected block, or within an " +
							"an enclosing protected block, or a disjoint protected block, or no protected block at all",
							source.OpCode.Name,
							source.Offset,
							target.Offset));
					}
				}

				//
				// awesome! all of the source checks are done. have to do the target checks now
				//

				//1.  If the target is within a filter or handler, the source shall be within the same filter or handler. 
				foreach (var handler in body.ExceptionHandlers) {

					if (handler.HandlerType == ExceptionHandlerType.Filter
						&& IsInstructionBetween(target, handler.FilterStart, handler.FilterEnd)
						&& !IsInstructionBetween(source, handler.FilterStart, handler.FilterEnd)) {
						errors.Add(string.Format(
							 "{0} target instruction [{1:X4}] is within filter [{2:X4}, {3:X4}], " +
							 "but {0} [{4:X4}] is not within the same handler",
							 source.OpCode.Name,
							 target.Offset,
							 handler.FilterStart.Offset,
							 handler.FilterEnd.Offset,
							 source.Offset));

					}

					if (IsInstructionBetween(target, handler.HandlerStart, handler.HandlerEnd)
						&& !IsInstructionBetween(source, handler.HandlerStart, handler.HandlerEnd)) {
						errors.Add(string.Format(
							 "{0} target instruction [{1:X4}] is within handler [{2:X4}, {3:X4}], " +
							 "but {0} [{4:X4}] is not within the same handler",
							 source.OpCode.Name,
							 target.Offset,
							 handler.HandlerStart.Offset,
							 handler.HandlerEnd.Offset,
							 source.Offset));
					}
				}


				//2.  If the target is within a protected block, except the first instruction of that protected block, the 
				//source shall be within the same protected block (including a nested one), 
				//or within the associated catch handler or filtered handler (but not a fault or a finally). 
				withinHandlers = from handler in body.ExceptionHandlers
								 where IsInstructionBetween(target, handler.TryStart, handler.TryEnd)
								 && handler.TryStart != target
								 select handler;

				foreach (var handler in withinHandlers) {
					// is the source within the same protected block (including a nested one)?
					var isValidSource = false
						|| IsInstructionBetween(source, handler.TryStart, handler.TryEnd);

					// is the source within the associated catch or filter handler
					//(but not a fault or finally handler)?
					isValidSource = isValidSource
						|| ((handler.HandlerType == ExceptionHandlerType.Catch || handler.HandlerType == ExceptionHandlerType.Filter)
							&& IsInstructionBetween(source, handler.HandlerStart, handler.HandlerEnd));

					if (!isValidSource) {
						errors.Add(string.Format(
							"{0} target instruction [{1:X4}] is within protected region [{2:X4}, {3:X4}] " +
							"(and not the first instruction), but {0} [{4:X4}]is not within the same protected region " +
							"or the associated catch handler or filtered handler",
							source.OpCode.Name,
							target.Offset,
							handler.TryStart.Offset,
							handler.TryEnd.Offset,
							source.Offset
							));
					}

				}

			} // end foreach (var instr in instrs)

			return new VerifyResult(errors);
		}

		private static VerifyResult VerifyProtectedBlocks(MethodBody body) {

			if (!body.HasExceptionHandlers)
				return new VerifyResult();

			var errors = new List<string>();

			//Rule: Each exception entry consists of 
			//* Optional: a type token (the type of exception to be handled) 
			//  or filterstart (the address of the first instruction of the user-supplied filter code)  
			//* Required: protected block
			//* Required: handler region.
			var invalidEntries = body.ExceptionHandlers
				.Where(eh => eh.TryStart == null || eh.TryEnd == null // protected block is required
					|| eh.HandlerStart == null || eh.HandlerEnd == null) // handler region is required
				.ToList();

			foreach (var entry in invalidEntries) {
				errors.Add(string.Format(
					"Invalid exception handler: {0} {1}",
					entry.TryStart == null || entry.TryEnd == null ? "No protected (try) block." : "",
					entry.HandlerStart == null || entry.HandlerEnd == null ? "No handler region." : ""));
			}


			//for the rest of the checks, we just want to consider the
			//exception handlers that passed the check above
			var exceptionEntries = body.ExceptionHandlers.Except(invalidEntries);

			//Rule: If an exception entry contains a filterstart, then filterstart strictly precedes handlerstart. 
			var invalid = from h in exceptionEntries
						  where h.FilterStart != null && h.FilterStart.Offset >= h.HandlerStart.Offset
						  select h;

			foreach (var handler in invalid) {
				errors.Add(string.Format(
					"The filterstart [{0:X4}] for protected block [{1:X4}, {2:X4}] is not located before the handler [{3:X4}]",
					handler.FilterStart.Offset,
					handler.TryStart.Offset,
					handler.TryEnd.Offset,
					handler.HandlerStart.Offset));
			}

			//Rule: The filter starts at the instruction specified by filterstart and
			//      contains all instructions up to (but not including) that specified by handlerstart.
			//NB: Cecil parses the *End properties as one instruction beyond the end of the block they represent,
			//    hence the conparison to FilterEnd, and not FilterEnd.Next
			invalid = from h in exceptionEntries
					  where h.FilterEnd != null && h.FilterEnd != h.HandlerStart
					  select h;
			foreach (var handler in invalid) {
				errors.Add(string.Format(
					"The filter for exception entry [{0:X4}, {1:X4}] does not " +
					"contain all instructions up to (but not including) that specified by handlerstart.",
					handler.TryStart.Offset,
					handler.TryEnd.Offset));
			}

			//NB: the following verification check is a duplication
			//NB: of a rule enforced for the endfilter instruction, but it is included here
			//NB: so that a user can run just this method without also running the endfilter checks
			//Rule: The lexically last instruction in the filter must be endfilter.
			//NB: Cecil parses the *End properties as one instruction beyond the end of the block they represent,
			//    hence the opcode check on FilterEnd.Previous instead of FilterEnd
			invalid = from h in exceptionEntries
					  where h.FilterEnd != null
						&& h.FilterEnd.Previous.OpCode != OpCodes.Endfilter
					  select h;
			foreach (var handler in invalid) {
				errors.Add(string.Format(
					"The last instruction in filter [[{0:X4}, {1:X4}] is not endfilter.",
					handler.FilterStart.Offset,
					handler.FilterEnd.Offset));
			}

			//Rule: No two regions (protected block, filter, handler region) of a single exception entry
			//may overlap with one another. 
			foreach (var entry in exceptionEntries) {

				var arePairwiseDisjoint = AreRegionsPairwiseDisjoint(
					entry.TryStart,
					PreviousInstr(entry.TryEnd),
					entry.FilterStart,
					PreviousInstr(entry.FilterEnd),
					entry.HandlerStart,
					PreviousInstr(entry.HandlerEnd));

				if (!arePairwiseDisjoint) {
					errors.Add(string.Format(
						"The protected block [{0:X4}, {1:X4}] " +
						"filter [{2:X4}, {3:X4}] " +
						"and handler [{4:X4}, {5:X4}] regions are not pairwise disjoint.",
						entry.TryStart.Offset,
						entry.TryEnd.Offset,
						entry.FilterStart == null ? (int?)null : entry.FilterStart.Offset,
						entry.FilterEnd == null ? (int?)null : entry.FilterEnd.Offset,
						entry.HandlerStart.Offset,
						entry.HandlerEnd.Offset));
				}
			}

			//Rule: Each region must begin and end on an instruction boundary.
			//-- handled by the parsing engine

			//Rule: For every pair of exception entries in a method, one of the following must be true: 
			//They nest: all three regions of one entry shall be within a single region of the other entry, 
			//    with the further restriction that the enclosing region shall not be a filter.
			//    [Note: Functions called from within a filter can contain exception handling. end note] 
			//They are disjoint: all six regions of the two entries are pairwise-disjoint (no addresses overlap).  
			//They mutually protect: the protected blocks are the same and the other regions are pairwise-disjoint.
			//    In this case, all handlers shall be either catch handlers or filtered handlers.
			var uniqueExceptionEntryPairs = exceptionEntries
				.Select((h, index) => new {
					Key = h,
					Uniques = exceptionEntries.Skip(index + 1)
				})
				.SelectMany(pairDef => pairDef.Uniques.Select(u => new {
					left = pairDef.Key,
					right = u
				}))
				.ToList(); //evaluating eagerly so it's a little easier to debug

			foreach (var pair in uniqueExceptionEntryPairs) {

				var left = pair.left; // assigning to a local to shorten syntax
				var right = pair.right; // ditto

				// does the pair of exception entries nest?
				var isValid = Rule_IsExceptionEntryPairNested(left, right);


				// is the pair of exception entries pairwise disjoint?
				if (!isValid) {
					isValid = AreRegionsPairwiseDisjoint(
									left.TryStart,
									PreviousInstr(left.TryEnd),
									left.FilterStart,
									PreviousInstr(left.FilterEnd),
									left.HandlerStart,
									PreviousInstr(left.HandlerEnd),
									right.TryStart,
									PreviousInstr(right.TryEnd),
									right.FilterStart,
									PreviousInstr(right.FilterEnd),
									right.HandlerStart,
									PreviousInstr(right.HandlerEnd));
				}

				// does the pair of exception entries mutually protect?
				if (!isValid
					&& left.TryStart.Offset == right.TryStart.Offset
					&& left.TryEnd.Offset == right.TryEnd.Offset) //
				{
					isValid =
						   (left.HandlerType == ExceptionHandlerType.Catch || left.HandlerType == ExceptionHandlerType.Filter)
						&& (right.HandlerType == ExceptionHandlerType.Catch || right.HandlerType == ExceptionHandlerType.Filter)
						&& AreRegionsPairwiseDisjoint(
								left.FilterStart,
								PreviousInstr(left.FilterEnd),
								left.HandlerStart,
								PreviousInstr(left.HandlerEnd),
								right.FilterStart,
								PreviousInstr(right.FilterEnd),
								right.HandlerStart,
								PreviousInstr(right.HandlerEnd));
				}

				if (!isValid) {
					errors.Add(string.Format(
						"The exception entry pair [{0:X4}, {1:X4}, {2:X4}] and [{3:X4}, {4:X4}, {5:X4}] " +
						"neither nest, nor are disjoint, nor mutually protect.",
						left.TryStart.Offset,
						left.FilterStart == null ? (int?)null : left.FilterStart.Offset,
						left.HandlerStart.Offset,
						right.TryStart.Offset,
						right.FilterStart == null ? (int?)null : right.FilterStart.Offset,
						right.HandlerStart.Offset));
				}
			}

			//Rule: only a filtered handler (not a catch handler, fault handler or finally handler) can have a filter.
			invalid = from h in exceptionEntries
					  where h.HandlerType != ExceptionHandlerType.Filter
						&& (h.FilterStart != null || h.FilterEnd != null)
					  select h;
			foreach (var handler in invalid) {
				errors.Add(string.Format(
					"The protected block [{0:X4}, {1:X4}] has a filter but no filtered handler.",
					handler.TryStart.Offset,
					handler.TryEnd.Offset));
			}

			return new VerifyResult(errors);
		}

		private static OpCode[] InstrsThatDontFallThrough = new[] {
				OpCodes.Br, OpCodes.Br_S,
				OpCodes.Ret,
				OpCodes.Leave, OpCodes.Leave_S,
				OpCodes.Endfilter, OpCodes.Endfinally,
				OpCodes.Throw, OpCodes.Rethrow
			};

		private static VerifyResult VerifyFallThrough(MethodBody body) {

			var errors = new List<string>();

			//Rule: This means filters and handlers cannot appear at the beginning of a method
			var filterAndHandlerStarts = body.ExceptionHandlers
				.SelectMany(eh => new[] { eh.FilterStart, eh.HandlerStart })
				.Where(instr => instr != null) // get rid of the filter starts that don't exist
				.Where(instr => instr.Previous == null);
			// if the handler or filter is at the start of a method it won't have a previous instruction
			foreach (var instr in filterAndHandlerStarts) {
				errors.Add("A handler or filter is at the beginning of the method.");
			}


			//Rule: filters and handlers cannot appear immediately following any
			//      instruction that can cause control flow to fall through. 
			var afterFallThroughInstr = body.ExceptionHandlers
				// we're interested in filters and handlers
				.SelectMany(eh => new[] { eh.FilterStart, eh.HandlerStart })
				// we don't care filters/handlers that failed the check above or that don't exist
				.Where(instr => instr != null && instr.Previous != null)
				// we only want the filters/handlers that are not after a non-fall through instruction
				.Where(instr => InstrsThatDontFallThrough.All(noFall => instr.Previous.OpCode != noFall));

			foreach (var instr in afterFallThroughInstr) {
				errors.Add(string.Format(
					"The filter or handler that begins at [{0:X4}] is incorrectly preceeded " +
					"by an instruction that could let control flow fall through.",
					instr.Offset));
			}

			//Rule: Exit from protected blocks, filters, or handlers cannot be accomplished via fall through.
			//-- interpretation: the last instruction in a protected block, filter, or handler cannot be one that allows fall through
			var endingInstructions = body.ExceptionHandlers
				.SelectMany(eh => new[] { eh.TryEnd, eh.FilterEnd, eh.HandlerEnd });

			// we don't care about trys/filters/handlers that don't exist
			var lastInstructions = endingInstructions
				.Where(instr => instr != null)
				.Select(instr => instr.Previous);

			// we only want the protected blocks/filters/handlers whose last instruction is one that could fall through
			var endsWithFallThroughInstr = lastInstructions
				.Where(instr => !InstrsThatDontFallThrough.Contains(i => i == instr.OpCode));

			foreach (var instr in endsWithFallThroughInstr) {
				errors.Add(string.Format(
					"The protected block, filter, or handler which ends at [{0:X4}] " +
					"does not end with an instruction that prevents fall through.",
					instr.Previous.Offset));
				// using instr.Previous since it's the last instruction in the protectd block/filter/handler,
				// and using its offset will make interpreting errors later much easier
			}


			return new VerifyResult(errors);
		}

		private static OpCode[] BranchInstructions = new[] {
				OpCodes.Beq, OpCodes.Beq_S, OpCodes.Bge, OpCodes.Bge_S, OpCodes.Bge_Un,
				OpCodes.Bge_Un_S, OpCodes.Bgt, OpCodes.Bgt_S, OpCodes.Bgt_Un, OpCodes.Bgt_Un_S,
				OpCodes.Ble, OpCodes.Ble_S, OpCodes.Ble_Un, OpCodes.Ble_Un_S, OpCodes.Blt,
				OpCodes.Blt_S, OpCodes.Blt_Un, OpCodes.Blt_Un_S, OpCodes.Bne_Un, OpCodes.Bne_Un_S,
				OpCodes.Br, OpCodes.Br_S, OpCodes.Brfalse, OpCodes.Brfalse_S, OpCodes.Brtrue,
				OpCodes.Brtrue_S, OpCodes.Switch
			};


		private static VerifyResult VerifyBranches(MethodBody body) {

			var errors = new List<string>();

			Func<Instruction, Instruction[]> getInstrTargets = instr =>
				instr.OpCode == OpCodes.Switch
					? (Instruction[])instr.Operand
					: new[] { (Instruction)instr.Operand };

			// Rule: 1. If the source of the branch is within a protected block, filter, or handler,
			//       the target(s) shall be within the same protected block, filter, or handler 
			var ruleViolations = body.Instructions
				.Where(i => BranchInstructions.Any(code => code == i.OpCode))
				.SelectMany(i => body.ExceptionHandlers.Select(eh => new[] {
					new { instr=i, name="protected block", start=eh.TryStart, end = eh.TryEnd, targets=getInstrTargets(i)},
					new { instr=i, name="filter", start=eh.FilterStart, end = eh.FilterEnd, targets=getInstrTargets(i)},
					new { instr=i, name="handler", start=eh.HandlerStart, end = eh.HandlerEnd, targets=getInstrTargets(i)}
				}))
				.SelectMany(a => a)
				.Where(a => a.start != null && a.end != null && IsInstructionBetween(a.instr, a.start, a.end))
				.Where(a => a.targets.Any(t => !IsInstructionBetween(t, a.start, a.end)));

			foreach (var violation in ruleViolations) {
				errors.Add(string.Format(
					"The {0} [{1:X4}, {2:X4}] contains branch instruction [{3:X4}], but not its target(s)!",
					violation.name,
					violation.start.Offset,
					violation.end.Offset,
					violation.instr.Offset));
			}

			//target 
			//The target of br, br.s, and the conditional branches, is the address specified. The targets of switch are all of the 
			//addresses specified in the jump table. 

			//Rule: 1.  If any target of the branch is within a protected block, except the first instruction of that protected 
			//      block, the source shall be within the same protected block. 
			ruleViolations = body.Instructions
				.Where(i => BranchInstructions.Any(code => code == i.OpCode))
				.SelectMany(i => body.ExceptionHandlers.Select(eh =>
					new {
						instr = i,
						name = "protected block",
						start = eh.TryStart,
						end = eh.TryEnd,
						targets = getInstrTargets(i)
					}
				))
				.Where(a => a.targets.Any(t => t != a.start && IsInstructionBetween(t, a.start, a.end)))
				.Where(a => !IsInstructionBetween(a.instr, a.start, a.end));

			foreach (var violation in ruleViolations) {
				errors.Add(string.Format(
					"The {0} [{1:X4}, {2:X4}] contains a target for branch instruction [{3:X4}], but not the branch instruction itself.",
					violation.name,
					violation.start.Offset,
					violation.end.Offset,
					violation.instr.Offset));
			}


			//Rule: 2.  If any target of the branch is within a filter or handler, the source shall be within the same filter or handler. 
			//      [Note: Code can branch to the first instruction of a protected block, but not into the middle of one. end note] 
			//      [Note: Since the conditional branches and switch have a fall-through case, they shall also obey the rules for fall 
			//      through. end note]
			ruleViolations = body.Instructions
				.Where(i => BranchInstructions.Any(code => code == i.OpCode))
				.SelectMany(i => body.ExceptionHandlers.Select(eh => new[] {
					new { instr=i, name="filter", start=eh.FilterStart, end = eh.FilterEnd, targets=getInstrTargets(i)},
					new { instr=i, name="handler", start=eh.HandlerStart, end = eh.HandlerEnd, targets=getInstrTargets(i)}
				}))
				.SelectMany(a => a)
				.Where(a => a.start != null && a.end != null && a.targets.Any(t => IsInstructionBetween(t, a.start, a.end)))
				.Where(a => !IsInstructionBetween(a.instr, a.start, a.end));

			foreach (var violation in ruleViolations) {
				errors.Add(string.Format(
					"The {0} [{1:X4}, {2:X4}] contains a target for branch instruction [{3:X4}], but not the branch instruction itself.",
					violation.name,
					violation.start.Offset,
					violation.end.Offset,
					violation.instr.Offset));
			}
			return new VerifyResult(errors);
		}

		#region helper methods

		/// <summary>
		/// Returns true if the inner section is nested within the outer section, but is not
		/// the same as it. The inner section is considered strictly nested within the outer section
		/// if its start is greater than the outer's start, and its end is less than the outer's end,
		/// and both ends are not coincident with each other.
		/// </summary>
		/// <param name="outerStart"></param>
		/// <param name="outerEnd"></param>
		/// <param name="innerStart"></param>
		/// <param name="innerEnd"></param>
		/// <returns></returns>
		private static bool IsNestedWithin(int outerStart, int outerEnd, int innerStart, int innerEnd) {
			return (outerStart <= innerStart && innerEnd < outerEnd)
				|| (outerStart < innerStart && innerEnd <= outerEnd);
		}

		private static bool AreDisjoint(
			Instruction firstStart, Instruction firstEnd,
			Instruction secondStart, Instruction secondEnd) //
		{
			return AreDisjoint(
				firstStart.Offset, firstEnd.Offset,
				secondStart.Offset, secondEnd.Offset);
		}

		private static bool AreDisjoint(int firstStart, int firstEnd, int secondStart, int secondEnd) {
			return firstStart <= firstEnd && secondStart <= secondEnd
				&& ((firstStart < secondStart && firstEnd < secondStart)
				 || (secondStart < firstStart && secondEnd < firstStart));
		}

		private static bool AreNotDisjoint(int firstStart, int firstEnd, int secondStart, int secondEnd) {
			return !AreDisjoint(firstStart, firstEnd, secondStart, secondStart);
		}

		private static bool AreRegionsPairwiseDisjoint(params Instruction[] startsAndEnds) {
			if (startsAndEnds == null || startsAndEnds.Length == 0)
				return true;

			if (startsAndEnds.Length % 2 == 1)
				throw new ArgumentException("An even number of arguments is expected.");

			var starts = startsAndEnds.Where((instr, index) => (index & 0x01) == 0).ToList();
			var ends = startsAndEnds.Where((instr, index) => (index & 0x01) == 1).ToList();

			var regions = starts.Zip(ends, (s, e) => new {
				Start = s == null ? int.MinValue : s.Offset,
				End = e == null ? int.MinValue : e.Offset
			});

			// delayed the check for null elements in the startsAndEnds
			// parameter list so that it would be easy to check and enforce
			// that null pairs always occur on pair boundaries.
			if (regions.Any(r => (r.Start == int.MinValue || r.End == int.MinValue) && r.Start != r.End))
				throw new ArgumentException("Null elements were given, but they do not create a complete region.");

			//convert to a list for efficiency
			regions = regions.Where(r => r.Start != int.MinValue).ToList();

			var distinctPairs = regions
				.Select((r, index) => new {
					Key = r,
					Uniques = regions.Skip(index + 1)
				});

			return distinctPairs.All(dp =>
				dp.Uniques
				  .All(u => AreDisjoint(dp.Key.Start, dp.Key.End, u.Start, u.End)));
		}

		private static Instruction PreviousInstr(Instruction instr) {
			return instr == null
				? null
				: instr.Previous;
		}

		private static int? PreviousInstrOffset(Instruction instr) {
			return instr == null
				? (int?)null
				: instr.Previous.Offset;
		}

		//They nest: all three regions of one entry shall be within a single region of the other entry, 
		//    with the further restriction that the enclosing region shall not be a filter.
		//    [Note: Functions called from within a filter can contain exception handling. end note] 
		private static bool Rule_IsExceptionEntryPairNested(ExceptionHandler left, ExceptionHandler right) {
			Func<Instruction, int?> nullableOffset = i => i == null ? (int?)null : i.Offset;

			var leftEntries = new[] {
				new { isFilter=false, start=(int?)left.TryStart.Offset, end=(int?)left.TryEnd.Offset},
				new { isFilter=true, start=nullableOffset(left.FilterStart), end=nullableOffset(left.FilterEnd)},
				new { isFilter=false, start=(int?)left.HandlerStart.Offset, end=(int?)left.HandlerEnd.Offset},
			};

			var rightEntries = new[] {
				new { isFilter=false, start=(int?)right.TryStart.Offset, end=(int?)right.TryEnd.Offset},
				new { isFilter=true, start=nullableOffset(right.FilterStart), end=nullableOffset(right.FilterEnd)},
				new { isFilter=false, start=(int?)right.HandlerStart.Offset, end=(int?)right.HandlerEnd.Offset},
			};

			// if all three entries (try, filter, and handler) of either the left or right exception handler
			// is completly within the try or handler entry of the other exception handler,
			// then we will return true, otherwise we will return false.
			//NB: we're checking whether the right is inside the left AND whether the left is inside
			//    the right b/c the way that left and right are generated in the calling function,
			//    we would only ever check if right was inside left, and the spec requires
			//    us to check both ways
			var inLeftEntry = leftEntries.Any(le => !le.isFilter
					&& rightEntries
						.Where(re => re.start != null)
						.All(re => IsNestedWithin(le.start.Value, le.end.Value, re.start.Value, re.end.Value)));

			var inRightEntry = rightEntries.Any(re => !re.isFilter
					&& leftEntries
						.Where(le => le.start != null)
						.All(le => IsNestedWithin(re.start.Value, re.end.Value, le.start.Value, le.end.Value)));

			return inLeftEntry || inRightEntry;

		}

		#endregion
	}
}
