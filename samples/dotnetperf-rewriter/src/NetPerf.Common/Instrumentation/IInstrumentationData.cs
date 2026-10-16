using System;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Diagnostics;

namespace NetPerf.Common.Instrumentation {


	[ContractClass(typeof(IInstrumentationDataContract))]
	public interface IInstrumentationData {

		ReadOnlyCollection<IAssemblyRecord> Assemblies {
			get;
		}

		/// <summary>
		/// Two guids put together to produce a large hash for a lookup.
		/// </summary>
		string InstanceHash {
			get;
			set;
		}

		ErrorTuple Save(FilePath idMappingFile);
	}

	[DebuggerDisplay("Count = {Assemblies}")]
	[ContractClassFor(typeof(IInstrumentationData))]
	abstract class IInstrumentationDataContract : IInstrumentationData {

		ReadOnlyCollection<IAssemblyRecord> IInstrumentationData.Assemblies {
			get {
				Contract.Ensures(Contract.Result<ReadOnlyCollection<IAssemblyRecord>>() != null);
				Contract.Ensures(Contract.Result<ReadOnlyCollection<IAssemblyRecord>>().Count >= 0);

				return default(ReadOnlyCollection<IAssemblyRecord>);
			}
		}

		public string InstanceHash {
			get {
				Contract.Ensures(Contract.Result<string>() != null);
				return default(string);
			}
			set {
				Contract.Ensures(value != null);
			}
		}

		ErrorTuple IInstrumentationData.Save(FilePath idMappingFile) {
			Contract.Requires(idMappingFile != null, "idMappingFile");
			Contract.Ensures(Contract.Result<ErrorTuple>() != null);

			return default(ErrorTuple);
		}

	}
}
