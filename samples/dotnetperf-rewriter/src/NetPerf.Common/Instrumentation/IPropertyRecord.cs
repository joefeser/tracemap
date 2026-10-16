using System;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Diagnostics;

namespace NetPerf.Common.Instrumentation {


	[ContractClass(typeof(IPropertyRecordContract))]
	public interface IPropertyRecord : IMethodContainer {

		int? GetMethodId {
			get;
		}

		int? SetMethodId {
			get;
		}
	}

	[DebuggerDisplay("{Signature}")]
	[ContractClassFor(typeof(IPropertyRecord))]
	abstract class IPropertyRecordContract : IPropertyRecord {

		int? IPropertyRecord.GetMethodId {
			get {
				Contract.Ensures(Contract.Result<int?>() != null || ((IPropertyRecord)this).SetMethodId != null);

				return default(int);
			}
		}

		int? IPropertyRecord.SetMethodId {
			get {
				Contract.Ensures(Contract.Result<int?>() != null || ((IPropertyRecord)this).GetMethodId != null);

				return default(int);
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
