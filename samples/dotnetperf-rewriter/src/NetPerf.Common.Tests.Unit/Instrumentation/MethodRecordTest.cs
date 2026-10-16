using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common.UnitTests.Instrumentation {
	[TestClass]
	public class MethodRecordTest {

		static HashAlgorithm Algo = HashAlgorithm.None;

		[TestMethod]
		public void Constructor_InitializesDefaultValues() {
			var x = new MethodRecord(4, 3, "sig", "hash", Algo);

			Assert.AreEqual("hash", x.Hash);
			Assert.AreEqual(Algo, x.HashAlgorithm);
		}

		[TestMethod]
		public void Constructor_InitializesSourceFileIds() {
			var expected = new[] { 1, 2, 3 };
			var x = new MethodRecord(4, 3, "sig", "hash", Algo, sourceFileIds: expected);

			CollectionAssert.IsSubsetOf(x.SourceFileIds, expected);
		}

		[TestMethod]
		public void AddPoint_AddsPointToPointCollection() {
			var x = new MethodRecord(44, 3, "sig", "hash", Algo);

			var point = x.AddPoint(1, 2, 3, 4, 5);
			CollectionAssert.Contains(x.Points, point);
		}

		[TestMethod]
		public void AddPoint_AllParametersNull_AddPointToPointCollection() {
			var x = new MethodRecord(44, 3, "sig", "hash", Algo);

			var point = x.AddPoint(null, null, null, null, null);
			CollectionAssert.Contains(x.Points, point);
		}

		[TestMethod]
		public void AddPoint_SetsParentIdToOwnId() {
			var x = new MethodRecord(44, 3, "sig", "hash", Algo);

			var point = x.AddPoint(1, 2, 3, 4, 5);
			Assert.AreEqual(44, point.ParentId);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkPoints_RejectsPointsForOtherMethods() {
			var method = new MethodRecord(1, 2, "sig", "hash", Algo);

			var point = new PointRecord(3, 2);

			method.LinkPoints(new[] { point });
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkPoints_RejectsPointsAlreadyLinkedToMethod() {
			var method = new MethodRecord(1, 2, "sig", "hash", Algo);

			var point1 = method.AddPoint(1, 2, 3, 4, 5);
			var point2 = method.AddPoint(6, 7, 8, 9, 10);

			method.LinkPoints(new[] { point1, point2 });
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkPoints_RejectsDuplicatePoints() {
			var method = new MethodRecord(11, 2, "sig", "hash", Algo);

			var point1 = new PointRecord(35, 11, 1, 2, 3, 4, 5);
			var point2 = new PointRecord(35, 11, 1, 7, 8, 9, 10);

			method.LinkPoints(new[] { point1, point2 });
		}

		[TestMethod]
		public void LinkPoints_AddsNewPoints() {
			var method = new MethodRecord(11, 2, "sig", "hash", Algo);

			var point1 = new PointRecord(35, 11, 1, 2, 3, 4, 5);
			var point2 = new PointRecord(36, 11, 1, 7, 8, 9, 10);


			method.LinkPoints(new[] { point1, point2 });

			CollectionAssert.Contains(method.Points, point1);
			CollectionAssert.Contains(method.Points, point2);
		}



	}
}
