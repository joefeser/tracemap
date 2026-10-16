using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace NetPerf.DCL.Cecil {
	class DefaultModuleDefinition :IModuleDefinition {

		private readonly Mono.Cecil.ModuleDefinition _module;

		public DefaultModuleDefinition(Mono.Cecil.ModuleDefinition mod) {
			Contract.Requires(mod != null);

			_module = mod;
		}

		public ICollection<Mono.Cecil.AssemblyNameReference> AssemblyReferences {
			get {
				return _module.AssemblyReferences;
			}
		}
	}
}
