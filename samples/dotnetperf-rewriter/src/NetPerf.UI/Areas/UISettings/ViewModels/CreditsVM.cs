using MetadataDashboard.NetperfUI.Infrastructure;

namespace MetadataDashboard.NetperfUI.Areas.UISettings.ViewModels {
	internal class CreditsVM : NPScreen {

		public string NetperfVersion {
			get;
			private set;
		}

		public CreditsVM() {
			DisplayName = "Credits";
			NetperfVersion = Utility.NetperfVersion();
		}

	}
}
