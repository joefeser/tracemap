using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common.UnitTests.Instrumentation {
	[TestClass]
	public class AChildRecordTest {

		[TestMethod]
		public void Constructor_InitializesProperties() {
			var x = new AChildRecordInstance(7);

			Assert.AreEqual(7, x.ParentId);
		}

	}

	class AChildRecordInstance : AChildRecord {
		public AChildRecordInstance(int pid)
			: base(1, pid, "hello") {

		}
	}
}
