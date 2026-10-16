using NetPerf.Common;
using System.IO;

namespace NetPerf {
	/// <summary>
	/// Not meant to be exposed or accessible from the command line.
	/// </summary>
	internal class RunExternalProcessCommand {

		public FilePath ApplicationToRun {
			get;
			set;
		}

		public string ApplicationParameters {
			get;
			set;
		}

		/// <summary>
		/// The base directory for resolving relative paths in the netperf config file
		/// this class came from, as well as the executing directory of the ApplicationToRun.
		/// </summary>
		public DirectoryPath ApplicationBasePath {
			get;
			set;
		}

		public string GetExecutingFolder() {
			var fi = new FileInfo(ApplicationToRun);
			return fi.DirectoryName;
		}

	}
}
