using System.Diagnostics.Contracts;
using System.Linq;

namespace System.Text.RegularExpressions {
	[System.Diagnostics.DebuggerDisplay("{_patternRegex}")]
	public sealed class Wildcard {

		private const RegexOptions _allRegexOptions =
			RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ECMAScript |
			RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace |
			RegexOptions.Multiline | RegexOptions.RightToLeft | RegexOptions.Singleline;

		private readonly Regex _patternRegex;

		static Wildcard() {
			var values = Enum.GetValues(typeof(RegexOptions));
		}

		public Wildcard(string pattern, RegexOptions options = RegexOptions.None) {
			Contract.Requires(pattern != null, "pattern");
			Contract.Requires(options >= RegexOptions.None);
			Contract.Requires(options <= _allRegexOptions);

			_patternRegex = new Regex(ConvertToRegularExpression(pattern), options);
		}

		public static string Escape(string input) {
			Contract.Requires(input != null, "input");
			if (string.IsNullOrEmpty(input))
				return input;

			var escapedString = input.Aggregate(new StringBuilder(),
				(sb, ch) => IsMetaCharacter(ch) ? sb.Append('\\').Append(ch) : sb.Append(ch));

			return escapedString.ToString();
		}

		private static bool IsMetaCharacter(char ch) {
			return ch == '*' || ch == '?' || ch == '\\' || ch == '[' || ch == ']';
		}

		private static string ConvertToRegularExpression(string pattern) {
			var regexStr = new StringBuilder('^');
			regexStr = pattern.Aggregate(regexStr,
				(sb, ch) => ch == '*' || ch == '?' ? sb.Append('.').Append(ch) : sb.Append(ch));
			regexStr.Append('$');
			return regexStr.ToString();
		}

		public bool IsMatch(string input) {
			Contract.Requires(input != null, "input");

			return _patternRegex.IsMatch(input);
		}

		public static bool IsMatch(string input, string pattern, RegexOptions options = RegexOptions.None) {
			Contract.Requires(input != null, "input");
			Contract.Requires(pattern != null, "pattern");

			return new Wildcard(pattern, options).IsMatch(input);
		}

		//will do these when they are needed
		//public static Match Match(string input, string pattern, RegexOptions options) {
		//    throw new NotImplementedException();
		//}

		//public static MatchCollection Matches(string input, string pattern, RegexOptions options) {
		//    throw new NotImplementedException();
		//}

		//public static string Replace(string input, string pattern, string replacement, RegexOptions options) {
		//    throw new NotImplementedException();
		//}

		//public static string Replace(string input, string pattern, MatchEvaluator evaluator, RegexOptions options) {
		//    throw new NotImplementedException();
		//}

		//public static string[] Split(string input, string pattern, RegexOptions options) {
		//    throw new NotImplementedException();
		//}

		//should perfectly reverse escape
		public static string Unescape(string input) {
			Contract.Requires(input != null, "input");
			if (string.IsNullOrEmpty(input))
				return input;

			if (input.Length < 2)
				return input;

			var unescaped = new StringBuilder(input.Length);
			for (int i = 0; i < input.Length; i++) {
				if (input[i] == '\\' && input.Length != i + 1) {
					if (IsMetaCharacter(input[i + 1])) {
						i += 1;
					}
				}

				unescaped.Append(input[i]);
			}

			return unescaped.ToString();
		}
	}
}
