
namespace NetPerf.Common.Configuration {
	public interface IReportSetting {

		bool IsActive {
			get;
			set;
		}

		FilePath OutputFile {
			get;
			set;
		}

		NetperfReportType OutputType {
			get;
			set;
		}
	}
}
