using FluentValidation.Validators;
using System.IO;

namespace NetPerf.Common.Validation {
	public class DirectoryExistsValidator : PropertyValidator {

		public DirectoryExistsValidator() : base("The selected directory must exist.") {

		}

		protected override bool IsValid(PropertyValidatorContext context) {
			var dir = context.PropertyValue as DirectoryPath;
			if (dir != null) {
				return System.IO.DirectoryStub.Exists(dir);
			}
			else {
				var strPath = context.PropertyValue as string;
				if (strPath != null) {
					var res = DirectoryPath.TryParse(strPath);
					if (res.HasError) {
						return false;
					}

					return DirectoryStub.Exists(res.Data);
				}
			}

			return true;
		}
	}
}
