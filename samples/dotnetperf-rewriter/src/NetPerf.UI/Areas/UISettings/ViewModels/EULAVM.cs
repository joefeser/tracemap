using MetadataDashboard.NetperfUI.Infrastructure;

namespace MetadataDashboard.NetperfUI.Areas.UISettings.ViewModels {
	internal class EULAVM : NPScreen {

		public System.IO.Stream EULAFileStream {
			get {
				var resourceName = typeof(StartupEULAVM).FullName.Replace(".StartupEULAVM", ".DotNetPerfEULA.rtf");
				var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
				return stream;
			}
		}

		public EULAVM() {
			DisplayName = "EULA";
		}
	}
}
