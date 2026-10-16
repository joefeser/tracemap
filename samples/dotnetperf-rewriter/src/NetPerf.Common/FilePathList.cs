using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace NetPerf.Common {
	public class FilePathList : List<FilePath> {

		public void ToAbsolute(DirectoryPath basePath) {
			Contract.Requires<ArgumentNullException>(basePath != null, "basePath");
			Contract.Requires<ArgumentException>(basePath.IsAbsolute, "Must be an absolute directory path");

			Contract.Ensures(Count == Contract.OldValue(Count));
			Contract.Ensures(Contract.ForAll(this, fp => fp == null || fp.IsAbsolute));

			MakeAllPathsAbsolute(basePath);
		}

		public void ForceAbsolute(DirectoryPath forcedBasepath) {
			Contract.Requires<ArgumentNullException>(forcedBasepath != null, "basePath");
			Contract.Requires<ArgumentException>(forcedBasepath.IsAbsolute, "Must be an absolute directory path");

			Contract.Ensures(Count == Contract.OldValue(Count));
			Contract.Ensures(Contract.ForAll(this, fp => fp == null || fp.IsAbsolute));
			Contract.Ensures(Contract.ForAll(this, fi => fi == null || fi.FullPath.StartsWith(forcedBasepath.FullPath)));

			MakeAllPathsAbsolute(forcedBasepath, rebaseIfAbsoluteAlready: true);
		}

		private void MakeAllPathsAbsolute(DirectoryPath basePath, bool rebaseIfAbsoluteAlready = false) {
			Contract.Requires<ArgumentNullException>(basePath != null, "basePath");
			Contract.Requires<ArgumentException>(basePath.IsAbsolute, "Must be an absolute directory path");

			Contract.Ensures(Count == Contract.OldValue(Count));
			Contract.Ensures(Contract.ForAll(this, fp => fp.IsAbsolute));

			// this contract says that if rebaseIsAbsoluteAlready is true,
			// then all of the FilePaths should start with the basePath.
			// Otherwise, all FilePaths on the save drive as the basePath should start with the basePath
			Contract.Ensures(Contract.ForAll(this,
				fi => fi == null ||
					  (rebaseIfAbsoluteAlready && fi.FullPath.StartsWith(basePath.FullPath)) ||
					  (!rebaseIfAbsoluteAlready && (DirectoryPath.AreOnSameDrive(basePath, fi) || fi.FullPath.StartsWith(basePath.FullPath)))));

			if (rebaseIfAbsoluteAlready) {
				MakeRelativeTo(basePath);
			}

			for (int i = 0; i < Count; i++) {
				if (this[i] != null && this[i].IsRelative) {
					this[i] = this[i].ToAbsolutePath(basePath);
				}
			}
		}

		public void MakeRelativeTo(DirectoryPath basePath) {
			Contract.Requires<ArgumentNullException>(basePath != null, "basePath");
			Contract.Requires<ArgumentException>(basePath.IsAbsolute, "Must be an absolute directory path");

			Contract.Ensures(Count == Contract.OldValue(Count));
			Contract.Ensures(Contract.ForAll(this, fp => fp == null || fp.IsRelative));

			for (int i = 0; i < Count; i++) {
				if (this[i] != null && this[i].IsAbsolute) {
					this[i] = this[i].MakeRelativeTo(basePath);
				}
			}
		}
	}
}
