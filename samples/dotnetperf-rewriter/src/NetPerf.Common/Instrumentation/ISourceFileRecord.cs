
using System.Diagnostics.Contracts;
using System.Diagnostics;
namespace NetPerf.Common.Instrumentation {

	[ContractClass(typeof(ISourceFileRecordContract))]
	public interface ISourceFileRecord : IChildRecord {
		string Hash {
			get;
		}

		// note: the HashAlgorithm property here must remain a string, since it comes from windows internal code, not our own
		string HashAlgorithm {
			get;
		}

		string SourceLanguage {
			get;
		}
	}

	[DebuggerDisplay("{Signature}")]
	[ContractClassFor(typeof(ISourceFileRecord))]
	abstract class ISourceFileRecordContract : ISourceFileRecord {
		string ISourceFileRecord.Hash {
			get {
				Contract.Ensures(Contract.Result<string>() != null);
				return default(string);
			}
		}

		string ISourceFileRecord.HashAlgorithm {
			get {
				Contract.Ensures(Contract.Result<string>() != null);
				return default(string);
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

		string ISourceFileRecord.SourceLanguage {
			get {
				Contract.Ensures(Contract.Result<string>() != null);
				return default(string);
			}
		}
	}

}
