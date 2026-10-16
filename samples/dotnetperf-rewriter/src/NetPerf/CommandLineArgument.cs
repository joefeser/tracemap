using System;
using System.Globalization;

namespace NetPerf {
	/// <summary>
	/// A single command line argument. Permits implicit conversions to several base .NET types, 
	/// and identifies a command line argument as a key (begins with the <see cref="KeyPrefix"/>) or as a simple value.
	/// </summary>
	[System.Diagnostics.DebuggerDisplay("{IsKey}, {Value}")]
	internal class CommandLineArgument {

		public const string DefaultKeyPrefix = "/";

		private readonly string _keyPrefix;

		/// <summary>
		/// Whether this <see cref="CommandLineArgument"/> begins with the <see cref="KeyPrefix"/> or not.
		/// </summary>
		public bool IsKey {
			get;
			private set;
		}

		/// <summary>
		/// The text typed into the command line (sans the <see cref="KeyPrefix"/>) that this <see cref="CommandLineArgument"/> was createed from.
		/// If this <see cref="CommandLineArgument"/> is a key, then this is the value without the <see cref="KeyPrefix"/>.
		/// </summary>
		public string Value {
			get;
			private set;
		}

		/// <summary>
		/// The string prefix that identifies this <see cref="CommandLineArgument"/> as a key or not.
		/// If the <see cref="CommandLineArgument"/> is a key, then this prefix plus the <see cref="Value"/>
		/// is the original text that created this <see cref="CommandLineArgument"/>.
		/// </summary>
		public string KeyPrefix {
			get {
				return _keyPrefix;
			}
		}

		public string AsKeyValue() {
			return Value.ToLowerInvariant().Trim();
		}

		public CommandLineArgument(string argument, string keyPrefix = DefaultKeyPrefix) {
			if (argument.IsNullOrWhitespace())
				throw new ArgumentOutOfRangeException("argument", "cannot be null or empty");

			if (keyPrefix.IsNullOrWhitespace())
				throw new ArgumentOutOfRangeException("keyPrefix", "cannot be null or empty");

			_keyPrefix = keyPrefix;
			IsKey = argument.StartsWith(_keyPrefix, false, CultureInfo.CurrentCulture);
			Value = IsKey
				? argument.Substring(1)
				: argument;
		}

		public static implicit operator string(CommandLineArgument arg) {
			return arg.Value;
		}

		public static explicit operator int(CommandLineArgument arg) {
			return int.Parse(arg.Value, CultureInfo.CurrentCulture);
		}

		public static explicit operator long(CommandLineArgument arg) {
			return long.Parse(arg.Value, CultureInfo.CurrentCulture);
		}

		public static explicit operator double(CommandLineArgument arg) {
			return double.Parse(arg.Value, CultureInfo.CurrentCulture);
		}

		public static explicit operator decimal(CommandLineArgument arg) {
			return decimal.Parse(arg.Value, CultureInfo.CurrentCulture);
		}
	}
}
