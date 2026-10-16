using System.Collections.Generic;

namespace NetPerf.DCL.Cecil {
	internal interface IAssemblyDefinition {

		ICollection<IModuleDefinition> Modules {
			get;
		}
	}
}
