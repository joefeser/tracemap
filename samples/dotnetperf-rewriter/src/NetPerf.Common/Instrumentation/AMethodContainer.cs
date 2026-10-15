using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;

namespace NetPerf.Common.Instrumentation {

	[ContractClass(typeof(IMethodContainerContract))]
	public interface IMethodContainer : IChildRecordWithCustomAttributes {
		ReadOnlyCollection<int> OtherMethodIds {
			get;
		}
	}

	[ContractClassFor(typeof(IMethodContainer))]
	abstract class IMethodContainerContract : IMethodContainer {
		ReadOnlyCollection<int> IMethodContainer.OtherMethodIds {
			get {
				Contract.Ensures(Contract.Result<ReadOnlyCollection<int>>() != null);

				return default(ReadOnlyCollection<int>);
			}
		}

		public ReadOnlyCollection<string> CustomAttributes {
			get {
				return default(ReadOnlyCollection<string>);
			}
		}

		public int ParentId {
			get {
				return default(int);
			}
		}

		public int Id {
			get {
				return default(int);
			}
		}

		public string Signature {
			get {
				return default(string);
			}
		}
	}

	internal abstract class AMethodContainer : AChildRecordWithCustomAttributes, IMethodContainer {

		readonly List<int> _otherMethodIds;

		public ReadOnlyCollection<int> OtherMethodIds {
			get {
				return _otherMethodIds.AsReadOnly();
			}
		}

		public AMethodContainer(int id, int parentId, string signature, string[] customAttributes, int[] otherMethodIds)
			: base(id, parentId, signature, customAttributes) //
		{
			Contract.Requires(id > 0);
			Contract.Requires(parentId > 0);
			Contract.Requires(!string.IsNullOrEmpty(signature));

			_otherMethodIds = new List<int>(otherMethodIds ?? new int[0]);
		}
	}
}
