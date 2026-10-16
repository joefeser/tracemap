using System.Diagnostics.Contracts;
using System.Diagnostics;

namespace NetPerf.Common.Instrumentation {

	[DebuggerDisplay("{Signature}")]
	internal class PointRecord : AChildRecord, IPointRecord {

		readonly int? _sourceFileId;
		readonly int? _startLine;
		readonly int? _startColumn;
		readonly int? _endLine;
		readonly int? _endColumn;

		public int? EndColumn {
			get {
				return _endColumn;
			}
		}

		public int? EndLine {
			get {
				return _endLine;
			}
		}

		public int? SourceFileId {
			get {
				return _sourceFileId;
			}
		}

		public int? StartColumn {
			get {
				return _startColumn;
			}
		}

		public int? StartLine {
			get {
				return _startLine;
			}
		}

		public PointRecord(int id, int methodId,
			int? sourceFileId = null,
			int? startLine = null, int? startColumn = null,
			int? endLine = null, int? endColumn = null)
			: base(id, methodId, string.Format(
				"{0} {1} {2} {3} {4} {5} {6}",
				id, methodId,
				sourceFileId,
				sourceFileId != null ? startLine : null,
				sourceFileId != null ? startColumn : null,
				sourceFileId != null ? endLine : null,
				sourceFileId != null ? endColumn : null).Trim()) //
		{
			Contract.Requires(id > 0);
			Contract.Requires(methodId > 0);
			Contract.Requires(sourceFileId == null || sourceFileId > 0);
			Contract.Requires(startLine == null || startLine >= 0);
			Contract.Requires(startColumn == null || startColumn >= 0);
			Contract.Requires(endLine == null || endLine >= 0);
			Contract.Requires(endColumn == null || endColumn >= 0);

			_sourceFileId = sourceFileId;
			if (_sourceFileId != null) {
				// these should only ever be set if source file information is present
				_startLine = startLine;
				_startColumn = startColumn;
				_endLine = endLine;
				_endColumn = endColumn;
			}
		}
	}
}
