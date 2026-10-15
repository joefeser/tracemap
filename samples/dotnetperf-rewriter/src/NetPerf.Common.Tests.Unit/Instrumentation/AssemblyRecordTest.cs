using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common.UnitTests.Instrumentation {

	[TestClass]
	public class AssemblyRecordTest {

		private static string HASH = "BLAHHASH";
		private static HashAlgorithm HASHTYPE = HashAlgorithm.SHA512;

		[TestMethod]
		public void Constructor_InitializesDefaultValues() {
			var x = new AssemblyRecord(5, "hello", HASH, HASHTYPE);

			Assert.IsNotNull(x.Modules);
			Assert.AreEqual(0, x.Modules.Count);
		}

		[TestMethod]
		public void AddModule_ReturnsPriorModuleWhenDuplicateNameAdded() {
			var x = new AssemblyRecord(5, "test assembly", HASH, HASHTYPE);

			var original = x.AddModule("test module");
			var copy = x.AddModule("test module");

			Assert.AreSame(original, copy);
		}

		[TestMethod]
		public void AddModule_SetsModuleAssemblyIdToOwnId() {
			var x = new AssemblyRecord(5, "test assembly", HASH, HASHTYPE);

			var module = x.AddModule("test module");

			Assert.AreEqual(x.Id, module.ParentId);
		}

		[TestMethod]
		public void AddModule_AddsNewModuleToModuleCollection() {
			var x = new AssemblyRecord(5, "test assembly", HASH, HASHTYPE);

			var module = x.AddModule("test module");

			Assert.AreEqual(1, x.Modules.Count);

			var first = x.Modules[0];

			Assert.AreSame(module, first);
		}

		[TestMethod]
		public void LinkModules_HandlesNullInput() {
			var x = new AssemblyRecord(5, "test assembly", HASH, HASHTYPE);

			x.LinkModules(null);

			ExAssert.IsEmpty(x.Modules);
		}

		[TestMethod]
		public void LinkModules_HandlesEmptyInput() {
			var x = new AssemblyRecord(5, "test assembly", HASH, HASHTYPE);

			x.LinkModules(new IModuleRecord[] { });

			ExAssert.IsEmpty(x.Modules);
		}

		[TestMethod]
		public void LinkModules_FiltersNullElements() {
			var x = new AssemblyRecord(5, "test assembly", HASH, HASHTYPE);

			x.LinkModules(new IModuleRecord[] { null, null, null });

			ExAssert.IsEmpty(x.Modules);
		}

		[TestMethod]
		public void LinkModules_AddsAllNonNullElements() {
			var x = new AssemblyRecord(5, "test assembly", HASH, HASHTYPE);
			var factory = new AssemblyRecord(5, "factory", HASH, HASHTYPE);

			var mod1 = factory.AddModule("module 1");
			var mod2 = factory.AddModule("module 2");
			var mod3 = factory.AddModule("module 3");

			x.LinkModules(new IModuleRecord[] { null, mod1, mod2, null, mod3 });

			Assert.AreEqual(3, x.Modules.Count);
			CollectionAssert.Contains(x.Modules, mod1);
			CollectionAssert.Contains(x.Modules, mod2);
			CollectionAssert.Contains(x.Modules, mod3);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkModules_FailsIfAnyAreNotLinkedToTheAssembly() {
			var x = new AssemblyRecord(5, "test assembly", HASH, HASHTYPE);
			var factory = new AssemblyRecord(5, "factory", HASH, HASHTYPE);
			var factory2 = new AssemblyRecord(7, "hello", HASH, HASHTYPE);

			var mod1 = factory.AddModule("module 1");
			var mod2 = factory2.AddModule("module 2");
			var mod3 = factory.AddModule("module 3");

			x.LinkModules(new IModuleRecord[] { null, mod1, mod2, null, mod3 });
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkModules_FailsIfAnyHaveDuplicateNames() {
			var x = new AssemblyRecord(5, "test assembly", HASH, HASHTYPE);
			var factory = new AssemblyRecord(5, "factory", HASH, HASHTYPE);
			var factory2 = new AssemblyRecord(5, "hello", HASH, HASHTYPE);

			var mod1 = factory.AddModule("module 1");
			var mod2 = factory.AddModule("module 2");
			var mod3 = factory2.AddModule("module 1");

			x.LinkModules(new IModuleRecord[] { null, mod1, mod2, null, mod3 });
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkModules_FailsIfAnyNameDuplicatesNameInModuleList() {
			var x = new AssemblyRecord(5, "test assembly", HASH, HASHTYPE);
			var factory = new AssemblyRecord(5, "factory", HASH, HASHTYPE);

			var mod1 = factory.AddModule("module 1");
			var mod2 = factory.AddModule("module 2");
			var mod3 = factory.AddModule("module 1");

			x.LinkModules(new[] { mod1 });
			x.LinkModules(new[] { mod2, null, mod3 });
		}

	}
}
