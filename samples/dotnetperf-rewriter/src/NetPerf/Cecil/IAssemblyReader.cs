using NetPerf.Common;
using System.Diagnostics.Contracts;

namespace NetPerf.DCL.Cecil {

	[ContractClass(typeof(IAssemblyReaderContract))]
	internal interface IAssemblyReader {

		IAssemblyDefinition ReadAssembly(FilePath path);
	}

	[ContractClassFor(typeof(IAssemblyReader))]
	abstract class IAssemblyReaderContract : IAssemblyReader  {

		public IAssemblyDefinition ReadAssembly(FilePath path) {
			Contract.Requires(path != null);
			return null;
		}
	}
}
