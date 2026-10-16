using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;

namespace NetPerf.Core.Interfaces {

	[ServiceContract]
	public interface IMessagingContract {

		[OperationContract]
		void SetupNewProcess(int processId, string processName, string machineName, long ticksPerSecond, long timestamp);

		[OperationContract] //TODO can threads switch appdomains?
		void SetupNewThread(int processId, long timestamp, int threadId, string threadName);

		[OperationContract]
		void SetupNewAssembly(int processId, long timestamp, string assemblyIdentity);

		//TODO make all of the Record* methods handle multiple records at a time

		[OperationContract]
		void RecordMethodEntrance(int processId, int threadId, long timestamp, int methodId);

		[OperationContract]
		void RecordGenericMethodEntrance(int processId, int threadId, long timestamp, int methodId, string[] classTypes, string[] methodTypes);

		[OperationContract]
		void RecordGenericMethodExit(int processId, int threadId, long timestamp, int methodId, string[] classTypes, string[] methodTypes);

		[OperationContract]
		void RecordMethodExit(int processId, int threadId, long timestamp, int methodId);

		[OperationContract]
		void RecordInstrumentationPoint(int processId, int threadid, long timestamp, int pointId);
	}
}
