using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;

namespace NetPerf.Common.Instrumentation {

	[ContractClass(typeof(IChildRecordWithCustomAttributesContract))]
	public interface IChildRecordWithCustomAttributes : IChildRecord {
		ReadOnlyCollection<string> CustomAttributes {
			get;
		}
	}

	[ContractClassFor(typeof(IChildRecordWithCustomAttributes))]
	abstract class IChildRecordWithCustomAttributesContract : IChildRecordWithCustomAttributes {

		ReadOnlyCollection<string> IChildRecordWithCustomAttributes.CustomAttributes {
			get {
				Contract.Ensures(Contract.Result<ReadOnlyCollection<string>>() != null);
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

	internal abstract class AChildRecordWithCustomAttributes : AChildRecord, IChildRecordWithCustomAttributes {

		readonly List<string> _customAttributes;

		public ReadOnlyCollection<string> CustomAttributes {
			get {
				return _customAttributes.AsReadOnly();
			}
		}

		public void AddCustomAttributes(IEnumerable<string> attributes) {
			if (attributes.IsNotEmpty()) {
				_customAttributes.AddRange(attributes);
			}
		}

		public AChildRecordWithCustomAttributes(int id, int parentId, string signature, string[] customAttributes)
			: base(id, parentId, signature) //
		{
			Contract.Requires(id > 0);
			Contract.Requires(parentId > 0);
			Contract.Requires(!string.IsNullOrEmpty(signature));

			Contract.Ensures(_customAttributes != null);

			_customAttributes = new List<string>(customAttributes ?? new string[0]);
		}

	}
}
