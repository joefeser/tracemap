using System;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Diagnostics;

namespace NetPerf.Common.Instrumentation {

	[ContractClass(typeof(IAssemblyRecordContract))]
	public interface IAssemblyRecord : IRecord {

		string PreInstrumentedHash {
			get;
		}

		HashAlgorithm PreInstrumentedHashAlgorithm {
			get;
		}

		ReadOnlyCollection<IModuleRecord> Modules {
			get;
		}

		IModuleRecord AddModule(string moduleName);
	}

	[DebuggerDisplay("{Signature}")]
	[ContractClassFor(typeof(IAssemblyRecord))]
	abstract class IAssemblyRecordContract : IAssemblyRecord {

		int IRecord.Id {
			get {
				return default(int);
			}
		}

		string IRecord.Signature {
			get {
				return default(string);
			}
		}

		string IAssemblyRecord.PreInstrumentedHash {
			get {
				Contract.Ensures(!string.IsNullOrEmpty(Contract.Result<string>()));
				return default(string);
			}
		}

		HashAlgorithm IAssemblyRecord.PreInstrumentedHashAlgorithm {
			get {
				Contract.Ensures(Contract.Result<HashAlgorithm>() != null);
				return default(HashAlgorithm);
			}
		}


		ReadOnlyCollection<IModuleRecord> IAssemblyRecord.Modules {
			get {
				Contract.Ensures(Contract.Result<ReadOnlyCollection<IModuleRecord>>() != null);
				Contract.Ensures(Contract.Result<ReadOnlyCollection<IModuleRecord>>().Count >= 0);
				return default(ReadOnlyCollection<IModuleRecord>);
			}
		}

		IModuleRecord IAssemblyRecord.AddModule(string moduleName) {
			Contract.Requires(!String.IsNullOrEmpty(moduleName));

			Contract.Ensures(Contract.Result<IModuleRecord>() != null);
			return default(IModuleRecord);
		}
		
	}

}
