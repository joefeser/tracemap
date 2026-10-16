using System.Linq;
using FluentValidation;
using FluentValidation.Results;

namespace NetPerf.Common.Validation {
	public abstract class SelfValidate : ISelfValidate {

		protected static IValidator m_classValidator = new InlineValidator<SelfValidate>();
		protected ValidationResult m_mostRecentValidation;

		public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;


		public bool IsValid {
			get {
				return Validate().IsValid;
			}
		}
				
		public string Error {
			get {
				return Validate().Errors.Select(e => e.ErrorMessage).FirstOrDefault();
			}
		}

		public string this[string columnName] {
			get {
				return Validate()
					.Errors
					.Where(e => e.PropertyName == columnName)
					.Select(e => e.ErrorMessage)
					.FirstOrDefault();
			}
		}

		public virtual ValidationResult Validate(params string[] properties) {
			if (m_mostRecentValidation == null) {
				m_mostRecentValidation = m_classValidator.Validate(this);
			}

			if (properties == null || properties.Length < 1) {
				return m_mostRecentValidation;
			}
			else {
				return new ValidationResult(m_mostRecentValidation.Errors.Where(f => properties.Contains(f.PropertyName)));
			}
		}

		protected virtual void NotifyOfPropertyChange(string propertyName) {
			m_mostRecentValidation = null;
			var e = PropertyChanged;
			if (e != null) {
				e(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
			}
		}
	}
}
