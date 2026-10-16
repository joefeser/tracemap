
namespace MetadataDashboard.NetperfUI.Infrastructure {
	internal class DowngradeUIConfigMessage {

		public bool DowngradeConfig {
			get;
			private set;
		}

		public DowngradeUIConfigMessage(bool downgrade) {
			DowngradeConfig = downgrade;
		}

	}
}
