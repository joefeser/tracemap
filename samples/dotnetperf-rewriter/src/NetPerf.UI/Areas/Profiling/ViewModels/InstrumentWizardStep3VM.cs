using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MetadataDashboard.NetperfUI.Infrastructure;
using Caliburn.Micro;
using FluentValidation;
using NetPerf.Common.Validation;
using NetPerf.Common;
using System.Windows.Input;
using System.Windows.Controls;

namespace MetadataDashboard.NetperfUI.Areas.Profiling.ViewModels {
	internal class InstrumentWizardStep3VM : NPScreen, IHandleFileDrop {

		public BindableCollection<string> IncludedAssemblies {
			get;
			private set;
		}

		public string StrongNameKeyFile {
			get;
			set;
		}

		public string HelpForStrongNameKeyFile {
			get {
				return UserMessages.HelpForStrongNameKeyFile;
			}
		}

		public string HelpForAssembliesToInstrument {
			get {
				return UserMessages.HelpForAssembliesToInstrument;
			}
		}

		static InstrumentWizardStep3VM() {
			var assemListValidator = new InlineValidator<string>();
			assemListValidator.RuleFor(r => r).NotNull().WithName("Assembly");
			assemListValidator.RuleFor(r => r).NotEmpty().WithName("Assembly");
			assemListValidator.RuleFor(r => r).FileExists().When(r => !string.IsNullOrEmpty(r)).WithName("Assembly");

			SetValidator(typeof(InstrumentWizardStep3VM), new InlineValidator<InstrumentWizardStep3VM>() {
				v => v.RuleFor(r => r.StrongNameKeyFile)
						.FileExists()
						.When(i => !string.IsNullOrEmpty(i.StrongNameKeyFile)),
				v => v.RuleFor(r => r.IncludedAssemblies)
						.NotEmpty()
						.WithMessage("At least one assembly must be selected for instrumentation."),
				v => v.RuleFor(r => r.IncludedAssemblies)
						.SetValidator(assemListValidator)
			});
		}

		private readonly IWindowManagerEx m_winManager;

		public InstrumentWizardStep3VM(IWindowManagerEx winManager) {
			DisplayName = "Select Assemblies";
			m_winManager = winManager;

			IncludedAssemblies = new BindableCollection<string>();
		}

		public void KeyPressed(KeyEventArgs key, ListView control) {
			if (key.Key == Key.Delete && control.SelectedIndex > -1) {
				var prevCtrlIdx = control.SelectedIndex;

				var item = (control.SelectedItem ?? string.Empty).ToString();
				var index = IncludedAssemblies.IndexOf(item);
				if (index > -1) {
					IncludedAssemblies.RemoveAt(index);
				}

				control.SelectedIndex = prevCtrlIdx;
			}
		}

		public void PickAssemblies() {
			var files = m_winManager.OpenFiles(
				canChooseMultiple: true,
				filterFormat: "Assemblies and Executables (*.dll, *.exe)|*.exe;*.dll");

			foreach (var f in files) {
				if (!IncludedAssemblies.Contains(f.FullPath)) {
					IncludedAssemblies.Add(f.FullPath);
				}
			}

		}

		public void PickStrongNameKey() {
			m_winManager.PickAFile(StrongNameKeyFile, "Public Key File (*.snk)|*.snk|All Files (*.*)|*.*", newPath => StrongNameKeyFile = newPath);
		}

		public bool CanAcceptFileDrop(string[] files) {
			if (files == null || files.Length < 1)
				return false;

			// as soon as we see a single valid file path for
			// an assembly or an executable, we can return true
			for (int i = 0; i < files.Length; i++) {
				if (IsValidInstrumentationTarget(files[i])) {
					return true;
				}
			}

			return false;
		}

		public void AcceptFileDrop(string[] files) {
			if (files == null || files.Length < 1)
				return;

			for (int i = 0; i < files.Length; i++) {
				if (IsValidInstrumentationTarget(files[i]) && !IncludedAssemblies.Contains(files[i])) {
					//TODO check whether the assembly is a .NET assembly or a native assembly before adding it
					IncludedAssemblies.Add(files[i]);
				}
			}
		}

		private bool IsValidInstrumentationTarget(string path) {
			return FilePath.TryParse(path).WasSuccess && (path.EndsWith(".dll") || path.EndsWith(".exe"));
		}
	}
}
