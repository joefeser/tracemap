using System.Diagnostics.Contracts;
using FluentValidation;
using MetadataDashboard.NetperfUI.Infrastructure;
using NetPerf.Common.Configuration;

namespace MetadataDashboard.NetperfUI.Areas.UISettings.ViewModels {

	internal class ProblemReportingOptionsVM : NPScreen {

		public ReportOnError ContactOption {
			get;
			set;
		}

		public bool CanContactForInfo {
			get;
			set;
		}

		public string UserEmail {
			get;
			set;
		}

		static ProblemReportingOptionsVM() {
			SetValidator(typeof(ProblemReportingOptionsVM), new InlineValidator<ProblemReportingOptionsVM> {
					v => v.RuleFor(o => o.UserEmail).NotEmpty()
						.When(o => o.CanContactForInfo)
						.WithMessage("An email address is required."),
					v => v.RuleFor(o => o.UserEmail).EmailAddress()
						.When(o => o.CanContactForInfo)
				});
		}

		public ProblemReportingOptionsVM(IAppConfig config) {
			Contract.Requires(config != null);

			DisplayName = "Reporting Problems";
			var options = config.ProblemReportingOptions;
			ContactOption = options.ContactOption;
			CanContactForInfo = options.CanContactForInfo;
			UserEmail = options.UserEmail;
		}

	}
}