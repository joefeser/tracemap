using FluentValidation.Validators;

namespace NetPerf.Common.Validation {
	public class FilePathValidator : PropertyValidator {

		public FilePathValidator()
			: base("Must be a valid filesystem path.") {

		}

		protected override bool IsValid(PropertyValidatorContext context) {
			var path = context.PropertyValue as string;
			if (!string.IsNullOrEmpty(path)) {
				return FilePath.TryParse(path);
			}

			return true;
		}

	}
}
