using System.Linq;
using System.Globalization;
using System.Diagnostics.Contracts;

namespace System {
	/// <summary>
	/// Extension methods for the <see cref="String"/> class.
	/// </summary>
	[Pure] // all extension methods should be side-effect free
	public static class StringExtensions {

		/// <summary>
		/// Returns true if the string is null, empty, or composed entirely of whitespace characters.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool IsNullOrWhitespace(this string value) {
			return string.IsNullOrEmpty(value) || !value.Any(ch => !char.IsWhiteSpace(ch));
		}

		/// <summary>
		/// Compares two strings using the <see cref="StringComparision.OrdinalIgnoreCase"/> comparer.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="other"></param>
		/// <returns></returns>
		public static bool IgnoreCaseEq(this string value, string other) {
			return string.Equals(value, other, StringComparison.OrdinalIgnoreCase);
		}

		public static string[] SplitAndRemoveEmpty(this string source, params char[] splitchars) {
			if (source == null)
				return new string[0];

			return source.Split(splitchars, StringSplitOptions.RemoveEmptyEntries);
		}

		public static int ParseStorageString(this string value) {
			return int.Parse(value, CultureInfo.InvariantCulture);
		}

		public static double ParseDoubleStorageString(this string value) {
			return double.Parse(value, CultureInfo.InvariantCulture);
		}
	}
}
