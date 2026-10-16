
using System;
using System.Diagnostics.Contracts;

namespace MetadataDashboard.NetperfUI.Infrastructure {

	internal class Webpage {

		public static readonly Webpage OpenCEIP = new Webpage("http://www.dotnetperf.com");
		public static readonly Webpage EvaluateLowCostEdition = new Webpage("http://www.dotnetperf.com");
		public static readonly Webpage CommunityLicenseLimitiations = new Webpage("http://www.dotnetperf.com");
		public static readonly Webpage AcquireNewLicense = new Webpage("http://www.dotnetperf.com");
		public static readonly Webpage DownloadNewVersion = new Webpage("http://www.dotnetperf.com");

		public string Url {
			get;
			private set;
		}

		private Webpage(string url) {
			Contract.Requires(!String.IsNullOrEmpty(url));

			Url = url;
		}

		public void OpenUrl() {
			System.Diagnostics.Process.Start(Url);
		}

	}

}