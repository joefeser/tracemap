using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using NetPerf.Common;

namespace NetPerf {
	internal class InstrumentCommand {

		public List<FilePath> AssemblyPaths {
			get;
			private set;
		}

		public DirectoryPath BackupDirectory {
			get;
			set;
		}

		public DirectoryPath BaseDirectory {
			get;
			private set;
		}

		public List<string> Excludes {
			get;
			private set;
		}

		//TODO rename to StrongNameKeyFile or some such and (in comments) indicate that it is per instrument commands
		public FilePath GlobalKeyFile {
			get;
			set;
		}

		public bool HasBackupDirectory {
			get {
				return BackupDirectory != null;
			}
		}

		public bool HasGlobalKeyFile {
			get {
				return GlobalKeyFile != null;
			}
		}

		/// <summary>
		/// Passed in if the points flag is passed on the command line.
		/// </summary>
		public bool InstrumentPoints {
			get;
			set;
		}

		public bool InstrumentWithoutSymbols {
			get;
			set;
		}

		public InstrumentCommand() {
			AssemblyPaths = new List<FilePath>();
			Excludes = new List<string>();
		}

		public DirectoryPath GetBackupDirForAssembly(FilePath assembly) {
			Contract.Requires<ArgumentNullException>(assembly != null, "assembly");

			Contract.Ensures(Contract.Result<DirectoryPath>() != null);
			Contract.Ensures(Contract.Result<DirectoryPath>().IsAbsolute);

			var backupDir = BackupDirectory ?? (assembly.ParentDirectory.AppendDirectoryPath("backup"));
			backupDir = backupDir.IsAbsolute ? backupDir : backupDir.ToAbsolutePath(BaseDirectory);
			return backupDir;
		}

		[ContractInvariantMethod]
		void ObjectInvariant() {
			Contract.Invariant(AssemblyPaths != null);
			Contract.Invariant(Excludes != null);
			Contract.Invariant(BaseDirectory == null || (BaseDirectory.IsAbsolute));
		}

		/// <summary>
		/// Transforms all paths on the same drive as the <paramref name="basePath"/>
		/// into paths relative to the <paramref name="basePath"/>.
		/// (But they're stored as absolute paths for ease of use.
		/// </summary>
		/// <param name="basePath"></param>
		public void SetBaseDirectory(DirectoryPath basePath) {
			Contract.Requires<ArgumentNullException>(basePath != null, "basePath");
			Contract.Requires<ArgumentException>(basePath.IsAbsolute);

			// 1. transform all absolute paths into relative paths using the previous RelativeBasePathDirectory
			// 2. transform all the (new) relative paths into absolute paths using the basepath
			// 3. store the basePath as the new RelativePathBaseDirectory

			if (basePath == BaseDirectory)
				return;

			var items = AssemblyPaths;
			if (BaseDirectory != null) {
				// transform all the paths that can be transformed into relative paths into relative paths
				if (HasBackupDirectory && BackupDirectory.IsAbsolute) {
					BackupDirectory = BackupDirectory.MakeRelativeTo(BaseDirectory);
				}

				for (int i = 0; i < AssemblyPaths.Count; i++) {
					if (AssemblyPaths[i].IsAbsolute) {
						AssemblyPaths[i] = AssemblyPaths[i].MakeRelativeTo(BaseDirectory);
					}
				}
			}

			// transform all the relative paths into absolute paths
			if (HasBackupDirectory && BackupDirectory.IsRelative) {
				BackupDirectory = BackupDirectory.ToAbsolutePath(basePath);
			}

			for (int i = 0; i < AssemblyPaths.Count; i++) {
				if (AssemblyPaths[i].IsRelative) {
					AssemblyPaths[i] = AssemblyPaths[i].ToAbsolutePath(basePath);
				}
			}

			BaseDirectory = basePath;
		}
	}
}
