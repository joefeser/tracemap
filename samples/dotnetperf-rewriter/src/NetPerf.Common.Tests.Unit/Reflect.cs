using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.Reflection;

namespace NetPerf.Common.UnitTests {
	public static class Reflect {

		public static void ReplaceFieldOrProperty(object owner, string memberOrFieldName, object newValue) {
			Contract.Requires<ArgumentNullException>(owner != null, "owner");
			Contract.Requires<ArgumentException>(!String.IsNullOrEmpty(memberOrFieldName));

			var typ = owner.GetType();
			var matches = typ.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
				.Where(mi => mi.Name == memberOrFieldName);

			if (matches.IsEmpty()) {
				throw new ArgumentException(string.Format(
					"Cannot find a public/private, static/instance field or property named '{0}' on type '{1}'",
					memberOrFieldName,
					typ),
					"memberOrFieldName");
			}

			if (matches.Count() > 1) {
				var names = matches.Aggregate(new StringBuilder(), (sb, mi) => sb.AppendLine(mi.MemberType + " " + mi.Name));
				throw new ArgumentException(string.Format(
					"Found too many public/private, static/instance fields or properties named '{0}' on type '{1}'{2}{3}",
					memberOrFieldName,
					typ,
					EnvironmentStub.NewLine,
					names.ToString()),
					"memberOrFieldName");
			}

			var member = matches.Single();
			if (member is FieldInfo) {
				var field = (FieldInfo)member;
				field.SetValue(owner, newValue);
			}
			else if (member is PropertyInfo) {
				var property = (PropertyInfo)member;
				property.SetValue(owner, newValue, null);
			}
			else {
				throw new ArgumentException(string.Format(
					"Could not find public/private, static/instance field or property named '{0}' on type '{1}'{2}Found instead '{3} {4}{5}'",
					memberOrFieldName,
					typ,
					EnvironmentStub.NewLine,
					member.MemberType,
					member.Name,
					member is MethodInfo ? "( ... )" : string.Empty
					),
					"memberOrFieldName");
			}
		}

	}
}
