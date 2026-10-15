using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetPerf.Client {
    public static class InstrBase {
        public static void MethodEnter(int methodId) {
            Messaging.EnterMethod(methodId);
        }

        public static void MethodExit(int methodId) {
            Console.WriteLine("inside methodexit. id is ", methodId);
        }
    }
}
