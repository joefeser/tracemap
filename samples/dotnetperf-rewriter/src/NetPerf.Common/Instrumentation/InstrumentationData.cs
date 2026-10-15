using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;

namespace NetPerf.Common.Instrumentation {

	internal class InstrumentationData : IInstrumentationData {
		readonly Dictionary<string, IAssemblyRecord> _assemblies;

		public ReadOnlyCollection<IAssemblyRecord> Assemblies {
			get {
				return _assemblies.Values.ToList().AsReadOnly();
			}
		}

		private string _instanceHash;

		public string InstanceHash {
			get {
				return _instanceHash;
			}
			set {
				if (value == null) {
					throw new ArgumentNullException("value");
				}
				_instanceHash = value;
			}
		}

		public void LinkAssemblies(IList<IAssemblyRecord> assemblies) {
			_assemblies.AddUniqueChildRecords(assemblies,
				a => true,
				(a, b) => a.Signature.IgnoreCaseEq(b.Signature),
				a => a.Signature);
		}

		public InstrumentationData() {
			Contract.Ensures(_assemblies != null);

			_assemblies = new Dictionary<string, IAssemblyRecord>(StringComparer.OrdinalIgnoreCase);
			InstanceHash = Guid.NewGuid().ToString() + "|" + Guid.NewGuid().ToString();
		}

		public ErrorTuple Save(FilePath idMappingFile) {
			return InstrumentationDataSerializer.Write(idMappingFile, this);
		}

	}
}
