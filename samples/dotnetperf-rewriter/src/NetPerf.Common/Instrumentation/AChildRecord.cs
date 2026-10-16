using System.Diagnostics.Contracts;

namespace NetPerf.Common.Instrumentation {
	
	[ContractClass(typeof(IChildRecordContract))]
	public interface IChildRecord : IRecord {
		int ParentId {
			get;
		}
	}

	[ContractClassFor(typeof(IChildRecord))]
	abstract class IChildRecordContract : IChildRecord {
		int IChildRecord.ParentId {
			get {
				Contract.Ensures(Contract.Result<int>() > 0);
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

	internal abstract class AChildRecord : ARecord, IChildRecord {

		readonly int _parentId;

		public int ParentId {
			get {
				return _parentId;
			}
		}

		public AChildRecord(int id, int parentId, string signature)
			: base(id, signature) {
			Contract.Requires(id > 0);
			Contract.Requires(parentId > 0);
			Contract.Requires(!string.IsNullOrEmpty(signature));

			_parentId = parentId;
		}

	}

	
}
