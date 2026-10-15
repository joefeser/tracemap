using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Tests.Integration {

    /// <summary>
    /// Fluent Interface used to store the call graph called by a method so it can be played back
    /// </summary>
    public class CallGraphTester {

        public IInstrumentationData InstrumentData {
            get;
            private set;
        }

        public List<CallGraphMethod> Events {
            get;
            private set;
        }

        private CallGraphTester() {

        }

        public CallGraphTester(IInstrumentationData metadata) {
            if (metadata == null) {
                throw new ArgumentNullException("metadata");
            }
            this.InstrumentData = metadata;
            Events = new List<CallGraphMethod>();
        }

        public CallGraphTester Call(string signature) {
            var retVal = new CallGraphMethod(signature, true);
            Events.Add(retVal);
            return this;
        }

        public CallGraphTester Exit(string signature) {
            var retVal = new CallGraphMethod(signature, false);
            Events.Add(retVal);
            return this;
        }

        public CallGraphTester Append(IEnumerable<CallGraphMethod> methods) {
            Events.AddRange(methods);
            return this;
        }

    }

    public class CallGraphMethod {

        public bool Enter {
            get;
            private set;
        }

        public string Signature {
            get;
            private set;
        }

        public CallGraphMethod(string signature, bool enter) {
            if (signature == null) {
                throw new ArgumentNullException("signature");
            }
            this.Signature = signature;
            this.Enter = enter;
        }

        public static CallGraphMethod EnterMethod(string signature) {
            return new CallGraphMethod(signature, true);
        }

        public static CallGraphMethod ExitMethod(string signature) {
            return new CallGraphMethod(signature, false);
        }
    }
}
