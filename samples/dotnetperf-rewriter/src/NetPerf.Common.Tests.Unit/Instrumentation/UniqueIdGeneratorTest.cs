using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;
using NetPerf.Common.Tests.Test_References;

namespace NetPerf.Common.UnitTests.Instrumentation {
	[TestClass]
	public class UniqueIdGeneratorTest {

		[TestMethod]
		public void NextId_ReturnsAscendingSequence() {
			// have to clear out the cache first
			UniqueIdGenerator_Privates.PreviousIds = new Dictionary<Type, int>();

			var intType = typeof(int);

			var first = UniqueIdGenerator.NextId(intType);
			UniqueIdGenerator.NextId(intType);
			UniqueIdGenerator.NextId(intType);
			var last = UniqueIdGenerator.NextId(intType);

			ExAssert.GreaterThan(first, last);
		}

		[TestMethod]
		public void NextId_IdSequenceIsPerType() {
			// have to clear out the cache first
			UniqueIdGenerator_Privates.PreviousIds = new Dictionary<Type, int>();

			var first1 = UniqueIdGenerator.NextId(typeof(int));
			var first2 = UniqueIdGenerator.NextId(typeof(string));

			Assert.AreEqual(first1, first2);
		}
	}
}
