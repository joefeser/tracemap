
namespace MetadataDashboard.NetperfUI.Infrastructure {
	internal static class UserMessages {

		public static string HelpForApplicationExecutable {
			get {
				return @"The ""Application Executable"" is the process that you want Netperf to profile. The executable will not be automatically instrumented for profiling; it must be added to the ""Assemblies to Instrument"" list on the last step of this wizard.";
			}
		}

		public static string HelpForApplicationParameters {
			get {
				return @"If the ""Application Executable"" requires any command-line parameters, they must be added in the ""Application Parameters"" field.";
			}
		}

		public static string HelpForWorkingFolder {
			get {
				return @"By default, the ""Application Executable""s working folder will be the path it runs from.  If the application's working folder should be different, then enter the desired working folder here.";
			}
		}

		public static string HelpForHTMLReport {
			get {
				return @"The HTML report provides a visual summary of the timing information gathered by Netperf. The report highlights the most time-consuming methods and presents an overall summary of the application's performance.";
			}
		}

		public static string HelpForXMLReport {
			get {
				return @"The XML report provides a complete record of the timing information gathered by Netperf. Netperf provides an xsl file capable of transforming the XML report into a visual HTML representation of the timing information, but the XML report also permits you to write custom reports against Netperf timing information.";
			}
		}

		public static string HelpForStrongNameKeyFile {
			get {
				return @"If any of the assemblies selected for instrumentation are signed with a strong-name key, the strong-name key specified here will be used to re-sign them after they have been instrumented.";
			}
		}

		public static string HelpForAssembliesToInstrument {
			get {
				return @"All the assemlies and executables that you want timing information from must be entered into the ""Assemblies to Instrument"" list. Remember that the ""Application Executable"" is not automatically instrumented.";
			}
		}

	}
}
