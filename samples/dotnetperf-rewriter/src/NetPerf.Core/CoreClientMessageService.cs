using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Core.Interfaces;
using System.Diagnostics;
using System.Xml.Serialization;
using System.IO;
using System.Threading;

namespace NetPerf.Core {

    /// <summary>
    /// Main Client Message service that should work for all of the basic services.
    /// </summary>
    public class CoreClientMessageService : IClientMessageService {

        private static readonly Process CurrentProcess;
        private static readonly int ProcessID;
        private readonly IMessageProcessor _processor = null;
        private readonly Dictionary<string, InstrumentationData> _knownInstances = new Dictionary<string, InstrumentationData>();
        private readonly Dictionary<string, Dictionary<int, InstrumentationMethodData>> _dataMap = new Dictionary<string, Dictionary<int, InstrumentationMethodData>>();

        private readonly List<int> _knownThreadIDs = new List<int>();
        private readonly List<int> _knownModuleIDs = new List<int>();

        //TODO we need to load the existing item from the db if it exists and create a map. Refactor this class for the mappings.

        protected IMessageProcessor Processor {
            get {
                return _processor;
            }
        }

        static CoreClientMessageService() {
            CurrentProcess = Process.GetCurrentProcess();
            ProcessID = CurrentProcess.Id;
        }

        public CoreClientMessageService(IMessageProcessor processor) {
            if (processor == null) {
                throw new ArgumentNullException("processor");
            }
            _processor = processor;
            _processor.OnNewProcess(ProcessID, Stopwatch.Frequency, Stopwatch.GetTimestamp(), CurrentProcess.ProcessName, CurrentProcess.MachineName);
        }

        public Dictionary<string, Dictionary<int, InstrumentationMethodData>> DataMap() {
            return _dataMap;
        }

        public InstrumentationMethodData InstrumentDataForMethod(int moduleId, int methodId) {
            //TODO this code is duplicated, consolidate
            var lastData = (from known in KnownInstances().Values
                            from assem in known.assemblies
                            from module in assem.modules
                            where module.id == moduleId
                            select known).FirstOrDefault();
            if (lastData != null) {
                return DataMap()[lastData.instanceHash][methodId];
            }
            return null;
        }

        public Dictionary<string, InstrumentationData> KnownInstances() {
            return _knownInstances;
        }

        public void RegisterMetadata(string assemblyName, string ilMetadata) {

            if (ilMetadata != null) {
                //TODO find a way to ask the processor for the metadata instead of deserializing twice and registering in both assemblies.
                var data = InstrumentationData.Deserialize(ilMetadata);
                //not sure if this could ever happen but we are going to check anyway   
                if (!_knownInstances.ContainsKey(data.instanceHash)) {
                    _knownInstances[data.instanceHash] = data;
                    _dataMap[data.instanceHash] = (from a in data.assemblies
                                                   from m in a.modules
                                                   from t in m.types
                                                   from meth in t.methods
                                                   select meth).ToDictionary(item => item.id);
                }
                _processor.RegisterMetadata(assemblyName, ilMetadata);
            }
        }

        public void EnterMethod(int moduleId, int methodId) {
            SetupCurrentAssembly(moduleId);
            var threadId = SetupCurrentThread();
            _processor.OnMethodEntered(ProcessID, moduleId, threadId, Stopwatch.GetTimestamp(), methodId);
        }

        public void EnterGenericMethod(int moduleId, int methodId, string[] classTypes, string[] methodTypes) {
            SetupCurrentAssembly(moduleId);
            var threadId = SetupCurrentThread();
            _processor.OnGenericMethodEntered(ProcessID, moduleId, threadId, Stopwatch.GetTimestamp(), methodId, classTypes, methodTypes);
        }

        public void ExitMethod(int moduleId, int methodId) {
            SetupCurrentAssembly(moduleId);
            var threadId = SetupCurrentThread();
            _processor.OnMethodExited(ProcessID, moduleId, threadId, Stopwatch.GetTimestamp(), methodId);
        }

        public void ExitGenericMethod(int moduleId, int methodId, string[] classTypes, string[] methodTypes) {
            SetupCurrentAssembly(moduleId);
            var threadId = SetupCurrentThread();
            _processor.OnGenericMethodExited(ProcessID, moduleId, threadId, Stopwatch.GetTimestamp(), methodId, classTypes, methodTypes);
        }

        public void InstrumentationPointHit(int moduleId, int pointId) {
            SetupCurrentAssembly(moduleId);
            var threadId = SetupCurrentThread();
            _processor.OnPointHit(ProcessID, moduleId, threadId, Stopwatch.GetTimestamp(), pointId);
        }

        public void Stop() {
            if (_processor != null) {
                _processor.Stop(ProcessID, Stopwatch.GetTimestamp());
            }
        }

        private int SetupCurrentThread() {

            var threadID = Thread.CurrentThread.ManagedThreadId;
            if (_knownThreadIDs.Any(id => id == threadID))
                return threadID;

            var threadName = Thread.CurrentThread.Name;

            //TODO allow duplicate thread id's if the name changes.
            _processor.OnNewThread(ProcessID, Stopwatch.GetTimestamp(), threadID, threadName);

            _knownThreadIDs.Add(threadID);
            return threadID;
        }

        private void SetupCurrentAssembly(int moduleId) {

            if (_knownModuleIDs.Any(an => an == moduleId))
                return;

            string assemblyName = null;

            var assembly = (from known in _knownInstances.Values
                            from assem in known.assemblies
                            from module in assem.modules
                            where module.id == moduleId
                            select assem).FirstOrDefault();

            if (assembly != null) {
                assemblyName = assembly.identity;
            }
            else {
                assemblyName = "~~Unknown~~";
            }

            //TODO determine why we are passing the assembly name when the service already knows what it is called.
            _processor.OnNewAssembly(ProcessID, Stopwatch.GetTimestamp(), assemblyName);

            _knownModuleIDs.Add(moduleId);
        }
    }
}
