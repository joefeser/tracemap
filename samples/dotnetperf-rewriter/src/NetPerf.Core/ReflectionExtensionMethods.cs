using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace NetPerf.Core {
   
    public static class ReflectionExtensionMethods {

        public static int ModuleIdentifier(this Module module) {
            var type = module.GetType("NetPerf.NetPerfMetadata"); //Assembly
            if (type == null) {
                return 0;
            }
            var idField = type.GetField("ModuleIdentifier");
            if (idField == null) {
                return 0;
            }
            return (int)idField.GetValue(null);
        }

        public static string InstrumentationDataString(this Module module) {
            var type = module.GetType("NetPerf.NetPerfMetadata"); //Assembly
            if (type == null) {
                return null;
            }
            var idField = type.GetField("InstrumentationData");
            if (idField == null) {
                return null;
            }
            var data = idField.GetValue(null) as string;
            return data;
        }

    }
}
