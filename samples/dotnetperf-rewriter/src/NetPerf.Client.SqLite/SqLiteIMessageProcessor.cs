using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SQLite;
using System.Threading;
using System.Collections;
using NetPerf.Core.Interfaces;
using NetPerf.Core;

namespace NetPerf.Client {

	public class SqLiteIMessageProcessor : IMessageProcessor {

		private string _filePath;
		//private FilePath _fileLoc;
		private object _fileSync = new object();
		private object _dbSync = new object();
		private List<object> _items = new List<object>();
		private long _pointCallCounter = 0;

		private SQLiteConnection _db;
		private Thread _timingSaver = null;
		private bool _threadComplete = false;

		private int RecordsToSavePerTransaction = 50000;

		private void Log(string message) {
			Console.WriteLine(message);
		}

		public SqLiteIMessageProcessor() {
			_filePath = Environment.CurrentDirectory + @"\" + Guid.NewGuid() + ".db3";
		}

		public void RegisterMetadata(string assemblyName, string ilMetadata) {
			if (ilMetadata != null) {
				var data = InstrumentationData.Deserialize(ilMetadata);
				EnsureDB(data);
			}
		}

		/// <summary>
		/// every new item must call this
		/// </summary>
		/// <param name="data"></param>
		public void AddMetadata(InstrumentationData data) {
			EnsureDB(data);
		}

		public void OnNewProcess(int processId, long ticksPerSecond, long timestamp, string processName, string machineName) {
			var item = new TimingProcess() {
				MachineName = machineName,
				ProcessId = processId,
				ProcessName = processName,
				TicksPerSecond = ticksPerSecond,
				TimeStamp = timestamp
			};
			lock (_fileSync) {
				_items.Add(item);
			}
		}

		public void OnNewThread(int processId, long timestamp, int threadId, string threadName) {
			var item = new TimingThread() {
				ProcessId = processId,
				ThreadId = threadId,
				ThreadName = threadName,
				TimeStamp = timestamp
			};
			lock (_fileSync) {
				_items.Add(item);
			}
		}

		public void OnNewAssembly(int processId, long timestamp, string assemblyIdentity) {
			var item = new TimingAssembly() {
				AssemblyIdentity = assemblyIdentity,
				ProcessId = processId,
				TimeStamp = timestamp
			};
			lock (_fileSync) {
				_items.Add(item);
			}
		}

		public void OnMethodEntered(int processId, int moduleId, int threadId, long timestamp, int methodId) {
			var item = new TimingMethod() {
				Enter = true,
				MethodId = methodId,
				ModuleId = moduleId,
				ProcessId = processId,
				ThreadId = threadId,
				TimeStamp = timestamp
			};
			lock (_fileSync) {
				_items.Add(item);
			}
		}

		public void OnGenericMethodEntered(int processId, int moduleId, int threadId, long timestamp, int methodId, string[] classTypes, string[] methodTypes) {
			var item = new TimingMethod() {
				Enter = true,
				MethodId = methodId,
				ModuleId = moduleId,
				ProcessId = processId,
				ThreadId = threadId,
				TimeStamp = timestamp,
				ClassTypes = classTypes != null ? GetGenericParameters(classTypes) : null,
				MethodTypes = methodTypes != null ? GetGenericParameters(methodTypes) : null
			};
			lock (_fileSync) {
				_items.Add(item);
			}
		}

		public void OnMethodExited(int processId, int moduleId, int threadId, long timestamp, int methodId) {
			var item = new TimingMethod() {
				Enter = false,
				MethodId = methodId,
				ModuleId = moduleId,
				ProcessId = processId,
				ThreadId = threadId,
				TimeStamp = timestamp
			};
			lock (_fileSync) {
				_items.Add(item);
			}
		}

		public void OnGenericMethodExited(int processId, int moduleId, int threadId, long timestamp, int methodId, string[] classTypes, string[] methodTypes) {
			var item = new TimingMethod() {
				Enter = false,
				MethodId = methodId,
				ModuleId = moduleId,
				ProcessId = processId,
				ThreadId = threadId,
				TimeStamp = timestamp,
				ClassTypes = classTypes != null ? GetGenericParameters(classTypes) : null,
				MethodTypes = methodTypes != null ? GetGenericParameters(methodTypes) : null
			};
			lock (_fileSync) {
				_items.Add(item);
			}
		}

		public void OnPointHit(int processId, int moduleId, int threadId, long timestamp, int pointId) {
			_pointCallCounter++;
			var item = new TimingPoint() {
				ModuleId = moduleId,
				PointId = pointId,
				ProcessId = processId,
				ThreadId = threadId,
				TimeStamp = timestamp
			};
			lock (_fileSync) {
				_items.Add(item);
			}
		}

		/// <summary>
		/// Blocking call that will clear out the remaining db inserts.
		/// </summary>
		public void Stop(int processId, long timestamp) {
			_threadComplete = true;
			ShutDownService();
		}

		private void EnsureDB(InstrumentationData data) {
			if (_db == null) {
				//TODO Add support for multiple processes, they will not be able to write to the same db
				_db = new SQLiteConnection(_filePath);
				_db.Execute("PRAGMA synchronous = OFF", new object[] { });
				//_db.Execute("PRAGMA journal_mode = MEMORY", new object[] { });

				_db.CreateTable<TimingGenericParameters>();
				_db.CreateTable<TimingAssembly>();
				_db.CreateTable<TimingMethod>(new string[] { "ClassTypes", "MethodTypes" });
				_db.CreateTable<TimingPoint>();
				_db.CreateTable<TimingProcess>();
				_db.CreateTable<TimingThread>();

				//we want to store the instrumentation meta data
				_db.CreateTable<InstrumentationData>(new TableNameAttribute("InstrumentationData"), default(IEnumerable<string>));
				_db.CreateTable<InstrumentationAssemblyData>(new TableNameAttribute("InstrumentationAssemblyData"), default(IEnumerable<string>));
				_db.CreateTable<InstrumentationModuleData>(new TableNameAttribute("InstrumentationModuleData"), default(IEnumerable<string>));
				_db.CreateTable<InstrumentationSourceFileData>(new TableNameAttribute("InstrumentationSourceFileData"), default(IEnumerable<string>));
				_db.CreateTable<InstrumentationTypeData>(new TableNameAttribute("InstrumentationTypeData"), default(IEnumerable<string>));
				_db.CreateTable<InstrumentationMethodData>(new TableNameAttribute("InstrumentationMethodData"), default(IEnumerable<string>));
				_db.CreateTable<InstrumentationPointData>(new TableNameAttribute("InstrumentationPointData"), default(IEnumerable<string>));

				InsertAssemblyMetadata(_db, data);

				_timingSaver = new Thread(InsertThread);
				_timingSaver.Name = "Timing Saver";
				_timingSaver.IsBackground = true;
				_timingSaver.Start();
			}
			else {
				InsertAssemblyMetadata(_db, data);
			}
		}

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
					lock (_dbSync) {
						_db.Insert(retVal);
					}
					_genericParamters[type] = retVal;
				}
			}
			return retVal;
		}

		private void InsertThread() {
			while (true) {
				//the first time it will never find anything.
				InsertTimingInformation(false);
				if (_threadComplete) {
					break;
				}
				//we need to come up with a better way to manage this. it works for now.
				Thread.Sleep(2000);
			}
		}

		private static void InsertAssemblyMetadata(SQLiteConnection db, InstrumentationData data) {

			//if data is not null, dump the data in.
			if (data != null) {

				var cmd = db.CreateCommand("Select * from " + typeof(InstrumentationData).Name + " Where InstanceHash = ?", data.instanceHash);
				var rootData = cmd.ExecuteQuery<InstrumentationData>().FirstOrDefault();

				//The hash is a 256 bit number. we are not going to determine if it is the same.
				if (rootData != null) {
					return;
				}

				db.BeginTransaction();
				//we want to know if we have this data in the system yet and this is how we will track it.
				db.Insert(data, typeof(InstrumentationData));
				foreach (var assem in data.assemblies) {
					db.Insert(assem, typeof(InstrumentationAssemblyData));

					foreach (var mod in assem.modules) {

						//next we need to see if there are any collisions for the module id for the data passed in and if there are, we need to change the mapping.

						cmd = db.CreateCommand("Select * from " + typeof(InstrumentationModuleData).Name + " Where Id = ?", mod.id);
						var moduleData = cmd.ExecuteQuery<InstrumentationModuleData>().FirstOrDefault();

						//TODO how do we change the mapping from here, we are already too low in the chain.
						if (moduleData != null) {
							throw new NotImplementedException("We have no way to resolve duplicate modules at this time.");
						}

						db.Insert(mod, typeof(InstrumentationModuleData));

						foreach (var source in mod.sourceFiles) {
							db.Insert(source, typeof(InstrumentationSourceFileData));
						}

						if (mod.types != null) {
							//TODO events and custom attributes?
							foreach (var type in mod.types) {
								db.Insert(type, typeof(InstrumentationTypeData));
								if (type.methods != null) {
									foreach (var method in type.methods) {
										db.Insert(method, typeof(InstrumentationMethodData));
										if (method.points != null) {
											foreach (var point in method.points) {
												db.Insert(point, typeof(InstrumentationPointData));
											}
										}
									}
								}
							}
						}
					} //mods
				} //assem
				db.Commit();
			} //data != null
		}

		private bool insertingLastRecords = false;

		private void InsertTimingInformation(bool stop) {
			Log("Start InsertTimingInformation:" + stop);
			//if we get a stop order, we are being killed by one way or another, so we can only save what we have.
			lock (_fileSync) {
				if (stop) {
					insertingLastRecords = true;
					Log("insertingLastRecords");
				}
				//we are already in, so just exit.
				if (!stop && insertingLastRecords) {
					Log("!stop && insertingLastRecords. exiting save thread.");
					return; //already done.
				}
			}

			if (_db != null) {
				//write it all out.
				List<object> newCopy = null;
				lock (_fileSync) {
					if (_items.Count == 0) {
						Log("(_items.Count == 0)");
						return;
					}
					newCopy = _items;
					_items = new List<object>();
				}

				//current count of records inserted based on the current copy of items.
				var lastCounter = 0;

				lock (_dbSync) {
					while (lastCounter < newCopy.Count) {
						_db.BeginTransaction();
						var inserted = _db.InsertAll(newCopy.Skip(lastCounter).Take(RecordsToSavePerTransaction));
						_db.Commit();
						lastCounter += RecordsToSavePerTransaction;
						Log("inserted:" + inserted + "=>" + stop);
					}
				}
			}

			//kill the timer thread if we are done.
			lock (_fileSync) {
				if (stop) {
					Log("stop");
					if (_timingSaver != null) {
						if (_timingSaver.IsAlive) {
							Log("IsAlive");
							_timingSaver.Abort();
						}
						_timingSaver = null;
					}
					if (_db != null) {
						_db.Close();
						_db = null;
					}
				}
			}
			Log("End InsertTimingInformation:" + stop);
		}

		private void ShutDownService() {
			InsertTimingInformation(true);
		}

		//public SqLiteInstrumentationMessageSerializer(FilePath storage, IInstrumentationData data) {
		//    Contract.Requires<ArgumentNullException>(storage != null, "storage");
		//    Contract.Requires<ArgumentOutOfRangeException>(storage.IsAbsolute, "path must be absolute");
		//    Contract.Requires<ArgumentNullException>(data != null, "data");
		//    _fileLoc = storage;
		//    _data = data;
		//}

		//public void Initialize() {
		//    //we need to set up a background thread to look at putting the records into the db.
		//    SetupDB();
		//    _timingSaver = new Thread(InsertThread);
		//    _timingSaver.Name = "Timing Saver";
		//    _timingSaver.Start();
		//}

	}
}
