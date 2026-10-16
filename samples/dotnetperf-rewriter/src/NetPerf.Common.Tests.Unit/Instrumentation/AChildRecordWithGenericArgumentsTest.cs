using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common.Tests.Instrumentation {

	[TestClass]
	public class AChildRecordWithGenericArgumentsTest {

		[TestMethod]
		public void Constructor_InitializesProperties() {
			var expected = new[] { "one", "two", "three" };
			var x = new AChildRecordWithGenericArgumentsInstance(null, expected);

			CollectionAssert.AreEqual(expected, x.GenericArguments);
		}

		[TestMethod]
		public void AddGenericArguments_EmptyInputLeavesExistingGenericArgumentListUnaffected() {
			var x = new AChildRecordWithGenericArgumentsInstance(null, null);
			x.AddGenericArguments(new string[0]);

			Assert.AreEqual(0, x.GenericArguments.Count);
		}

		[TestMethod]
		public void AddGenericArguments_NullInputLeavesExistingGenericArgumentListUnaffected() {
			var expected = new[] { "T", "K" };
			var x = new AChildRecordWithGenericArgumentsInstance(null, expected);
			x.AddGenericArguments(null);

			CollectionAssert.AreEqual(expected, x.GenericArguments);
		}

		[TestMethod]
		public void AddGenericArguments_AddsInputToGenericArgumentList() {
			var initial = new[] { "T", "K" };
			var x = new AChildRecordWithGenericArgumentsInstance(null, initial);

			var additional = new[] { "R", "U" };
			x.AddGenericArguments(additional);

			var expected = initial.Concat(additional).ToArray();

			CollectionAssert.AreEqual(expected, x.GenericArguments);
		}

	}

	class AChildRecordWithGenericArgumentsInstance : AChildRecordWithGenericArguments {
		public AChildRecordWithGenericArgumentsInstance(string[] customAttributes, string[] genericArguments)
			: base(1, 2, "sig", customAttributes, genericArguments) {

		}
	}
}
