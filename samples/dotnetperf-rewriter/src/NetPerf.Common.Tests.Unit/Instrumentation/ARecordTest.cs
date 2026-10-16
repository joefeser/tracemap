using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common.UnitTests.Instrumentation {
	[TestClass]
	public class ARecordTest {

		[TestMethod]
		public void Constructor_InitializesProperties() {
			var x = new ARecordInstance(1, "hello");

			Assert.AreEqual(1, x.Id);
			Assert.AreEqual("hello", x.Signature);
		}

	}

	class ARecordInstance : ARecord {
		public ARecordInstance(int id, string str)
			: base(id, str) {

		}
	}
}
