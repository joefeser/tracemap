using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Caliburn.Micro;
using FluentValidation;
using FluentValidation.Results;
using NetPerf.Common.Validation;

namespace MetadataDashboard.NetperfUI.Infrastructure {
	/// <summary>
	/// Sub-classes Caliburn.Micro.Screen to provide access to functionality common to all Netperf screens.
	/// </summary>
	internal class NPScreen : Screen, ISelfValidate {

		protected ValidationResult m_mostRecentValidation;
		private static Dictionary<Type, IValidator> m_validators = new Dictionary<Type, IValidator>();

		protected IValidator Validator {
			get {
				if (!m_validators.ContainsKey(this.GetType())) {
					m_validators[this.GetType()] = new InlineValidator<NPScreen>();
				}

				return m_validators[this.GetType()];
			}
		}

		protected bool HasChangedSinceLastValidation {
			get {
				return m_mostRecentValidation == null;
			}
		}

		public bool IsDirty {
			get;
			set;
		}

		protected static void SetValidator(Type forType, IValidator validator) {
			Contract.Requires(forType != null);

			m_validators[forType] = validator ?? new InlineValidator<NPScreen>();
		}

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
				m_mostRecentValidation = Validator.Validate(this);
			}

			if (properties == null || properties.Length < 1) {
				return m_mostRecentValidation;
			}
			else {
				return new ValidationResult(m_mostRecentValidation.Errors.Where(f => properties.Contains(f.PropertyName)));
			}
		}

		protected string[] _ignoredProperties = new string[] { "IsValid", "IsDirty", "IsActive", "IsInitialized", "Parent", "DisplayName" };

		public override void NotifyOfPropertyChange(string propertyName) {
			if (IsNotifying) {
				this.IsNotifying = false;
				m_mostRecentValidation = null;

				if (!_ignoredProperties.Contains(propertyName)) {
					IsDirty = true;
				}

				Execute.OnUIThread(() => this.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(propertyName)));
				this.IsNotifying = true;
			}
			
		}

	}

}