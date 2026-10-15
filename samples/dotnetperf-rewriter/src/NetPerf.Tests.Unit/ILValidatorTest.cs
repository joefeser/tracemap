using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Tests.Test_References;

namespace NetPerf.Tests {

	[TestClass]
	public partial class ILValidatorTest {

		[TestMethod]
		public void AreDisjoint_FirstBeforeSecond_ReturnsTrue() {
			Assert.IsTrue(ILValidator_Privates.AreDisjoint(1, 3, 4, 5));
		}

		[TestMethod]
		public void AreDisjoint_FirstAfterSecond_ReturnsTrue() {
			Assert.IsTrue(ILValidator_Privates.AreDisjoint(4, 5, 1, 3));
		}

		[TestMethod]
		public void AreDisjoint_FirstInsideSecond_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.AreDisjoint(2, 4, 1, 5));
		}

		[TestMethod]
		public void AreDisjoint_SecondInsideFirst_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.AreDisjoint(1, 5, 2, 4));
		}

		[TestMethod]
		public void AreDisjoint_FirstOverlapsBeginningOfSecond_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.AreDisjoint(1, 4, 3, 5));
		}

		[TestMethod]
		public void AreDisjoint_FirstOverlapsEndOfSecond_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.AreDisjoint(3, 5, 1, 4));
		}

		[TestMethod]
		public void AreDisjoint_FirstEndMatchesSecondStart_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.AreDisjoint(1, 2, 2, 3));
		}

		[TestMethod]
		public void AreDisjoint_SecondEndMatchesFirstStart_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.AreDisjoint(3, 4, 1, 3));
		}

		[TestMethod]
		public void AreDisjoint_FirstMatchesSecond_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.AreDisjoint(1, 2, 1, 2));
		}

		[TestMethod]
		public void AreDisjoint_StartsMatchAndSecondEndBeforeFirst_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.AreDisjoint(1, 3, 1, 2));
		}

		[TestMethod]
		public void AreDisjoint_StartsMatchAndFirstEndBeforeSecond_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.AreDisjoint(1, 2, 1, 3));
		}

		[TestMethod]
		public void AreDisjoint_EndsMatchAndSecondStartBeforeFirst_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.AreDisjoint(2, 3, 1, 3));
		}

		[TestMethod]
		public void AreDisjoint_EndsMatchAndFirstStartBeforeSecond_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.AreDisjoint(1, 3, 2, 3));
		}

		//TODO figure out how to make sure the AreNotDisjoint method always returns the opposite of AreDisjoint for all test inputs above

		[TestMethod]
		public void IsNestedWithin_WithinAndNeitherEndCoincident_ReturnsTrue() {
			Assert.IsTrue(ILValidator_Privates.IsNestedWithin(0, 10, 1, 3));
		}

		[TestMethod]
		public void IsNestedWithin_WithinAndEndCoincident_ReturnsTrue() {
			Assert.IsTrue(ILValidator_Privates.IsNestedWithin(0, 10, 1, 10));
		}

		[TestMethod]
		public void IsNestedWithin_WithinAndStartCoincident_ReturnsTrue() {
			Assert.IsTrue(ILValidator_Privates.IsNestedWithin(0, 10, 0, 3));
		}

		[TestMethod]
		public void IsNestedWithin_WithinAndBothEndsCoincident_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.IsNestedWithin(0, 10, 0, 10));
		}

		[TestMethod]
		public void IsNestedWithin_Disjoint_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.IsNestedWithin(0, 10, 11, 13));
			Assert.IsFalse(ILValidator_Privates.IsNestedWithin(11, 13, 0, 10));
		}

		[TestMethod]
		public void IsNestedWithin_InnerContainsOuter_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.IsNestedWithin(1, 3, 0, 10));
		}

		[TestMethod]
		public void IsNestedWithin_InnerStartsBeforeOuter_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.IsNestedWithin(5, 10, 1, 7));
		}

		[TestMethod]
		public void IsNestedWithin_InnerEndsAfterOuter_ReturnsFalse() {
			Assert.IsFalse(ILValidator_Privates.IsNestedWithin(0, 10, 1, 12));
		}
	}
}
