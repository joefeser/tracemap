using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetPerf.Core.Interfaces {

	/// <summary>
	/// Interface used for all communications over the wire on the sender side.
	/// </summary>
	public interface IMessageProcessor {

		//Message 0x01
		void OnNewProcess(int processId, long ticksPerSecond, long timestamp, string processName, string machineName);
		//Message 0x02
		void OnNewThread(int processId, long timestamp, int threadId, string threadName);
		//Message 0x03
		void OnNewAssembly(int processId, long timestamp, string assemblyIdentity);

		//The new clients will know what to do with this, for now it is just for the new SqLite Client
		//Message 0x04
		void RegisterMetadata(string assemblyName, string ilMetadata);

		//we want to leave room for some registration messages

		//Message 0x40
		void OnMethodEntered(int processId, int moduleId, int threadId, long timestamp, int methodId);
		//Message 0x41
		void OnGenericMethodEntered(int processId, int moduleId, int threadId, long timestamp, int methodId, string[] classTypes, string[] methodTypes);
		//Message 0x42
		void OnMethodExited(int processId, int moduleId, int threadId, long timestamp, int methodId);
		//Message 0x43
		void OnGenericMethodExited(int processId, int moduleId, int threadId, long timestamp, int methodId, string[] classTypes, string[] methodTypes);
		//Message 0x44
		void OnPointHit(int processId, int moduleId, int threadId, long timestamp, int pointId);

		//Message 0xFF
		/// <summary>
		/// perform cleanup. we assume we are done and can close the pipes
		/// </summary>
		void Stop(int processId, long timestamp);
	}
}
