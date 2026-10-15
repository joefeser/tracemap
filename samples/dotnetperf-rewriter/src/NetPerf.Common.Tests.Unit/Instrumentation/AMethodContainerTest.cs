using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common.UnitTests.Instrumentation {
	[TestClass]
	public class AMethodContainerTest {

		[TestMethod]
		public void Constructor_InitializesProperties() {
			var expected = new[] { 1, 2, 3 };
			var x = new AMethodContainerInstance(expected);

			CollectionAssert.AreEqual(expected, x.OtherMethodIds);
		}
	}

	class AMethodContainerInstance : AMethodContainer {

		public AMethodContainerInstance(int[] otherMethodIds)
			: base(1, 1, "sig", null, otherMethodIds) {

		}

	}

}
