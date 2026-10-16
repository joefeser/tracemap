using System.Security.Principal;
using System;
using NetPerf.Common;
using System.IO;
using System.Diagnostics;
using System.Net;

namespace MetadataDashboard.NetperfUI {
	internal static class Utility {

		public static bool IsAdministrator() {
			var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
			return principal.IsInRole(WindowsBuiltInRole.Administrator);
		}

		public static bool Is64Bit() {
			return IntPtr.Size == 8;
		}

		public static FilePath ApplicationConfigFilePath {
			get {
				var filePath = PathStub.Combine(
					EnvironmentStub.GetFolderPath(Environment.SpecialFolder.ApplicationData),
					"NetPerf",
					"UiConfiguration.xml");
				return FilePath.Parse(filePath);
			}
		}

		public static FilePath NetperfConsole {
			get {
				var result = FilePath.TryParse(System.Reflection.Assembly.GetEntryAssembly().Location);
				if (result) {
					return result.Data.ChangeFileNameWithoutExtension("netperf").ChangeExtension("exe");
				}

				return null;
			}
		}

		public static string NetperfVersion() {
			return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
			//return String.Format("{0}.{1}.{2}.{3}", fvi.ProductMajorPart, fvi.ProductMinorPart, fvi.ProductVersion, fvi.ProductBuildPart);
			//return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
		}

		public static string NetperfFileVersion() {
			var fvi = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
			return fvi.ProductVersion;
		}
	}
}
