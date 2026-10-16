using System.Diagnostics.Contracts;
using System.Windows.Input;
using Caliburn.Micro;
using MetadataDashboard.NetperfUI.Infrastructure;
using MetadataDashboard.NetperfUI.Infrastructure.Utilities;
using NetPerf.Common.Configuration;

namespace MetadataDashboard.NetperfUI.Areas.UISettings.ViewModels {

	// Customer Experience Improvement Program using Caliburn.Micro;
	internal class CEIPVM : NPScreen {

		public bool ParticipateInCEIP {
			get;
			set;
		}

		public ICommand OpenCEIPWebsite {
			get;
			private set;
		}

		public CEIPVM(IAppConfig config, IEventAggregator aggregator) {
			Contract.Requires(config != null);
			Contract.Requires(aggregator != null);

			DisplayName = "Customer Feedback";
			ParticipateInCEIP = config.ParticipateInCEIP;

			OpenCEIPWebsite = new DelegateCommand(o => aggregator.Publish(Webpage.OpenCEIP));
		}

	}
}