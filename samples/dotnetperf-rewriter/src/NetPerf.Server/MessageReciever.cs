using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Client;
using System.Threading;
using System.ServiceModel;
using NetPerf.Core.Interfaces;

namespace NetPerf.Server {

	[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
	internal class MessageReciever : IMessagingContract {

		private readonly IMessageProcessor _processor;

		public MessageReciever(IMessageProcessor callback) {
			if (callback == null)
				throw new ArgumentNullException("callback");

			_processor = callback;
		}

		public void SetupNewProcess(int processId, string processName, string machineName, long ticksPerSecond, long timestamp) {
			ThreadPool.QueueUserWorkItem(ThreadPoolExecute,
				new Action(() => _processor.OnNewProcess(processId, ticksPerSecond, timestamp, processName, machineName)));
		}

		public void SetupNewThread(int processId, long timestamp, int threadID, string threadName) {
			ThreadPool.QueueUserWorkItem(ThreadPoolExecute,
				new Action(() => _processor.OnNewThread(processId, timestamp, threadID, threadName)));
		}

		public void SetupNewAssembly(int processId, long timestamp, string assemblyIdentity) {
			ThreadPool.QueueUserWorkItem(ThreadPoolExecute,
				new Action(() => _processor.OnNewAssembly(processId, timestamp, assemblyIdentity)));
		}

		public void RecordMethodEntrance(int processId, int threadId, long timestamp, int methodId) {
			ThreadPool.QueueUserWorkItem(ThreadPoolExecute,
				new Action(() => _processor.OnMethodEntered(processId, 0 /*TODO redo with moduleid*/, threadId, timestamp, methodId)));
		}

		public void RecordGenericMethodEntrance(int processId, int threadId, long timestamp, int methodId, string[] classTypes, string[] methodTypes) {
			ThreadPool.QueueUserWorkItem(ThreadPoolExecute,
				new Action(() => _processor.OnGenericMethodEntered(processId, 0 /*TODO redo with moduleid*/, threadId, timestamp, methodId, classTypes, methodTypes)));
		}

		public void RecordMethodExit(int processId, int threadId, long timestamp, int methodId) {
			ThreadPool.QueueUserWorkItem(ThreadPoolExecute,
				new Action(() => _processor.OnMethodExited(processId, 0 /*TODO redo with moduleid*/, threadId, timestamp, methodId)));
		}

		public void RecordGenericMethodExit(int processId, int threadId, long timestamp, int methodId, string[] classTypes, string[] methodTypes) {
			ThreadPool.QueueUserWorkItem(ThreadPoolExecute,
				new Action(() => _processor.OnGenericMethodExited(processId, 0 /*TODO redo with moduleid*/, threadId, timestamp, methodId, classTypes, methodTypes)));
		}

		public void RecordInstrumentationPoint(int processId, int threadid, long timestamp, int pointId) {
			ThreadPool.QueueUserWorkItem(ThreadPoolExecute,
				new Action(() => _processor.OnPointHit(processId, 0 /*TODO redo with moduleid*/, threadid, timestamp, pointId)));
		}

		private void ThreadPoolExecute(object actionFunc) {
			var action = (Action)actionFunc;
			action();
		}

	}
}
