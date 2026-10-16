using System.Diagnostics.Contracts;
using Caliburn.Micro;
using MetadataDashboard.NetperfUI.Infrastructure.Aspects;

namespace MetadataDashboard.NetperfUI.Areas.ViewModels {
	[IgnorePropertyChanged]
	internal class MessageWindowVM : Screen {

		public string Message {
			get;
			private set;
		}

		public MessageWindowVM(string message, string title = null) {
			Contract.Requires(message != null);

			Message = message;
			DisplayName = title;
		}

	}
}
