using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common.UnitTests.Instrumentation {

	[TestClass]
	public class InstrumentationDataTest {

		private static string HASH = "BLAHHASH";
		private static HashAlgorithm HASHTYPE = HashAlgorithm.SHA512;

		[TestMethod]
		public void Constructor_InitializesDefaultValues() {
			var x = new InstrumentationData();
			Assert.IsNotNull(x.Assemblies);
			Assert.IsNotNull(x.InstanceHash);
			ExAssert.IsEmpty(x.Assemblies);
		}


		[TestMethod]
		public void InstanceHash_VerifyHashIsRead() {
			var expected = Guid.NewGuid().ToString() + "|" + Guid.NewGuid().ToString();
			var x = new InstrumentationData() {
				InstanceHash = expected
			};

			Assert.AreEqual(expected, x.InstanceHash);

		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentNullException))]
		public void InstanceHash_FailIfHashIsNull() {
			var x = new InstrumentationData() {
				InstanceHash = null
			};

			Assert.IsNull(x.InstanceHash);
		}

		[TestMethod]
		public void LinkAssemblies_HandlesNullInput() {
			var x = new InstrumentationData();

			x.LinkAssemblies(null);

			ExAssert.IsEmpty(x.Assemblies);
		}

		[TestMethod]
		public void LinkAssemblies_HandlesEmptyInput() {
			var x = new InstrumentationData();

			x.LinkAssemblies(new IAssemblyRecord[] { });

			ExAssert.IsEmpty(x.Assemblies);
		}

		[TestMethod]
		public void LinkAssemblies_FiltersNullElements() {
			var x = new InstrumentationData();

			x.LinkAssemblies(new IAssemblyRecord[] { null, null, null });

			ExAssert.IsEmpty(x.Assemblies);
		}

		[TestMethod]
		public void LinkAssemblies_AddsAllNonNullElements() {
			var x = new InstrumentationData();

			var mod1 = new AssemblyRecord(1, "assembly1", HASH, HASHTYPE);
			var mod2 = new AssemblyRecord(2, "assembly2", HASH, HASHTYPE);
			var mod3 = new AssemblyRecord(3, "assembly3", HASH, HASHTYPE);

			x.LinkAssemblies(new IAssemblyRecord[] { null, mod1, mod2, null, mod3 });

			Assert.AreEqual(3, x.Assemblies.Count);
			CollectionAssert.Contains(x.Assemblies, mod1);
			CollectionAssert.Contains(x.Assemblies, mod2);
			CollectionAssert.Contains(x.Assemblies, mod3);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkAssemblies_FailsIfAnyHaveDuplicateNames() {
			var x = new InstrumentationData();

			var mod1 = new AssemblyRecord(1, "assembly1", HASH, HASHTYPE);
			var mod2 = new AssemblyRecord(2, "assembly2", HASH, HASHTYPE);
			var mod3 = new AssemblyRecord(3, "assembly1", HASH, HASHTYPE);

			x.LinkAssemblies(new IAssemblyRecord[] { null, mod1, mod2, null, mod3 });
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LinkAssemblies_FailsIfAnyNameDuplicatesNameAlreadyInAssemblyList() {
			var x = new InstrumentationData();

			var mod1 = new AssemblyRecord(1, "assembly1", HASH, HASHTYPE);
			var mod2 = new AssemblyRecord(2, "assembly2", HASH, HASHTYPE);
			var mod3 = new AssemblyRecord(3, "assembly1", HASH, HASHTYPE);

			x.LinkAssemblies(new[] { mod1 });
			x.LinkAssemblies(new[] { mod2, null, mod3 });
		}
	}
}
