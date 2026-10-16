using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Diagnostics;

namespace NetPerf.Common.Instrumentation {

	[DebuggerDisplay("{Signature}")]
	internal class MethodRecord : AChildRecordWithGenericArguments, IMethodRecord {

		readonly string _hash;
		readonly HashAlgorithm _hashAlgorithm;
		readonly List<int> _sourceFileIds;
		readonly Dictionary<int, IPointRecord> _points;

		public string Hash {
			get {
				return _hash;
			}
		}

		public HashAlgorithm HashAlgorithm {
			get {
				return _hashAlgorithm;
			}
		}

		public ReadOnlyCollection<int> SourceFileIds {
			get {
				return _sourceFileIds.AsReadOnly();
			}
		}

		public ReadOnlyCollection<IPointRecord> Points {
			get {
				return _points.Values.ToList().AsReadOnly();
			}
		}

		public MethodRecord(int id, int typeId, string signature, string hash, HashAlgorithm hashAlgorithm, string[] customAttributes = null, int[] sourceFileIds = null, string[] genericArguments = null)
			: base(id, typeId, signature, customAttributes, genericArguments) //
		{
			Contract.Requires(id > 0);
			Contract.Requires(typeId > 0);
			Contract.Requires(!String.IsNullOrEmpty(signature));
			Contract.Requires(hash != null);
			Contract.Requires(hashAlgorithm != null);


			Contract.Ensures(_sourceFileIds != null);
			Contract.Ensures(_points != null);

			_hash = hash;
			_hashAlgorithm = hashAlgorithm;
			_sourceFileIds = new List<int>(sourceFileIds ?? new int[0]);
			_points = new Dictionary<int, IPointRecord>();
		}

		public IPointRecord AddPoint(int? sourceFileId, int? startLine, int? startColumn, int? endLine, int? endColumn) {
			var uniqueId = UniqueIdGenerator.NextId(typeof(IPointRecord));
			return _points.AtomicAdd(
				uniqueId,
				() => new PointRecord(uniqueId, Id, sourceFileId, startLine, startColumn, endLine, endColumn),
				record => record);
		}

		public void LinkPoints(IEnumerable<IPointRecord> points) {
			_points.AddUniqueChildRecords(
				points,
				record => record.ParentId == Id,
				(left, right) => left.Id == right.Id,
				record => record.Id);
		}
	}
}
