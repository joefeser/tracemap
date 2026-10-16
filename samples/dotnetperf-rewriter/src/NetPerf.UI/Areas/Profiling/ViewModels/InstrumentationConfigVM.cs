using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Caliburn.Micro;
using FluentValidation;
using MetadataDashboard.NetperfUI.Infrastructure;
using NetPerf.Common;
using NetPerf.Common.Configuration;
using NetPerf.Common.Validation;

namespace MetadataDashboard.NetperfUI.Areas.Profiling.ViewModels {
	internal class InstrumentationConfigVM : NPScreen, IHandleFileDrop {

		private readonly IWindowManagerEx m_winManager;
		private readonly IInstrumentationSettings m_settings;

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

		public FilePath ConfigFilePath {
			get;
			set;
		}

		public string ExtractReportPath {
			get;
			set;
		}

		public bool ExtractReportIsSelected {
			get;
			set;
		}

		public BindableCollection<string> IncludedAssemblies {
			get;
			private set;
		}

		public string StrongNameKeyFile {
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

		static InstrumentationConfigVM() {
			var assemListValidator = new InlineValidator<string>();
			assemListValidator.RuleFor(r => r).NotNull().WithName("Assembly");
			assemListValidator.RuleFor(r => r).NotEmpty().WithName("Assembly");
			assemListValidator.RuleFor(r => r).FileExists().When(r => !string.IsNullOrEmpty(r)).WithName("Assembly");


			var x = new InlineValidator<InstrumentationConfigVM> {
					v => v.RuleFor(r => r.ApplicationToRun).NotEmpty(),
					v => v.RuleFor(r => r.ApplicationToRun).FilePathValid(),
					v => v.RuleFor(r => r.ApplicationToRun).FileExists(),
					
					v => v.RuleFor(r => r.WorkingFolder).NotEmpty(),
					v => v.RuleFor(r => r.WorkingFolder).DirectoryPathValid(),
					v => v.RuleFor(r => r.WorkingFolder).DirectoryExists(),

					v => v.RuleFor(r => r.StrongNameKeyFile)
						.FileExists()
						.When(i => !string.IsNullOrEmpty(i.StrongNameKeyFile)),

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
						.When(i => i.ExtractReportIsSelected),

					v => v.RuleFor(r => r.IncludedAssemblies)
						.NotEmpty()
						.WithMessage("At least one assembly must be selected for instrumentation."),
					v => v.RuleFor(r => r.IncludedAssemblies)
						.SetValidator(assemListValidator)
				};

			SetValidator(typeof(InstrumentationConfigVM), x);
		}

		public InstrumentationConfigVM(IWindowManagerEx win, IInstrumentationSettings settings, FilePath saveLocation = null) {
			Contract.Requires(win != null);
			Contract.Requires(settings != null);

			DisplayName = "Profiling Settings";
			ConfigFilePath = saveLocation;

			IncludedAssemblies = new BindableCollection<string>();
			//in the strange case we miss something that is changed, we want to wire up to the collection change so that we always obtain the change.
			IncludedAssemblies.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(IncludedAssemblies_CollectionChanged);
			m_winManager = win;
			m_settings = settings;
			
			PopulateFromModel();
		}

		private void IncludedAssemblies_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
			NotifyOfPropertyChange(string.Empty);
		}

		private void PopulateFromModel() {
			ApplicationToRun = m_settings.ApplicationToRun ?? string.Empty;
			ApplicationParameters = m_settings.ApplicationParameters ?? string.Empty;
			IncludedAssemblies.AddRange(m_settings.AssembliesToInstrument.Select(a => a.FullPath));
			StrongNameKeyFile = m_settings.StrongNameKeyFile ?? string.Empty;
			WorkingFolder = m_settings.WorkingFolder ?? string.Empty;

			if (m_settings.SummaryReport != null) {
				SummaryReportIsSelected = m_settings.SummaryReport.IsActive;
				SummaryReportPath = m_settings.SummaryReport.OutputFile ?? string.Empty;
			}

			if (m_settings.ExtractReport != null) {
				ExtractReportIsSelected = m_settings.ExtractReport.IsActive;
				ExtractReportPath = m_settings.ExtractReport.OutputFile ?? string.Empty;
			}
		}

		public IInstrumentationSettings ExtractProfilingConfig() {
			if (!this.IsValid) {
				throw new InvalidOperationException("The current settings are not valid! They cannot be extracted.");
			}

			m_settings.ApplicationParameters = this.ApplicationParameters;
			m_settings.ApplicationToRun = FilePath.Parse(this.ApplicationToRun);
			m_settings.AssembliesToInstrument.Clear();
			m_settings.AssembliesToInstrument.AddRange(this.IncludedAssemblies.Distinct().Select(ia => FilePath.Parse(ia)));
			m_settings.WorkingFolder = DirectoryPath.Parse(this.WorkingFolder);

			m_settings.ExtractReport = new ReportSetting();
			m_settings.ExtractReport.IsActive = this.ExtractReportIsSelected;
			m_settings.ExtractReport.OutputType = NetperfReportType.Extract;

			m_settings.SummaryReport = new ReportSetting();
			m_settings.SummaryReport.IsActive = this.SummaryReportIsSelected;
			m_settings.SummaryReport.OutputType = NetperfReportType.Summary;

			var result = FilePath.TryParse(this.ExtractReportPath);
			if (result.WasSuccess) {
				m_settings.ExtractReport.OutputFile = result.Data;
			}

			result = FilePath.TryParse(this.SummaryReportPath);
			if (result.WasSuccess) {
				m_settings.SummaryReport.OutputFile = result.Data;
			}

			result = FilePath.TryParse(this.StrongNameKeyFile);
			if (result.WasSuccess) {
				m_settings.StrongNameKeyFile = result.Data;
			}

			return m_settings;
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

		public void PickExecutableFile() {
			m_winManager.PickAFile(ApplicationToRun, "Executable Files (*.exe)|*.exe", newPath => ApplicationToRun = newPath);
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

		public void PickWorkingFolder() {
			var selectedpath = m_winManager.OpenFolder(WorkingFolder, "Select the working folder");
			if (selectedpath != null) {
				WorkingFolder = selectedpath;
			}
		}

		public void PickStrongNameKey() {
			m_winManager.PickAFile(StrongNameKeyFile, "Public Key File (*.snk)|*.snk|All Files (*.*)|*.*", newPath => StrongNameKeyFile = newPath);
		}

		public void PickSummaryReportPath() {
			m_winManager.SaveFile(newPath => SummaryReportPath = newPath, "HTML file (*.html)|*.html", SummaryReportPath);
		}

		public void PickExtractReportPath() {
			m_winManager.SaveFile(newPath => ExtractReportPath = newPath, "XML File (*.xml)|*.xml", ExtractReportPath);
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
