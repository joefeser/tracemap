using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;
using System.Reflection;

namespace NetPerf.Tests.Integration.CallGraphTestClassesNS {

    /// <summary>
    /// This is used as a base class for all items that use the CallGraphTester
    /// </summary>
    public class CallGraphValidateBaseClass {

        protected static Assembly CallGraphTestClasses {
            get;
            private set;
        }

        protected static IInstrumentationData InstrumentationData {
            get;
            private set;
        }

        static CallGraphValidateBaseClass() {
            IntegrationTestIClientMessageService.RegisterWithIOC();
            CallGraphTestClasses = System.Reflection.Assembly.Load("CallGraphTestClasses");
            InstrumentationData = CallGraphTestClasses.InstrumentationData();
        }
    }
}
