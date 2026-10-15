using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Core;
using NetPerf.Core.Interfaces;

namespace NetPerf {

	internal class NamedPipeMessageProcessor : CoreMessageProcessor, IInstrumentationDataMapAndInstances {

		private readonly Dictionary<string, InstrumentationData> _knownInstances = new Dictionary<string, InstrumentationData>();
		private readonly Dictionary<string, Dictionary<int, InstrumentationMethodData>> _dataMap = new Dictionary<string, Dictionary<int, InstrumentationMethodData>>();
		private readonly Dictionary<string, Dictionary<int, InstrumentationMethodDataMapping>> _dataMapFull = new Dictionary<string, Dictionary<int, InstrumentationMethodDataMapping>>();
		private readonly List<TimingThread> _threadList = new List<TimingThread>();
		private readonly List<TimingProcess> _processList = new List<TimingProcess>();

		public Dictionary<string, Dictionary<int, InstrumentationMethodData>> DataMap() {
			return _dataMap;
		}

		public InstrumentationMethodDataMapping InstrumentDataForMethod(int moduleId, int methodId) {
			//TODO this code is duplicated, consolidate
			var lastData = (from known in KnownInstances().Values
							from assem in known.assemblies
							from module in assem.modules
							where module.id == moduleId
							select known).FirstOrDefault();
			if (lastData != null) {
				return _dataMapFull[lastData.instanceHash][methodId];
			}
			return null;
		}

		public Dictionary<string, InstrumentationData> KnownInstances() {
			return _knownInstances;
		}

		public List<TimingProcess> ProcessList() {
			return _processList;
		}

		public List<TimingThread> ThreadList() {
			return _threadList;
		}

		public override void OnNewProcess(int processId, long ticksPerSecond, long timestamp, string processName, string machineName) {
			lock (lockObject) {
				_processList.Add(new TimingProcess() {
					MachineName = machineName,
					ProcessId = processId,
					ProcessName = processName,
					TicksPerSecond = ticksPerSecond,
					TimeStamp = timestamp
				});

				base.OnNewProcess(processId, ticksPerSecond, timestamp, processName, machineName);
			}
		}

		public override void OnNewThread(int processId, long timestamp, int threadId, string threadName) {
			lock (lockObject) {
				_threadList.Add(new TimingThread() {
					ProcessId = processId,
					ThreadId = threadId,
					ThreadName = threadName,
					TimeStamp = timestamp
				});

				base.OnNewThread(processId, timestamp, threadId, threadName);
			}
		}

		//we need to collection the metadata
		public override void RegisterMetadata(string assemblyName, string ilMetadata) {

			if (ilMetadata != null) {
				lock (lockObject) {
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
						_dataMapFull[data.instanceHash] = (from a in data.assemblies
														   from m in a.modules
														   from t in m.types
														   from meth in t.methods
														   select new InstrumentationMethodDataMapping() {
															   Method = meth,
															   Module = m,
															   Type = t
														   }).ToDictionary(item => item.Method.id);
					}
				} //end lock
				base.RegisterMetadata(assemblyName, ilMetadata);
			}
		}
	}
}
