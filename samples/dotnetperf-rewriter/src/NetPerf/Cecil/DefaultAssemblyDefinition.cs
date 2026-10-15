using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace NetPerf.DCL.Cecil {
	class DefaultAssemblyDefinition : IAssemblyDefinition {

		private readonly Mono.Cecil.AssemblyDefinition _assemDef;

		public DefaultAssemblyDefinition(Mono.Cecil.AssemblyDefinition def) {
			Contract.Requires(def != null);

			_assemDef = def;
		}

		public ICollection<IModuleDefinition> Modules {
			get {
				return (from m in _assemDef.Modules
						select (IModuleDefinition)(new DefaultModuleDefinition(m))).ToList();
			}
		}
	}
}
