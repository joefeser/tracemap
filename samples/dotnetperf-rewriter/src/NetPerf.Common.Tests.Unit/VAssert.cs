using System;
using System.Linq;
using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Validation;

namespace NetPerf.Common.Tests {
	public static class VAssert {
		
		public static void AssertHasRule<TObject, TProperty>(this TObject item, Expression<Func<TObject, TProperty>> expr, Type validatorType) where TObject : ISelfValidate {
			var propName = ((System.Linq.Expressions.MemberExpression)expr.Body).Member.Name;

			var validator = new PrivateType(item.GetType()).GetStaticField("m_classValidator") as IValidator<TObject>;

			if (validator == null) {
				Assert.Fail("Missing expected static field '_classValidator' within object '{0}'", typeof(IValidator<TObject>));
				return;
			}

			var rulesForMember = validator.Cast<PropertyRule>().Where(r => r.Member.Name == propName);

			if (rulesForMember == null) {
				Assert.Fail("There are no validation rules defined for the '{0}' property on '{1}'. Expected a rule of type '{2}'.",
					propName,
					typeof(TObject),
					validatorType);
				return;
			}

			var actualRule = rulesForMember
				.SelectMany(pr => pr.Validators)
				.Select(v => v.GetType() == typeof(DelegatingValidator) ? ((DelegatingValidator)v).InnerValidator : v)
				.FirstOrDefault(v => v.GetType() == validatorType);

			if (actualRule == null) {
				Assert.Fail("Did not find a '{0}' validation rule for the '{1}' property on '{2}'.",
					validatorType,
					propName,
					typeof(TObject));
				return;
			}
			
			// pass
		}

		public static void AssertCheckedForNull<TObject, TProperty>(this TObject item, Expression<Func<TObject, TProperty>> expr) where TObject : ISelfValidate {
			AssertHasRule(item, expr, typeof(NotNullValidator));
		}

		public static void AssertCheckedForEmpty<TObject, TProperty>(this TObject item, Expression<Func<TObject, TProperty>> expr) where TObject : ISelfValidate {
			AssertHasRule(item, expr, typeof(NotEmptyValidator));
		}
	}
}
