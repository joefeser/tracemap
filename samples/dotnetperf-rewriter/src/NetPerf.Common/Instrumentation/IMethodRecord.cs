using System;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Diagnostics;

namespace NetPerf.Common.Instrumentation {


	[ContractClass(typeof(IMethodRecordContract))]
	public interface IMethodRecord : IChildRecordWithGenericArguments {

		string Hash {
			get;
		}

		HashAlgorithm HashAlgorithm {
			get;
		}


		ReadOnlyCollection<int> SourceFileIds {
			get;
		}

		ReadOnlyCollection<IPointRecord> Points {
			get;
		}

		IPointRecord AddPoint(int? sourceFileId, int? startLine, int? startColumn, int? endLine, int? endColumn);
	}

	[DebuggerDisplay("{Signature}")]
	[ContractClassFor(typeof(IMethodRecord))]
	abstract class IMethodRecordContract : IMethodRecord {

		string IMethodRecord.Hash {
			get {
				Contract.Ensures(Contract.Result<string>() != null);
				return default(string);
			}
		}

		HashAlgorithm IMethodRecord.HashAlgorithm {
			get {
				Contract.Ensures(Contract.Result<HashAlgorithm>() != null);
				return default(HashAlgorithm);
			}
		}

		ReadOnlyCollection<int> IMethodRecord.SourceFileIds {
			get {
				Contract.Ensures(Contract.Result<ReadOnlyCollection<int>>() != null);
				return default(ReadOnlyCollection<int>);
			}
		}

		ReadOnlyCollection<IPointRecord> IMethodRecord.Points {
			get {
				Contract.Ensures(Contract.Result<ReadOnlyCollection<IPointRecord>>() != null);
				return default(ReadOnlyCollection<IPointRecord>);
			}
		}

		IPointRecord IMethodRecord.AddPoint(int? sourceFileId, int? startLine, int? startColumn, int? endLine, int? endColumn) {
			Contract.Requires(sourceFileId == null || sourceFileId > 0);
			Contract.Requires(startLine == null || startLine >= 0);
			Contract.Requires(startColumn == null || startColumn >= 0);
			Contract.Requires(endLine == null || endLine >= 0);
			Contract.Requires(endColumn == null || endColumn >= 0);

			Contract.Ensures(Contract.Result<IPointRecord>() != null);

			return default(IPointRecord);
		}

		ReadOnlyCollection<string> IChildRecordWithGenericArguments.GenericArguments {
			get {
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
}
