using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.ServiceModel;
using System.Reflection;
using NetPerf.Client;
using Autofac;
using NetPerf.Common.Instrumentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using NetPerf.Core.Interfaces;
using NetPerf.Core;
using System.Xml.Serialization;
using System.IO;

namespace NetPerf.Tests.Integration {

    public class IntegrationTestIClientMessageService : CoreClientMessageService {

        private static bool _isRegisteredWithIOC = false;
       
        /// <summary>
        /// Helper method to be called from the tests to ensure it is registered with the IOC Container.
        /// </summary>
        public static void RegisterWithIOC() {
            if (!_isRegisteredWithIOC) {

                var builder = new ContainerBuilder();
                builder.RegisterType<CoreMessageProcessor>().As<IMessageProcessor>().SingleInstance();
                builder.RegisterType<IntegrationTestIClientMessageService>().As<IClientMessageService>().SingleInstance();

                var container = builder.Build();

                Debug.WriteLine("Resolve IClientMessageService");
                Messaging.Service = container.Resolve<IClientMessageService>();
                Debug.WriteLine("IClientMessageService" + Messaging.Service != null ? Messaging.Service.GetType().FullName : "<none>");
                container.Dispose();

                _isRegisteredWithIOC = true;
            }
        }

        public static IntegrationTestIClientMessageService Instance {
            get {
                RegisterWithIOC();
                return Messaging.Service as IntegrationTestIClientMessageService;
            }
        }

        public List<TimingMethod> MethodEvents {
            get {
                return (Processor as CoreMessageProcessor).MethodEvents;
            }
        }

        public List<TimingPoint> PointEvents {
            get {
                return (Processor as CoreMessageProcessor).PointEvents;
            }
        }

        public IntegrationTestIClientMessageService(IMessageProcessor processor)
            : base(processor) {
                if (!typeof(CoreMessageProcessor).IsAssignableFrom(processor.GetType())) {
                    throw new ApplicationException("Integration Requires that the processor inherits from CoreMessageProcessor");
                }
        }

        public void ClearCalls() {
            (Instance.Processor as CoreMessageProcessor).ClearCalls();
        }

        //private methods

        public static string ToTraceStatements(MethodInfo methodInfo) {
            return ToTraceStatements(methodInfo.Name);
        }

        public static string ToTraceStatements(string methodName) {
            var sb = new StringBuilder();
            sb.Append("//");
            sb.AppendLine(methodName);
            sb.AppendLine("var tester = new CallGraphTester(InstrumentationData)");
            //make sure they are in time order.

            foreach (var item in Instance.MethodEvents.OrderBy(item => item.TimeStamp)) {
                var data = (from known in Instance. KnownInstances().Values
                            from assem in known.assemblies
                            from module in assem.modules
                            where module.id == item.ModuleId
                            select known).FirstOrDefault();

                var method = Instance.DataMap()[data.instanceHash][item.MethodId];
                if (item.Enter) {
                    sb.Append(".Call(\"");
                }
                else {
                    sb.Append(".Exit(\"");
                }
                sb.Append(method.signature);
                sb.AppendLine("\")");

            }
            sb.Length -= 2; //so the ; is on the last line correctly.
            sb.AppendLine(";");
            return sb.ToString();
        }

        public static void ValidateCallGraph(CallGraphTester graph) {

            if (graph.Events.Count != Instance.MethodEvents.Count) {
                Assert.Fail(string.Format("Count does not match. graph:{0} events:{1}\r\nGraph:\r\n{2}\r\nEvents:\r\n{3}",
                    graph.Events.Count, Instance.MethodEvents.Count, ToTraceString(graph), ToTraceString(Instance.MethodEvents)));
            }

            int lastModuleId = 0;
            InstrumentationData lastData = null;

            var graphCalls = graph.Events.GetEnumerator();

            foreach (var item in Instance.MethodEvents.OrderBy(item => item.TimeStamp)) {
                //perf boost so we don't keep looking up the value
                if (lastModuleId != item.ModuleId) {
                    lastData = (from known in Instance.KnownInstances().Values
                                from assem in known.assemblies
                                from module in assem.modules
                                where module.id == item.ModuleId
                                select known).FirstOrDefault();
                    lastModuleId = item.ModuleId;
                }
                var method = Instance.DataMap()[lastData.instanceHash][item.MethodId];
                graphCalls.MoveNext();
                var graphMethod = graphCalls.Current;
                //verify the signature and the enter or exit is correct.
                Assert.AreEqual<string>(method.signature, graphMethod.Signature, "Signature does not match");
                Assert.AreEqual<bool>(item.Enter, graphMethod.Enter, "Enter/Exit does not match");

                //verify all of the points of all of the methods that were visited were actually accounted for

                foreach (var sourePoint in method.points) {
                    Assert.IsTrue(Instance.PointEvents.Any(pt => pt.PointId == sourePoint.id), "Point was not found:" + sourePoint.id);
                }
            }
        }

        private static string ToTraceString(IEnumerable<TimingMethod> tester) {
            var retVal = new StringBuilder();

            int lastModuleId = 0;
            InstrumentationData lastData = null;

            foreach (var item in tester.OrderBy(item => item.TimeStamp)) {

                if (lastModuleId != item.ModuleId) {
                    lastData = (from known in Instance.KnownInstances().Values
                                from assem in known.assemblies
                                from module in assem.modules
                                where module.id == item.ModuleId
                                select known).FirstOrDefault();
                    lastModuleId = item.ModuleId;
                }
                var method = Instance.DataMap()[lastData.instanceHash][item.MethodId];

                retVal.AppendLine(method.signature + ":" + item.Enter);
            }
            return retVal.ToString();
        }

        private static string ToTraceString(CallGraphTester tester) {
            var retVal = new StringBuilder();
            foreach (var item in tester.Events) {
                retVal.AppendLine(item.Signature + ":" + item.Enter);
            }
            return retVal.ToString();
        }

    }
}
