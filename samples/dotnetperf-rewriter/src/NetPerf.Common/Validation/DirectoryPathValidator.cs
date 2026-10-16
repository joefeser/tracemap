using FluentValidation.Validators;

namespace NetPerf.Common.Validation {
	public class DirectoryPathValidator : PropertyValidator {

		public DirectoryPathValidator()
			: base("The path must be a valid filesystem path.") {

		}

		protected override bool IsValid(PropertyValidatorContext context) {
			var path = context.PropertyValue as string;
			if (!string.IsNullOrEmpty(path)) {
				return DirectoryPath.TryParse(path);
			}

			return true;
		}
	}
}
