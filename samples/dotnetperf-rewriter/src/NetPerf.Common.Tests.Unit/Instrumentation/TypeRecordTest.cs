using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common.UnitTests.Instrumentation {
	[TestClass]
	public class TypeRecordTest {

		static HashAlgorithm Algo = HashAlgorithm.SHA512;

		[TestMethod]
		public void AddEvent_AddsNewEvent() {
			var x = new TypeRecord(1, 2, "typesig");
			var record = x.AddEvent("eventsig", 1, 2);

			Assert.IsNotNull(record);
			Assert.AreEqual("eventsig", record.Signature);
		}

		[TestMethod]
		public void AddEvent_ReturnsExistingEvent() {
			var x = new TypeRecord(1, 2, "typesig");
			var first = x.AddEvent("eventsig", 1, 2);
			var second = x.AddEvent("eventsig", 1, 2);

			Assert.AreSame(first, second);
		}

		[TestMethod]
		public void AddMethod_AddsNewMethod() {
			var x = new TypeRecord(1, 2, "hello");

			var method = x.AddMethod("sig", "hash", Algo);

			Assert.AreEqual(1, x.Methods.Count);
			CollectionAssert.Contains(x.Methods, method);
		}

		[TestMethod]
		public void AddMethod_ReturnsExistingMethodIfSignaturesMatch() {
			var x = new TypeRecord(1, 2, "hello");

			var method1 = x.AddMethod("sig", "hash", Algo);
			var method2 = x.AddMethod("sig", "hash2", Algo);

			Assert.AreEqual(method1.Id, method2.Id);
			Assert.AreEqual(1, x.Methods.Count);
			CollectionAssert.Contains(x.Methods, method1);
		}

		[TestMethod]
		public void AddMethod_SetsMethodProperties() {
			var expectedAttrs = new[] { "attr1", "attr2" };
			var x = new TypeRecord(1, 2, "hello");
			var method = x.AddMethod("sig", "hash", Algo, expectedAttrs);

			Assert.AreEqual(1, method.ParentId);
			Assert.AreEqual("sig", method.Signature);
			Assert.AreEqual("hash", method.Hash);
			Assert.AreEqual(Algo, method.HashAlgorithm);
			CollectionAssert.AreEqual(expectedAttrs, method.CustomAttributes);
		}

		[TestMethod]
		public void AddMethod_SetsMethodGenericArguments() {
			var expected = new[] { "attr1", "attr2" };
			var x = new TypeRecord(1, 2, "hello");
			var method = x.AddMethod("sig", "hash", Algo, genericArguments: expected);

			CollectionAssert.IsSubsetOf(method.GenericArguments, expected);
		}

		[TestMethod]
		public void AddProperty_AddsNewProperty() {
			var x = new TypeRecord(1, 2, "typesig");
			var record = x.AddProperty("propertysig", 1, 2);

			Assert.IsNotNull(record);
			Assert.AreEqual("propertysig", record.Signature);
		}

		[TestMethod]
		public void AddProperty_ReturnsExistingProperty() {
			var x = new TypeRecord(1, 2, "typesig");
			var first = x.AddProperty("propertysig", 1, 2);
			var second = x.AddProperty("propertysig", 1, 2);

			Assert.AreSame(first, second);
		}

		[TestMethod]
		public void LinkEvents_HandlesNullInput() {
			var x = new TypeRecord(1, 2, "hello");

			x.LinkEvents(null);

			Assert.AreEqual(0, x.Events.Count);
		}

		[TestMethod]
		public void LinkEvents_HandlesEmptyInput() {
			var x = new TypeRecord(1, 2, "hello");

			x.LinkEvents(new IEventRecord[0]);

			Assert.AreEqual(0, x.Events.Count);
		}

		[TestMethod]
		public void LinkEvents_AddsNewEvents() {
			var x = new TypeRecord(1, 2, "hello");

			var item1 = new EventRecord(1, 1, "sig1", 3, 4);
			var item2 = new EventRecord(2, 1, "sig2", 3, 4);

			x.LinkEvents(new[] { item1, item2 });

			CollectionAssert.IsSubsetOf(x.Events, new[] { item1, item2 });
		}

		[TestMethod]
		public void LinkMethods_HandlesNullInput() {
			var x = new TypeRecord(1, 2, "hello");

			x.LinkMethods(null);

			Assert.AreEqual(0, x.Methods.Count);
		}

		[TestMethod]
		public void LinkMethods_HandlesEmptyInput() {
			var x = new TypeRecord(1, 2, "hello");

			x.LinkMethods(new IMethodRecord[0]);

			Assert.AreEqual(0, x.Methods.Count);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkMethods_RejectsMethodsForOtherTypes() {
			var x = new TypeRecord(1, 2, "hello");

			var method = new MethodRecord(1, 4, "sig", "hash", Algo);

			x.LinkMethods(new[] { method });
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkMethods_RejectsMethodsAlreadyLinkedToType() {
			var x = new TypeRecord(1, 2, "hello");

			var method = x.AddMethod("sig", "hash", Algo);

			x.LinkMethods(new[] { method });
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkMethods_RejectsDuplicateMethods() {
			var x = new TypeRecord(1, 2, "hello");

			var method1 = new MethodRecord(1, 1, "sig", "hash", Algo);
			var method2 = new MethodRecord(1, 1, "sig", "hash", Algo);

			x.LinkMethods(new[] { method1, method2 });
		}

		[TestMethod]
		public void LinkMethods_AddsNewMethods() {
			var x = new TypeRecord(1, 2, "hello");

			var method1 = new MethodRecord(1, 1, "sig1", "hash", Algo);
			var method2 = new MethodRecord(2, 1, "sig2", "hash", Algo);

			x.LinkMethods(new[] { method1, method2 });

			Assert.AreEqual(2, x.Methods.Count);
			CollectionAssert.Contains(x.Methods, method1);
			CollectionAssert.Contains(x.Methods, method2);
		}

		[TestMethod]
		public void LinkProperties_HandlesNullInput() {
			var x = new TypeRecord(1, 2, "hello");

			x.LinkProperties(null);

			Assert.AreEqual(0, x.Properties.Count);
		}

		[TestMethod]
		public void LinkProperties_HandlesEmptyInput() {
			var x = new TypeRecord(1, 2, "hello");

			x.LinkProperties(new IPropertyRecord[0]);

			Assert.AreEqual(0, x.Properties.Count);
		}

		[TestMethod]
		public void LinkProperties_AddsNewEvents() {
			var x = new TypeRecord(1, 2, "hello");

			var item1 = new PropertyRecord(1, 1, "sig1", 3, 4);
			var item2 = new PropertyRecord(2, 1, "sig2", 3, 4);

			x.LinkProperties(new[] { item1, item2 });

			CollectionAssert.IsSubsetOf(x.Properties, new[] { item1, item2 });
		}
	}
}
