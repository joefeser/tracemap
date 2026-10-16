using System.Collections.Generic;
using Mono.Cecil;

namespace NetPerf.DCL.Cecil {
	internal interface IModuleDefinition {

		ICollection<AssemblyNameReference> AssemblyReferences {
			get;
		}

	}
}
