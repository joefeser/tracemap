using System.Diagnostics.Contracts;
using MetadataDashboard.NetperfUI.Infrastructure;
using NetPerf.Common.Configuration;

namespace MetadataDashboard.NetperfUI.Areas.UISettings.ViewModels {
	internal class AutoUpdateOptionsVM : NPScreen {

		public UpdateOption AutoUpdate {
			get;
			set;
		}


		public AutoUpdateOptionsVM(IAppConfig config) {
			Contract.Requires(config != null);

			DisplayName = "Automatic Updates";
			AutoUpdate = config.AutoUpdateOption;

		}


	}
}
