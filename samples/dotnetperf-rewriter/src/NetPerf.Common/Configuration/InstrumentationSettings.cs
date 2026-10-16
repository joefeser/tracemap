using System.Collections.Generic;
using System.ComponentModel;
using FluentValidation;
using NetPerf.Common.Validation;

namespace NetPerf.Common.Configuration {
	internal class InstrumentationSettings : SelfValidate, IInstrumentationSettings {

		private FilePath _applicationToRun;
		public FilePath ApplicationToRun {
			get {
				return _applicationToRun;
			}
			set {
				if (value != _applicationToRun) {
					_applicationToRun = value;
				}
			}
		}

		private string _applicationParameters;
		public string ApplicationParameters {
			get {
				return _applicationParameters;
			}
			set {
				if (value != _applicationParameters) {
					_applicationParameters = value;
				}
			}
		}

		private FilePath _strongNameKeyFile;
		public FilePath StrongNameKeyFile {
			get {
				return _strongNameKeyFile;
			}
			set {
				if (value != _strongNameKeyFile) {
					_strongNameKeyFile = value;
				}
			}
		}

		public IList<FilePath> AssembliesToInstrument {
			get;
			internal set;
		}

		public IList<string> InstrumentationExclusions {
			get;
			internal set;
		}

		private IReportSetting _summaryReport;
		public IReportSetting SummaryReport {
			get {
				return _summaryReport;
			}
			set {
				if (value != _summaryReport) {
					_summaryReport = value;
				}
			}
		}

		private IReportSetting _extractReport;
		public IReportSetting ExtractReport {
			get {
				return _extractReport;
			}
			set {
				if (value != _extractReport) {
					_extractReport = value;
				}
			}
		}

		private DirectoryPath _workingFolder;
		public DirectoryPath WorkingFolder {
			get {
				return _workingFolder;
			}
			set {
				if (_workingFolder != value) {
					_workingFolder = value;
				}
			}
		}

		static InstrumentationSettings() {
			m_classValidator = new InlineValidator<IInstrumentationSettings>() {
					v => v.RuleFor(x => x.ApplicationToRun)
						.NotEmpty().WithMessage("An executable is required."),
					v => v.RuleFor(x => x.ApplicationToRun)
						.SetValidator(new FileExistsValidator())
							.When(x => x.ApplicationToRun != null)
							.WithMessage("The executable must exist."),
					v => v.RuleFor(x => x.StrongNameKeyFile)
						.SetValidator(new FileExistsValidator())
							.When(x => x.StrongNameKeyFile != null)
							.WithMessage("The strong name key file must exist."),
					v => v.RuleFor(x => x.AssembliesToInstrument)
						.NotEmpty().WithMessage("At least one assembly must be selected for profiling."),
					v => v.RuleFor(x => x.AssembliesToInstrument)
						.SetValidator(new FileExistsValidator())
							.When(x => x.AssembliesToInstrument != null),
					// InstrumentationExclusions is not supported yet
					v => v.RuleFor(x => x.SummaryReport)
						.SetValidator(new IReportSettingValidator())
							.When(s => s.SummaryReport != null)
							.WithMessage("A summary report file path is required."),
					v => v.RuleFor(x => x.ExtractReport)
						.SetValidator(new IReportSettingValidator())
							.When(s => s.ExtractReport != null)
							.WithMessage("A extract report file path is required."),
				};
		}

		public InstrumentationSettings() {

			var tempAssem = new BindingList<FilePath>();
			var tempExclude = new BindingList<string>();

			AssembliesToInstrument = tempAssem;
			InstrumentationExclusions = tempExclude;
			
		}
	}
}
