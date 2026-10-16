using System;
using System.IO;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetPerf.Common.Tests.Extensions {
	[TestClass]
	public class XmlExtensionsTest {

		struct EnumReadTest {
			public int Id;

			public EnumReadTest(int id) {
				Id = id;
			}

		}

		enum MyEnum {
			first,
			second,
			third
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public void ReadElementAsEnum_GenericTypeIsNotEnumerationType_ThrowsException() {
			var xml = "<element>5</element>";

			using (var reader = XmlReader.Create(new StringReader(xml))) {
				reader.MoveToContent();
				XmlExtensions.ReadElementAsEnum<EnumReadTest>(reader);
			}
		}

		[TestMethod]
		public void ReadElementAsEnum_ItemIsCorrectlyCased_ReadsItem() {
			var xml = "<element>second</element>";

			MyEnum item;
			using (var reader = XmlReader.Create(new StringReader(xml))) {
				reader.MoveToContent();
				item = XmlExtensions.ReadElementAsEnum<MyEnum>(reader);
			}

			Assert.AreEqual(MyEnum.second, item);
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public void ReadElementAsEnum_ItemIsIncorrectlyCased_ThrowsException() {
			var xml = "<element>THIRD</element>";

			using (var reader = XmlReader.Create(new StringReader(xml))) {
				reader.MoveToContent();
				XmlExtensions.ReadElementAsEnum<MyEnum>(reader);
			}
		}

	}
}
