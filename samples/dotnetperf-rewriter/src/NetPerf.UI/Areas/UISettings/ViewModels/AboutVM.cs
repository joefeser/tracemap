using Caliburn.Micro;
using MetadataDashboard.NetperfUI.Infrastructure;

namespace MetadataDashboard.NetperfUI.Areas.UISettings.ViewModels {

	internal class AboutVM : Conductor<NPScreen>.Collection.OneActive {

		public AboutVM() {
			DisplayName = "About Netperf";
		}

		protected override void OnInitialize() {
			base.OnInitialize();

			Items.Add(IoC.Get<CreditsVM>());
			Items.Add(IoC.Get<ApplicationInfoVM>());
			Items.Add(IoC.Get<EULAVM>());
		}

		public void CloseAboutNetperf() {
			this.TryClose();
		}
	}
}
