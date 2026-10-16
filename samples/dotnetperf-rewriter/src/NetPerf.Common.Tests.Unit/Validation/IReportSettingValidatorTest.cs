using System;
using System.Linq;
using FluentValidation.Validators;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NetPerf.Common.Configuration;
using NetPerf.Common.Validation;

namespace NetPerf.Common.Tests.Validation {
	[TestClass]
	public class IReportSettingValidatorTest {

		class helper {
			public IReportSetting Setting {
				get;
				set;
			}
		}

		static IReportSettingValidator _validator = new IReportSettingValidator();

		static PropertyValidatorContext CreateValidationContext(IReportSetting value) {
			return new PropertyValidatorContext("no description", new helper(), value, "Setting");
		}

		[TestMethod]
		public void Validate_PropertyValueIsNull_ReturnsTrue() {
			var failures = _validator.Validate(CreateValidationContext(null));
			Assert.IsTrue(failures.IsEmpty());
		}

		[TestMethod]
		public void Validate_ReportSettingIsNotActive_ReturnsTrue() {
			var mock = new Mock<IReportSetting>();

			mock.SetupGet(s => s.IsActive)
				.Returns(false);

			var failures = _validator.Validate(CreateValidationContext(mock.Object));
			Assert.IsTrue(failures.IsEmpty());
		}

		[TestMethod]
		public void Validate_ReportSettingHasNoOutputFile_ReturnsFalse() {
			var mock = new Mock<IReportSetting>();

			mock.SetupGet(s => s.IsActive)
				.Returns(true);
			mock.SetupGet(s => s.OutputFile)
				.Returns((FilePath)null);

			var failures = _validator.Validate(CreateValidationContext(mock.Object));
			Assert.IsFalse(failures.IsEmpty());
		}

		[TestMethod]
		public void Validate_ReportSettingHasAnOutputFile_ReturnsTrue() {
			var mock = new Mock<IReportSetting>();

			// grab the first file in the system directory b/c we want a file that exists
			// we need a file that exists b/c this validator doesn't care whether a file exists
			// or not, and to test that correctly we need to test w/ a file that exists and a file that doesn't exist.
			var firstFile = System.IO.Directory.EnumerateFiles(Environment.SystemDirectory).First();

			mock.SetupGet(s => s.IsActive)
				.Returns(true);
			mock.SetupGet(s => s.OutputFile)
				.Returns(FilePath.Parse(firstFile));

			var failures = _validator.Validate(CreateValidationContext(mock.Object));
			Assert.IsTrue(failures.IsEmpty());
		}

		[TestMethod]
		public void Validate_ReportSettingOutputFileDoesNotExist_ReturnsTrue() {
			var mock = new Mock<IReportSetting>();

			mock.SetupGet(s => s.IsActive)
				.Returns(true);
			mock.SetupGet(s => s.OutputFile)
				.Returns(FilePath.Parse(System.IO.Path.GetRandomFileName()));

			var failures = _validator.Validate(CreateValidationContext(mock.Object));
			Assert.IsTrue(failures.IsEmpty());
		}
	}
}
