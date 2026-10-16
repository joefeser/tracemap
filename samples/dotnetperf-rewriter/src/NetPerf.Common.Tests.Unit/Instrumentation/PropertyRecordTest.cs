using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common.UnitTests.Instrumentation {
	[TestClass]
	public class PropertyRecordTest {

		[TestMethod]
		public void Constructor_InitializesDefaultValues() {
			var x = new PropertyRecord(1, 2, "signature", 3, 4);

			Assert.AreEqual(3, x.GetMethodId);
			Assert.AreEqual(4, x.SetMethodId);
		}
	}
}
