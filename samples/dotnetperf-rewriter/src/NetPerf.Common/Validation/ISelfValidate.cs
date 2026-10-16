using System.ComponentModel;
using System.Diagnostics.Contracts;
using FluentValidation.Results;

namespace NetPerf.Common.Validation {

	[ContractClass(typeof(ISelfValidateContract))]
	public interface ISelfValidate : IDataErrorInfo, INotifyPropertyChanged {

		bool IsValid {
			get;
		}

		ValidationResult Validate(params string[] properties);
	}

	[ContractClassFor(typeof(ISelfValidate))]
	abstract class ISelfValidateContract : ISelfValidate {

		bool ISelfValidate.IsValid {
			get {
				return default(bool);
			}
		}

		ValidationResult ISelfValidate.Validate(params string[] properties) {
			Contract.Ensures(Contract.Result<ValidationResult>() != null);
			Contract.Ensures(Contract.Result<ValidationResult>().Errors != null);
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
		
#pragma warning disable 0067
		//disabling the "unused event" warning only within the contract class
		public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 0067
	}
}
