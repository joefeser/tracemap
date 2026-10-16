using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common.UnitTests.Instrumentation {
	[TestClass]
	public class EventRecordTest {

		[TestMethod]
		public void Constructor_InitializesDefaultValues() {
			var x = new EventRecord(1, 2, "sig", 11, 22);

			Assert.AreEqual(11, x.AddMethodId);
			Assert.AreEqual(22, x.RemoveMethodId);
		}

		[TestMethod]
		public void Constructor_InitializesFireMethodId() {
			var x = new EventRecord(1, 2, "sig", 1, 1, 45);

			Assert.AreEqual(45, x.FireMethodId);
		}
	}
}
