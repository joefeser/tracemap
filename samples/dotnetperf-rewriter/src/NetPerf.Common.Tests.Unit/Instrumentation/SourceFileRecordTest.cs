using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common.UnitTests.Instrumentation {
	[TestClass]
	public class SourceFileRecordTest {

		[TestMethod]
		public void Constructor_InitializesProperties() {
			var x = new SourceFileRecord(1, 2, "hello", "hash", "algorithm", "language");

			Assert.AreEqual("hash", x.Hash);
			Assert.AreEqual("algorithm", x.HashAlgorithm);
			Assert.AreEqual("language", x.SourceLanguage);
		}

		[TestMethod]
		public void Constructor_NullHashBecomesEmptyString() {
			var x = new SourceFileRecord(1, 2, "hello", null, "algorithm", "language");

			Assert.AreEqual(string.Empty, x.Hash);
		}

		[TestMethod]
		public void Constructor_NullHashAlgorithmBecomesEmptyString() {
			var x = new SourceFileRecord(1, 2, "hello", "hash", null, "language");

			Assert.AreEqual(string.Empty, x.HashAlgorithm);
		}

		[TestMethod]
		public void Constructor_NullSourceLanguageBecomesEmptyString() {
			var x = new SourceFileRecord(1, 2, "hello", "hash", "algorithm", null);

			Assert.AreEqual(string.Empty, x.SourceLanguage);
		}

	}
}
