using System;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;

namespace NetPerf.Common.Instrumentation {
	[ContractClass(typeof(IEventRecordContract))]
	public interface IEventRecord : IMethodContainer {
		int? AddMethodId {
			get;
		}

		int? FireMethodId {
			get;
		}

		int? RemoveMethodId {
			get;
		}
	}


	[ContractClassFor(typeof(IEventRecord))]
	abstract class IEventRecordContract : IEventRecord {

		int? IEventRecord.AddMethodId {
			get {
				Contract.Ensures(Contract.Result<int?>() == null || Contract.Result<int?>() > 0);
				return default(int?);
			}
		}

		int? IEventRecord.FireMethodId {
			get {
				Contract.Ensures(Contract.Result<int?>() == null || Contract.Result<int?>() > 0);
				return default(int?);
			}
		}

		int? IEventRecord.RemoveMethodId {
			get {
				Contract.Ensures(Contract.Result<int?>() == null || Contract.Result<int?>() > 0);
				return default(int?);
			}
		}

		ReadOnlyCollection<int> IMethodContainer.OtherMethodIds {
			get {
				return default(ReadOnlyCollection<int>);
			}
		}

		ReadOnlyCollection<string> IChildRecordWithCustomAttributes.CustomAttributes {
			get {
				return default(ReadOnlyCollection<string>);
			}
		}

		int IChildRecord.ParentId {
			get {
				return default(int);
			}
		}

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
	}
}
