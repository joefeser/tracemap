using MetadataDashboard.NetperfUI.Infrastructure;

namespace MetadataDashboard.NetperfUI.Areas.UISettings.ViewModels {
	internal class ApplicationInfoVM : NPScreen {

		public string AppVersion {
			get;
			private set;
		}

		public string FileVersion {
			get;
			private set;
		}

		public ApplicationInfoVM() {
			DisplayName = "Info";

			AppVersion = Utility.NetperfVersion();
			FileVersion = Utility.NetperfFileVersion();
		}
	}
}
