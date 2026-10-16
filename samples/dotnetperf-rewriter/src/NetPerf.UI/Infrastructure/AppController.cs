using System.Diagnostics.Contracts;
using Caliburn.Micro;
using NetPerf.Common.Configuration;
using MetadataDashboard.NetperfUI.Areas.ViewModels;

namespace MetadataDashboard.NetperfUI.Infrastructure {

	/// <summary>
	/// Master controller for the application. primarily responsible for coordinating
	/// the hand-off between the Startup workflow and the application screen workflow.
	/// </summary>
	internal class AppController : IHandle<Webpage>
	{
		private readonly IWindowManagerEx _winManager;
		private readonly System.Action _appExitCallback;

		public AppController(IWindowManagerEx windowManager, IEventAggregator events, System.Action onApplicationExit) {
			Contract.Requires(windowManager != null);
			Contract.Requires(events != null);
			Contract.Requires(onApplicationExit != null);

			_winManager = windowManager;
			events.Subscribe(this);
			_appExitCallback = onApplicationExit;
		}

		/// <summary>
		/// Loads all necessary configuration, shows the application's main window, and decides whether a
		/// sub-workflow (e.g licensing, configuration) needs to start. If so, starts the sub-workflow.
		/// </summary>
		public void StartApplication(bool hasNewerUIConfig) {
			var config = IoC.Get<IAppConfig>();
			if (config.HasFinishedFirstStartup) {
				OpenMainShell();
			}
			else {
				var wizard = IoC.Get<MetadataDashboard.NetperfUI.Areas.UISettings.ViewModels.StartupShellVM>();
				wizard.MustHandleNewerUIConfig = hasNewerUIConfig;
				wizard.Deactivated += new System.EventHandler<DeactivationEventArgs>(wizard_Deactivated);
				_winManager.ShowWindow(wizard);
			}
		}

		void wizard_Deactivated(object sender, DeactivationEventArgs e) {
			if (e.WasClosed) {
				((IDeactivate)sender).Deactivated -= wizard_Deactivated;

				// force the user to complete the initial configuration before starting the main application
				if (IoC.Get<IAppConfig>().HasFinishedFirstStartup) {
					OpenMainShell();
				}
				else {
					CloseApplication();
				}
			}
		}

		private void OpenMainShell() {
			var shell = IoC.Get<ShellVM>();
			shell.Deactivated += new System.EventHandler<DeactivationEventArgs>(shell_Deactivated);

			_winManager.ShowWindow(shell);
		}

		public void CloseApplication() {
			this._appExitCallback();
		}

		void shell_Deactivated(object sender, DeactivationEventArgs e) {
			if (e.WasClosed) {
				((IDeactivate)sender).Deactivated -= shell_Deactivated;
				CloseApplication();
			}
		}

		public void Handle(Webpage message) {
			message.OpenUrl();
		}
	}

}