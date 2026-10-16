using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Diagnostics;

namespace NetPerf.Common.Instrumentation {

	[DebuggerDisplay("{Signature}")]
	internal class ModuleRecord : AChildRecord, IModuleRecord {

		readonly Dictionary<string, ISourceFileRecord> _sourceFiles;
		readonly Dictionary<string, ITypeRecord> _types;

		public ReadOnlyCollection<ISourceFileRecord> SourceFiles {
			get {
				return _sourceFiles.Values.ToList().AsReadOnly();
			}
		}

		public ReadOnlyCollection<ITypeRecord> Types {
			get {
				return _types.Values.ToList().AsReadOnly();
			}
		}

		public ISourceFileRecord AddSourceFile(string uri, string hash, string hashAlgorithm, string sourceLanguage) {
			return _sourceFiles.AtomicAdd(uri,
				() => new SourceFileRecord(UniqueIdGenerator.NextId(typeof(SourceFileRecord)), Id, uri, hash, hashAlgorithm, sourceLanguage),
				record => record);
		}

		public ITypeRecord AddType(string signature, string[] customAttributes = null, string[] genericArguments = null) {
			return _types.AtomicAdd(signature,
				() => new TypeRecord(UniqueIdGenerator.NextId(typeof(TypeRecord)), Id, signature, customAttributes, genericArguments),
				record => record);
		}

		public void LinkSourceFiles(IEnumerable<ISourceFileRecord> sourceFiles) {
			_sourceFiles.AddUniqueChildRecords(sourceFiles,
				sf => sf.ParentId == Id,
				(left, right) => left.Signature.IgnoreCaseEq(right.Signature),
				sf => sf.Signature);
		}

		public void LinkTypes(IEnumerable<ITypeRecord> types) {
			_types.AddUniqueChildRecords(types,
				type => type.ParentId == Id,
				(left, right) => left.Signature == right.Signature,
				type => type.Signature);
		}

		public ModuleRecord(int id, int assemblyId, string name)
			: base(id, assemblyId, name) //
		{
			Contract.Requires(id > 0);
			Contract.Requires(assemblyId > 0);
			Contract.Requires(!String.IsNullOrEmpty(name));

			Contract.Ensures(_sourceFiles != null);
			Contract.Ensures(_types != null);

			_sourceFiles = new Dictionary<string, ISourceFileRecord>(StringComparer.OrdinalIgnoreCase);
			_types = new Dictionary<string, ITypeRecord>();
		}
	}
}
