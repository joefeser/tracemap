using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace NetPerf.Common.Instrumentation {

	[DebuggerDisplay("{Signature}")]
	internal class SourceFileRecord : AChildRecord, ISourceFileRecord {

		readonly string _hash;
		readonly string _hashAlgorithm;
		readonly string _sourceLanguage;

		public string Hash {
			get {
				return _hash;
			}
		}

		public string HashAlgorithm {
			get {
				return _hashAlgorithm;
			}
		}

		public string SourceLanguage {
			get {
				return _sourceLanguage;
			}
		}

		public SourceFileRecord(int id, int moduleId, string uri, string hash, string hashAlgorithm, string sourceLanguage)
			: base(id, moduleId, uri) //
		{
			// note: the hashAlgorithm parameter here must remain a string, since it comes from windows internal code, not our own
			Contract.Requires(id > 0);
			Contract.Requires(moduleId > 0);
			Contract.Requires(!String.IsNullOrEmpty(uri));

			_hash = hash ?? string.Empty;
			_hashAlgorithm = hashAlgorithm ?? string.Empty;
			_sourceLanguage = sourceLanguage ?? string.Empty;
		}
	}
}
