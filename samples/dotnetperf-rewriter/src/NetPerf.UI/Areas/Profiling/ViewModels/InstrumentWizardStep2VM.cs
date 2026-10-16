using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MetadataDashboard.NetperfUI.Infrastructure;
using FluentValidation;
using NetPerf.Common.Validation;

namespace MetadataDashboard.NetperfUI.Areas.Profiling.ViewModels {
	internal class InstrumentWizardStep2VM : NPScreen {

		public string ExtractReportPath {
			get;
			set;
		}

		public bool ExtractReportIsSelected {
			get;
			set;
		}

		public string SummaryReportPath {
			get;
			set;
		}

		public bool SummaryReportIsSelected {
			get;
			set;
		}

		public string HelpForHTMLReport {
			get {
				return UserMessages.HelpForHTMLReport;
			}
		}

		public string HelpForXMLReport {
			get {
				return UserMessages.HelpForXMLReport;
			}
		}

		static InstrumentWizardStep2VM() {
			SetValidator(typeof(InstrumentWizardStep2VM), new InlineValidator<InstrumentWizardStep2VM>() {
				v => v.RuleFor(r => r.SummaryReportPath)
					.NotNull()
					.When(i => i.SummaryReportIsSelected),

				v => v.RuleFor(r => r.SummaryReportPath)
					.FilePathValid()
					.When(i => i.SummaryReportIsSelected),

				v => v.RuleFor(r => r.ExtractReportPath)
					.NotNull()
					.When(i => i.ExtractReportIsSelected),

				v => v.RuleFor(r => r.ExtractReportPath)
					.FilePathValid()
					.When(i => i.ExtractReportIsSelected)
			});
		}

		private readonly IWindowManagerEx m_winManager;

		public InstrumentWizardStep2VM(IWindowManagerEx winManager) {
			DisplayName = "Choose Reports";
			m_winManager = winManager;
		}

		public void PickSummaryReportPath() {
			m_winManager.SaveFile(newPath => SummaryReportPath = newPath, "HTML file (*.html)|*.html", SummaryReportPath);
		}

		public void PickExtractReportPath() {
			m_winManager.SaveFile(newPath => ExtractReportPath = newPath, "XML File (*.xml)|*.xml", ExtractReportPath);
		}

	}
}
