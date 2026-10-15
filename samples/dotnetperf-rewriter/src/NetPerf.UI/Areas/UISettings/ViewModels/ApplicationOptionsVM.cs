using System.Diagnostics.Contracts;
using System.Linq;
using Caliburn.Micro;
using NetPerf.Common.Configuration;
using System.ComponentModel;
using NetPerf.Common.Validation;

namespace MetadataDashboard.NetperfUI.Areas.UISettings.ViewModels {

	internal class ApplicationOptionsVM : Conductor<Screen>.Collection.OneActive {

		private readonly IAppConfig _config;

		public bool CanAcceptChanges {
			get {
				foreach (var item in Items) {
					var validate = item as ISelfValidate;
					if (validate != null) {
						if (!validate.IsValid)
							return false;
					}
				}
				
				return true;
			}
		}

		public ApplicationOptionsVM(IAppConfig config) {
			Contract.Requires(config != null);

			_config = config;

			DisplayName = "Application Options";
		}

		protected override void OnActivate() {
			Items.Add(IoC.Get<CEIPVM>());
			Items.Add(IoC.Get<ProblemReportingOptionsVM>());
			Items.Add(IoC.Get<AutoUpdateOptionsVM>());

			foreach (var item in Items) {
				item.PropertyChanged += new PropertyChangedEventHandler(item_PropertyChanged);
			}

			ActivateItem(Items.First());

			base.OnActivate();
		}

		protected override void OnDeactivate(bool close) {
			if (close) {
				foreach (var item in Items) {
					item.PropertyChanged -= new PropertyChangedEventHandler(item_PropertyChanged);
				}
			}
			
			base.OnDeactivate(close);
		}

		void item_PropertyChanged(object sender, PropertyChangedEventArgs e) {
			NotifyOfPropertyChange(() => CanAcceptChanges);
		}

		public void AcceptChanges() {
			// save all of the changes.
			var ceip = (CEIPVM)Items.Single(i => i is CEIPVM);
			_config.ParticipateInCEIP = ceip.ParticipateInCEIP;

			var pro = (ProblemReportingOptionsVM)Items.Single(i => i is ProblemReportingOptionsVM);
			var options = _config.ProblemReportingOptions;
			options.CanContactForInfo = pro.CanContactForInfo;
			options.ContactOption = pro.ContactOption;
			options.UserEmail = pro.UserEmail;

			var update = (AutoUpdateOptionsVM)Items.Single(i => i is AutoUpdateOptionsVM);
			_config.AutoUpdateOption = update.AutoUpdate;

			this.TryClose();
		}

		public void CancelChanges() {
			// don't save any state
			this.TryClose();
		}
	}

}