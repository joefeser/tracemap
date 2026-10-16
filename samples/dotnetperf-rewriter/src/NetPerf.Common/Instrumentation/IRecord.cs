using System.Diagnostics.Contracts;

namespace NetPerf.Common.Instrumentation {

	[ContractClass(typeof(IRecordContract))]
	public interface IRecord {
		int Id {
			get;
		}

		string Signature {
			get;
		}
	}

	[ContractClassFor(typeof(IRecord))]
	abstract class IRecordContract : IRecord {

		int IRecord.Id {
			get {
				Contract.Ensures(Contract.Result<int>() != 0);
				return default(int);
			}
		}

		string IRecord.Signature {
			get {
				Contract.Ensures(!string.IsNullOrEmpty(Contract.Result<string>()));
				return default(string);
			}
		}
	}

}