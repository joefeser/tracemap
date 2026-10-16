using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MetadataDashboard.NetperfUI.Infrastructure;
using FluentValidation;
using NetPerf.Common.Validation;
using NetPerf.Common;

namespace MetadataDashboard.NetperfUI.Areas.Profiling.ViewModels {
	internal class InstrumentWizardStep1VM : NPScreen {

		private string m_applicationToRun;
		public string ApplicationToRun {
			get {
				return m_applicationToRun;
			}
			set {
				m_applicationToRun = value;
				if (WorkingFolder.IsNullOrWhitespace() && FilePath.TryParse(m_applicationToRun)) {
					WorkingFolder = System.IO.PathStub.GetDirectoryName(m_applicationToRun);
				}
			}
		}

		public string ApplicationParameters {
			get;
			set;
		}

		public string WorkingFolder {
			get;
			set;
		}

		public string HelpForApplicationExecutable {
			get {
				return UserMessages.HelpForApplicationExecutable;
			}
		}

		public string HelpForApplicationParameters {
			get {
				return UserMessages.HelpForApplicationParameters;
			}
		}

		public string HelpForWorkingFolder {
			get {
				return UserMessages.HelpForWorkingFolder;
			}
		}

		static InstrumentWizardStep1VM() {
			SetValidator(typeof(InstrumentWizardStep1VM), new InlineValidator<InstrumentWizardStep1VM>() {
				v => v.RuleFor(r => r.ApplicationToRun).NotEmpty(),
				v => v.RuleFor(r => r.ApplicationToRun).FilePathValid(),
				v => v.RuleFor(r => r.ApplicationToRun).FileExists(),
				
				v => v.RuleFor(r => r.WorkingFolder).NotEmpty(),
				v => v.RuleFor(r => r.WorkingFolder).DirectoryPathValid(),
				v => v.RuleFor(r => r.WorkingFolder).DirectoryExists(),
			});
		}

		private readonly IWindowManagerEx m_winManager;

		public InstrumentWizardStep1VM(IWindowManagerEx winManager) {
			DisplayName = "Select Application";

			m_winManager = winManager;
		}

		public void PickExecutableFile() {
			m_winManager.PickAFile(ApplicationToRun, "Executable Files (*.exe)|*.exe", newPath => ApplicationToRun = newPath);
		}

		public void PickWorkingFolder() {
			var selectedpath = m_winManager.OpenFolder(WorkingFolder, "Select the working folder");
			if (selectedpath != null) {
				WorkingFolder = selectedpath;
			}
		}

	}
}
