using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;


namespace NetPerf.Common.UnitTests.Instrumentation {

	[TestClass]
	public class InstrumentFactoryTest {

		private static string HASH = "BLAHHASH";
		private static HashAlgorithm HASHTYPE = HashAlgorithm.SHA512;

		[TestMethod]
		public void Constructor_InitializesDefaultValues() {
			var x = new InstrumentFactory();

			Assert.IsNotNull(x.Assemblies);
			ExAssert.IsEmpty(x.Assemblies);
			Assert.IsNotNull(x.MessageController);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void LookupSourceFile_FailsIfMethodNotFound() {
			var x = new InstrumentFactory();

			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			var sourceId = x.RegisterSourceFile(moduleId, "sourcefile", null, null, null);
			var methodId = x.RegisterMethod(x.RegisterType(moduleId, "mytype"), "sig", sourceFileIds: new[] { sourceId });

			x.LookupSourceFile(methodId + 1, "sourcefile");
		}

		[TestMethod]
		public void LookupSourceFile_MethodHasNoSourceFiles_ReturnsNegativeOne() {
			var x = new InstrumentFactory();

			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			var sourceId = x.RegisterSourceFile(moduleId, "sourcefile", null, null, null);
			var methodId = x.RegisterMethod(x.RegisterType(moduleId, "mytype"), "sig");

			var lookupId = x.LookupSourceFile(methodId, "sourcefile");

			Assert.AreEqual(-1, lookupId);
		}

		[TestMethod]
		public void LookupSourceFile_SourceFileNotForMethod_ReturnsNegativeOne() {
			var x = new InstrumentFactory();

			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			var sourceId = x.RegisterSourceFile(moduleId, "sourcefile", null, null, null);
			var methodId = x.RegisterMethod(x.RegisterType(moduleId, "mytype"), "sig", sourceFileIds: new[] { sourceId + 1 });

			var lookupId = x.LookupSourceFile(methodId, "sourcefile");

			Assert.AreEqual(-1, lookupId);
		}

		[TestMethod]
		public void LookupSourceFile_ModuleHasNoSourceFiles_ReturnsNegativeOne() {
			var x = new InstrumentFactory();

			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			var methodId = x.RegisterMethod(x.RegisterType(moduleId, "mytype"), "sig", sourceFileIds: new[] { 1, 2, 3 });

			var lookupId = x.LookupSourceFile(methodId, "sourcefile");

			Assert.AreEqual(-1, lookupId);
		}

		[TestMethod]
		public void LookupSourceFile_SourceFileIsForMethod_ReturnsSourceFileId() {
			var x = new InstrumentFactory();

			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			var sourceId = x.RegisterSourceFile(moduleId, "sourcefile", null, null, null);
			var methodId = x.RegisterMethod(x.RegisterType(moduleId, "mytype"), "sig", sourceFileIds: new[] { 5, sourceId, 6, 8 });

			var actualId = x.LookupSourceFile(methodId, "sourcefile");

			Assert.AreEqual(sourceId, actualId);
		}

		[TestMethod]
		public void RegisterAssembly_AddsNewAssemblyIfIdentityHasNoMatches() {
			var x = new InstrumentFactory();

			var ida = x.RegisterAssembly("identitya", HASH, HASHTYPE);
			var idb = x.RegisterAssembly("identityb", HASH, HASHTYPE);

			Assert.AreEqual(2, x.Assemblies.Count);
			CollectionAssert.Contains(x.Assemblies.Select(a => a.Id).ToList(), ida);
			CollectionAssert.Contains(x.Assemblies.Select(a => a.Id).ToList(), idb);

			CollectionAssert.Contains(x.Assemblies.Select(a => a.Signature).ToList(), "identitya");
			CollectionAssert.Contains(x.Assemblies.Select(a => a.Signature).ToList(), "identityb");
		}

		[TestMethod]
		public void RegisterAssembly_ReturnsExistingAssemblyIfIdentityMatches() {
			var x = new InstrumentFactory();

			var identity = "assembly identity";
			var expectedId = x.RegisterAssembly(identity, HASH, HASHTYPE);
			var actualId = x.RegisterAssembly(identity, HASH, HASHTYPE);

			Assert.AreEqual(1, x.Assemblies.Count);
			Assert.AreEqual(expectedId, actualId);
		}

		[TestMethod]
		public void RegisterAssembly_MatchesAssemblyIdentityCaseInsensitivly() {
			var x = new InstrumentFactory();

			var identity = "assembly identity";
			var expectedId = x.RegisterAssembly(identity, HASH, HASHTYPE);
			var actualId = x.RegisterAssembly(identity.ToUpperInvariant(), HASH, HASHTYPE);

			Assert.AreEqual(1, x.Assemblies.Count);
			Assert.AreEqual(expectedId, actualId);
		}

		[TestMethod]
		public void RegisterModule_AddsModuleToAssemblyWithTheGivenId() {
			var x = new InstrumentFactory();

			x.RegisterAssembly("assemblya", HASH, HASHTYPE);
			var assemblyId = x.RegisterAssembly("assemblyb", HASH, HASHTYPE);

			x.RegisterModule(assemblyId, "module");

			var expectedEmpty = x.Assemblies.Where(a => a.Id != assemblyId).First();
			var expectedPopulated = x.Assemblies.Where(a => a.Id == assemblyId).First();
			Assert.AreEqual(0, expectedEmpty.Modules.Count);
			Assert.AreEqual(1, expectedPopulated.Modules.Count);
		}

		[TestMethod]
		public void RegisterModule_AddsNewModuleIfNameHasNoMatches() {
			var x = new InstrumentFactory();

			var name = "module";
			var assemblyId = x.RegisterAssembly("assembly", HASH, HASHTYPE);
			var id = x.RegisterModule(assemblyId, name);

			Assert.AreEqual(1, x.Assemblies[0].Modules.Count);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void RegisterModule_FailsIfAssemblyIdIsNotFound() {
			var x = new InstrumentFactory();

			var assemblyId = x.RegisterAssembly("assembly", HASH, HASHTYPE);
			x.RegisterModule(assemblyId + 1, "should fail");
		}

		[TestMethod]
		public void RegisterModule_ReturnsExistingModuleIfNameMatches() {
			var x = new InstrumentFactory();

			var assemblyId = x.RegisterAssembly("assembly", HASH, HASHTYPE);
			var expectedId = x.RegisterModule(assemblyId, "module");
			var actualId = x.RegisterModule(assemblyId, "module");

			Assert.AreEqual(expectedId, actualId);
			Assert.AreEqual(1, x.Assemblies[0].Modules.Count);
		}

		[TestMethod]
		public void RegisterType_AddsTypeToModuleWithTheGivenId() {
			var x = new InstrumentFactory();

			var assemblyId = x.RegisterAssembly("assembly", HASH, HASHTYPE);
			x.RegisterModule(assemblyId, "modulea");
			var moduleId = x.RegisterModule(assemblyId, "moduleb");

			x.RegisterType(moduleId, "type signature", null);

			var expectedEmpty = (from a in x.Assemblies
								 from m in a.Modules
								 where m.Id != moduleId
								 select m).First();
			var expectedPopulated = (from a in x.Assemblies
									 from m in a.Modules
									 where m.Id == moduleId
									 select m).First();

			Assert.AreEqual(0, expectedEmpty.Types.Count);
			Assert.AreEqual(1, expectedPopulated.Types.Count);
		}

		[TestMethod]
		public void RegisterType_AddsNewTypeIfSignatureHasNoMatches() {
			var x = new InstrumentFactory();

			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			var firstId = x.RegisterType(moduleId, "typea");
			var secondId = x.RegisterType(moduleId, "typeb");

			Assert.AreNotEqual(firstId, secondId);
			Assert.AreEqual(2, x.Assemblies[0].Modules[0].Types.Count);
		}

		[TestMethod]
		public void RegisterType_AddsCustomAttributesToType() {
			var x = new InstrumentFactory();

			var expected = new[] { "attr1", "attr2" };
			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			x.RegisterType(moduleId, "typea", expected);
			var type = x.Assemblies.First()
				.Modules.First()
				.Types.First();

			CollectionAssert.AreEqual(expected, type.CustomAttributes);
		}

		[TestMethod]
		public void RegisterType_AddsGenericArgumentsToType() {
			var x = new InstrumentFactory();

			var expected = new[] { "one", "two" };
			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			x.RegisterType(moduleId, "typea", genericArguments: expected);
			var type = x.Assemblies.First()
				.Modules.First()
				.Types.First();

			CollectionAssert.AreEqual(expected, type.GenericArguments);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void RegisterType_FailsIfModuleIdIsNotFound() {
			var x = new InstrumentFactory();

			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			x.RegisterType(moduleId + 1, "should fail");
		}

		[TestMethod]
		public void RegisterType_ReturnsExistingTypeIfSignatureMatches() {
			var x = new InstrumentFactory();

			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			var expectedId = x.RegisterType(moduleId, "type");
			var actualId = x.RegisterType(moduleId, "type");

			Assert.AreEqual(expectedId, actualId);
			Assert.AreEqual(1, x.Assemblies[0].Modules[0].Types.Count);
		}


		[TestMethod]
		public void RegisterEvent_AddsNewEventIfEventSignatureHasNoMatches() {
			var x = new InstrumentFactory();

			var typeId = x.RegisterType(x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module"), "type");
			var firstId = x.RegisterEvent(typeId, "sig", 1, 2);
			var secondId = x.RegisterEvent(typeId, "sig2", 1, 2);

			Assert.AreNotEqual(firstId, secondId);
			Assert.AreEqual(2, x.Assemblies[0].Modules[0].Types[0].Events.Count);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void RegisterEvent_FailsIfTypeIdIsNotFound() {
			var x = new InstrumentFactory();

			var typeId = x.RegisterType(x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module"), "type signature");
			x.RegisterEvent(typeId + 1, "sig", 1, 2);
		}

		[TestMethod]
		public void RegisterEvent_ReturnsExistingEventIfEventSignatureMatches() {
			var x = new InstrumentFactory();

			var typeId = x.RegisterType(x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module"), "type");
			var expectedId = x.RegisterEvent(typeId, "sig", 1, 2);
			var actualId = x.RegisterEvent(typeId, "sig", 3, 4);

			Assert.AreEqual(expectedId, actualId);
			Assert.AreEqual(1, x.Assemblies[0].Modules[0].Types[0].Events.Count);
		}

		[TestMethod]
		public void RegisterMethod_AddsMethodToTypeWithTheGivenId() {
			var x = new InstrumentFactory();

			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			x.RegisterType(moduleId, "type signaturea", null);
			var typeId = x.RegisterType(moduleId, "type signature a");

			x.RegisterMethod(typeId, "sig");

			var expectedEmpty = (from a in x.Assemblies
								 from m in a.Modules
								 from t in m.Types
								 where t.Id != typeId
								 select t).First();
			var expectedPopulated = (from a in x.Assemblies
									 from m in a.Modules
									 from t in m.Types
									 where t.Id == typeId
									 select t).First();

			Assert.AreEqual(0, expectedEmpty.Methods.Count);
			Assert.AreEqual(1, expectedPopulated.Methods.Count);
		}

		[TestMethod]
		public void RegisterMethod_CalculatesHashForMethod() {
			var x = new InstrumentFactory();

			var typeId = x.RegisterType(x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module"), "type");
			var methodId = x.RegisterMethod(typeId, "sig1", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

			var methods = from a in x.Assemblies
						  from m in a.Modules
						  from t in m.Types
						  from mm in t.Methods
						  where mm.Id == methodId && mm.ParentId == typeId
						  select mm;

			var method = methods.Single();
			var expectedHash = "F5A56821F77AC3A3A49B7D39463F2EE191C433F6890DBF590BE373C05D806056FAD5196B3BEE17758C63E6C84FED97564A23E6D0A003811AE8E2F6C6B0EB490A";
			Assert.AreEqual(expectedHash, method.Hash);
			Assert.AreEqual(HashAlgorithm.SHA512, method.HashAlgorithm);
		}

		[TestMethod]
		public void RegisterMethod_AddsCustomAttributesToMethod() {
			var x = new InstrumentFactory();

			var expected = new[] { "attr1", "attr2" };
			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			var typeId = x.RegisterType(moduleId, "typea");
			x.RegisterMethod(typeId, "sig", new byte[1], expected);
			var method = x.Assemblies.First()
				.Modules.First()
				.Types.First()
				.Methods.First();

			Assert.AreEqual(expected.Length, method.CustomAttributes.Count);
			CollectionAssert.AreEqual(expected, method.CustomAttributes);
		}

		[TestMethod]
		public void RegisterMethod_AddsGenericArgumentsToMethod() {
			var x = new InstrumentFactory();

			var expected = new[] { "one", "two" };
			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			var typeId = x.RegisterType(moduleId, "typea");
			x.RegisterMethod(typeId, "sig", new byte[1], genericArguments: expected);
			var method = x.Assemblies.First()
				.Modules.First()
				.Types.First()
				.Methods.First();

			CollectionAssert.AreEqual(expected, method.GenericArguments);
		}

		[TestMethod]
		public void RegisterMethod_AddsSourceFileIdsToMethod() {
			var x = new InstrumentFactory();

			var expected = new[] { 10, 20, 30 };
			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			var typeId = x.RegisterType(moduleId, "typea");
			x.RegisterMethod(typeId, "sig", sourceFileIds: expected);
			var method = x.Assemblies.First()
				.Modules.First()
				.Types.First()
				.Methods.First();

			CollectionAssert.AreEqual(expected, method.SourceFileIds);
		}

		[TestMethod]
		public void RegisterMethod_AddsNewMethodIfMethodSignatureHasNoMatches() {
			var x = new InstrumentFactory();

			var typeId = x.RegisterType(x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module"), "type");
			var firstId = x.RegisterMethod(typeId, "sig1");
			var secondId = x.RegisterMethod(typeId, "sig2");

			Assert.AreNotEqual(firstId, secondId);
			Assert.AreEqual(2, x.Assemblies[0].Modules[0].Types[0].Methods.Count);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void RegisterMethod_FailsIfTypeIdIsNotFound() {
			var x = new InstrumentFactory();

			var typeId = x.RegisterType(x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module"), "type signature");
			x.RegisterMethod(typeId + 1, "sig");
		}

		[TestMethod]
		public void RegisterMethod_ReturnsExistingMethodIfMethodSignatureMatches() {
			var x = new InstrumentFactory();

			var typeId = x.RegisterType(x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module"), "type");
			var expectedId = x.RegisterMethod(typeId, "sig");
			var actualId = x.RegisterMethod(typeId, "sig");

			Assert.AreEqual(expectedId, actualId);
			Assert.AreEqual(1, x.Assemblies[0].Modules[0].Types[0].Methods.Count);
		}

		[TestMethod]
		public void RegisterPoint_AddsNewPointToMethodEvenWhenAllDataIsTheSame() {
			var x = new InstrumentFactory();

			var methodId = x.RegisterMethod(x.RegisterType(x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module"), "type"), "sig");

			var point1 = x.RegisterPoint(methodId, 3, 4, 5, 6, 7);
			var point2 = x.RegisterPoint(methodId, 3, 4, 5, 6, 7);

			Assert.AreNotEqual(point1, point2);
			Assert.AreEqual(2, x.Assemblies[0].Modules[0].Types[0].Methods[0].Points.Count);
		}

		[TestMethod]
		public void RegisterMethod_NoSourceFileInfo_AddsNewPointToMethod() {
			var x = new InstrumentFactory();

			var methodId = x.RegisterMethod(x.RegisterType(x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module"), "type"), "sig");

			var pointId = x.RegisterPoint(methodId);

			Assert.AreEqual(1, x.Assemblies[0].Modules[0].Types[0].Methods[0].Points.Count);
			Assert.AreEqual(pointId, x.Assemblies[0].Modules[0].Types[0].Methods[0].Points[0].Id);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void RegisterPoint_FailsIfMethodIdIsNotFound() {
			var x = new InstrumentFactory();

			var methodId = x.RegisterMethod(x.RegisterType(x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module"), "type"), "sig");

			x.RegisterPoint(methodId + 1, 3, 4, 5, 6, 7);
		}

		[TestMethod]
		public void RegisterProperty_AddsNewPropertyIfPropertySignatureHasNoMatches() {
			var x = new InstrumentFactory();

			var typeId = x.RegisterType(x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module"), "type");
			var firstId = x.RegisterProperty(typeId, "sig", 1, 2);
			var secondId = x.RegisterProperty(typeId, "sig2", 1, 2);

			Assert.AreNotEqual(firstId, secondId);
			Assert.AreEqual(2, x.Assemblies[0].Modules[0].Types[0].Properties.Count);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void RegisterProperty_FailsIfTypeIdIsNotFound() {
			var x = new InstrumentFactory();

			var typeId = x.RegisterType(x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module"), "type signature");
			x.RegisterProperty(typeId + 1, "sig", 1, 2);
		}

		[TestMethod]
		public void RegisterProperty_ReturnsExistingPropertyIfPropertySignatureMatches() {
			var x = new InstrumentFactory();

			var typeId = x.RegisterType(x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module"), "type");
			var expectedId = x.RegisterProperty(typeId, "sig", 1, 2);
			var actualId = x.RegisterProperty(typeId, "sig", 3, 4);

			Assert.AreEqual(expectedId, actualId);
			Assert.AreEqual(1, x.Assemblies[0].Modules[0].Types[0].Properties.Count);
		}

		[TestMethod]
		public void RegisterSourceFile_AddsSourceFileToModuleWithTheGivenId() {
			var x = new InstrumentFactory();

			var assemblyId = x.RegisterAssembly("assembly", HASH, HASHTYPE);
			x.RegisterModule(assemblyId, "modulea");
			var moduleId = x.RegisterModule(assemblyId, "moduleb");

			x.RegisterSourceFile(moduleId, "source file", null, null, null);

			var expectedEmpty = (from a in x.Assemblies
								 from m in a.Modules
								 where m.Id != moduleId
								 select m).First();
			var expectedPopulated = (from a in x.Assemblies
									 from m in a.Modules
									 where m.Id == moduleId
									 select m).First();

			Assert.AreEqual(0, expectedEmpty.SourceFiles.Count);
			Assert.AreEqual(1, expectedPopulated.SourceFiles.Count);
		}

		[TestMethod]
		public void RegisterSourceFile_AddsNewSourceFileIfUriHasNoMatches() {
			var x = new InstrumentFactory();

			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			var firstId = x.RegisterSourceFile(moduleId, "source file a", null, null, null);
			var secondId = x.RegisterSourceFile(moduleId, "source file b", null, null, null);

			Assert.AreNotEqual(firstId, secondId);
			Assert.AreEqual(2, x.Assemblies[0].Modules[0].SourceFiles.Count);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void RegisterSourceFile_FailsIfModuleIdIsNotFound() {
			var x = new InstrumentFactory();

			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			x.RegisterSourceFile(moduleId + 1, "should fail", null, null, null);
		}

		[TestMethod]
		public void RegisterSourceFile_ReturnsExistingSourceFileIfUriMatches() {
			var x = new InstrumentFactory();

			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			var expectedId = x.RegisterSourceFile(moduleId, "source file", null, null, null);
			var actualId = x.RegisterSourceFile(moduleId, "source file", null, null, null);

			Assert.AreEqual(expectedId, actualId);
			Assert.AreEqual(1, x.Assemblies[0].Modules[0].SourceFiles.Count);
		}

		[TestMethod]
		public void RegisterSourceFile_ConvertsHashToStringRepresentation() {
			var x = new InstrumentFactory();

			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			var sfId = x.RegisterSourceFile(moduleId, "source file", new byte[] { 1, 2, 3, 4, 5 }, null, null);

			var sourceFiles = from a in x.Assemblies
							  from m in a.Modules
							  from s in m.SourceFiles
							  where m.Id == moduleId && s.Id == sfId
							  select s;

			var sourceFile = sourceFiles.Single();
			Assert.AreEqual("0102030405", sourceFile.Hash);
		}

		[TestMethod]
		public void RegisterSourceFile_AddsHashAlgoAndLanguage() {
			var x = new InstrumentFactory();

			var moduleId = x.RegisterModule(x.RegisterAssembly("assembly", HASH, HASHTYPE), "module");
			var sfId = x.RegisterSourceFile(moduleId, "source file", null, "myhash", "mylanguage");

			var sourceFiles = from a in x.Assemblies
							  from m in a.Modules
							  from s in m.SourceFiles
							  where m.Id == moduleId && s.Id == sfId
							  select s;

			var sourceFile = sourceFiles.Single();
			Assert.AreEqual("myhash", sourceFile.HashAlgorithm);
			Assert.AreEqual("mylanguage", sourceFile.SourceLanguage);
		}

		[TestMethod]
		public void InstanceHash_VerifyHashIsRead() {
			var expected = Guid.NewGuid().ToString() + "|" + Guid.NewGuid().ToString();
			var x = new InstrumentFactory() {
				InstanceHash = expected
			};

			Assert.AreEqual(expected, x.InstanceHash);

		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentNullException))]
		public void InstanceHash_FailIfHashIsNull() {
			var x = new InstrumentFactory() {
				InstanceHash = null
			};

			Assert.IsNull(x.InstanceHash);
		}

	}
}
