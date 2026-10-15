using System;
using System.Linq;
using FluentValidation.Validators;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Validation;

namespace NetPerf.Common.Tests.Validation {
	[TestClass]
	public class FileExistsValidatorTest {

		class helper {
			public FilePath MyPath {
				get;
				set;
			}
		}

		static FileExistsValidator _validator = new FileExistsValidator();

		static PropertyValidatorContext CreateValidationContext(FilePath value) {
			return new PropertyValidatorContext("no description", new helper(), value, "MyPath");
		}

		[TestMethod]
		public void Validate_PropertyValueIsNull_ReturnsTrue() {
			var failures = _validator.Validate(CreateValidationContext(null));
			Assert.IsTrue(failures.IsEmpty());
		}

		[TestMethod]
		public void Validate_FileDoesNotExist_ReturnsFalse() {
			var failures = _validator.Validate(CreateValidationContext(FilePath.Parse(System.IO.Path.GetRandomFileName())));
			Assert.IsFalse(failures.IsEmpty());
		}

		[TestMethod]
		public void Validate_FileExists_ReturnsTrue() {
			// grab the first file in the system directory b/c we want a file that exists
			// we need a file that exists b/c this validator doesn't care whether a file exists
			// or not, and to test that correctly we need to test w/ a file that exists and a file that doesn't exist.
			var firstFile = System.IO.Directory.EnumerateFiles(Environment.SystemDirectory).First();

			var failures = _validator.Validate(CreateValidationContext(FilePath.Parse(firstFile)));
			Assert.IsTrue(failures.IsEmpty());
		}
	}
}
