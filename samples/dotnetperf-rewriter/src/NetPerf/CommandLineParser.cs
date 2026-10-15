using System.Collections;
using System.Collections.Generic;

namespace NetPerf {
	/// <summary>
	/// Parses the command line of a console program into <see cref="CommandLineArgument"/>s consisting of
	/// both keys and values. Acts as a bi-directional iterator over the <see cref="CommandLineArgument"/>s of a console program.
	/// </summary>
	internal sealed class CommandLineParser {
		private readonly string[] _args;
		private int _index;

		public CommandLineArgument Current {
			get;
			private set;
		}

		public bool HasAnyArguments {
			get {
				return _args != null && _args.Length > 0;
			}
		}

		public bool WasLastMoveSuccess {
			get;
			private set;
		}

		public CommandLineParser(string[] arguments) {
			_args = arguments;
			_index = -1;
		}

		public CommandLineParser MoveNext() {
			if (!HasAnyArguments || _index >= _args.Length - 1) {
				WasLastMoveSuccess = false;
				return this;
			}

			Current = new CommandLineArgument(_args[++_index]);

			WasLastMoveSuccess = true;
			return this;
		}

		public CommandLineParser MovePrevious() {
			if (!HasAnyArguments || _index < 0) {
				WasLastMoveSuccess = false;
				return this;
			}

			Current = new CommandLineArgument(_args[--_index]);

			WasLastMoveSuccess = true;
			return this;
		}

		public void Reset() {
			_index = -1;
			Current = null;
		}

		public static implicit operator bool(CommandLineParser parser) {
			return parser.WasLastMoveSuccess;
		}
	}
}
