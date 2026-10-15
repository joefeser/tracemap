using System.Diagnostics.Contracts;
using System.Collections.Generic;

namespace NetPerf.Common.Configuration {
	[ContractClass(typeof(IAppConfigContract))]
	public interface IAppConfig {

		bool ParticipateInCEIP {
			get;
			set;
		}

		bool HasFinishedFirstStartup {
			get;
			set;
		}

		UpdateOption AutoUpdateOption {
			get;
			set;
		}

		IScreenPosition MainWindowPosition {
			get;
		}

		IList<FilePath> RecentSettingFiles {
			get;
		}

		IProblemReportingOptions ProblemReportingOptions {
			get;
		}

	}

	[ContractClassFor(typeof(IAppConfig))]
	abstract class IAppConfigContract : IAppConfig {

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

		public IScreenPosition MainWindowPosition {
			get {
				Contract.Ensures(Contract.Result<IScreenPosition>() != null);
				return default(IScreenPosition);
			}
		}

		public IList<FilePath> RecentSettingFiles {
			get {
				Contract.Ensures(Contract.Result<IList<FilePath>>() != null);
				return default(IList<FilePath>);
			}
		}

		public IProblemReportingOptions ProblemReportingOptions {
			get {
				Contract.Ensures(Contract.Result<IProblemReportingOptions>() != null);
				return default(IProblemReportingOptions);
			}
		}

	}
}
