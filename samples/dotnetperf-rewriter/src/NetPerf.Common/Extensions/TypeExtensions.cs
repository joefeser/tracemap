using System.Linq;
using System.Diagnostics.Contracts;

namespace System {
	[Pure]
	public static class TypeExtensions {
		public static bool Implements(this Type element, Type implementedType) {
			if (implementedType.IsInterface) {
				return element.FindInterfaces((candidateType, typeToMatch) => candidateType == (Type)typeToMatch, implementedType).IsNotEmpty();
			}

			var baseType = element.BaseType;
			while (baseType != null) {
				if (baseType == implementedType)
					return true;

				baseType = baseType.BaseType;
			}

			return false;
		}

		public static bool Implements<T>(this Type element) {
			return Implements(element, typeof(T));
		}
	}
}
