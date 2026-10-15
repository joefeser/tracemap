using System;
using System.Diagnostics.Contracts;

namespace NetPerf {
	internal class RestoreCommand : InstrumentCommand {

		public RestoreCommand(InstrumentCommand fromBase) {
			Contract.Requires<ArgumentNullException>(fromBase != null, "fromBase");

			Contract.Ensures(AssemblyPaths.Count == fromBase.AssemblyPaths.Count);
			Contract.Ensures(Contract.ForAll(0, fromBase.AssemblyPaths.Count,
				index => AssemblyPaths[index] == fromBase.AssemblyPaths[index]));

			Contract.Ensures(BackupDirectory == fromBase.BackupDirectory);
			Contract.Ensures(BaseDirectory == fromBase.BaseDirectory);
			Contract.Ensures(Excludes.Count == fromBase.Excludes.Count);
			Contract.Ensures(Contract.ForAll(0, fromBase.Excludes.Count,
				index => Excludes[index] == fromBase.Excludes[index]));

			AssemblyPaths.AddRange(fromBase.AssemblyPaths);
			BackupDirectory = fromBase.BackupDirectory;

			if (fromBase.BaseDirectory != null) {
				SetBaseDirectory(fromBase.BaseDirectory);
			}

			Excludes.AddRange(fromBase.Excludes);
		}

	}
}
