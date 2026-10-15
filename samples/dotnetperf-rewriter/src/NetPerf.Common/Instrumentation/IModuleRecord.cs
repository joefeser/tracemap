using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Diagnostics;

namespace NetPerf.Common.Instrumentation {


	[ContractClass(typeof(IModuleRecordContract))]
	public interface IModuleRecord : IChildRecord {

		ReadOnlyCollection<ISourceFileRecord> SourceFiles {
			get;
		}

		ReadOnlyCollection<ITypeRecord> Types {
			get;
		}

		ISourceFileRecord AddSourceFile(string uri, string hash, string hashAlgorithm, string sourceLanguage);
		ITypeRecord AddType(string signature, string[] customAttributes = null, string[] genericArguments = null);
	}

	[DebuggerDisplay("{Signature}")]
	[ContractClassFor(typeof(IModuleRecord))]
	abstract class IModuleRecordContract : IModuleRecord {

		ReadOnlyCollection<ISourceFileRecord> IModuleRecord.SourceFiles {
			get {
				Contract.Ensures(Contract.Result<ReadOnlyCollection<ISourceFileRecord>>() != null);
				return default(ReadOnlyCollection<ISourceFileRecord>);
			}
		}

		ReadOnlyCollection<ITypeRecord> IModuleRecord.Types {
			get {
				Contract.Ensures(Contract.Result<ReadOnlyCollection<ITypeRecord>>() != null);
				return default(ReadOnlyCollection<ITypeRecord>);
			}
		}

		ISourceFileRecord IModuleRecord.AddSourceFile(string uri, string hash, string hashAlgorithm, string sourceLanguage) {
			Contract.Requires(!String.IsNullOrEmpty(uri));

			Contract.Ensures(Contract.Result<ISourceFileRecord>() != null);
			return default(ISourceFileRecord);
		}

		ITypeRecord IModuleRecord.AddType(string signature, string[] customAttributes, string[] genericArguments) {
			Contract.Requires(!string.IsNullOrEmpty(signature));

			Contract.Ensures(Contract.Result<ITypeRecord>() != null);
			return default(ITypeRecord);
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
