using System.Globalization;

namespace System {
	public static class NumericExtensions {

		public static string ToStorageString(this int val) {
			return val.ToString(CultureInfo.InvariantCulture);
		}

		public static string ToUserString(this int val) {
			return val.ToString(CultureInfo.CurrentCulture);
		}

		public static string ToStorageString(this long val) {
			return val.ToString(CultureInfo.InvariantCulture);
		}

		public static string ToUserString(this long val) {
			return val.ToString(CultureInfo.CurrentCulture);
		}

		public static string ToStorageString(this decimal val) {
			return val.ToString(CultureInfo.InvariantCulture);
		}

		public static string ToUserString(this decimal val) {
			return val.ToString(CultureInfo.CurrentCulture);
		}

		public static string ToStorageString(this float val) {
			return val.ToString(CultureInfo.InvariantCulture);
		}

		public static string ToUserString(this float val) {
			return val.ToString(CultureInfo.CurrentCulture);
		}

		public static string ToStorageString(this double val) {
			return val.ToString(CultureInfo.InvariantCulture);
		}

		public static string ToUserString(this double val) {
			return val.ToString(CultureInfo.CurrentCulture);
		}

		public static string ToStorageString(this bool val) {
			return val.ToString().ToLowerInvariant();
		}
	}
}
