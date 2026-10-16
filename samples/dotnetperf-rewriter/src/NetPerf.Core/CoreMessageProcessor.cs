using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Core.Interfaces;
using System.Diagnostics;
using System.Collections;
using System.Threading;

namespace NetPerf.Core {

    /// <summary>
    /// Core Message Processor that is used for in memory capture of events.
    /// </summary>
    public class CoreMessageProcessor : IMessageProcessor {

        protected static object lockObject = new object();

        private List<TimingMethod> _methodEvents = new List<TimingMethod>();
        private List<TimingPoint> _pointEvents = new List<TimingPoint>();

        public List<TimingMethod> MethodEvents {
            get {
                return _methodEvents;
            }
        }

        public List<TimingPoint> PointEvents {
            get {
                return _pointEvents;
            }
        }

        public void ClearCalls() {
            lock (lockObject) {
                _methodEvents = new List<TimingMethod>();
                _pointEvents = new List<TimingPoint>();
            }
        }

        #region IMessageProcessor Members

        public virtual void OnNewProcess(int processId, long ticksPerSecond, long timestamp, string processName, string machineName) {
            //we don't need to know about the known list. The service is the only thing that deals with it.
            //when we deal with over the wire calls, this method will be useful.
        }

        public virtual void OnNewThread(int processId, long timestamp, int threadId, string threadName) {
            //we don't need to know about the known list. The service is the only thing that deals with it.
            //when we deal with over the wire calls, this method will be useful.
        }

        public virtual void OnNewAssembly(int processId, long timestamp, string assemblyIdentity) {
            //we don't need to know about the known list. The service is the only thing that deals with it.
            //when we deal with over the wire calls, this method will be useful.
        }

        public virtual void RegisterMetadata(string assemblyName, string ilMetadata) {
            //we don't need to know about the known list. The service is the only thing that deals with it.
            //when we deal with over the wire calls, this method will be useful.
        }

        public void OnMethodEntered(int processId, int moduleId, int threadId, long timestamp, int methodId) {
            lock (lockObject) {
                _methodEvents.Add(new TimingMethod() {
                    Enter = true,
                    MethodId = methodId,
                    ModuleId = moduleId,
                    ProcessId = processId,
                    ThreadId = threadId,
                    TimeStamp = timestamp
                });
            }
        }

        public void OnGenericMethodEntered(int processId, int moduleId, int threadId, long timestamp, int methodId, string[] classTypes, string[] methodTypes) {
            lock (lockObject) {
                _methodEvents.Add(new TimingMethod() {
                    Enter = true,
                    MethodId = methodId,
                    ModuleId = moduleId,
                    ProcessId = processId,
                    ThreadId = threadId,
                    TimeStamp = timestamp,
                    ClassTypes = classTypes != null ? GetGenericParameters(classTypes) : null,
                    MethodTypes = methodTypes != null ? GetGenericParameters(methodTypes) : null
                });
            }
        }

        public void OnMethodExited(int processId, int moduleId, int threadId, long timestamp, int methodId) {
            lock (lockObject) {
                _methodEvents.Add(new TimingMethod() {
                    Enter = false,
                    MethodId = methodId,
                    ModuleId = moduleId,
                    ProcessId = processId,
                    ThreadId = threadId,
                    TimeStamp = timestamp
                });
            }
        }

        public void OnGenericMethodExited(int processId, int moduleId, int threadId, long timestamp, int methodId, string[] classTypes, string[] methodTypes) {
            lock (lockObject) {
                _methodEvents.Add(new TimingMethod() {
                    Enter = false,
                    MethodId = methodId,
                    ModuleId = moduleId,
                    ProcessId = processId,
                    ThreadId = threadId,
                    TimeStamp = timestamp,
                    ClassTypes = classTypes != null ? GetGenericParameters(classTypes) : null,
                    MethodTypes = methodTypes != null ? GetGenericParameters(methodTypes) : null
                });
            }
        }

        public void OnPointHit(int processId, int moduleId, int threadId, long timestamp, int pointId) {
            lock (lockObject) {
                _pointEvents.Add(new TimingPoint() {
                    ModuleId = moduleId,
                    PointId = pointId,
                    ProcessId = processId,
                    ThreadId = threadId,
                    TimeStamp = timestamp
                });
            }
        }

        public virtual void Stop(int processId, long timestamp) {
            Console.WriteLine("Stop Received for Process:{0} Thread:{1} {2}", processId, Thread.CurrentThread.ManagedThreadId, timestamp);
            Debug.WriteLine(string.Format("Stop Received for Process:{0} Thread:{1} {2}", processId, Thread.CurrentThread.ManagedThreadId, timestamp));
        }

        #endregion

        private Dictionary<string, TimingGenericParameters> _genericParamters = new Dictionary<string, TimingGenericParameters>();
        private int _genericParamterId = 0;

        private TimingGenericParameters GetGenericParameters(string[] types) {
            if (types == null) {
                return null;
            }

            var type = string.Join(",", types);
            TimingGenericParameters retVal = null;

            lock ((_genericParamters as ICollection).SyncRoot) {
                _genericParamters.TryGetValue(type, out retVal);
            }
            if (retVal != null) {
                return retVal;
            }
            lock ((_genericParamters as ICollection).SyncRoot) {
                if (!_genericParamters.TryGetValue(type, out retVal)) {
                    //save the item.
                    retVal = new TimingGenericParameters() {
                        GenericParameterId = ++_genericParamterId,
                        Types = type
                    };
                    _genericParamters[type] = retVal;
                }
            }
            return retVal;
        }

    }
}
