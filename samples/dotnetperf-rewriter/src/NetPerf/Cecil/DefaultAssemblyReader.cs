using NetPerf.Common;
using System.Diagnostics.Contracts;

namespace NetPerf.DCL.Cecil {
	class DefaultAssemblyReader : IAssemblyReader {

		public IAssemblyDefinition ReadAssembly(FilePath path) {
			return new DefaultAssemblyDefinition(Mono.Cecil.AssemblyDefinition.ReadAssembly(path));
		}
	}
}
