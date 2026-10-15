using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common.UnitTests.Instrumentation {
	[TestClass]
	public class AChildRecordWithCustomAttributesTest {

		[TestMethod]
		public void Constructor_InitializesProperties() {
			var expected = new[] { "one", "two", "three" };
			var x = new AChildRecordWithCustomAttributesInstance(expected);

			CollectionAssert.AreEqual(expected, x.CustomAttributes);
		}

		[TestMethod]
		public void AddCustomAttributes_EmptyInputLeavesExistingCustomAttributeListUnaffected() {
			var x = new AChildRecordWithCustomAttributesInstance(null);
			x.AddCustomAttributes(new string[0]);

			Assert.AreEqual(0, x.CustomAttributes.Count);
		}

		[TestMethod]
		public void AddCustomAttributes_NullInputLeavesExistingCustomAttributeListUnaffected() {
			var expected = new[] { "attr1", "attr2" };
			var x = new AChildRecordWithCustomAttributesInstance(expected);
			x.AddCustomAttributes(null);

			CollectionAssert.AreEqual(expected, x.CustomAttributes);
		}

		[TestMethod]
		public void AddCustomAttributes_AddsInputToCustomAttributeList() {
			var initial = new[] { "attr1", "attr2" };
			var x = new AChildRecordWithCustomAttributesInstance(initial);

			var additional = new[] { "attr3", "attr4" };
			x.AddCustomAttributes(additional);

			var expected = initial.Concat(additional).ToArray();

			CollectionAssert.AreEqual(expected, x.CustomAttributes);
		}
	}

	class AChildRecordWithCustomAttributesInstance : AChildRecordWithCustomAttributes {

		public AChildRecordWithCustomAttributesInstance(string[] customAttributes)
			: base(1, 2, "hello", customAttributes) {

		}
	}

}
