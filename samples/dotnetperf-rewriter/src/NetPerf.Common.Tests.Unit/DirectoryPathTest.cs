using NetPerf.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using NetPerf.Common.SystemStubs;
using Moq;
using NetPerf.Common.Tests;
using NetPerf.Common.Tests.Test_References;

namespace NetPerf.Common.UnitTests {
	/// <summary>
	///This is a test class for DirectoryPathTest and is intended
	///to contain all DirectoryPathTest Unit Tests
	///</summary>
	[TestClass]
	public class DirectoryPathTest : TestClassBase {

		[TestMethod]
		public void Clone_ReturnsNewObject() {
			var original = DirectoryPath.Parse(@"hello\there");

			var cloned = original.Clone();

			Assert.AreNotSame(original, cloned);
		}

		[TestMethod]
		public void StringConversion_ReturnsFullPathDuplicate() {
			var original = DirectoryPath.Parse(@"hello\there");

			string strFullPath = original;

			Assert.AreEqual(original.FullPath, strFullPath);
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public void DirectoryPathConversion_FailsIfInvalidPath() {
			var origpath = @"hel::/this/is/invalid/path";

			var dirPath = (DirectoryPath)origpath;

			Assert.IsNotNull(dirPath);
			Assert.AreEqual(origpath, dirPath.FullPath);
		}

		[TestMethod]
		public void DirectoryPathConversion_FullPathIsOriginalString() {
			var origpath = @"this\is\another\path";

			var dirPath = (DirectoryPath)origpath;

			Assert.IsNotNull(dirPath);
			Assert.AreEqual(origpath, dirPath.FullPath);
		}

		[TestMethod]
		public void Equals_OtherIsNull_ExpectNotEqual() {
			var path = DirectoryPath.Parse(@"C:\dev\hello");

			Assert.IsFalse(path.Equals(null));
		}

		[TestMethod]
		public void Equals_CompensatesForRelativePaths() {
			var original = DirectoryPath.Parse(@"this\is\a\relative");

			var absolute = original.ToAbsolutePath();
			var relative = original.Clone();

			Assert.IsTrue(absolute.Equals(relative));
			Assert.IsTrue(relative.Equals(absolute));

			var abs1 = original.ToAbsolutePath();
			var abs2 = original.ToAbsolutePath();

			Assert.IsTrue(abs1.Equals(abs2));

			var rel1 = original.Clone();
			var rel2 = original.Clone();

			Assert.IsTrue(rel1.Equals(rel2));
		}

		[TestMethod]
		public void Equals_IgnoresDirectorySeperatorChar() {
			var path = DirectoryPath.Parse(@"C:\this\is\another\path");
			var compare = DirectoryPath.Parse(@"C:/this/is/another/path");

			Assert.IsTrue(path.Equals(compare));
		}

		[TestMethod]
		public void Equals_IgnoresPathCasing() {
			var path = DirectoryPath.Parse(@"C:\Hell\o\AM\I\A\star");
			var compare = DirectoryPath.Parse(@"c:\hell\o\aM\i\A\StAR");

			Assert.IsTrue(path.Equals(compare));
		}

		[TestMethod]
		public void Equals_CompensatesForLocalDirectories() {
			var path = DirectoryPath.Parse(@"C:\dev\hello\.\.\this.\is\.\a\dir.\.\.\");
			var compare = DirectoryPath.Parse(@"C:\dev\hello\this.\is\a\dir.");

			Assert.AreEqual(path, compare);

			compare = DirectoryPath.Parse(@"C:\dev\hello\other dir\is\a\dir");

			Assert.AreNotEqual<DirectoryPath>(path, compare);
		}

		[TestMethod]
		public void Equals_CompensatesForParentDirectories() {
			var path = DirectoryPath.Parse(@"C:\dev\hello\..\..\this\is\..\a\dir");
			var compare = DirectoryPath.Parse(@"C:\this\a\dir");

			Assert.AreEqual(path, compare);

			compare = DirectoryPath.Parse(@"C:\this\other\dir");

			Assert.AreNotEqual<DirectoryPath>(path, compare);
		}

		[TestMethod]
		public void Equals_CompensatesForParentAndLocalDirectories() {
			var path = DirectoryPath.Parse(@"C:\dev\hello\..\.\this\is\..\a\dir\.\..\..\.\.\..\");
			var compare = DirectoryPath.Parse(@"C:\dev");

			Assert.AreEqual(path, compare);

			compare = DirectoryPath.Parse(@"C:\development");

			Assert.AreNotEqual<DirectoryPath>(path, compare);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void Equals_FailsIfTooManyParentDirectories() {
			var path = DirectoryPath.Parse(@"C:\dev\hello\..\..\..\");
			var compare = DirectoryPath.Parse(@"C:\dev");

			Assert.AreEqual(path, compare);
		}

		[TestMethod]
		public void Equals_DifferentRootDrive_ExpectFalse() {
			var path = DirectoryPath.Parse(@"C:\dev\this\is\a\drive");
			var compare = DirectoryPath.Parse(@"D:\dev\this\is\a\drive");

			Assert.AreNotEqual<DirectoryPath>(path, compare);
		}

		[TestMethod]
		public void Equals_IsReflexive() {
			var path = DirectoryPath.Parse(@"this\is\a\relative");

			Assert.IsTrue(path.Equals(path));
		}

		[TestMethod]
		public void Equals_IsSymmetric() {
			var left = DirectoryPath.Parse(@"this\is\a\path");
			var right = DirectoryPath.Parse(@"this\is\a\path");

			Assert.IsTrue(left.Equals(right));
			Assert.IsTrue(right.Equals(left));

			right = DirectoryPath.Parse(@"another\path");

			Assert.IsFalse(left.Equals(right));
			Assert.IsFalse(right.Equals(left));
		}

		[TestMethod]
		public void MakeRelativeTo_OriginalIsRelative_OriginalReturned() {
			var original = DirectoryPath.Parse(@"..\this\is\relative\path");
			var newBase = DirectoryPath.Parse(@"C:\dev\hello");

			var relative = original.MakeRelativeTo(newBase);

			Assert.IsTrue(relative.IsRelative);
			Assert.AreEqual(original.FullPath, relative.FullPath);
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public void MakeRelativeTo_NewDirBaseIsRelative_ExpectException() {
			var original = DirectoryPath.Parse(@"C:\hello\there");
			var newBase = DirectoryPath.Parse(@"..\another\relative path");

			original.MakeRelativeTo(newBase);
		}

		[TestMethod]
		public void MakeRelativeTo_DirectoryPathsAreTheSame_CurrentDirectoryReturned() {
			// the "C:\" and "D:\" protions should be ignored
			var original = DirectoryPath.Parse(@"c:\dev\hello\there");
			var newBase = DirectoryPath.Parse(@"C:\DEV\hello\THere");

			var relative = original.MakeRelativeTo(newBase);

			Assert.IsTrue(relative.IsRelative);
			Assert.AreEqual(@".\", relative.FullPath);
		}

		[TestMethod]
		public void MakeRelativeTo_NoCommonRootPath_DepthEqual() {
			var original = DirectoryPath.Parse(@"C:\1\1\1");
			var newBase = DirectoryPath.Parse(@"C:\2\2\2\");

			var relative = original.MakeRelativeTo(newBase);

			Assert.IsTrue(relative.IsRelative);
			Assert.AreEqual(@"..\..\..\1\1\1\", relative.FullPath);
		}

		[TestMethod]
		public void MakeRelativeTo_NoCommonRootPath_OriginalIsDeeper() {
			var original = DirectoryPath.Parse(@"C:\1\1\1\1\");
			var newBase = DirectoryPath.Parse(@"C:\3\3");

			var relative = original.MakeRelativeTo(newBase);

			Assert.IsTrue(relative.IsRelative);
			Assert.AreEqual(@"..\..\1\1\1\1\", relative.FullPath);
		}

		[TestMethod]
		public void MakeRelativeTo_NoCommonRootPath_NewBaseIsDeeper() {
			var original = DirectoryPath.Parse(@"C:\1\1");
			var newBase = DirectoryPath.Parse(@"C:\3\3\3\3\3");

			var relative = original.MakeRelativeTo(newBase);

			Assert.IsTrue(relative.IsRelative);
			Assert.AreEqual(@"..\..\..\..\..\1\1\", relative.FullPath);
		}

		[TestMethod]
		public void MakeRelativeTo_PartialCommonRootPath_DepthEqual() {
			var original = DirectoryPath.Parse(@"C:\1\1\3\4");
			var newBase = DirectoryPath.Parse(@"C:\1\1\5\6");

			var relative = original.MakeRelativeTo(newBase);

			Assert.IsTrue(relative.IsRelative);
			Assert.AreEqual(@"..\..\3\4\", relative.FullPath);
		}

		[TestMethod]
		public void MakeRelativeTo_PartialCommonRootPath_OriginalIsDeeper() {
			var original = DirectoryPath.Parse(@"C:\1\2\3");
			var newBase = DirectoryPath.Parse(@"C:\1");

			var relative = original.MakeRelativeTo(newBase);

			Assert.IsTrue(relative.IsRelative);
			Assert.AreEqual(@".\2\3\", relative.FullPath);
		}

		[TestMethod]
		public void MakeRelativeTo_PartialCommonRootPath_NewBaseIsDeeper() {
			var original = DirectoryPath.Parse(@"C:\1");
			var newBase = DirectoryPath.Parse(@"C:\1\2\3");

			var relative = original.MakeRelativeTo(newBase);

			Assert.IsTrue(relative.IsRelative);
			Assert.AreEqual(@"..\..\", relative.FullPath);
		}

		[TestMethod]
		public void MakeRelativeTo_DifferentDrivePath_PreventsMakingARelativePath() {
			// the "D:\" and "C:\" portions of the path should be ignored
			var original = DirectoryPath.Parse(@"D:\1\2\3\4\");
			var newBase = DirectoryPath.Parse(@"C:\1\1\3\4");

			var relative = original.MakeRelativeTo(newBase);

			Assert.IsTrue(relative.IsAbsolute);
			Assert.AreEqual(@"D:\1\2\3\4\", relative.FullPath);
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public void Parse_FailsIfInvalidPath() {
			var origPath = @"D>\invalid\path";

			DirectoryPath.Parse(origPath);
		}

		[TestMethod]
		public void Parse_FullPathIsOriginalString() {
			var origPath = @"C:\hello\this\is\great\path";

			var dirPath = DirectoryPath.Parse(origPath);

			Assert.IsNotNull(dirPath);
			Assert.AreEqual(origPath, dirPath.FullPath);
		}

		[TestMethod]
		public void TryParse_PathIsEmpty_ParseFails() {
			var origPath = "";

			var result = DirectoryPath.TryParse(origPath);
			Assert.IsFalse(result.WasSuccess);
			Assert.AreEqual("The potential directory path was empty.", result.ErrorMessage);
		}

		[TestMethod]
		public void TryParse_PathIsNull_ParseFails() {
			string origPath = null;

			var result = DirectoryPath.TryParse(origPath);
			Assert.IsFalse(result.WasSuccess);
			Assert.AreEqual("The potential directory path was empty.", result.ErrorMessage);
		}

		[TestMethod]
		public void TryParse_PathIsWhitespace_ParseFails() {
			var origPath = "    ";

			var result = DirectoryPath.TryParse(origPath);
			Assert.IsFalse(result.WasSuccess);
			Assert.AreEqual("The potential directory path was empty.", result.ErrorMessage);
		}

		[TestMethod]
		public void TryParse_InvalidPath_ParseFails() {
			var origPath = @"see\here/now: I/am/not;\a/man";

			var result = DirectoryPath.TryParse(origPath);
			Assert.IsFalse(result.WasSuccess);
			Assert.AreEqual("The path '" + origPath + "' is not a legal directory path.", result.ErrorMessage);
		}

		[TestMethod]
		public void TryParse_RelativePath_RelativeDirectoryReturned() {
			var origPath = @"blue/is\the\sky";

			var result = DirectoryPath.TryParse(origPath);
			Assert.IsTrue(result.WasSuccess);
			Assert.IsNotNull(result.Data);
			Assert.IsTrue(result.Data.IsRelative);
			StringAssert.EndsWith(result.Data.FullPath, origPath);
		}

		[TestMethod]
		public void TryParse_AbsolutePath_AbsoluteDirectoryReturned() {
			var origPath = @"D:\tertullian\loved/sarcasm";

			var result = DirectoryPath.TryParse(origPath);
			Assert.IsTrue(result.WasSuccess);
			Assert.IsNotNull(result.Data);
			Assert.IsTrue(result.Data.IsAbsolute);
			Assert.AreEqual(origPath, result.Data.FullPath);
		}

		[TestMethod]
		public void ToAbsolute_UsesCurrentDirectoryAsDefaultBasePath() {
			var envBaseDir = @"F:\current\environment\directory";
			var origPath = DirectoryPath.Parse(@"another\base\path\ya");

			var mock = new Mock<IEnvironmentStub>(MockBehavior.Strict);
			mock.Setup(env => env.CurrentDirectory)
				.Returns(envBaseDir)
				.Verifiable("didn't use Environemnt.CurrentDirectory");

			ReplaceStubWith(mock.Object);

			var absPath = origPath.ToAbsolutePath();

			StringAssert.StartsWith(absPath.FullPath, envBaseDir);
			mock.VerifyAll();
		}

		[TestMethod]
		public void ToAbsolute_ReturnsAnAbsolutePath() {
			var origDir = DirectoryPath.Parse(@"..\some\test\dir");

			var absDir = origDir.ToAbsolutePath();

			Assert.IsNotNull(absDir);
			Assert.IsTrue(absDir.IsAbsolute);
		}

		[TestMethod]
		public void ToAbsolute_UsesGivenDirectoryAsBasePath() {
			var baseDir = DirectoryPath.Parse(@"C:\");
			var origPath = DirectoryPath.Parse(@"..\some\test\dir");

			var absPath = origPath.ToAbsolutePath(baseDir);

			StringAssert.StartsWith(absPath.FullPath, baseDir.FullPath);
		}

		[TestMethod]
		public void ToAbsolute_ReturnsANewObject() {
			var origDir = DirectoryPath.Parse(@"..\some\test\dir");

			var absDir = origDir.ToAbsolutePath();

			Assert.AreNotSame(origDir, absDir);
		}

		[TestMethod]
		[ExpectedException(typeof(InvalidOperationException))]
		public void ToAbsolute_FailsIfAlreadyAbsolute() {
			var origDir = DirectoryPath.Parse(@"C:\dev\some\project");

			origDir.ToAbsolutePath();
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public void ToAbsolute_FailsIfBasePathIsRelative() {
			var baseDir = DirectoryPath.Parse(@"a\relative\dir");
			var origDir = DirectoryPath.Parse(@"some\other\directory\");

			origDir.ToAbsolutePath(baseDir);
		}

		[TestMethod]
		public void ToAbsolute_StripsLocalDirectoryPaths() {
			var baseDir = DirectoryPath.Parse(@"C:\dev\project\.\");
			var origDir = DirectoryPath.Parse(@".\.\hello\.\");

			var finalDir = origDir.ToAbsolutePath(baseDir);
			Assert.AreEqual(@"C:\dev\project\hello", finalDir.FullPath);
		}

		[TestMethod]
		public void RemoveLocalDirectoryPaths_HandlesDirOrAltDirSeperatorChar() {
			var result = DirectoryPath_Privates.RemoveLocalDirectoryPaths(@"this\is\a\dir\.\.\");

			Assert.AreEqual(@"this\is\a\dir", result);

			result = DirectoryPath_Privates.RemoveLocalDirectoryPaths(@"this/is/a/dir/././");

			Assert.AreEqual(@"this\is\a\dir", result);
		}

		[TestMethod]
		public void RemoveLocalDirectoryPaths_ReturnsAnEmptyStringWhenGivenOnlyLocalDirectoryPaths() {
			var result = DirectoryPath_Privates.RemoveLocalDirectoryPaths(@".\");
			Assert.AreEqual(string.Empty, result);

			result = DirectoryPath_Privates.RemoveLocalDirectoryPaths(@"\.\.\");
			Assert.AreEqual(string.Empty, result);
		}

		[TestMethod]
		public void RemoveLocalDirectoryPaths_StripsAllLocalDirectoryPaths() {
			var result = DirectoryPath_Privates.RemoveLocalDirectoryPaths(@"C:\dev\.\.\this\is\a\.\local\.\.\path");

			Assert.AreEqual(@"C:\dev\this\is\a\local\path", result);
		}

		[TestMethod]
		public void RemoveLocalDirectoryPaths_GivenDrivePath_ReturnsDrivePathWithTrailingDirSeperatorChar() {
			var result = DirectoryPath_Privates.RemoveLocalDirectoryPaths(@"C:\");

			Assert.AreEqual(@"C:\", result);

			result = DirectoryPath_Privates.RemoveLocalDirectoryPaths(@"C:\.\.\.\");

			Assert.AreEqual(@"C:\", result);
		}

		[TestMethod]
		public void ToString_ReturnsFullPath() {
			var expected = @"C:\this\is\a\path";
			var dirObj = DirectoryPath.Parse(expected);

			Assert.AreEqual(expected, dirObj.ToString());
		}
	}
}
