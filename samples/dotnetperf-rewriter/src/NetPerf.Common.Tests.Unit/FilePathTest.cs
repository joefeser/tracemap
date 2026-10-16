using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NetPerf.Common.SystemStubs;
using NetPerf.Common.Tests;

namespace NetPerf.Common.UnitTests {
	[TestClass]
	public class FilePathTest : TestClassBase {

		[TestMethod]
		public void ChangeExtension_ReturnsADifferentFilePath() {
			var path = FilePath.Parse(@"file.txt");
			var other = path.ChangeExtension("hello");

			Assert.AreNotSame(path, other);
		}

		[TestMethod]
		public void ChangeExtension_ChangesSingleFileExtension() {
			var path = FilePath.Parse(@"file.txt").ChangeExtension("happy");

			Assert.AreEqual(".happy", path.Extension);
		}

		[TestMethod]
		public void ChangeExtension_ChangesLastOfMultipleExtensions() {
			var path = FilePath.Parse(@"file.txt.xml.other").ChangeExtension("happy");

			Assert.AreEqual(".happy", path.Extension);
		}

		[TestMethod]
		public void ChangeExtension_AddsExtensionIfNonePresent() {
			var path = FilePath.Parse(@"file");

			Assert.AreEqual("", path.Extension);

			path = path.ChangeExtension("happy");

			Assert.AreEqual(".happy", path.Extension);
		}

		[TestMethod]
		public void ChangeExtension_LeavesFilenameWithoutExtensionUnmodified() {
			var path = FilePath.Parse(@"file other.xml");

			Assert.AreEqual("file other", path.FileNameWithoutExtension);
			path = path.ChangeExtension("happy");
			Assert.AreEqual("file other", path.FileNameWithoutExtension);
		}

		[TestMethod]
		public void ChangeExtension_ChangesFilename() {
			var path = FilePath.Parse(@"file other.xml");

			Assert.AreEqual("file other.xml", path.FileName);
			path = path.ChangeExtension("txt");
			Assert.AreEqual("file other.txt", path.FileName);
		}

		[TestMethod]
		public void ChangeFileNameWithoutExtension_ReturnsADifferentObject() {
			var path = FilePath.Parse(@"file.txt");

			var other = path.ChangeFileNameWithoutExtension("hello");

			Assert.AreNotSame(path, other);
		}

		[TestMethod]
		public void ChangeFileNameWithoutExtension_LeavesExtensionUnmodified() {
			var path = FilePath.Parse(@"file.txt");
			var other = path.ChangeFileNameWithoutExtension("hello");

			Assert.AreEqual(".txt", other.Extension);
			Assert.AreEqual("hello.txt", other.FileName);
		}

		[TestMethod]
		public void ChangeFileNameWithoutExtension_ModifiesSingleExtensionName() {
			var path = FilePath.Parse(@"file.txt");
			var other = path.ChangeFileNameWithoutExtension("hello");

			Assert.AreEqual(".txt", other.Extension);
			Assert.AreEqual("hello.txt", other.FileName);
		}

		[TestMethod]
		public void ChangeFileNameWithoutExtension_ModifiesMultipleExtensionName() {
			var path = FilePath.Parse(@"file.txt.xml.type");
			var other = path.ChangeFileNameWithoutExtension("hello");

			Assert.AreEqual(".type", other.Extension);
			Assert.AreEqual("hello.type", other.FileName);
		}

		[TestMethod]
		public void ChangeParentDirectory_ReturnsADifferentObject() {
			var path = FilePath.Parse(@"file.txt");
			var parentDir = DirectoryPath.Parse(@"im\in\ur\codz");

			var other = path.ChangeParentDirectory(parentDir);

			Assert.AreNotSame(path, other);
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentNullException))]
		public void ChangeParentDirectory_FailsIfNewParentIsNull() {
			var path = FilePath.Parse(@"file.txt");

			var other = path.ChangeParentDirectory(null);
		}

		[TestMethod]
		public void ChangeParentDirectory_FilenameIsNotChanged() {
			var path = FilePath.Parse(@"file.txt");
			var parentDir = DirectoryPath.Parse(@"im\in\ur\codz");

			var other = path.ChangeParentDirectory(parentDir);

			Assert.AreEqual(path.FileName, other.FileName);
		}

		[TestMethod]
		public void ChangeParentDirectory_ChangesTheParentDirectory() {
			var path = FilePath.Parse(@"file.txt");
			var parentDir = DirectoryPath.Parse(@"im\in\ur\codz");

			var other = path.ChangeParentDirectory(parentDir);

			Assert.AreNotEqual(path.ParentDirectory, other.ParentDirectory);
			Assert.AreEqual(parentDir, other.ParentDirectory);
		}

		[TestMethod]
		public void Clone_ReturnsADifferentObject() {
			var original = FilePath.Parse(@"C:\hello.txt");
			var copy = original.Clone();

			Assert.AreNotSame(original, copy);
		}

		[TestMethod]
		public void StringConvertions_ReturnsTheFullPath() {
			var expected = @"C:\expected\dir\hello.txt";
			var original = FilePath.Parse(expected);

			string strOrig = original;

			Assert.AreEqual(expected, strOrig);
		}

		[TestMethod]
		public void Equals_IsFalseIfOtherIsNull() {
			var original = FilePath.Parse(@"file.txt");

			Assert.IsFalse(original.Equals(null));
		}

		[TestMethod]
		public void Equals_IsReflexive() {
			var original = FilePath.Parse(@"file.txt");

			Assert.IsTrue(original.Equals(original));
		}

		[TestMethod]
		public void Equals_IsSymmetric() {
			var left = FilePath.Parse(@"some file.txt");
			var right = FilePath.Parse(@"some file.txt");

			Assert.IsTrue(left.Equals(right));
			Assert.IsTrue(right.Equals(left));

			right = FilePath.Parse(@"another file.xml");

			Assert.IsFalse(left.Equals(right));
			Assert.IsFalse(right.Equals(left));
		}

		[TestMethod]
		public void Equals_IgnoresFileNameCase() {
			var left = FilePath.Parse(@"some FILe.tXt");
			var right = FilePath.Parse(@"sOME file.Txt");

			Assert.IsTrue(left.Equals(right));
		}

		[TestMethod]
		public void Equals_IgnoresRelativeOrAbsolutePaths() {
			var left = FilePath.Parse(@"file.txt");
			var right = FilePath.Parse(System.IO.Path.Combine(Environment.CurrentDirectory, @"file.txt"));

			Assert.IsTrue(left.Equals(right));
		}

		[TestMethod]
		public void Equals_ReturnsFalseIfFileNamesAreDifferent() {
			var left = FilePath.Parse(@"some file.txt");
			var right = FilePath.Parse(@"another file.txt");

			Assert.IsFalse(left.Equals(right));
		}

		[TestMethod]
		public void Equals_ReturnsFalseIfDirectoriesAreDifferent() {
			var left = FilePath.Parse(@"some file.txt");
			var right = FilePath.Parse(@"thuis\is\a\directory\some file.txt");

			Assert.IsFalse(left.Equals(right));
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public void MakeRelativeTo_FailsIfRootPathIsRelative() {
			var path = FilePath.Parse(@"document.txt");
			var basePath = DirectoryPath.Parse(@"relative\path");

			path.MakeRelativeTo(basePath);
		}

		[TestMethod]
		public void MakeRelativeTo_ReturnsADifferentObject() {
			var path = FilePath.Parse(@"C:\document.txt");
			var basePath = DirectoryPath.Parse(@"C:\absolute\path");

			var other = path.MakeRelativeTo(basePath);

			Assert.AreNotSame(path, other);
		}

		[TestMethod]
		public void MakeRelativeTo_OriginalIsRelative_ReturnsUnmodifiedFilePath() {
			var path = FilePath.Parse(@"document.txt");
			var basePath = DirectoryPath.Parse(@"C:\absolute\path");

			var other = path.MakeRelativeTo(basePath);

			Assert.AreEqual(path.FullPath, other.FullPath);
		}

		[TestMethod]
		[Ignore] // ignored because Moq can only mock interfaces or abstract types
		public void MakeRelativeTo_DelegatesWorkToTheParentDirectory() {
			var path = FilePath.Parse(@"C:\document.txt");
			var basePath = DirectoryPath.Parse(@"C:\absolute\path");

			var mock = new Mock<DirectoryPath>(MockBehavior.Strict);
			mock.Setup(dir => dir.MakeRelativeTo(basePath))
				.Returns(DirectoryPath.Parse(@"..\..\document.txt"))
				.Verifiable("did not call DirectoryPath.MakeRelativeTo");

			Reflect.ReplaceFieldOrProperty(path, "ParentDirectory", mock.Object);

			var other = path.MakeRelativeTo(basePath);
			mock.VerifyAll();
		}

		[TestMethod]
		[ExpectedException(typeof(NotSupportedException))]
		public void Parse_InvalidPath_ParseThrowsException() {
			var path = @"he:llo\there.txt";

			FilePath.Parse(path);
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentOutOfRangeException))]
		public void Parse_PathHasNoFileName_ParseFails() {
			var path = @"this\is\a\directory\";

			FilePath.Parse(path);
		}

		[TestMethod]
		public void Parse_FileNameHasNoExtension_FileNameIsSameAsFileNameWithoutExtension() {
			var path = @"this_is_ny_file";

			var file = FilePath.Parse(path);

			Assert.AreEqual(path, file.FileName);
			Assert.AreEqual(file.FileName, file.FileNameWithoutExtension);
			Assert.AreEqual(string.Empty, file.Extension);
		}

		[TestMethod]
		public void Parse_FileNameHasMultipleExtensions_LastExtensionIsConsideredExtension() {
			var path = @"iFile.min.js";

			var file = FilePath.Parse(path);

			Assert.AreEqual(".js", file.Extension);
		}

		[TestMethod]
		public void Parse_FileNameHasSingleExtension_FileNameAndExtensionAreParsedCorrectly() {
			var path = @"myFile.txt";

			var file = FilePath.Parse(path);

			Assert.AreEqual(path, file.FileName);
			Assert.AreEqual(".txt", file.Extension);
			Assert.AreEqual("myFile", file.FileNameWithoutExtension);
		}

		[TestMethod]
		public void Parse_PathHasDirectory_FileNameIsCorrectlySeperatedFromDirectory() {
			var path = @"o\the\rage\of\men.shkspre";

			var file = FilePath.Parse(path);

			Assert.AreEqual("men.shkspre", file.FileName);
			Assert.AreEqual(@"o\the\rage\of", file.ParentDirectory.FullPath);
		}

		[TestMethod]
		public void Parse_PathHasNoDirectory_LocalDirectoryIsUsed() {
			var path = @"myfile.happy";

			var file = FilePath.Parse(path);

			Assert.AreEqual(DirectoryPath.LocalDirectory, file.ParentDirectory);
		}

		[TestMethod]
		public void Parse_PathHasRelativeDirectory_FilePathIsRelative() {
			var path = @"hello\myfile.txt";

			var file = FilePath.Parse(path);
			Assert.IsTrue(file.IsRelative);
		}

		[TestMethod]
		public void Parse_PathHasAbsoluteDirectory_FilePathIsAbsolute() {
			var path = @"F:\happiness\is/a/bowl\of\choclates";

			var file = FilePath.Parse(path);

			Assert.IsTrue(file.IsAbsolute);
		}

		[TestMethod]
		public void TryParse_PathIsNull_Fails() {
			string path = null;

			var result = FilePath.TryParse(path);

			Assert.IsFalse(result.WasSuccess);
		}

		[TestMethod]
		public void TryParse_PathIsEmpty_Fails() {
			var path = @"";

			var result = FilePath.TryParse(path);

			Assert.IsFalse(result.WasSuccess);
		}

		[TestMethod]
		public void TryParse_PathIsWhitespace_Fails() {
			var path = @"  ";

			var result = FilePath.TryParse(path);

			Assert.IsFalse(result.WasSuccess);
		}

		[TestMethod]
		public void TryParse_InvalidPath_Fails() {
			var path = @"well\this:\is;/a\su:!prise.txt";

			var result = FilePath.TryParse(path);

			Assert.IsFalse(result.WasSuccess);
		}

		[TestMethod]
		public void TryParse_PathHadNoFileName_Fails() {
			var path = @"madam\im\adam\";

			var result = FilePath.TryParse(path);

			Assert.IsFalse(result.WasSuccess);
		}

		[TestMethod]
		public void TryParse_RelativePath_RelativeFileNameReturned() {
			var path = @"to\be\or\to\kill.isaquestion";

			var result = FilePath.TryParse(path);

			Assert.IsTrue(result.WasSuccess);
			Assert.IsTrue(result.Data.IsRelative);
		}

		[TestMethod]
		public void TryParse_AbsolutePath_AbsoluteFileNameReturned() {
			var path = @"C:\to\be\or\to\kill.isaquestion";

			var result = FilePath.TryParse(path);

			Assert.IsTrue(result.WasSuccess);
			Assert.IsTrue(result.Data.IsAbsolute);
		}

		[TestMethod]
		public void ToAbsolute_ReturnsADifferentObject() {
			var path = FilePath.Parse(@"hello.txt");
			var absolute = path.ToAbsolutePath();

			Assert.AreNotSame(path, absolute);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void ToAbsolute_FailsIfTheFilePathIsAlreadyAbsolute() {
			var path = FilePath.Parse(@"C:\hello\file.xml");
			path.ToAbsolutePath();
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public void ToAbsolute_FailsIfTheBaseDirectoryIsRelative() {
			var path = FilePath.Parse(@"file.xml");
			var baseDir = DirectoryPath.Parse(@"relative\dir\yes");
			path.ToAbsolutePath(baseDir);
		}

		[TestMethod]
		public void ToAbsolute_MakesARelativeFilePathAbsolute() {
			var path = FilePath.Parse(@"hello.txt");
			var absolute = path.ToAbsolutePath();

			Assert.IsTrue(absolute.IsAbsolute);
		}

		[TestMethod]
		public void ToAbsolute_UsesTheCurrentDirectoryAsTheDefaultBaseDirectory() {
			var currentDir = @"F:\absolute\path";
			var path = FilePath.Parse(@"file.xml");

			var mock = new Mock<IEnvironmentStub>(MockBehavior.Strict);
			mock.Setup(stub => stub.CurrentDirectory)
				.Returns(currentDir)
				.Verifiable("didn't use Environment.CurrentDirectory");

			ReplaceStubWith(mock.Object);

			var absolute = path.ToAbsolutePath();

			Assert.IsTrue(absolute.IsAbsolute);
			StringAssert.StartsWith(absolute.FullPath, currentDir);

			mock.VerifyAll();
		}

		[TestMethod]
		public void ToAbsolute_UsesTheGivenDirectoryPathAsTheBaseDirectory() {
			var path = FilePath.Parse(@"file.xml");
			var baseDir = DirectoryPath.Parse(@"D:\absolute\dir\yes");

			var absolute = path.ToAbsolutePath(baseDir);

			StringAssert.StartsWith(absolute.FullPath, baseDir.FullPath);
		}

		[TestMethod]
		public void ToString_ReturnsFullPath() {
			var expected = @"C:\expected\dir\hello.txt";
			var original = FilePath.Parse(expected);

			Assert.AreEqual(expected, original.ToString());
		}
	}
}
