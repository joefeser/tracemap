using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting.Contexts;
using System.Runtime.Remoting.Activation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace NetPerf.Common.Tests {
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public sealed class TestClassContextAttribute : ContextAttribute {

		public TestClassContextAttribute()
			: base("TestContextAttribute") {
			// do nothing
		}

		public override void GetPropertiesForNewContext(IConstructionCallMessage ctorMsg) {
			if (ctorMsg == null)
				throw new ArgumentNullException("ctorMsg");

			var obj = Type.GetType(ctorMsg.TypeName);
			if (obj != null) {
				// look at all the methods of the type
				// for methods that have the [TestMethod] atribute
				// and an attribute derived from [TestContextAttribute]
				// and register all of the attributes derived from [TestContextAttribute]

				var methodAttributes = obj.GetMethods()
					.Where(m => m.GetCustomAttributes(typeof(TestMethodAttribute), true).IsNotEmpty()
						&& m.GetCustomAttributes(typeof(IgnoreAttribute), true).IsEmpty())
					.SelectMany(m => m.GetCustomAttributes(true))
					.Where(attr => attr.GetType().IsSubclassOf(typeof(TestContextAttribute)))
					.Distinct((x, y) => x.GetType() == y.GetType());


				var fieldInfo = typeof(ContextAttribute).GetField("AttributeName", BindingFlags.NonPublic | BindingFlags.Instance);
				foreach (var contextAttr in methodAttributes) {
					var typeName = contextAttr.GetType().AssemblyQualifiedName;
					var constr = contextAttr.GetType().GetConstructor(Type.EmptyTypes);
					if (constr != null) {
						var item = constr.Invoke(null);
						fieldInfo.SetValue(item, typeName);
						ctorMsg.ContextProperties.Add(item);
					}
				}
			}
		}
	}
}
