using System;
using NetPerf.Common.Configuration;

namespace MetadataDashboard.NetperfUI.Areas.ViewModels {
	public interface IAutoUpdateAdaptor : IDisposable {

		void StartUpdateCheck(UpdateOption option);
	}
}
