using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Diagnostics;

namespace NetPerf.Common.Instrumentation {

	[DebuggerDisplay("{Signature}")]
	internal class AssemblyRecord : ARecord, IAssemblyRecord {

		//TODO 104 We are going to keep a static AssemblyRecord randomizer but someday we may want to revisit how this is being done.
		private static object LockObject = new object();
		private static readonly Random Random = new Random(DateTime.Now.Millisecond);
		private static readonly List<int> UsedRandom = new List<int>();

		readonly Dictionary<string, IModuleRecord> _modules;

		public string PreInstrumentedHash {
			get;
			set;
		}

		public HashAlgorithm PreInstrumentedHashAlgorithm {
			get;
			set;
		}

		public ReadOnlyCollection<IModuleRecord> Modules {
			get {
				return _modules.Values.ToList().AsReadOnly();
			}
		}

		public IModuleRecord AddModule(string moduleName) {
			return _modules.AtomicAdd(moduleName,
				() => new ModuleRecord(GetModuleId(), Id, moduleName),
				module => module);
		}

		public AssemblyRecord(int id, string identity, string preInstrumentedHash, HashAlgorithm preInstrumentedHashAlgorithm)
			: base(id, identity) //
		{
			Contract.Requires(id != 0);
			Contract.Requires(!string.IsNullOrEmpty(identity));
			Contract.Requires(!string.IsNullOrEmpty(preInstrumentedHash));
			Contract.Requires(preInstrumentedHashAlgorithm != null);
			Contract.Ensures(_modules != null);

			PreInstrumentedHash = preInstrumentedHash;
			PreInstrumentedHashAlgorithm = preInstrumentedHashAlgorithm;

			_modules = new Dictionary<string, IModuleRecord>();
		}

		public void LinkModules(IEnumerable<IModuleRecord> modules) {
			_modules.AddUniqueChildRecords(modules,
				module => module.ParentId == Id,
				(left, right) => left.Signature == right.Signature,
				module => module.Signature);
		}

		private static int GetModuleId() {
			int retVal = 0;
			lock (LockObject) {
				do {
					retVal = Random.Next();
				} while (UsedRandom.Contains(retVal));
				UsedRandom.Add(retVal);
			}
			return retVal;
		}
	}
}
