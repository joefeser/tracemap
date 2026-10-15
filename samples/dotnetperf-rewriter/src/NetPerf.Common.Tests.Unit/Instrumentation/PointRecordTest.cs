using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common.Tests.Instrumentation {
	[TestClass]
	public class PointRecordTest {

		[TestMethod]
		public void Constructor_InitializesDefaultValues() {
			var x = new PointRecord(1, 2, 3, 4, 5, 6, 7);

			Assert.AreEqual(1, x.Id);
			Assert.AreEqual(2, x.ParentId);
			Assert.AreEqual(3, x.SourceFileId);
			Assert.AreEqual(4, x.StartLine);
			Assert.AreEqual(5, x.StartColumn);
			Assert.AreEqual(6, x.EndLine);
			Assert.AreEqual(7, x.EndColumn);

			Assert.AreEqual("1 2 3 4 5 6 7", x.Signature);
		}

		[TestMethod]
		public void Constructor_AcceptsNoSourceFileInfo() {
			var x = new PointRecord(1, 2);

			Assert.AreEqual("1 2", x.Signature);
			Assert.IsNull(x.SourceFileId);
			Assert.IsNull(x.StartLine);
			Assert.IsNull(x.StartColumn);
			Assert.IsNull(x.EndLine);
			Assert.IsNull(x.EndColumn);
		}

		[TestMethod]
		public void Constructor_NoSourceFileId_LineAndColumnParamsIgnored() {
			var x = new PointRecord(1, 2, null, 4, 5, 6, 7);

			Assert.AreEqual("1 2", x.Signature);
			Assert.IsNull(x.SourceFileId);
			Assert.IsNull(x.StartLine);
			Assert.IsNull(x.StartColumn);
			Assert.IsNull(x.EndLine);
			Assert.IsNull(x.EndColumn);
		}
	}
}
