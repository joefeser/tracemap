using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Common.Instrumentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetPerf.Common.Tests.Test_References {
	internal class UniqueIdGenerator_Privates {

		
		public static Dictionary<Type, int> PreviousIds {
			get {
				var obj = new PrivateType(typeof(UniqueIdGenerator));
				return (Dictionary<Type, int>)obj.GetStaticField("_previousIds");
			}
			set {
				var obj = new PrivateType(typeof(UniqueIdGenerator));
				obj.SetStaticField("_previousIds", value);
			}
		}

	}
}
