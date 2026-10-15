using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetPerf.Tests.Integration {
    
    /// <summary>
    /// Default values for a type and other items.
    /// </summary>
    public static class ReflectionDefaultValues {

        /// <summary>
        /// Get the default value for a type used when creating a method on the fly.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static object DefaultValueForType(Type t) {

            if (t == typeof(int)) {
                return 1;
            }
            else if (t == typeof(long)) {
                return (long)1;
            }
            else if (t == typeof(short)) {
                return (short)1;
            }
            else if (t == typeof(byte)) {
                return (byte)1;
            }
            else if (t == typeof(string)) {
                return "DEFAULT STRING";
            }

            return null;
        }
    
    }
}
