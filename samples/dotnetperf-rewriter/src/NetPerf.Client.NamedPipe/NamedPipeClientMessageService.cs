using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.ServiceModel;
using System.Reflection;
using NetPerf.Core.Interfaces;

namespace NetPerf.Client {

	public class NamedPipeClientMessageService : IClientMessageService {

		private static readonly int ProcessID;
		private static readonly IMessagingContract _pipe;
		private static readonly List<int> _knownThreadIDs;
		private static readonly List<string> _knownAssemblies;
		private static List<Action<IMessagingContract>> _queuedMessages;

		private static bool IsConnected {
			get;
			set;
		}

		private static bool IsInitialized {
			get;
			set;
		}

		static NamedPipeClientMessageService() {
			var curProc = Process.GetCurrentProcess();
			ProcessID = curProc.Id;

			_knownThreadIDs = new List<int>();
			_knownAssemblies = new List<string>();
			_queuedMessages = new List<Action<IMessagingContract>>();

			var tempPipe = InitializeClientPipe();
			if (ConnectToNetPerfServer(tempPipe, curProc)) {
				_pipe = tempPipe;
				IsConnected = true;

				SendQueuedMessages();
			}

			IsInitialized = true;
		}

		public void RegisterMetadata(string assemblyName, string ilMetadata) {
			//TODO we need this data to be passed over the wire in order to work.
			//throw new NotImplementedException();
		}

		public void EnterMethod(int moduleId, int methodId) {
			SetupCurrentAssembly(Assembly.GetCallingAssembly());
			var threadId = SetupCurrentThread();

			if (IsConnected || !IsInitialized) {
				if (IsInitialized) {
					// procid, threadid, timestamb, methodid
					// assumes that the server (the recieving end) knows enough to handle the request asyncrounously
					_pipe.RecordMethodEntrance(ProcessID, threadId, Stopwatch.GetTimestamp(), methodId);
				}
				else {
					// store the message to send later
					var procId = ProcessID;
					var time = Stopwatch.GetTimestamp();

					var message = new Action<IMessagingContract>(pipe => pipe.RecordMethodEntrance(procId, threadId, time, methodId));
					_queuedMessages.Add(message);
				}
			}
		}

		public void EnterGenericMethod(int moduleId, int methodId, string[] classTypes, string[] methodTypes) {
			SetupCurrentAssembly(Assembly.GetCallingAssembly());
			var threadId = SetupCurrentThread();

			if (IsConnected || !IsInitialized) {
				if (IsInitialized) {
					// procid, threadid, timestamb, methodid
					// assumes that the server (the recieving end) knows enough to handle the request asyncrounously
					_pipe.RecordGenericMethodEntrance(ProcessID, threadId, Stopwatch.GetTimestamp(), methodId, classTypes, methodTypes);
				}
				else {
					// store the message to send later
					var procId = ProcessID;
					var time = Stopwatch.GetTimestamp();

					var message = new Action<IMessagingContract>(pipe => pipe.RecordGenericMethodEntrance(procId, threadId, time, methodId, classTypes, methodTypes));
					_queuedMessages.Add(message);
				}
			}
		}

		public void ExitMethod(int moduleId, int methodId) {
			SetupCurrentAssembly(Assembly.GetCallingAssembly());
			var threadId = SetupCurrentThread();

			if (IsConnected || !IsInitialized) {
				if (IsInitialized) {
					// procid, threadid, timestamb, methodid
					// assumes that the server (the recieving end) knows enough to handle the request asyncrounously
					_pipe.RecordMethodExit(ProcessID, threadId, Stopwatch.GetTimestamp(), methodId);
				}
				else {
					// store the message to send later
					var procId = ProcessID;
					var time = Stopwatch.GetTimestamp();

					var message = new Action<IMessagingContract>(pipe => pipe.RecordMethodExit(procId, threadId, time, methodId));
					_queuedMessages.Add(message);
				}
			}
		}

		public void ExitGenericMethod(int moduleId, int methodId, string[] classTypes, string[] methodTypes) {
			SetupCurrentAssembly(Assembly.GetCallingAssembly());
			var threadId = SetupCurrentThread();

			if (IsConnected || !IsInitialized) {
				if (IsInitialized) {
					// procid, threadid, timestamb, methodid
					// assumes that the server (the recieving end) knows enough to handle the request asyncrounously
					_pipe.RecordGenericMethodExit(ProcessID, threadId, Stopwatch.GetTimestamp(), methodId, classTypes, methodTypes);
				}
				else {
					// store the message to send later
					var procId = ProcessID;
					var time = Stopwatch.GetTimestamp();

					var message = new Action<IMessagingContract>(pipe => pipe.RecordGenericMethodExit(procId, threadId, time, methodId, classTypes, methodTypes));
					_queuedMessages.Add(message);
				}
			}
		}

		public void InstrumentationPointHit(int moduleId, int pointId) {
			SetupCurrentAssembly(Assembly.GetCallingAssembly());
			var threadId = SetupCurrentThread();

			if (IsConnected || !IsInitialized) {
				if (IsInitialized) {
					_pipe.RecordInstrumentationPoint(ProcessID, threadId, Stopwatch.GetTimestamp(), pointId);
				}
				else {
					// store the message to send later
					var procId = ProcessID;
					var time = Stopwatch.GetTimestamp();

					var message = new Action<IMessagingContract>(pipe => pipe.RecordInstrumentationPoint(procId, threadId, time, pointId));
					_queuedMessages.Add(message);
				}
			}
		}

		public void Stop() {
			//TODO if this is called, the app domain is shutting down, you need to cleanup.
			//_pipe.Stop();
		}

		//private methods

		private static bool ConnectToNetPerfServer(IMessagingContract pipe, Process curProc) {
			try {
				var procname = curProc.ProcessName;
				var compname = curProc.MachineName;
				pipe.SetupNewProcess(ProcessID, procname, compname, Stopwatch.Frequency, Stopwatch.GetTimestamp());

				return true;
			}
			catch (EndpointNotFoundException) {
				return false;
			}
		}

		private static IMessagingContract InitializeClientPipe() {
			var binding = new NetNamedPipeBinding();
			var endpoint = new EndpointAddress(Constants.NamedPipeServerUri);

			// DO NOT dispose the 'channel' object, or you won't be able to use the NamedPipe
			var channel = new ChannelFactory<IMessagingContract>(binding, endpoint);
			return channel.CreateChannel();
		}

		private static void SendQueuedMessages() {
			if (_queuedMessages != null && _queuedMessages.Count > 0) {
				//TODO make this part thread safe
				var items = _queuedMessages;
				_queuedMessages = new List<Action<IMessagingContract>>();

				int index;
				for (index = 0; index < items.Count; index++) {
					var message = items[index];
					try {
						message(_pipe);
					}
					catch (EndpointNotFoundException) {
						index -= 1; // the message that threw the exception was not sent
						IsConnected = false;
					}
				}

				if (index < items.Count) {
					// add the unsent messages back to the queue
					_queuedMessages.AddRange(items.Skip(items.Count - index));
				}
			}
		}

		private static int SetupCurrentThread() {
			if (IsConnected || !IsInitialized) {
				var threadID = Thread.CurrentThread.ManagedThreadId;
				if (_knownThreadIDs.Any(id => id == threadID))
					return threadID;

				try {
					var threadName = Thread.CurrentThread.Name;

					if (IsInitialized) {
						// notify the server syncronously
						_pipe.SetupNewThread(ProcessID, Stopwatch.GetTimestamp(), threadID, threadName);
					}
					else {
						// store the message to send it later
						var procId = ProcessID;
						var time = Stopwatch.GetTimestamp();

						var message = new Action<IMessagingContract>((pipe) => pipe.SetupNewThread(procId, time, threadID, threadName));
						_queuedMessages.Add(message);
					}

					_knownThreadIDs.Add(threadID);
					return threadID;
				}
				catch (EndpointNotFoundException) {
					IsConnected = false;
				}
			}

			return -1;
		}

		private static void SetupCurrentAssembly(Assembly current) {
			if (IsConnected || !IsInitialized) {
				var assemName = current.FullName;
				if (_knownAssemblies.Any(an => an == assemName))
					return;

				try {
					if (IsInitialized) {
						// send the message syncronously across the pipe
						_pipe.SetupNewAssembly(ProcessID, Stopwatch.GetTimestamp(), assemName);
					}
					else {
						// store the message to send it later
						var procId = ProcessID;
						var time = Stopwatch.GetTimestamp();

						var message = new Action<IMessagingContract>(pipe => pipe.SetupNewAssembly(procId, time, assemName));
						_queuedMessages.Add(message);
					}

					_knownAssemblies.Add(assemName);
				}
				catch (EndpointNotFoundException) {
					IsConnected = false;
				}
			}
		}

	}
}
