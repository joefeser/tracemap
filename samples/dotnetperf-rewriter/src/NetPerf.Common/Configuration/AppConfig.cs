
using System.Collections.Generic;
namespace NetPerf.Common.Configuration {
	internal class AppConfig : IAppConfig {

		public bool ParticipateInCEIP {
			get;
			set;
		}

		public bool HasFinishedFirstStartup {
			get;
			set;
		}

		public UpdateOption AutoUpdateOption {
			get;
			set;
		}

		public IList<FilePath> RecentSettingFiles {
			get;
			private set;
		}

		public IScreenPosition MainWindowPosition {
			get;
			internal set;
		}

		public IProblemReportingOptions ProblemReportingOptions {
			get;
			internal set;
		}

		public AppConfig() {
			// fulfil contracts
			RecentSettingFiles = new List<FilePath>();
			MainWindowPosition = new ScreenPosition();
			ProblemReportingOptions = new ProblemReportingOptions();
		}
	}
}
