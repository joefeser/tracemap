using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;
using System.Diagnostics;

namespace NetPerf.Common.Instrumentation {


	[ContractClass(typeof(APointRecord))]
	public interface IPointRecord : IChildRecord {

		int? EndColumn {
			get;
		}

		int? EndLine {
			get;
		}

		int? SourceFileId {
			get;
		}

		int? StartColumn {
			get;
		}

		int? StartLine {
			get;
		}
	}

	[DebuggerDisplay("{Signature}")]
	[ContractClassFor(typeof(IPointRecord))]
	abstract class APointRecord : IPointRecord {

		int? IPointRecord.EndColumn {
			get {
				Contract.Ensures(Contract.Result<int?>() == null || Contract.Result<int?>() >= 0);
				return default(int);
			}
		}

		int? IPointRecord.EndLine {
			get {
				Contract.Ensures(Contract.Result<int?>() == null || Contract.Result<int?>() >= 0);
				return default(int);
			}
		}

		int IRecord.Id {
			get {
				return default(int);
			}
		}

		int IChildRecord.ParentId {
			get {
				return default(int);
			}
		}

		string IRecord.Signature {
			get {
				return default(string);
			}
		}

		int? IPointRecord.SourceFileId {
			get {
				Contract.Ensures(Contract.Result<int?>() == null || Contract.Result<int?>() > 0);
				return default(int);
			}
		}

		int? IPointRecord.StartColumn {
			get {
				Contract.Ensures(Contract.Result<int?>() == null || Contract.Result<int?>() >= 0);
				return default(int);
			}
		}

		int? IPointRecord.StartLine {
			get {
				Contract.Ensures(Contract.Result<int?>() == null || Contract.Result<int?>() >= 0);
				return default(int);
			}
		}
	}
}
