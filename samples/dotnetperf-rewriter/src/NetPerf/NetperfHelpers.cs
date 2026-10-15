using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Common;

namespace NetPerf {

	/// <summary>
	/// helpers until we can determine if they can be moved to core
	/// </summary>
	public static class NetperfHelpers {

		/// <summary>
		/// Get the folder that netperf is executing from.
		/// </summary>
		/// <returns></returns>
		public static string NetperfFolder() {
			return FilePath.Parse(typeof(ConsoleUI).Assembly.Location).ParentDirectory;
		}

	}
}
