using NetPerf.Common.Configuration;

namespace MetadataDashboard.NetperfUI.Areas.ViewModels {
	internal class AutoUpdateAdaptor : IAutoUpdateAdaptor {

		private wyDay.Controls.AutomaticUpdater _control;

		public AutoUpdateAdaptor(wyDay.Controls.AutomaticUpdater control) {
			//control.Arguments
			//control.Changes
			//control.DaysBetweenChecks
			//control.KeepHidden
			//control.UpdateStepOn
			//control.UpdateType
			//control.Version
			//control.WaitBeforeCheckSecs
			_control = control;
			_control.UpdateType = wyDay.Controls.UpdateType.DoNothing;
			_control.KeepHidden = false;
		}

		public void StartUpdateCheck(UpdateOption option) {

			try {
				switch (option) {
					case UpdateOption.CheckOnly:
						_control.UpdateType = wyDay.Controls.UpdateType.OnlyCheck;
						break;
					case UpdateOption.Never:
						_control.UpdateType = wyDay.Controls.UpdateType.DoNothing;
						break;
					case UpdateOption.CheckAndDownload:
						_control.UpdateType = wyDay.Controls.UpdateType.CheckAndDownload;
						break;
					case UpdateOption.AutomaticInstall:
						_control.UpdateType = wyDay.Controls.UpdateType.Automatic;
						break;
					default:
						_control.UpdateType = wyDay.Controls.UpdateType.DoNothing;
						break;
				}

				_control.ForceCheckForUpdate();
			}
			finally {
				_control.UpdateType = wyDay.Controls.UpdateType.DoNothing;
			}

		}

		public void Dispose() {
			if (_control != null) {
				// unattach from any event handlers
			}

			_control = null; // the View is responsible for actually disposing of the control
		}
	}
}
