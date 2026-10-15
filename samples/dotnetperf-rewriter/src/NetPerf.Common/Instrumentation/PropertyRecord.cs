using System.Diagnostics.Contracts;
using System.Diagnostics;

namespace NetPerf.Common.Instrumentation {

	[DebuggerDisplay("{Signature}")]
	internal class PropertyRecord : AMethodContainer, IPropertyRecord {

		private readonly int? _getId;
		private readonly int? _setId;

		public int? GetMethodId {
			get {
				return _getId;
			}
		}

		public int? SetMethodId {
			get {
				return _setId;
			}
		}

		public PropertyRecord(int id, int typeId, string signature, int? getMethodId, int? setMethodId,
			int[] otherMethodIds = null, string[] customAttributes = null)
			: base(id, typeId, signature, customAttributes, otherMethodIds) //
		{
			Contract.Requires(id > 0);
			Contract.Requires(typeId > 0);
			Contract.Requires(!string.IsNullOrEmpty(signature));
			Contract.Requires((getMethodId.HasValue && getMethodId > 0) || (setMethodId.HasValue && setMethodId > 0));

			_getId = getMethodId;
			_setId = setMethodId;
		}
	}
}
