using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;

namespace NetPerf.Common.Instrumentation {

	[ContractClass(typeof(IChildRecordWithGenericArgumentsContract))]
	public interface IChildRecordWithGenericArguments : IChildRecordWithCustomAttributes {
		ReadOnlyCollection<string> GenericArguments {
			get;
		}
	}

	[ContractClassFor(typeof(IChildRecordWithGenericArguments))]
	abstract class IChildRecordWithGenericArgumentsContract : IChildRecordWithGenericArguments {

		ReadOnlyCollection<string> IChildRecordWithGenericArguments.GenericArguments {
			get {
				Contract.Ensures(Contract.Result<ReadOnlyCollection<string>>() != null);
				return default(ReadOnlyCollection<string>);
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

	internal abstract class AChildRecordWithGenericArguments : AChildRecordWithCustomAttributes, IChildRecordWithGenericArguments {

		readonly List<string> _genericArguments;

		public ReadOnlyCollection<string> GenericArguments {
			get {
				return _genericArguments.AsReadOnly();
			}
		}

		public void AddGenericArguments(IEnumerable<string> arguments) {
			if (arguments.IsNotEmpty()) {
				_genericArguments.AddRange(arguments);
			}
		}

		public AChildRecordWithGenericArguments(int id, int parentId, string signature, string[] customAttributes, string[] genericArguments)
			: base(id, parentId, signature, customAttributes) //
		{
			Contract.Requires(id > 0);
			Contract.Requires(parentId > 0);
			Contract.Requires(!string.IsNullOrEmpty(signature));

			Contract.Ensures(_genericArguments != null);

			_genericArguments = new List<string>(genericArguments ?? new string[0]);
		}
	}
}
