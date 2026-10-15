using FluentValidation.Validators;
using NetPerf.Common.Configuration;

namespace NetPerf.Common.Validation {
	public class IReportSettingValidator : PropertyValidator {

		public IReportSettingValidator() : base("A report file path is required.") {

		}

		protected override bool IsValid(PropertyValidatorContext context) {

			var reportSetting = context.PropertyValue as IReportSetting;
			if (reportSetting != null && reportSetting.IsActive) {
				return reportSetting.OutputFile != null;
			}

			return true;
		}
	}
}
