using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common.UnitTests.Instrumentation {
	[TestClass]
	public class ModuleRecordTest {

		[TestMethod]
		public void AddSourceFile_SetsModuleIdToOwnId() {
			var x = new ModuleRecord(4, 5, "new module");

			var file = x.AddSourceFile("new file", null, null, null);
			Assert.AreEqual(4, file.ParentId);
		}

		[TestMethod]
		public void AddSourceFile_AddsNewSourceFileToSourceFileCollection() {
			var x = new ModuleRecord(4, 5, "new module");

			var file = x.AddSourceFile("new file", null, null, null);
			CollectionAssert.Contains(x.SourceFiles, file);
		}

		[TestMethod]
		public void AddType_SetsModuleIdToOwnId() {
			var x = new ModuleRecord(4, 5, "new module");

			var type = x.AddType("new type");
			Assert.AreEqual(4, type.ParentId);
		}

		[TestMethod]
		public void AddType_AddsNewTypeToTypeCollection() {
			var x = new ModuleRecord(4, 5, "new module");

			var type = x.AddType("new type");
			CollectionAssert.Contains(x.Types, type);
		}

		[TestMethod]
		public void AddType_AddsCustomAttributes() {
			var expected = new[] { "attr1", "attr2" };
			var x = new ModuleRecord(4, 5, "name");

			var type = x.AddType("sig", expected);

			CollectionAssert.AreEqual(expected, type.CustomAttributes);
		}

		[TestMethod]
		public void AddType_AddsGenericArguments() {
			var expected = new[] { "one", "two", "three" };
			var x = new ModuleRecord(4, 5, "name");

			var type = x.AddType("sig", genericArguments: expected);

			CollectionAssert.AreEqual(expected, type.GenericArguments);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkSourceFiles_RejectsSourceFilesForOtherModules() {
			var x = new ModuleRecord(4, 5, "new module");

			var file = new SourceFileRecord(1, 34, "hello", null, null, null);

			x.LinkSourceFiles(new[] { file });
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkSourceFiles_RejectsSourceFilesAlreadyLinkedToModule() {
			var x = new ModuleRecord(4, 5, "new module");

			var file1 = x.AddSourceFile("hello", null, null, null);
			var file2 = x.AddSourceFile("hello2", null, null, null);

			x.LinkSourceFiles(new[] { file1, file2 });
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkSourceFiles_RejectsDuplicateSourceFiles() {
			var x = new ModuleRecord(4, 5, "new module");

			var file1 = new SourceFileRecord(6, 4, "file1", null, null, null);
			var file2 = new SourceFileRecord(7, 4, "file1", null, null, null);

			x.LinkSourceFiles(new[] { file1, file2 });
		}

		[TestMethod]
		public void LinkSourceFiles_AddsNewSourceFiles() {
			var x = new ModuleRecord(4, 5, "new module");

			var file1 = new SourceFileRecord(6, 4, "file1", null, null, null);
			var file2 = new SourceFileRecord(7, 4, "file2", null, null, null);

			x.LinkSourceFiles(new[] { file1, file2 });

			Assert.AreEqual(2, x.SourceFiles.Count);
			CollectionAssert.Contains(x.SourceFiles, file1);
			CollectionAssert.Contains(x.SourceFiles, file2);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkTypes_RejectsTypesAlreadyLinkedToModule() {
			var x = new ModuleRecord(4, 5, "new module");

			var type = x.AddType("type1");

			x.LinkTypes(new[] { type });
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkTypes_RejectsTypesForOtherModules() {
			var x = new ModuleRecord(4, 5, "new module");

			var type = new TypeRecord(1, 7, "type");

			x.LinkTypes(new[] { type });
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkTypes_RejectsDuplicateTypes() {
			var x = new ModuleRecord(4, 5, "new module");

			var type1 = new TypeRecord(1, 4, "type");
			var type2 = new TypeRecord(2, 4, "type");

			x.LinkTypes(new[] { type1, type2 });
		}

		[TestMethod]
		public void LinkTypes_AddsNewTypes() {
			var x = new ModuleRecord(4, 5, "new module");

			var type = new TypeRecord(1, 4, "type");

			x.LinkTypes(new[] { type });

			Assert.AreEqual(1, x.Types.Count);
			CollectionAssert.Contains(x.Types, type);
		}

	}
}
