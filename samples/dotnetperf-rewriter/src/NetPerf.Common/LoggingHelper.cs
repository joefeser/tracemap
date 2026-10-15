using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace NetPerf.Common {

	/// <summary>
	/// Class used for now to log to debug and output
	/// </summary>
	public static class LoggingHelper {

		public static void LogAndOutputError(Exception ex) {
			if (ex != null) {
				Debug.WriteLine("Error:" + ex.ToString());
				ConsoleStub.WriteLine("Error:" + ex.Message);
			}
		}

		public static void LogAndOutputError(string error) {
			if (error != null) {
				Debug.WriteLine(error);
				ConsoleStub.WriteLine(error);
			}
		}
	}
}
