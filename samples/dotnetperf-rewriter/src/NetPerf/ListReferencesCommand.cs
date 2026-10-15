using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using NetPerf.DCL.Cecil;
using NetPerf.Common;

namespace NetPerf
{
	/// <summary>
	/// Usage: netperf localreferences ROOT ASSEMBLY PATH
	/// </summary>
	internal class ListLocalReferencesCommand
	{
		/// <summary>
		/// The assembly for which to list references.
		/// </summary>
		public FilePath RootAssembly {
			get;
			set;
		}

		internal IAssemblyReader AssemblyReader {
			get;
			set;
		}

		public ListLocalReferencesCommand() {
			Contract.Ensures(AssemblyReader != null);

			AssemblyReader = new DefaultAssemblyReader();
		}

		public List<FilePath> FindAllReferences() {
			Contract.Requires(RootAssembly != null);

			Contract.Ensures(Contract.Result<List<FilePath>>() != null);
			Contract.Ensures(Contract.ForAll(Contract.Result<List<FilePath>>(), fp => fp != null));

			var alreadyChecked = new Dictionary<string, FilePath>();
			var toCheck = new Stack<FilePath>();
			toCheck.Push(RootAssembly);

			while (toCheck.Count > 0) {
				var assemDef = AssemblyReader.ReadAssembly(toCheck.Pop());

				foreach (var module in assemDef.Modules) {
					foreach (var assemRef in module.AssemblyReferences) {
						if (alreadyChecked.ContainsKey(assemRef.FullName))
							continue;

						var assemRefPath = GetLocaPath(assemRef.Name, RootAssembly.ParentDirectory);
						alreadyChecked.Add(assemRef.FullName, assemRefPath);

						if (assemRefPath != null)
							toCheck.Push(assemRefPath);
					}
				}
			}

			return alreadyChecked.Values.Where(val => val != null).ToList();
		}

		private FilePath GetLocaPath(string filename, DirectoryPath dir) {
			Contract.Requires(!filename.IsNullOrWhitespace());
			Contract.Requires(dir != null);

			var extensions = new[] { ".exe", ".dll" };
			foreach (var ext in extensions) {
				var path = FilePath.Create(filename + ext, dir);
				if (FileStub.Exists(path))
					return path;
			}

			return null;
		}

	}
}
