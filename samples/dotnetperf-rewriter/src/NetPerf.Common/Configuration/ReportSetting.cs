
namespace NetPerf.Common.Configuration {
	public class ReportSetting : IReportSetting {
		public bool IsActive {
			get;
			set;
		}

		public FilePath OutputFile {
			get;
			set;
		}

		public NetperfReportType OutputType {
			get;
			set;
		}
	}
}
