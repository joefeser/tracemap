using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentValidation;
using System.Diagnostics.Contracts;
using FluentValidation.Validators;

namespace NetPerf.Common.Validation {
	public static class ValidationExtensions {

		public static IRuleBuilderOptions<T, TProperty> FileExists<T, TProperty>(this  IRuleBuilderInitial<T, TProperty> builder) {
			return builder.SetValidator(new FileExistsValidator());
		}

		public static IRuleBuilderOptions<T, TProperty> FilePathValid<T, TProperty>(this  IRuleBuilderInitial<T, TProperty> builder) {
			return builder.SetValidator(new FilePathValidator());
		}

		public static IRuleBuilderOptions<T, TProperty> DirectoryExists<T, TProperty>(this  IRuleBuilderInitial<T, TProperty> builder) {
			return builder.SetValidator(new DirectoryExistsValidator());
		}

		public static IRuleBuilderOptions<T, TProperty> DirectoryPathValid<T, TProperty>(this  IRuleBuilderInitial<T, TProperty> builder) {
			return builder.SetValidator(new DirectoryPathValidator());
		}

	}
}
