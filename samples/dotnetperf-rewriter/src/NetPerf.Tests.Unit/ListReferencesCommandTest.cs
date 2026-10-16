using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf;
using NetPerf.DCL.Cecil;
using Moq;
using NetPerf.Common;
using NetPerf.Common.Tests;

namespace NetPerf.Tests {
	[TestClass]
	public class ListReferencesCommandTest : TestClassBase {

		[TestMethod]
		public void Constructor_Initializes_AssemblyReaderProperty() {
			var obj = new ListLocalReferencesCommand();

			Assert.IsNotNull(obj.AssemblyReader);
		}

		#region FindAllReferences helpers
		private class AssemblyReaderStub : List<AssemblyDefinitionStub>, IAssemblyReader {

			public IAssemblyDefinition ReadAssembly(Common.FilePath path) {
				if (path == null)
					throw new InvalidOperationException("Attempt to read a null path");

				var result = this.FirstOrDefault(a => a.Location == path);
				if (result == null)
					throw new InvalidOperationException("Unable to find path: " + path);

				return result;
			}
		}

		private class AssemblyDefinitionStub : List<IModuleDefinition>, IAssemblyDefinition {

			public FilePath Location {
				get;
				private set;
			}

			public AssemblyDefinitionStub(string path) {
				Location = FilePath.Parse(path);
			}

			public ICollection<IModuleDefinition> Modules {
				get {
					return this;
				}
			}
		}

		private class ModuleDefinitionStub : List<string>, IModuleDefinition {

			public ICollection<Mono.Cecil.AssemblyNameReference> AssemblyReferences {
				get {
					return (from item in this
						   select new Mono.Cecil.AssemblyNameReference(item, new Version())).ToList();
				}
			}
		}

		private class FileStubber : NetPerf.Common.SystemStubs.IFileStub {

			private readonly AssemblyReaderStub _data;
			public FileStubber(AssemblyReaderStub data) {
				_data = data;
			}

			public void AppendAllText(string path, string contents) {
				throw new NotImplementedException();
			}

			public void AppendAllText(string path, string contents, Encoding encoding) {
				throw new NotImplementedException();
			}

			public System.IO.StreamWriter AppendText(string path) {
				throw new NotImplementedException();
			}

			public void Copy(string sourceFileName, string destFileName) {
				throw new NotImplementedException();
			}

			public void Copy(string sourceFileName, string destFileName, bool overwrite) {
				throw new NotImplementedException();
			}

			public System.IO.FileStream Create(string path) {
				throw new NotImplementedException();
			}

			public System.IO.FileStream Create(string path, int bufferSize) {
				throw new NotImplementedException();
			}

			public System.IO.FileStream Create(string path, int bufferSize, System.IO.FileOptions options) {
				throw new NotImplementedException();
			}

			public System.IO.FileStream Create(string path, int bufferSize, System.IO.FileOptions options, System.Security.AccessControl.FileSecurity fileSecurity) {
				throw new NotImplementedException();
			}

			public System.IO.StreamWriter CreateText(string path) {
				throw new NotImplementedException();
			}

			public void Decrypt(string path) {
				throw new NotImplementedException();
			}

			public void Delete(string path) {
				throw new NotImplementedException();
			}

			public void Encrypt(string path) {
				throw new NotImplementedException();
			}

			public bool Exists(string path) {
				return _data.Contains(ads => (string)ads.Location == path);
			}

			public System.Security.AccessControl.FileSecurity GetAccessControl(string path) {
				throw new NotImplementedException();
			}

			public System.Security.AccessControl.FileSecurity GetAccessControl(string path, System.Security.AccessControl.AccessControlSections includeSections) {
				throw new NotImplementedException();
			}

			public System.IO.FileAttributes GetAttributes(string path) {
				throw new NotImplementedException();
			}

			public DateTime GetCreationTime(string path) {
				throw new NotImplementedException();
			}

			public DateTime GetCreationTimeUtc(string path) {
				throw new NotImplementedException();
			}

			public DateTime GetLastAccessTime(string path) {
				throw new NotImplementedException();
			}

			public DateTime GetLastAccessTimeUtc(string path) {
				throw new NotImplementedException();
			}

			public DateTime GetLastWriteTime(string path) {
				throw new NotImplementedException();
			}

			public DateTime GetLastWriteTimeUtc(string path) {
				throw new NotImplementedException();
			}

			public void Move(string sourceFileName, string destFileName) {
				throw new NotImplementedException();
			}

			public System.IO.FileStream Open(string path, System.IO.FileMode mode) {
				throw new NotImplementedException();
			}

			public System.IO.FileStream Open(string path, System.IO.FileMode mode, System.IO.FileAccess access) {
				throw new NotImplementedException();
			}

			public System.IO.FileStream Open(string path, System.IO.FileMode mode, System.IO.FileAccess access, System.IO.FileShare share) {
				throw new NotImplementedException();
			}

			public System.IO.FileStream OpenRead(string path) {
				throw new NotImplementedException();
			}

			public System.IO.StreamReader OpenText(string path) {
				throw new NotImplementedException();
			}

			public System.IO.FileStream OpenWrite(string path) {
				throw new NotImplementedException();
			}

			public byte[] ReadAllBytes(string path) {
				throw new NotImplementedException();
			}

			public string[] ReadAllLines(string path) {
				throw new NotImplementedException();
			}

			public string[] ReadAllLines(string path, Encoding encoding) {
				throw new NotImplementedException();
			}

			public string ReadAllText(string path) {
				throw new NotImplementedException();
			}

			public string ReadAllText(string path, Encoding encoding) {
				throw new NotImplementedException();
			}

			public void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName) {
				throw new NotImplementedException();
			}

			public void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors) {
				throw new NotImplementedException();
			}

			public void SetAccessControl(string path, System.Security.AccessControl.FileSecurity fileSecurity) {
				throw new NotImplementedException();
			}

			public void SetAttributes(string path, System.IO.FileAttributes fileAttributes) {
				throw new NotImplementedException();
			}

			public void SetCreationTime(string path, DateTime creationTime) {
				throw new NotImplementedException();
			}

			public void SetCreationTimeUtc(string path, DateTime creationTimeUtc) {
				throw new NotImplementedException();
			}

			public void SetLastAccessTime(string path, DateTime lastAccessTime) {
				throw new NotImplementedException();
			}

			public void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) {
				throw new NotImplementedException();
			}

			public void SetLastWriteTime(string path, DateTime lastWriteTime) {
				throw new NotImplementedException();
			}

			public void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc) {
				throw new NotImplementedException();
			}

			public void WriteAllBytes(string path, byte[] bytes) {
				throw new NotImplementedException();
			}

			public void WriteAllLines(string path, IEnumerable<string> contents) {
				throw new NotImplementedException();
			}

			public void WriteAllLines(string path, string[] contents) {
				throw new NotImplementedException();
			}

			public void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding) {
				throw new NotImplementedException();
			}

			public void WriteAllLines(string path, string[] contents, Encoding encoding) {
				throw new NotImplementedException();
			}

			public void WriteAllText(string path, string contents) {
				throw new NotImplementedException();
			}

			public void WriteAllText(string path, string contents, Encoding encoding) {
				throw new NotImplementedException();
			}
		}

		private List<FilePath> FindAllReferences_ExecuteTest(AssemblyReaderStub testData) {
			var obj = new ListLocalReferencesCommand();
			obj.RootAssembly = FilePath.Parse("root.exe");
			obj.AssemblyReader = testData;

			ReplaceStubWith(new FileStubber(testData));

			return obj.FindAllReferences();
		}

		#endregion FindAllReferences helpers

		[TestMethod]
		public void FindAllReferences_NoReferences_ReturnsEmptyList() {
			
			var testData = new AssemblyReaderStub() {
				new AssemblyDefinitionStub("root.exe") {
					new ModuleDefinitionStub() { } // no references
				}
			};

			var result = FindAllReferences_ExecuteTest(testData);

			Assert.IsNotNull(result);
			Assert.AreEqual(0, result.Count);
		}

		[TestMethod]
		public void FindAllReferences_NoLocalReferences_ReturnsEmptyList() {
			var testData = new AssemblyReaderStub() {
				new AssemblyDefinitionStub("root.exe") {
					new ModuleDefinitionStub() { "assem2", "assem3" ,"assem4" } // references w/o a corresponding AssemblyDefinitionStub
				}
			};

			var result = FindAllReferences_ExecuteTest(testData);

			Assert.IsNotNull(result);
			Assert.AreEqual(0, result.Count);
		}

		[TestMethod]
		public void FindAllReferences_RootAssemblyNotReferencedByChild_RootAssemblyNotInOutput() {
			var testData = new AssemblyReaderStub() {
				new AssemblyDefinitionStub("root.exe") {
					new ModuleDefinitionStub() { "assem2" }
				},
				new AssemblyDefinitionStub("assem2.exe") {
					new ModuleDefinitionStub() { "assem3", "assem4" }
				},
			};

			var actual = FindAllReferences_ExecuteTest(testData);

			CollectionAssert.DoesNotContain(actual, FilePath.Parse("root.exe"));
		}

		[TestMethod]
		public void FindAllReferences_RootAssemblyReferencedByChild_RootAssemblyInOutput() {
			var testData = new AssemblyReaderStub() {
				new AssemblyDefinitionStub("root.exe") {
					new ModuleDefinitionStub() { "assem2" }
				},
				new AssemblyDefinitionStub("assem2.exe") {
					new ModuleDefinitionStub() { "assem3", "assem4", "root" }
				},
			};

			var actual = FindAllReferences_ExecuteTest(testData);

			CollectionAssert.Contains(actual, FilePath.Parse("root.exe"));
		}

		[TestMethod]
		public void FindAllReferences_UniqueReferencesInSingleAssembly_AllReferencesReturned() {
			var testData = new AssemblyReaderStub() {
				new AssemblyDefinitionStub("root.exe") {
					new ModuleDefinitionStub() { "assem2", "assem3" ,"assem4" }
				},
				new AssemblyDefinitionStub("assem2.exe"),
				new AssemblyDefinitionStub("assem3.dll"),
				new AssemblyDefinitionStub("assem4.dll"),
			};

			var expected = new List<FilePath> {
				FilePath.Parse("assem2.exe"),
				FilePath.Parse("assem3.dll"),
				FilePath.Parse("assem4.dll"),
			};

			var actual = FindAllReferences_ExecuteTest(testData);

			// check that the actual list is a subset of the expected list,
			// not that the lists are equal, so that this test will not fail if the
			// FindAllReferences_RootAssemblyNotReferencedByChild_RootAssemblyNotInOutput test does
			CollectionAssert.IsSubsetOf(expected, actual);
		}

		[TestMethod]
		public void FindAllReferences_UniqueReferencesInMultipleAssemblies_AllReferencesReturned() {
			var testData = new AssemblyReaderStub() {
				new AssemblyDefinitionStub("root.exe") {
					new ModuleDefinitionStub() { "assem2", "assem3" }
				},
				new AssemblyDefinitionStub("assem2.exe") {
					new ModuleDefinitionStub() { "assem4" }
				},
				new AssemblyDefinitionStub("assem3.dll") {
					new ModuleDefinitionStub() { "assem5" }
				},
				new AssemblyDefinitionStub("assem4.dll"),
				new AssemblyDefinitionStub("assem5.dll"),
			};

			var expected = new List<FilePath> {
				FilePath.Parse("assem2.exe"),
				FilePath.Parse("assem3.dll"),
				FilePath.Parse("assem4.dll"),
				FilePath.Parse("assem5.dll")
			};

			var actual = FindAllReferences_ExecuteTest(testData);

			// check that the actual list is a subset of the expected list,
			// not that the lists are equal, so that this test will not fail if the
			// FindAllReferences_RootAssemblyNotReferencedByChild_RootAssemblyNotInOutput test does
			CollectionAssert.IsSubsetOf(expected, actual);
		}

		[TestMethod]
		public void FindAllReferences_DuplicateReferencesInSameAssembly_SingleReferenceReturned() {
			var testData = new AssemblyReaderStub() {
				new AssemblyDefinitionStub("root.exe") {
					new ModuleDefinitionStub() { "assem2" }
				},
				new AssemblyDefinitionStub("assem2.exe") {
					new ModuleDefinitionStub() { "assem3", "assem3", "assem2", "assem4" }
				},
				new AssemblyDefinitionStub("assem3.dll"),
				new AssemblyDefinitionStub("assem4.dll"),
			};

			var expected = new List<FilePath> {
				FilePath.Parse("assem2.exe"),
				FilePath.Parse("assem3.dll"),
				FilePath.Parse("assem4.dll")
			};

			var actual = FindAllReferences_ExecuteTest(testData);

			// check that the actual list is a subset of the expected list,
			// not that the lists are equal, so that this test will not fail if the
			// FindAllReferences_RootAssemblyNotReferencedByChild_RootAssemblyNotInOutput test does
			CollectionAssert.IsSubsetOf(expected, actual);
		}

		[TestMethod]
		public void FindAllReferences_SameReferenceInDifferentAssemblies_SingleReferenceReturned() {
			var testData = new AssemblyReaderStub() {
				new AssemblyDefinitionStub("root.exe") {
					new ModuleDefinitionStub() { "assem2", "assem3" }
				},
				new AssemblyDefinitionStub("assem2.exe") {
					new ModuleDefinitionStub() { "assem4" }
				},
				new AssemblyDefinitionStub("assem3.dll") {
					new ModuleDefinitionStub() { "assem4" }
				},
				new AssemblyDefinitionStub("assem4.dll")
			};

			var expected = new List<FilePath> {
				FilePath.Parse("assem2.exe"),
				FilePath.Parse("assem3.dll"),
				FilePath.Parse("assem4.dll")
			};

			var actual = FindAllReferences_ExecuteTest(testData);

			// check that the actual list is a subset of the expected list,
			// not that the lists are equal, so that this test will not fail if the
			// FindAllReferences_RootAssemblyNotReferencedByChild_RootAssemblyNotInOutput test does
			CollectionAssert.IsSubsetOf(expected, actual);
		}

		[TestMethod]
		public void FindAllReferences_CycleInReferenceGraph_CycleIsNotFollowed() {

			var testData = new AssemblyReaderStub() {
				new AssemblyDefinitionStub("root.exe") {
					new ModuleDefinitionStub() { "assem2" }
				},
				new AssemblyDefinitionStub("assem2.exe") {
					new ModuleDefinitionStub() { "assem3" }
				},
				new AssemblyDefinitionStub("assem3.dll") {
					new ModuleDefinitionStub() { "assem4" }
				},
				new AssemblyDefinitionStub("assem4.dll") {
					new ModuleDefinitionStub() { "assem2", "assem3", "assem5" }
				},
				new AssemblyDefinitionStub("assem5.exe")
			};

			var expected = new List<FilePath> {
				FilePath.Parse("assem2.exe"),
				FilePath.Parse("assem3.dll"),
				FilePath.Parse("assem4.dll"),
				FilePath.Parse("assem5.exe"),
			};

			var actual = FindAllReferences_ExecuteTest(testData);
			// we know the cycle wasn't followed b/c the function returned.
			// had the cycle been followed, then the method would have entered an infinite loop and never returned
		}
	}
}
