using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetPerf.Tests.Test_References {
	internal static class ILValidator_Privates {

		private static readonly PrivateType _privates = new PrivateType(typeof(ILValidator));

		public static ILValidator.VerifyResult VerifyThrow(Mono.Cecil.Cil.MethodBody body) {
			return (ILValidator.VerifyResult)_privates.InvokeStatic("VerifyThrow", body);
		}

		public static ILValidator.VerifyResult VerifyRethrow(Mono.Cecil.Cil.MethodBody body) {
			return (ILValidator.VerifyResult)_privates.InvokeStatic("VerifyRethrow", body);
		}

		public static ILValidator.VerifyResult VerifyRet(Mono.Cecil.Cil.MethodBody body) {
			return (ILValidator.VerifyResult)_privates.InvokeStatic("VerifyRet", body);
		}

		public static ILValidator.VerifyResult VerifyJmp(Mono.Cecil.Cil.MethodBody body) {
			return (ILValidator.VerifyResult)_privates.InvokeStatic("VerifyJmp", body);
		}

		public static ILValidator.VerifyResult VerifyEndFilter(Mono.Cecil.Cil.MethodBody body) {
			return (ILValidator.VerifyResult)_privates.InvokeStatic("VerifyEndFilter", body);
		}

		public static ILValidator.VerifyResult VerifyEndFinally(Mono.Cecil.Cil.MethodBody body) {
			return (ILValidator.VerifyResult)_privates.InvokeStatic("VerifyEndFinally", body);
		}

		public static ILValidator.VerifyResult VerifyLeave(Mono.Cecil.Cil.MethodBody body) {
			return (ILValidator.VerifyResult)_privates.InvokeStatic("VerifyLeave", body);
		}

		public static ILValidator.VerifyResult VerifyProtectedBlocks(Mono.Cecil.Cil.MethodBody body) {
			return (ILValidator.VerifyResult)_privates.InvokeStatic("VerifyProtectedBlocks", body);
		}

		public static ILValidator.VerifyResult VerifyFallThrough(Mono.Cecil.Cil.MethodBody body) {
			return (ILValidator.VerifyResult)_privates.InvokeStatic("VerifyFallThrough", body);
		}

		public static ILValidator.VerifyResult VerifyBranches(Mono.Cecil.Cil.MethodBody body) {
			return (ILValidator.VerifyResult)_privates.InvokeStatic("VerifyBranches", body);
		}

		public static bool IsNestedWithin(int outerStart, int outerEnd, int innerStart, int innerEnd) {
			return (bool)_privates.InvokeStatic("IsNestedWithin", outerStart,outerEnd, innerStart, innerEnd );
		}

		public static bool AreDisjoint(int firstStart, int firstEnd, int secondStart, int secondEnd) {
			return (bool)_privates.InvokeStatic("AreDisjoint", firstStart, firstEnd, secondStart, secondEnd );
		}

		
	}
}
