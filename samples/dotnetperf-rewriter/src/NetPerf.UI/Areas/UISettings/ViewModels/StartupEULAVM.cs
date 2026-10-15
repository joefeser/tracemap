using FluentValidation;
using MetadataDashboard.NetperfUI.Infrastructure;

namespace MetadataDashboard.NetperfUI.Areas.UISettings.ViewModels {
	internal class StartupEULAVM : EULAVM {

		public bool HasAcceptedEULA {
			get;
			set;
		}

		static StartupEULAVM() {
			SetValidator(typeof(StartupEULAVM), new InlineValidator<StartupEULAVM> {
					v => v.RuleFor(o => o.HasAcceptedEULA).Must(b => b == true).WithMessage("You must accept the EULA to continue.")
				});
		}

		public StartupEULAVM() {
			DisplayName = "License Agreement";
		}
	}
}
