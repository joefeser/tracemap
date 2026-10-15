using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using FluentValidation.Results;
using NetPerf.Common.Validation;

namespace NetPerf.Common.Configuration {
	[ContractClass(typeof(IInstrumentationSettingsContract))]
	public interface IInstrumentationSettings : ISelfValidate {

		FilePath ApplicationToRun {
			get;
			set;
		}

		string ApplicationParameters {
			get;
			set;
		}

		FilePath StrongNameKeyFile {
			get;
			set;
		}

		IList<FilePath> AssembliesToInstrument {
			get;
		}

		IList<string> InstrumentationExclusions {
			get;
		}

		IReportSetting SummaryReport {
			get;
			set;
		}

		IReportSetting ExtractReport {
			get;
			set;
		}

		DirectoryPath WorkingFolder {
			get;
			set;
		}
	}

	[ContractClassFor(typeof(IInstrumentationSettings))]
	abstract class IInstrumentationSettingsContract : IInstrumentationSettings {

		FilePath IInstrumentationSettings.ApplicationToRun {
			get;
			set;
		}

		string IInstrumentationSettings.ApplicationParameters {
			get;
			set;
		}

		FilePath IInstrumentationSettings.StrongNameKeyFile {
			get;
			set;
		}

		IList<FilePath> IInstrumentationSettings.AssembliesToInstrument {
			get {
				Contract.Ensures(Contract.Result<IList<FilePath>>() != null);
				return default(IList<FilePath>);
			}
		}

		IList<string> IInstrumentationSettings.InstrumentationExclusions {
			get {
				Contract.Ensures(Contract.Result<IList<string>>() != null);
				return default(IList<string>);
			}
		}

		IReportSetting IInstrumentationSettings.SummaryReport {
			get;
			set;
		}

		IReportSetting IInstrumentationSettings.ExtractReport {
			get;
			set;
		}

		DirectoryPath IInstrumentationSettings.WorkingFolder {
			get;
			set;
		}

		bool ISelfValidate.IsValid {
			get {
				return default(bool);
			}
		}

		ValidationResult ISelfValidate.Validate(params string[] properties) {
			return default(ValidationResult);
		}

		string IDataErrorInfo.Error {
			get {
				return default(string);
			}
		}

		string IDataErrorInfo.this[string columnName] {
			get {
				return default(string);
			}
		}

		event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged {
			add {
				throw new System.NotImplementedException();
			}
			remove {
				throw new System.NotImplementedException();
			}
		}
	}
}
