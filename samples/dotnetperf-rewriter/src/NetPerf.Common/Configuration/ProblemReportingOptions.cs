using System;
using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;
using NetPerf.Common.Validation;

namespace NetPerf.Common.Configuration {
	internal class ProblemReportingOptions : SelfValidate, IProblemReportingOptions {
		
		public ReportOnError ContactOption {
			get;
			set;
		}

		public bool CanContactForInfo {
			get;
			set;
		}

		public string UserEmail {
			get;
			set;
		}

		static ProblemReportingOptions() {
			m_classValidator = new InlineValidator<IProblemReportingOptions> {
					v => v.RuleFor(o => o.UserEmail).EmailAddress().When(o => o.CanContactForInfo)
				};
		}

		public ProblemReportingOptions() {
			// set defaults
			ContactOption = ReportOnError.Ask;
			CanContactForInfo = true;
			UserEmail = null;
		}
	}
}
