using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using NetPerf.Core.Interfaces;

namespace NetPerf.Core {

    /// <summary>
    /// Visitor Class for walking the timing data.
    /// </summary>
    [DebuggerDisplay("Data:{Data.Count()}")]
    public class TimingVisitorBase {

        protected IEnumerable<TimingMethod> Data {
            get;
            private set;
        }

        protected int Level {
            get {
                return LevelMethod.Count;
            }
        }

        protected TimingMethod LastMethod {
            get;
            set;
        }

        /// <summary>
        /// Will contain one item per Level
        /// </summary>
        protected Stack<TimingMethodNode> LevelMethod {
            get;
            private set;
        }

        protected IInstrumentationDataMapAndInstances Map {
            get;
            private set;
        }

        protected IMessageProcessor Processor {
            get;
            private set;
        }

        /// <summary>
        /// Items added to the root that contain all of the timing data.
        /// </summary>
        protected List<TimingMethodNode> RootMethods {
            get;
            private set;
        }

        public TimingVisitorBase(CoreMessageProcessor processor, IInstrumentationDataMapAndInstances map) {
            if (processor == null) {
                throw new ArgumentNullException("processor");
            }
            if (map == null) {
                throw new ArgumentNullException("map");
            }
            Data = processor.MethodEvents;
            Processor = processor;
            Map = map;

            //Level = 1; //never use 0
            LevelMethod = new Stack<TimingMethodNode>();
            RootMethods = new List<TimingMethodNode>();
            Visit();
        }

        protected void EnterMethod(TimingMethod method) {

            var newItem = new TimingMethodNode(method);

            if (LevelMethod.Count > 0) {
                var parent = LevelMethod.Peek();
                parent.AddMethod(newItem);
            }
            else {
                RootMethods.Add(newItem);
            }

            LevelMethod.Push(newItem);
            LastMethod = method;
        }

        protected void ExitMethod(TimingMethod method) {
            var item = LevelMethod.Peek();
            if (item.ModuleId == method.ModuleId && item.MethodId == method.MethodId) {
                //Level = Level - 1; //before or after?
                var parentItem = LevelMethod.Pop();
                parentItem.Exit(method);
            }
            else {
                //TODO we are missing data and really should ignore this
                Console.Write("");
            }

            LastMethod = method;
        }

        public TimingDataInformation GetTimingData() {

            var retVal = new TimingDataInformation();

            var start = Data.Any() ? Data.Min(item => item.TimeStamp) : DateTime.Now.Ticks;
            var end = Data.Any() ? Data.Max(item => item.TimeStamp) : DateTime.Now.Ticks;

            //TODO determine real reason why Max is returning 0 for the Extension Method
            if (end == 0) {
                foreach (var item in Data) {
                    if (item.TimeStamp > end) {
                        end = item.TimeStamp;
                    }
                }
            }

            retVal.TotalExecutionTimeTaken = end - start;

            var collection = retVal.TimingElements;

            foreach (var method in RootMethods) {
                GetTimingForMethod(collection, method);
            }

            //the sum can only be on the roots or the times will be double counted
            retVal.TotalMethodTime = RootMethods.Sum(item => item.MethodTime);
            retVal.TotalChildTime = RootMethods.Sum(item => item.ChildTime);

            retVal.TotalSumMethodTime = retVal.TimingElements.Sum(item => item.MethodTime);
            retVal.TotalSumChildTime = retVal.TimingElements.Sum(item => item.ChildTime);

            return retVal;
        }

        private void GetTimingForMethod(List<TimingData> retVal, TimingMethodNode method) {

            var timing = retVal.FirstOrDefault(item => item.ProcessId == method.ProcessId
                                            && item.MethodId == method.MethodId
                                            && item.ModuleId == item.ModuleId);

            if (method.ExitMethod == null) {
                var md = Map.InstrumentDataForMethod(method.ModuleId, method.MethodId);
                if (md != null) {
                    var sb = new StringBuilder();
                    sb.Append("Method Does not have an exit:");
                    sb.Append(" Module Name:").Append(md.Module.name);
                    sb.Append(" Type Signature:").Append(md.Type.signature);

                    sb.Append(" Method Signature:").Append(md.Method.signature);
                    var parts = md.Method.signature.Split(new string[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2) {
                        sb.Append(" Method Name").Append(parts[1]);
                    }
                    Debug.WriteLine(sb.ToString());
                }
            }

            if (timing == null) {
                timing = new TimingData() {
                    MethodId = method.MethodId,
                    ModuleId = method.ModuleId,
                    ProcessId = method.ProcessId
                };
                retVal.Add(timing);
            }

            timing.ChildTime = timing.ChildTime + method.ChildTime;
            timing.MethodTime = timing.MethodTime + method.MethodTime;

            foreach (var child in method.Children) {
                GetTimingForMethod(retVal, child);
            }

        }

        private void Visit() {

            var sw = new Stopwatch();
            sw.Start();

            var processGroup = Data.GroupBy<TimingMethod, int>(item => item.ProcessId);

            foreach (var process in processGroup) {
                //TODO find a better way to calculate the data. for now we just process per thread so we have a simple visitor.
                var threadGroup = process.GroupBy<TimingMethod, int>(item => item.ThreadId);
                foreach (var thread in threadGroup) {
                    //TODO fire new thread event
                    //we need to sort by process since many processes can have the same thread id.
                    foreach (var timing in thread.OrderBy(item => item.TimeStamp)) {
                        if (timing.Enter) {
                            EnterMethod(timing);
                        }
                        else {
                            ExitMethod(timing);
                        }
                    }
                }
            }

            sw.Stop();

            Debug.WriteLine(string.Format("visit time for methods {0}: {1}ms", Data.Count(), sw.ElapsedMilliseconds));
        }
    }
}
