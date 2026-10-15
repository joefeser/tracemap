using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml;
using NetPerf.Common.Instrumentation;
using System.IO;
using NetPerf.Common.Tests.Test_References;

namespace NetPerf.Common.UnitTests.Instrumentation {

	[TestClass]
	public class InstrumentationDataSerializerTest {

		private static string HASH = "BLAHHASH";
		private static HashAlgorithm HASHTYPE = HashAlgorithm.SHA512;

		private ITypeRecord ReadTypeRecordCommon(string content) {
			using (var reader = XmlReader.Create(new StringReader(content))) {
				reader.MoveToContent();
				return InstrumentationDataSerializer_Privates.ReadTypeRecord(reader, 1);
			}
		}

		[TestMethod]
		public void ReadType_ReadsAllAttributes() {
			var data = "<type " +
				"signature=\"sig\" " +
				"/>";
			var type = ReadTypeRecordCommon(data);

			Assert.AreEqual("sig", type.Signature);
		}

		[TestMethod]
		public void ReadType_ReadsCustomAttributeElements() {
			var data = "<type " +
				"signature=\"sig\" " +
				">" +
				"<customAttributes>" +
				"<customAttribute>attribute1</customAttribute>" +
				"<customAttribute>attribute2</customAttribute>" +
				"</customAttributes>" +
				"</type>";
			var type = ReadTypeRecordCommon(data);

			CollectionAssert.AreEqual(new[] { "attribute1", "attribute2" }, type.CustomAttributes);
		}

		[TestMethod]
		public void ReadType_ReadsGenericArgumentElements() {
			var data = "<type " +
				"signature=\"sig\" >" +
				"<genericArguments>" +
				"<genericArgument>H</genericArgument>" +
				"<genericArgument>G</genericArgument>" +
				"</genericArguments>" +
				"</type>";
			var type = ReadTypeRecordCommon(data);

			CollectionAssert.AreEqual(new[] { "H", "G" }, type.GenericArguments);
		}

		[TestMethod]
		public void ReadType_ReadsMethodElements() {
			var data = "<type " +
				"signature=\"sig\" " +
				">" +
				"<methods>" +
				DefaultMethodContent(4) +
				"</methods>" +
				"</type>";
			var type = ReadTypeRecordCommon(data);

			Assert.AreEqual(1, type.Methods.Count);
		}

		private IEventRecord ReadEventRecordCommon(string content) {
			using (var reader = XmlReader.Create(new StringReader(content))) {
				reader.MoveToContent();
				return InstrumentationDataSerializer_Privates.ReadEventRecord(reader, 1);
			}
		}

		private string DefaultEventContent(bool leaveOpen = false) {
			return "<event signature=\"event sig\" add=\"1\" remove=\"2\" fire=\"3\" others=\"4;5;6\"" + (leaveOpen ? ">" : "/>");
		}

		[TestMethod]
		public void ReadEvent_ReadsAllAttributes() {
			var record = ReadEventRecordCommon(DefaultEventContent());

			Assert.AreEqual("event sig", record.Signature);
			Assert.AreEqual(1, record.AddMethodId);
			Assert.AreEqual(2, record.RemoveMethodId);
			Assert.AreEqual(3, record.FireMethodId);

			CollectionAssert.AreEqual(new[] { 4, 5, 6 }, record.OtherMethodIds);
		}

		[TestMethod]
		public void ReadEvent_ReadsCustomAttributeElements() {
			var data = DefaultEventContent(true) +
				"<customAttributes>" +
				"<customAttribute>attribute1</customAttribute>" +
				"<customAttribute>attribute2</customAttribute>" +
				"</customAttributes>" +
				"</event>";
			var record = ReadEventRecordCommon(data);

			CollectionAssert.AreEqual(new[] { "attribute1", "attribute2" }, record.CustomAttributes);
		}

		private string DefaultMethodContent(int id, bool leaveOpen = false) {
			return string.Format("<method id=\"{0}\" signature=\"sig\" hash=\"myhash\" hashAlgorithm=\"myalgorithm\" sourceFileIds=\"1;2;3\" {1}",
				id,
				leaveOpen ? ">" : "/>");
		}

		private IMethodRecord ReadMethodRecordCommon(string content) {
			using (var reader = XmlReader.Create(new StringReader(content))) {
				reader.MoveToContent();
				return InstrumentationDataSerializer_Privates.ReadMethodRecord(reader, 1);
			}
		}

		[TestMethod]
		public void ReadMethod_ReadsAllAttributes() {
			var method = ReadMethodRecordCommon(DefaultMethodContent(4));

			Assert.AreEqual(4, method.Id);
			Assert.AreEqual("sig", method.Signature);
			Assert.AreEqual("myhash", method.Hash);
			Assert.AreEqual(HashAlgorithm.None, method.HashAlgorithm);
			CollectionAssert.AreEqual(new[] { 1, 2, 3 }, method.SourceFileIds);
		}

		[TestMethod]
		public void ReadMethod_CanReadWithoutHashAttribute() {
			var content = "<method id=\"4\" signature=\"sig\" sourceFileIds=\"1;2;3\" />";
			var method = ReadMethodRecordCommon(content);

			Assert.AreEqual(4, method.Id);
			Assert.AreEqual("sig", method.Signature);
			Assert.AreEqual("", method.Hash);
			Assert.AreEqual(HashAlgorithm.None, method.HashAlgorithm);
			CollectionAssert.AreEqual(new[] { 1, 2, 3 }, method.SourceFileIds);
		}

		[TestMethod]
		public void ReadMethod_CanReadWithoutSourceFileIds() {
			var content = "<method id=\"4\" signature=\"sig\" hash=\"myhash\" hashAlgorithm=\"SHA512\" />";
			var method = ReadMethodRecordCommon(content);

			Assert.AreEqual(4, method.Id);
			Assert.AreEqual("sig", method.Signature);
			Assert.AreEqual("myhash", method.Hash);
			Assert.AreEqual(HashAlgorithm.SHA512, method.HashAlgorithm);
			CollectionAssert.AreEqual(new int[0], method.SourceFileIds);
		}

		[TestMethod]
		public void ReadMethod_ReadsCustomAttributeElements() {
			var data = DefaultMethodContent(4, true) +
				"<customAttributes>" +
				"<customAttribute>attribute1</customAttribute>" +
				"<customAttribute>attribute2</customAttribute>" +
				"</customAttributes>" +
				"</method>";
			var method = ReadMethodRecordCommon(data);

			CollectionAssert.AreEqual(new[] { "attribute1", "attribute2" }, method.CustomAttributes);
		}

		[TestMethod]
		public void ReadMethod_ReadsGenericArgumentElements() {
			var data = DefaultMethodContent(4, true) +
				"<genericArguments>" +
				"<genericArgument>T</genericArgument>" +
				"<genericArgument>K</genericArgument>" +
				"</genericArguments>" +
				"</method>";

			var method = ReadMethodRecordCommon(data);

			CollectionAssert.AreEqual(new[] { "T", "K" }, method.GenericArguments);
		}

		[TestMethod]
		public void ReadMethod_ReadsPointElements() {
			var data = DefaultMethodContent(4, true) +
				"<points>" +
				DefaultPointContent(44) +
				DefaultPointContent(45) +
				"</points>" +
				"</method>";

			var method = ReadMethodRecordCommon(data);

			Assert.AreEqual(2, method.Points.Count);
		}

		private IPointRecord ReadPointRecordCommon(string content) {
			using (var reader = XmlReader.Create(new StringReader(content))) {
				reader.MoveToContent();
				return InstrumentationDataSerializer_Privates.ReadPointRecord(reader, 1);
			}
		}

		private string DefaultPointContent(int pointId) {
			return string.Format("<point id=\"{0}\" sourceFileId=\"3\" startLine=\"4\" startColumn=\"5\" endLine=\"6\" endColumn=\"7\" />", pointId);
		}

		[TestMethod]
		public void ReadPointRecord_ReadsAllAttributes() {
			var data = DefaultPointContent(2);
			var record = ReadPointRecordCommon(data);

			Assert.AreEqual(2, record.Id);
			Assert.AreEqual(1, record.ParentId);
			Assert.AreEqual(3, record.SourceFileId);
			Assert.AreEqual(4, record.StartLine);
			Assert.AreEqual(5, record.StartColumn);
			Assert.AreEqual(6, record.EndLine);
			Assert.AreEqual(7, record.EndColumn);
		}

		[TestMethod]
		public void ReadPointRecord_CanReadWithoutSourceFileInfo() {
			var data = "<point id=\"2\" />";
			var record = ReadPointRecordCommon(data);

			Assert.AreEqual(2, record.Id);
			Assert.AreEqual(1, record.ParentId);
			Assert.IsNull(record.SourceFileId);
			Assert.IsNull(record.StartLine);
			Assert.IsNull(record.StartColumn);
			Assert.IsNull(record.EndLine);
			Assert.IsNull(record.EndColumn);
		}

		private IPropertyRecord ReadPropertyRecordCommon(string content) {
			using (var reader = XmlReader.Create(new StringReader(content))) {
				reader.MoveToContent();
				return InstrumentationDataSerializer_Privates.ReadPropertyRecord(reader, 1);
			}
		}

		private string DefaultPropertyContent(bool leaveOpen = false) {
			return "<property signature=\"property sig\" get=\"1\" set=\"2\" others=\"3;4;5;6\"" + (leaveOpen ? ">" : "/>");
		}

		[TestMethod]
		public void ReadProperty_ReadsAllAttributes() {
			var record = ReadPropertyRecordCommon(DefaultPropertyContent());

			Assert.AreEqual("property sig", record.Signature);
			Assert.AreEqual(1, record.GetMethodId);
			Assert.AreEqual(2, record.SetMethodId);

			CollectionAssert.AreEqual(new[] { 3, 4, 5, 6 }, record.OtherMethodIds);
		}

		[TestMethod]
		public void ReadProperty_ReadsCustomAttributeElements() {
			var data = DefaultPropertyContent(true) +
				"<customAttributes>" +
				"<customAttribute>attribute1</customAttribute>" +
				"<customAttribute>attribute2</customAttribute>" +
				"</customAttributes>" +
				"</property>";
			var record = ReadPropertyRecordCommon(data);

			CollectionAssert.AreEqual(new[] { "attribute1", "attribute2" }, record.CustomAttributes);
		}

		private ISourceFileRecord ReadSourceFileRecordCommon(string content) {
			using (var reader = XmlReader.Create(new StringReader(content))) {
				reader.MoveToContent();
				return InstrumentationDataSerializer_Privates.ReadSourceFileRecord(reader, 1);
			}
		}

		[TestMethod]
		public void ReadSourceFile_ReadsAllAttributes() {
			var data = "<sourceFile id=\"5\" uri=\"mysig\" hash=\"myhash\" hashAlgorithm=\"myalgorithm\" language=\"mylanguage\" />";

			var record = ReadSourceFileRecordCommon(data);

			Assert.AreEqual(5, record.Id);
			Assert.AreEqual(1, record.ParentId);
			Assert.AreEqual("mysig", record.Signature);
			Assert.AreEqual("myhash", record.Hash);
			Assert.AreEqual("myalgorithm", record.HashAlgorithm);
			Assert.AreEqual("mylanguage", record.SourceLanguage);
		}

		private string WriteCommon(IInstrumentationData record) {
			var builder = new StringBuilder();

			using (var writer = XmlWriter.Create(builder)) {
				InstrumentationDataSerializer_Privates.Write(writer, record);
			}

			return builder.ToString();
		}

		[TestMethod]
		public void Write_WritesAllAttributes() {
			var data = new InstrumentationData();
			data.InstanceHash = "my hash";

			var output = WriteCommon(data);

			StringAssert.Contains(output, "version=\"" + InstrumentationDataSerializer.CurrentVersion + "\"");
			StringAssert.Contains(output, "instanceHash=\"my hash\"");
		}

		[TestMethod]
		public void Write_WritesAllAssemblies() {
			var data = new InstrumentationData() {
				InstanceHash = "my hash"
			};

			var assem1 = new AssemblyRecord(1, "assem1", HASH, HASHTYPE);
			assem1.AddModule("mod1");
			var assem2 = new AssemblyRecord(2, "assem2", HASH, HASHTYPE);
			assem2.AddModule("mod2");

			data.LinkAssemblies(new List<IAssemblyRecord>() { assem1, assem2 });

			var output = WriteCommon(data);

			ExAssert.ContainsCount(output, "<assembly ", 2);
			StringAssert.Contains(output, "identity=\"assem1\"");
			StringAssert.Contains(output, "identity=\"assem2\"");
		}

		[TestMethod]
		public void Write_OnlyWritesNonEmptyAssemblies() {
			var data = new InstrumentationData() {
				InstanceHash = "my hash"
			};

			var assem1 = new AssemblyRecord(1, "assem1", HASH, HASHTYPE);
			var assem2 = new AssemblyRecord(2, "assem2", HASH, HASHTYPE);
			assem2.AddModule("mod2");

			data.LinkAssemblies(new List<IAssemblyRecord>() { assem1, assem2 });

			var output = WriteCommon(data);

			ExAssert.ContainsCount(output, "<assembly ", 1);
			StringAssert.Contains(output, "identity=\"assem2\"");
		}

		private string WriteAssemblyRecordCommon(IAssemblyRecord record) {
			var builder = new StringBuilder();

			using (var writer = XmlWriter.Create(builder)) {
				InstrumentationDataSerializer_Privates.WriteAssemblyRecord(writer, record);
			}

			return builder.ToString();
		}

		[TestMethod]
		public void WriteAssemblyRecord_WritesAllAttributes() {
			var record = new AssemblyRecord(5, "an identity", HASH, HASHTYPE);
			var output = WriteAssemblyRecordCommon(record);

			StringAssert.Contains(output, "<assembly ");
			StringAssert.Contains(output, "identity=\"an identity\"");
			ExAssert.DoesNotContain(output, "id=\"");
		}

		[TestMethod]
		public void WriteAssemblyRecord_WritesAllElements() {
			var record = new AssemblyRecord(5, "an identity", HASH, HASHTYPE);
			record.AddModule("module1").AddType("type1");
			record.AddModule("module2").AddType("type2");
			var output = WriteAssemblyRecordCommon(record);

			StringAssert.Contains(output, "<assembly ");
			StringAssert.Contains(output, "</assembly>");
			StringAssert.Contains(output, "<modules>");
			StringAssert.Contains(output, "</modules>");

			ExAssert.ContainsCount(output, "<module ", 2);
		}

		[TestMethod]
		public void WriteAssemblyRecord_OnlyWritesModulesWithTypes() {
			var record = new AssemblyRecord(5, "an identity", HASH, HASHTYPE);
			record.AddModule("module1");
			record.AddModule("module2").AddType("type2");
			var output = WriteAssemblyRecordCommon(record);

			ExAssert.ContainsCount(output, "<module ", 1);
			StringAssert.Contains(output, "name=\"module2\"");
		}

		[TestMethod]
		public void WriteAssemblyRecord_OnlyWritesModulesWithSourceFiles() {
			var record = new AssemblyRecord(5, "an identity", HASH, HASHTYPE);
			record.AddModule("module1");
			record.AddModule("module2").AddSourceFile("file2", HASH, HASHTYPE.ToString(), "C#");

			var output = WriteAssemblyRecordCommon(record);

			ExAssert.ContainsCount(output, "<sourceFile ", 1);
			StringAssert.Contains(output, "uri=\"file2\"");
		}

		private string WriteModuleRecordCommon(IModuleRecord record) {
			var builder = new StringBuilder();

			using (var writer = XmlWriter.Create(builder)) {
				InstrumentationDataSerializer_Privates.WriteModuleRecord(writer, record);
			}

			return builder.ToString();
		}

		[TestMethod]
		public void WriteModuleRecord_WritesAllAttributes() {
			var record = new ModuleRecord(5, 3, "my module");
			var output = WriteModuleRecordCommon(record);

			StringAssert.Contains(output, "<module ");
			StringAssert.Contains(output, "name=\"my module\"");
		}

		[TestMethod]
		public void WriteModuleRecord_WritesAllElements() {
			var record = new AssemblyRecord(5, "an identity", HASH, HASHTYPE).AddModule("module");
			record.AddSourceFile("file 1", null, null, null);
			record.AddSourceFile("file 2", null, null, null);
			record.AddType("type 1").AddMethod("method1", HASH, HASHTYPE);
			record.AddType("type 2").AddMethod("method2", HASH, HASHTYPE);
			var output = WriteModuleRecordCommon(record);

			StringAssert.Contains(output, "<module ");
			StringAssert.Contains(output, "</module>");
			StringAssert.Contains(output, "<types>");
			StringAssert.Contains(output, "</types>");
			StringAssert.Contains(output, "<sourceFiles>");
			StringAssert.Contains(output, "</sourceFiles>");

			ExAssert.ContainsCount(output, "<type ", 2);
			ExAssert.ContainsCount(output, "<sourceFile ", 2);
		}

		[TestMethod]
		public void WriteModuleRecord_OnlyWritesTypesWithMethods() {
			var record = new AssemblyRecord(5, "an identity", HASH, HASHTYPE).AddModule("module");
			record.AddType("type1").AddMethod("method1", HASH, HASHTYPE);
			record.AddType("type2");

			var output = WriteModuleRecordCommon(record);

			ExAssert.ContainsCount(output, "<type ", 1);
			StringAssert.Contains(output, "signature=\"type1\"");
		}

		[TestMethod]
		public void WriteModuleRecord_OnlyWritesTypesWithProperties() {
			var record = new AssemblyRecord(5, "an identity", HASH, HASHTYPE).AddModule("module");
			record.AddType("type1").AddProperty("prop1", 1,2);
			record.AddType("type2");

			var output = WriteModuleRecordCommon(record);

			ExAssert.ContainsCount(output, "<type ", 1);
			StringAssert.Contains(output, "signature=\"type1\"");
		}

		[TestMethod]
		public void WriteModuleRecord_OnlyWritesTypesWithEvents() {
			var record = new AssemblyRecord(5, "an identity", HASH, HASHTYPE).AddModule("module");
			record.AddType("type1").AddEvent("event1", 1,2);
			record.AddType("type2");

			var output = WriteModuleRecordCommon(record);

			ExAssert.ContainsCount(output, "<type ", 1);
			StringAssert.Contains(output, "signature=\"type1\"");
		}

		[TestMethod]
		public void WriteSourceFileRecord_WritesAllAttributes() {
			var record = new SourceFileRecord(2, 3, "my uri", "myhash", "myalgorithm", "mylang");
			var builder = new StringBuilder();

			using (var writer = XmlWriter.Create(builder)) {
				InstrumentationDataSerializer_Privates.WriteSourceFileRecord(writer, record);
			}

			var output = builder.ToString();

			StringAssert.Contains(output, "<sourceFile ");
			StringAssert.Contains(output, "id=\"2\"");
			StringAssert.Contains(output, "uri=\"my uri\"");
			StringAssert.Contains(output, "hash=\"myhash\"");
			StringAssert.Contains(output, "hashAlgorithm=\"myalgorithm\"");
			StringAssert.Contains(output, "language=\"mylang\"");
		}

		private string WriteTypeRecordCommon(ITypeRecord record) {
			var builder = new StringBuilder();

			using (var writer = XmlWriter.Create(builder)) {
				InstrumentationDataSerializer_Privates.WriteTypeRecord(writer, record);
			}

			return builder.ToString();
		}

		[TestMethod]
		public void WriteTypeRecord_WritesAllAttributes() {
			var record = new TypeRecord(1, 2, "my type sig");
			var output = WriteTypeRecordCommon(record);

			StringAssert.Contains(output, "<type ");
			StringAssert.Contains(output, "signature=\"my type sig\"");
			ExAssert.DoesNotContain(output, "id=\"");
		}

		[TestMethod]
		public void WriteTypeRecord_WritesMethodElements() {
			var record = new AssemblyRecord(5, "an identity", HASH, HASHTYPE).AddModule("module").AddType("type");
			record.AddMethod("sig1", "hash", HASHTYPE);
			record.AddMethod("sig2", "hash", HASHTYPE);
			var output = WriteTypeRecordCommon(record);

			StringAssert.Contains(output, "<type ");
			StringAssert.Contains(output, "</type>");
			StringAssert.Contains(output, "<methods>");
			StringAssert.Contains(output, "</methods>");

			ExAssert.ContainsCount(output, "<method ", 2);
		}

		[TestMethod]
		public void WriteTypeRecord_WritesCustomAttributeElements() {
			var record = new AssemblyRecord(5, "an identity", HASH, HASHTYPE)
				.AddModule("module")
				.AddType("type", new[] { "attribute1", "attribute2" });
			var output = WriteTypeRecordCommon(record);

			StringAssert.Contains(output, "<customAttributes>");
			StringAssert.Contains(output, "</customAttributes>");
			StringAssert.Contains(output, "<customAttribute>attribute1</customAttribute>");
			StringAssert.Contains(output, "<customAttribute>attribute2</customAttribute>");

		}

		[TestMethod]
		public void WriteTypeRecord_WritesGenericArgumentElements() {
			var record = new AssemblyRecord(5, "an identity", HASH, HASHTYPE)
				.AddModule("module")
				.AddType("type", genericArguments: new[] { "T", "K" });
			var output = WriteTypeRecordCommon(record);

			StringAssert.Contains(output, "<genericArguments>");
			StringAssert.Contains(output, "</genericArguments>");
			StringAssert.Contains(output, "<genericArgument>T</genericArgument>");
			StringAssert.Contains(output, "<genericArgument>K</genericArgument>");
		}

		private string WriteEventRecordCommon(IEventRecord record) {
			var builder = new StringBuilder();

			using (var writer = XmlWriter.Create(builder)) {
				InstrumentationDataSerializer_Privates.WriteEventRecord(writer, record);
			}

			return builder.ToString();
		}

		[TestMethod]
		public void WriteEventRecord_WritesAllAttributes() {
			var record = new EventRecord(1, 2, "sig", 1, 2, 3, new[] { 4, 5 });
			var output = WriteEventRecordCommon(record);

			StringAssert.Contains(output, "<event");
			StringAssert.Contains(output, "signature=\"sig\"");
			StringAssert.Contains(output, "add=\"1\"");
			StringAssert.Contains(output, "remove=\"2\"");
			StringAssert.Contains(output, "fire=\"3\"");
			StringAssert.Contains(output, "others=\"4;5\"");
		}

		[TestMethod]
		public void WriteEventRecord_WritesCustomAttributeElements() {
			var record = new EventRecord(1, 2, "sig", 1, 2, 3, customAttributes: new[] { "attribute1", "attribute2" });
			var output = WriteEventRecordCommon(record);

			StringAssert.Contains(output, "<customAttributes>");
			StringAssert.Contains(output, "</customAttributes>");
			StringAssert.Contains(output, "<customAttribute>attribute1</customAttribute>");
			StringAssert.Contains(output, "<customAttribute>attribute2</customAttribute>");
		}

		private string WriteMethodRecordCommon(IMethodRecord record) {
			var builder = new StringBuilder();

			using (var writer = XmlWriter.Create(builder)) {
				InstrumentationDataSerializer_Privates.WriteMethodRecord(writer, record);
			}

			return builder.ToString();
		}

		[TestMethod]
		public void WriteMethodRecord_WritesAllAttributes() {
			var method = new MethodRecord(1, 2, "sig", "myhash", HASHTYPE, sourceFileIds: new[] { 1, 2, 3 });
			var output = WriteMethodRecordCommon(method);

			StringAssert.Contains(output, "<method ");
			StringAssert.Contains(output, "id=\"1\"");
			StringAssert.Contains(output, "sourceFileIds=\"1;2;3\"");
			StringAssert.Contains(output, "signature=\"sig\"");
			StringAssert.Contains(output, "hash=\"myhash\"");
			StringAssert.Contains(output, "hashAlgorithm=\"SHA512\"");
		}

		[TestMethod]
		public void WriteMethodRecord_WhenHasNoSourceFiles_NoSourceFileIdsWritten() {
			var method = new MethodRecord(1, 2, "sig", "hash", HASHTYPE);
			var output = WriteMethodRecordCommon(method);

			ExAssert.DoesNotContain(output, "sourceFileIds");
		}

		[TestMethod]
		public void WriteMethodRecord_WhenHasNoHash_NoHashIsWritten() {
			var method = new MethodRecord(1, 2, "sig", "", HASHTYPE);
			var output = WriteMethodRecordCommon(method);

			ExAssert.DoesNotContain(output, "hash");
			ExAssert.DoesNotContain(output, "hashAlgorithm");
		}

		[TestMethod]
		public void WriteMethodRecord_WritesCustomAttributeElements() {
			var method = new MethodRecord(1, 2, "sig", "hash", HASHTYPE, new[] { "attribute1", "attribute2" });
			var output = WriteMethodRecordCommon(method);

			StringAssert.Contains(output, "<customAttributes>");
			StringAssert.Contains(output, "</customAttributes>");
			StringAssert.Contains(output, "<customAttribute>attribute1</customAttribute>");
			StringAssert.Contains(output, "<customAttribute>attribute2</customAttribute>");
		}

		[TestMethod]
		public void WriteMethodRecord_WritesGenericArgumentElements() {
			var method = new MethodRecord(1, 2, "sig", "hash", HASHTYPE, genericArguments: new[] { "T", "K" });
			var output = WriteMethodRecordCommon(method);

			StringAssert.Contains(output, "<genericArguments>");
			StringAssert.Contains(output, "</genericArguments>");
			StringAssert.Contains(output, "<genericArgument>T</genericArgument>");
			StringAssert.Contains(output, "<genericArgument>K</genericArgument>");
		}

		[TestMethod]
		public void WriteMethodRecord_WritesPointElements() {
			var method = new MethodRecord(1, 2, "sig", "hash", HASHTYPE);
			method.AddPoint(3, 4, 5, 6, 7);
			method.AddPoint(8, 9, 10, 11, 12);

			var output = WriteMethodRecordCommon(method);

			StringAssert.Contains(output, "<points>");
			StringAssert.Contains(output, "</points>");
			StringAssert.Contains(output, "<point id=\"");
		}

		private string WritePointRecordCommon(IPointRecord record) {
			var builder = new StringBuilder();

			using (var writer = XmlWriter.Create(builder)) {
				InstrumentationDataSerializer_Privates.WritePointRecord(writer, record);
			}

			return builder.ToString();
		}

		[TestMethod]
		public void WritePointRecord_WritesAllAttributes() {
			var record = new PointRecord(1, 2, 3, 4, 5, 6, 7);
			var output = WritePointRecordCommon(record);

			StringAssert.Contains(output, "<point");
			StringAssert.Contains(output, "/>");
			StringAssert.Contains(output, "id=\"1\"");
			StringAssert.Contains(output, "sourceFileId=\"3\"");
			StringAssert.Contains(output, "startLine=\"4\"");
			StringAssert.Contains(output, "startColumn=\"5\"");
			StringAssert.Contains(output, "endLine=\"6\"");
			StringAssert.Contains(output, "endColumn=\"7\"");
		}

		[TestMethod]
		public void WritePointRecord_DoesNotWriteSignature() {
			var record = new PointRecord(1, 2, 3, 4, 5, 6, 7);
			var output = WritePointRecordCommon(record);

			ExAssert.DoesNotContain(output, record.Signature);
		}

		[TestMethod]
		public void WritePointRecord_NoSourceFileInfo_DoesntWriteSourceFileInfo() {
			var record = new PointRecord(1, 2);
			var output = WritePointRecordCommon(record);

			ExAssert.DoesNotContain(output, "sourceFileId=\"3\"");
			ExAssert.DoesNotContain(output, "startLine=\"4\"");
			ExAssert.DoesNotContain(output, "startColumn=\"5\"");
			ExAssert.DoesNotContain(output, "endLine=\"6\"");
			ExAssert.DoesNotContain(output, "endColumn=\"7\"");
		}

		[TestMethod]
		public void WritePointRecord_NoSourceFileInfo_WritesId() {
			var record = new PointRecord(1, 2);
			var output = WritePointRecordCommon(record);

			StringAssert.Contains(output, "id=\"1\"");
		}


		private string WritePropertyRecordCommon(IPropertyRecord record) {
			var builder = new StringBuilder();

			using (var writer = XmlWriter.Create(builder)) {
				InstrumentationDataSerializer_Privates.WritePropertyRecord(writer, record);
			}

			return builder.ToString();
		}

		[TestMethod]
		public void WritePropertyRecord_WritesAllAttributes() {
			var record = new PropertyRecord(1, 2, "sig", 1, 2, new[] { 3, 4, 5 });
			var output = WritePropertyRecordCommon(record);

			StringAssert.Contains(output, "<property");
			StringAssert.Contains(output, "/>");
			StringAssert.Contains(output, "signature=\"sig\"");
			StringAssert.Contains(output, "get=\"1\"");
			StringAssert.Contains(output, "set=\"2\"");
			StringAssert.Contains(output, "others=\"3;4;5\"");
		}

		[TestMethod]
		public void WritePropertyRecord_WritesCustomAttributeElements() {
			var record = new PropertyRecord(1, 2, "sig", 1, 2, customAttributes: new[] { "attribute1", "attribute2" });
			var output = WritePropertyRecordCommon(record);

			StringAssert.Contains(output, "<customAttributes>");
			StringAssert.Contains(output, "</customAttributes>");
			StringAssert.Contains(output, "<customAttribute>attribute1</customAttribute>");
			StringAssert.Contains(output, "<customAttribute>attribute2</customAttribute>");
		}
	}
}
