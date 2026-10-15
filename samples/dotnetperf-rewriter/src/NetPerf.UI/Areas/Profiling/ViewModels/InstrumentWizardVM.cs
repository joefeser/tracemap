using System.Diagnostics.Contracts;
using System.Linq;
using MetadataDashboard.NetperfUI.Areas.ViewModels;
using MetadataDashboard.NetperfUI.Infrastructure;

namespace MetadataDashboard.NetperfUI.Areas.Profiling.ViewModels {
	internal class InstrumentWizardVM : WizardVM {

		private readonly IWindowManagerEx m_winManager;
		private readonly InstrumentationConfigVM m_config;

		public InstrumentWizardVM(IWindowManagerEx winManager, InstrumentationConfigVM config) {
			Contract.Requires(winManager != null);
			Contract.Requires(config != null);

			DisplayName = "Netperf Profiling Wizard";

			m_winManager = winManager;
			m_config = config;
		}

		protected override void OnActivate() {

			var step1 = new InstrumentWizardStep1VM(m_winManager);
			step1.ApplicationParameters = m_config.ApplicationParameters;
			step1.ApplicationToRun = m_config.ApplicationToRun;
			step1.WorkingFolder = m_config.WorkingFolder;
			
			var step2 = new InstrumentWizardStep2VM(m_winManager);
			step2.ExtractReportIsSelected = m_config.ExtractReportIsSelected;
			step2.ExtractReportPath = m_config.ExtractReportPath;
			step2.SummaryReportIsSelected = m_config.SummaryReportIsSelected;
			step2.SummaryReportPath = m_config.SummaryReportPath;

			var step3 = new InstrumentWizardStep3VM(m_winManager);
			step3.StrongNameKeyFile = m_config.StrongNameKeyFile;
			step3.IncludedAssemblies.Clear();
			step3.IncludedAssemblies.AddRange(m_config.IncludedAssemblies);

			Items.Add(step1);
			Items.Add(step2);
			Items.Add(step3);

			base.OnActivate();
		}

		public override bool CanFinishWizard {
			get {
				return true;
			}
		}

		public override void FinishWizard() {
			var step1 = (InstrumentWizardStep1VM)Items.Single(i => i is InstrumentWizardStep1VM);
			var step2 = (InstrumentWizardStep2VM)Items.Single(i => i is InstrumentWizardStep2VM);
			var step3 = (InstrumentWizardStep3VM)Items.Single(i => i is InstrumentWizardStep3VM);

			m_config.ApplicationParameters = step1.ApplicationParameters;
			m_config.ApplicationToRun = step1.ApplicationToRun;
			m_config.WorkingFolder = step1.WorkingFolder;

			m_config.ExtractReportIsSelected = step2.ExtractReportIsSelected;
			m_config.ExtractReportPath = step2.ExtractReportPath;
			m_config.SummaryReportIsSelected = step2.SummaryReportIsSelected;
			m_config.SummaryReportPath = step2.SummaryReportPath;


			m_config.StrongNameKeyFile = step3.StrongNameKeyFile;
			m_config.IncludedAssemblies.Clear();
			m_config.IncludedAssemblies.AddRange(step3.IncludedAssemblies);
			
			base.FinishWizard();
		}
	}
}
