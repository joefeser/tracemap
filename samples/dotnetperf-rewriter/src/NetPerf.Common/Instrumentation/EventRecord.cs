using System.Diagnostics.Contracts;

namespace NetPerf.Common.Instrumentation {
	internal class EventRecord : AMethodContainer, IEventRecord {

		readonly int? _addMethodId;
		readonly int? _fireMethodId;
		readonly int? _removeMethodId;

		public int? AddMethodId {
			get {
				return _addMethodId;
			}
		}

		public int? FireMethodId {
			get {
				return _fireMethodId;
			}
		}

		public int? RemoveMethodId {
			get {
				return _removeMethodId;
			}
		}

		public EventRecord(int id, int typeId, string signature, int? addMethod, int? removeMethod,
			int? fireMethod = null, int[] otherMethods = null, string[] customAttributes = null)
			: base(id, typeId, signature, customAttributes, otherMethods) //
		{
			Contract.Requires(id > 0);
			Contract.Requires(typeId > 0);
			Contract.Requires(!string.IsNullOrEmpty(signature));
			Contract.Requires((addMethod == null && removeMethod == null) || (addMethod > 0 && removeMethod > 0));

			_addMethodId = addMethod;
			_removeMethodId = removeMethod;
			_fireMethodId = fireMethod;
		}
	}
}
