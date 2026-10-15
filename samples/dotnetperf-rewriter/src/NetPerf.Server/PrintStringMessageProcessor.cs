using System;
using NetPerf.Common;
using NetPerf.Common.Instrumentation;
using NetPerf.Core.Interfaces;

namespace NetPerf.Server {

	internal class PrintStringMessageProcessor : IMessageProcessor {

		private readonly Action<string> _print;
		private readonly InstrumentationPlanMapper _map; //TODO get rid of this as soon as the data processor artichecture is done

		public PrintStringMessageProcessor(Action<string> printCallback, IInstrumentationData data) {
			if (printCallback == null)
				throw new ArgumentNullException("printCallback");

			if (data == null)
				throw new ArgumentNullException("data");

			_print = printCallback;
			_map = new InstrumentationPlanMapper(data);
		}

		public void OnNewProcess(int processId, long ticksPerSecond, long timestamp, string processName, string machineName) {
			_print("[" + timestamp + "] new process started: " + processName);
		}

		public void OnNewThread(int processId, long timestamp, int threadId, string threadName) {
			_print("[" + timestamp + "] new thread started: " + threadId.ToUserString());
		}

		public void OnNewAssembly(int processId, long timestamp, string assemblyIdentity) {
			_print("[" + timestamp + "] new assembly: " + assemblyIdentity);
		}

		public void RegisterMetadata(string assemblyName, string ilMetadata) {
			_print("[" + assemblyName + "] new RegisterMetadata: " + ilMetadata);
		}


		public void OnMethodEntered(int processId, int moduleId, int threadId, long timestamp, int methodId) {
			_print("[" + timestamp + "] method entered: " + _map.GetMethodName(methodId));
		}

		public void OnGenericMethodEntered(int processId, int moduleId, int threadId, long timestamp, int methodId, string[] classTypes, string[] methodTypes) {
			_print("[" + timestamp + "] method entered: " +
				_map.GetMethodName(methodId) +
				"  w/ closed types " +
				string.Join(", ", classTypes ?? new string[0]) +
				", " + string.Join(", ", methodTypes ?? new string[0]));
		}

		public void OnMethodExited(int processId, int moduleId, int threadId, long timestamp, int methodId) {
			_print("[" + timestamp + "] method exited: " + _map.GetMethodName(methodId));
		}

		public void OnGenericMethodExited(int processId, int moduleId, int threadId, long timestamp, int methodId, string[] classTypes, string[] methodTypes) {
			_print("[" + timestamp + "] method exited: " +
				_map.GetMethodName(methodId) +
				"  w/ closed types " +
				string.Join(", ", classTypes ?? new string[0]) +
				", " + string.Join(", ", methodTypes ?? new string[0]));
		}

		public void OnPointHit(int processId, int moduleId, int threadId, long timestamp, int pointId) {
			_print("[" + timestamp + "] point hit: " + pointId);
		}

		public void Stop(int processId, long timestamp) {
			_print("[" + timestamp + "] stop: " + processId);
		}
	}
}
