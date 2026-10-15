using MetadataDashboard.NetperfUI.Infrastructure;

namespace MetadataDashboard.NetperfUI.Areas.UISettings.ViewModels {
	internal class StartupHandleNewerUIConfigVM : NPScreen {

		public bool DowngradeUIConfig {
			get;
			set;
		}

		public StartupHandleNewerUIConfigVM() {

			DisplayName = "Downgrade UI Config?";
		}
	}
}
