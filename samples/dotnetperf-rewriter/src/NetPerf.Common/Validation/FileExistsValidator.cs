using FluentValidation.Validators;
using System.IO;

namespace NetPerf.Common.Validation {

	public class FileExistsValidator : PropertyValidator {

		public FileExistsValidator() : base ("The selected file must exist.") {

		}

		protected override bool IsValid(PropertyValidatorContext context) {
			var path = context.PropertyValue as FilePath;
			if (path != null) {
				return FileStub.Exists(path);
			}
			else {
				var strPath = context.PropertyValue as string;
				if (strPath != null) {
					var res = FilePath.TryParse(strPath);
					if (res.HasError ) {
						return false;
					}

					return FileStub.Exists(res.Data);
				}
			}

			return true;
		}
	}

}