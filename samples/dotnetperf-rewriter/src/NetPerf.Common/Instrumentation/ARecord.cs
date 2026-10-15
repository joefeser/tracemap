using System.Diagnostics.Contracts;

namespace NetPerf.Common.Instrumentation {
	internal abstract class ARecord : IRecord {

		readonly int _id;
		// protected so that child classes (e.g. PointRecord can override it)
		protected readonly string _signature;

		public int Id {
			get {
				return _id;
			}
		}

		public string Signature {
			get {
				return _signature;
			}
		}

		public ARecord(int id, string signature) {
			Contract.Requires(id > 0);
			Contract.Requires(!string.IsNullOrEmpty(signature));

			_id = id;
			_signature = signature;
		}
	}
}
