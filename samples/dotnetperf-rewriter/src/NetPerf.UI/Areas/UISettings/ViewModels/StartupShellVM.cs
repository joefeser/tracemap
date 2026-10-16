using System.Diagnostics.Contracts;
using System.Linq;
using Caliburn.Micro;
using MetadataDashboard.NetperfUI.Areas.ViewModels;
using MetadataDashboard.NetperfUI.Infrastructure;
using NetPerf.Common.Configuration;

namespace MetadataDashboard.NetperfUI.Areas.UISettings.ViewModels {

	internal class StartupShellVM : WizardVM {

		private readonly IAppConfig _config;
		private readonly IEventAggregator _events;

		public bool MustHandleNewerUIConfig {
			get;
			set;
		}

		public StartupShellVM(IAppConfig config, IEventAggregator events) {
			Contract.Requires(config != null);
			Contract.Requires(events != null);

			_config = config;
			_events = events;

			DisplayName = "Configure Netperf";
			IsSequentialWizard = true;
		}

		protected override void OnActivate() {

			if (MustHandleNewerUIConfig) {
				Items.Add(IoC.Get<StartupHandleNewerUIConfigVM>());
			}

			Items.Add(IoC.Get<StartupEULAVM>());
			Items.Add(IoC.Get<CEIPVM>());
			Items.Add(IoC.Get<ProblemReportingOptionsVM>());
			Items.Add(IoC.Get<AutoUpdateOptionsVM>());

			base.OnActivate();
		}

		public override void FinishWizard() {
			_config.HasFinishedFirstStartup = true;

			var newerConfig = Items.FirstOrDefault(i => i is StartupHandleNewerUIConfigVM);
			if (newerConfig != null) {
				_events.Publish(new DowngradeUIConfigMessage(((StartupHandleNewerUIConfigVM)newerConfig).DowngradeUIConfig ));
			}

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

		public override void CancelWizard() {
			_config.HasFinishedFirstStartup = false;
			this.TryClose();
		}
	}

}