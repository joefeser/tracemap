using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Input;
using PostSharp.Aspects;
using PostSharp.Extensibility;

namespace MetadataDashboard.NetperfUI.Infrastructure.Aspects {

	[Serializable]
	[MulticastAttributeUsage(MulticastTargets.Property, 
		AllowMultiple=false, 
		Inheritance=MulticastInheritance.None,
		TargetMemberAttributes= MulticastAttributes.NonLiteral,
		AllowExternalAssemblies = false)]
	[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, 
		AllowMultiple=false, 
		Inherited=true)]
	internal class CaliburnPropertyChangedAspect : LocationInterceptionAspect {

		public override bool CompileTimeValidate(PostSharp.Reflection.LocationInfo locationInfo) {
			if (locationInfo.DeclaringType.GetCustomAttributes(typeof(IgnorePropertyChangedAttribute), false).Length > 0) {
				return false; // ignore all classes that have the IgnorePropertyChangedAttribute applied
			}

			if (!typeof(Caliburn.Micro.PropertyChangedBase).IsAssignableFrom(locationInfo.DeclaringType)) {
				// don't want to instrument types that don't implement the Caliburn INotifyOfPropertyChange implementation
				return false;
			}

			var propInfo = locationInfo.PropertyInfo;
			return locationInfo.LocationKind == PostSharp.Reflection.LocationKind.Property // only want properties, not fields or methods
				&& propInfo.CanRead && propInfo.CanWrite // only want properties that can be written to
				&& propInfo.GetIndexParameters().Length == 0 // don't handle indexed properties
				&& !( typeof(ICommand).IsAssignableFrom(locationInfo.LocationType) ) // don't handle ICommand properties
				&& propInfo.GetCustomAttributes(typeof(IgnorePropertyChangedAttribute), false).Length == 0
				&& propInfo.GetGetMethod() != null; // the get method must be public
		}

		public override void OnSetValue(LocationInterceptionArgs args) {

			if (args.Value != args.GetCurrentValue()) {
				args.ProceedSetValue();

				var propName = args.LocationName == "IsDirty" ? "IsDirty" : "";
				GetNotifyOfPropertyChange(args.Instance.GetType())
					.Invoke(args.Instance, new[] { propName });
			}
		}

		private static Dictionary<Type, MethodInfo> _cachedMethods = new Dictionary<Type, MethodInfo>();

		private static MethodInfo GetNotifyOfPropertyChange(Type instance) {

			if (!_cachedMethods.ContainsKey(instance)) {
				_cachedMethods[instance] = instance.GetMethod("NotifyOfPropertyChange", new Type[] { typeof(string) });
			}

			return _cachedMethods[instance];
		}

	}

}