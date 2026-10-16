using NetPerf.Common.Validation;

namespace NetPerf.Common.Configuration {
	public interface IProblemReportingOptions : ISelfValidate {
		ReportOnError ContactOption {
			get;
			set;
		}

		bool CanContactForInfo {
			get;
			set;
		}

		string UserEmail {
			get;
			set;
		}
	}
}
