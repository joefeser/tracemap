using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace NetPerf.Common {
	/// <summary>
	/// A strongly typed representation of a filesystem directory on system drive.
	/// </summary>
	[Pure] // b/c DirectoryPath is supposed to function like System.String
	[System.Diagnostics.DebuggerDisplay("{FullPath}")]
	public sealed class DirectoryPath : ICloneable<DirectoryPath>, IEquatable<DirectoryPath> {
		public static readonly DirectoryPath LocalDirectory = new DirectoryPath("." + Path.DirectorySeparatorChar);
		public static readonly DirectoryPath ParentDirectory = new DirectoryPath(".." + Path.DirectorySeparatorChar);

		/// <summary>
		/// A <see cref="DirectoryPath"/> equivalent to <see cref="Environment.CurrentDirectory"/>.
		/// </summary>
		public static DirectoryPath CurrentDirectory {
			get {
				return Parse(EnvironmentStub.CurrentDirectory);
			}
		}

		/// <summary>
		/// The system drive that the directory is on.
		/// </summary>
		public string DriveLetter {
			get {
				return IsAbsolute ? PathStub.GetPathRoot(FullPath) : CurrentDirectory.DriveLetter;
			}
		}

		/// <summary>
		/// Whether the <see cref="DirectoryPath"/> is absolute or not.
		/// </summary>
		public bool IsAbsolute {
			get {
				return PathStub.IsPathRooted(FullPath);
			}
		}

		/// <summary>
		/// Whether the <see cref="DirectoryPath"/> is relative or not.
		/// </summary>
		public bool IsRelative {
			get {
				return !IsAbsolute;
			}
		}

		/// <summary>
		/// The directory's full filesystem path, including its directory information and (if its absolute) its drive letter.
		/// </summary>
		[XmlElement]
		public string FullPath {
			get;
			private set;
		}

		private DirectoryPath(string path) {
			Contract.Requires(!String.IsNullOrEmpty(path));
			Contract.Ensures(!String.IsNullOrEmpty(FullPath));

			FullPath = path;
		}

		/// <summary>
		/// Converts a <see cref="DirectoryPath"/> into a <see cref="System.String"/>,
		/// using the <see cref="DirectoryPath"/>'s <see cref="FullPath"/> property.
		/// </summary>
		/// <param name="item">The <see cref="DirectoryPath"/> to convert.</param>
		/// <returns>The <see cref="FullPath"/> of the <see cref="DirectoryPath"/>.</returns>
		public static implicit operator string(DirectoryPath item) {
			Contract.Requires(item != null);
			Contract.Ensures(!String.IsNullOrEmpty(Contract.Result<string>()));

			return item.FullPath;
		}

		/// <summary>
		/// Converts a <see cref="System.String"/> into a <see cref="DirectoryPath"/>.
		/// Equivalent to calling <see cref="Parse"/> and passing in the <paramref name="dirPath"/>.
		/// </summary>
		/// <param name="dirPath">The path to convert to a <see cref="DirectoryPath"/>.</param>
		/// <returns>A <see cref="DirectoryPath"/> formed out of the <paramref name="dirPath"/>.</returns>
		public static explicit operator DirectoryPath(string dirPath) {
			Contract.Requires(!String.IsNullOrEmpty(dirPath));
			Contract.Ensures(Contract.Result<DirectoryPath>() != null);

			return DirectoryPath.Parse(dirPath);
		}

		/// <summary>
		/// Compares two <see cref="DirectoryPath"/>'s for equality. Returns True if their <see cref="DirectoryPath"/>'s
		/// are equal, False otherwise.
		/// </summary>
		/// <param name="left">One of the <see cref="DirectoryPath"/>'s to compare.</param>
		/// <param name="right">One of the <see cref="DirectoryPath"/>'s to compare.</param>
		/// <returns>True if the <see cref="DirectoryPath"/>'s are equal, False otherwise.</returns>
		public static bool operator ==(DirectoryPath left, DirectoryPath right) {
			if (object.ReferenceEquals(left, null) && object.ReferenceEquals(right, null))
				return true;

			return object.ReferenceEquals(left, null) ? right.Equals(left) : left.Equals(right);
		}

		/// <summary>
		/// Compares two <see cref="DirectoryPath"/>'s for inequality. Returns True if their <see cref="DirectoryPath"/>'s
		/// are not equal, False otherwise.
		/// </summary>
		/// <param name="left">One of the <see cref="DirectoryPath"/>'s to compare.</param>
		/// <param name="right">One of the <see cref="DirectoryPath"/>'s to compare.</param>
		/// <returns>True if the <see cref="DirectoryPath"/>'s are not equal, False otherwise.</returns>
		public static bool operator !=(DirectoryPath left, DirectoryPath right) {
			if (object.ReferenceEquals(left, null) && object.ReferenceEquals(right, null))
				return false;

			return !(object.ReferenceEquals(left, null) ? right.Equals(left) : left.Equals(right));
		}

		/// <summary>
		/// Creates a <see cref="DirectoryPath"/> out of the <see cref="pathToAppend"/>, and then appends
		/// it to the current <see cref="DirectoryPath"/>.
		/// </summary>
		/// <param name="pathToAppend">The path to append.</param>
		/// <returns>A new <see cref="DirectoryPath"/> that is a copy of the current <see cref="DirectoryPath"/> with <paramref name="pathToAppend"/> appended to it.</returns>
		public DirectoryPath AppendDirectoryPath(string pathToAppend) {
			Contract.Requires(!String.IsNullOrEmpty(pathToAppend));

			Contract.Ensures(Contract.Result<DirectoryPath>() != null);
			Contract.Ensures(!object.ReferenceEquals(this, Contract.Result<DirectoryPath>()));
			Contract.Ensures(IsAbsolute == Contract.Result<DirectoryPath>().IsAbsolute);
			Contract.Ensures(IsRelative == Contract.Result<DirectoryPath>().IsRelative);
			Contract.Ensures(Contract.Result<DirectoryPath>().FullPath.StartsWith(FullPath));

			var parsedPath = DirectoryPath.Parse(pathToAppend);
			return DirectoryPath.Parse(PathStub.Combine(FullPath, parsedPath.FullPath));
		}

		internal static bool AreOnSameDrive(DirectoryPath left, DirectoryPath right) {
			return left.DriveLetter.IgnoreCaseEq(right.DriveLetter);
		}

		internal static bool AreOnSameDrive(DirectoryPath left, FilePath right) {
			return left.DriveLetter.IgnoreCaseEq(right.ParentDirectory.DriveLetter);
		}

		/// <summary>
		/// Clones the current <see cref="DirectoryPath"/>.
		/// </summary>
		/// <returns>A copy of the current <see cref="DirectoryPath"/>.</returns>
		public DirectoryPath Clone() {
			Contract.Ensures(Contract.Result<DirectoryPath>() != null);
			Contract.Ensures(!object.ReferenceEquals(this, Contract.Result<DirectoryPath>()));

			return new DirectoryPath(this.FullPath);
		}

		object ICloneable.Clone() {
			Contract.Ensures(Contract.Result<object>() != null);
			Contract.Ensures(!object.ReferenceEquals(this, Contract.Result<object>()));
			return Clone();
		}

		/// <summary>
		/// Compares two <see cref="DirectoryPath"/>'s for equality. Returns True if the two <see cref="DirectoryPath"/>'s
		/// are equal, False otherwise. Also returns false if the <paramref name="obj"/> is null.
		/// </summary>
		/// <param name="obj">The <see cref="DirectoryPath"/> to compare this one to.</param>
		/// <returns>True if the current instance is equal to the other, False otherwise.</returns>
		public override bool Equals(object obj) {
			if (obj == null || !(obj is DirectoryPath))
				return false;

			return Equals((DirectoryPath)obj);
		}

		/// <summary>
		/// Compares two <see cref="DirectoryPath"/>'s for equality. Returns True if the two <see cref="DirectoryPath"/>'s
		/// are equal, False otherwise.
		/// </summary>
		/// <param name="other">The <see cref="DirectoryPath"/> to compare this one to.</param>
		/// <returns>True if the current instance is equal to the other, False otherwise.</returns>
		public bool Equals(DirectoryPath other) {
			if (other == null)
				return false;

			if (object.ReferenceEquals(this, other))
				return true;

			// since the goal is to determine whether the directories refer
			// to the same file location on the computer, we have to use
			// absolute paths
			var absThis = IsAbsolute ? this : this.ToAbsolutePath();
			var absOther = other.IsAbsolute ? other : other.ToAbsolutePath();

			Func<List<string>, string, List<string>> aggregator = (list, str) => {
				if (str == ".") {
					// do nothing
				}
				else if (str == "..") {
					// check against less than 2 b/c the first position is
					// guaranted to be the drive letter, since we are working with absolute paths
					if (list.Count < 2)
						throw new InvalidOperationException("DirectoryPath has too many parent directories ('..\') in it.");

					list.RemoveAt(list.Count - 1);
				}
				else {
					list.Add(str);
				}

				return list;
			};

			var splitChars = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
			var thisFullPath = absThis.FullPath.Split(splitChars, StringSplitOptions.RemoveEmptyEntries)
				.Aggregate(new List<string>(), aggregator)
				.ToArray();
			var otherFullPath = absOther.FullPath.Split(splitChars, StringSplitOptions.RemoveEmptyEntries)
				.Aggregate(new List<string>(), aggregator)
				.ToArray();

			var thisPath = string.Join(Path.DirectorySeparatorChar.ToString(), thisFullPath);
			var otherPath = string.Join(Path.DirectorySeparatorChar.ToString(), otherFullPath);

			return thisPath.IgnoreCaseEq(otherPath);
		}

		/// <summary>
		/// Serves as a hash function for the <see cref="DirectoryPath"/>.
		/// </summary>
		/// <returns>A hash value based on the <see cref="FullPath"/>.</returns>
		public override int GetHashCode() {
			return FullPath.GetHashCode();
		}

		/// <summary>
		/// Returns a relative path if the <paramref name="newRootPath"/> and the current path
		/// are on the same drive. If the <paramref name="newRootPath"/> is relative, an exception is thrown.
		/// </summary>
		/// <param name="newRootPath">The desired base path for the returned <see cref="DirectoryPath"/>.</param>
		/// <returns>A copy of the current <see cref="DirectoryPath"/> that is relative to the <paramref name="newRootPath"/>.</returns>
		public DirectoryPath MakeRelativeTo(DirectoryPath newRootPath) {
			Contract.Requires(newRootPath != null);
			Contract.Requires(!String.IsNullOrEmpty(newRootPath.FullPath));
			Contract.Requires<ArgumentException>(newRootPath.IsAbsolute, "Cannot rebase using a relative path.");

			Contract.Ensures(Contract.Result<DirectoryPath>() != null);
			Contract.Ensures(!object.ReferenceEquals(this, Contract.Result<DirectoryPath>()));
			Contract.Ensures(((AreOnSameDrive(this, newRootPath) || this.IsRelative) && Contract.Result<DirectoryPath>().IsRelative)
				|| Contract.Result<DirectoryPath>().IsAbsolute);

			if (IsRelative)
				return new DirectoryPath(FullPath);

			// if the drive letter is different, we can't make this dir path
			// relative to the one passed in, so just return this dir path
			if (!AreOnSameDrive(this, newRootPath))
				return new DirectoryPath(FullPath);

			var leftSide = FullPath.Substring(Path.GetPathRoot(FullPath).Length);
			var rightSide = newRootPath.FullPath.Substring(Path.GetPathRoot(newRootPath.FullPath).Length);

			// if the directories are the same, then the relative path is the current directory
			if (leftSide.IgnoreCaseEq(rightSide))
				return LocalDirectory;

			var dirSeperators = new[] { PathStub.DirectorySeparatorChar, PathStub.AltDirectorySeparatorChar };
			var leftDirs = leftSide.Split(dirSeperators, StringSplitOptions.RemoveEmptyEntries);
			var rightDirs = rightSide.Split(dirSeperators, StringSplitOptions.RemoveEmptyEntries);

			var lastMatchingDir = 0; // will use as an index, but the algorithm treats as one based
			while (lastMatchingDir < leftDirs.Length
					&& lastMatchingDir < rightDirs.Length
					&& (leftDirs[lastMatchingDir].IgnoreCaseEq(rightDirs[lastMatchingDir]))) {

				lastMatchingDir += 1;
			}
			// have to backup by one, because the last directory wasn't a match
			// unless it got all the way to the end of the dirs, in which case they are complete matches
			lastMatchingDir -= 1;

			var newDirs = rightDirs.Skip(lastMatchingDir + 1)
				.Select(dir => ParentDirectory.FullPath)
				.DefaultIfEmpty(LocalDirectory.FullPath)
				.Concat(leftDirs
					.Skip(lastMatchingDir + 1)
					.Select(dir => dir + PathStub.DirectorySeparatorChar));

			return Parse(string.Join("", newDirs.ToArray()));
		}

		[ContractInvariantMethod]
		void ObjectInvariant() {
			Contract.Invariant(!String.IsNullOrEmpty(FullPath));
			Contract.Invariant(!String.IsNullOrEmpty(DriveLetter));
		}

		/// <summary>
		/// Converts the <paramref name="dirPath"/> into a <see cref="DirectoryPath"/>. 
		/// If the <paramref name="dirPath"/> cannot be converted to a valid <see cref="DirectoryPath"/>,
		/// an exception is thrown.
		/// </summary>
		/// <param name="dirPath">The path to convert.</param>
		/// <returns>A new <see cref="DirectoryPath"/> created from the <paramref name="dirPath"/>.</returns>
		public static DirectoryPath Parse(string dirPath) {
			Contract.Requires(!String.IsNullOrEmpty(dirPath));
			Contract.Ensures(Contract.Result<DirectoryPath>() != null);

			//hacky, but it does validate the format of the directory path
			PathStub.GetFullPath(dirPath);
			return new DirectoryPath(dirPath);
		}

		/// <summary>
		/// Converts the <see cref="DirectoryPath"/> to an absolute path using the current local directory as the base path.
		/// If the <see cref="DirectoryPath"/> is already absolute, an exception is thrown.
		/// </summary>
		/// <returns>A copy of the current <see cref="DirectoryPath"/> that is absolute, not relative.</returns>
		public DirectoryPath ToAbsolutePath() {
			Contract.Ensures(Contract.Result<DirectoryPath>() != null);
			Contract.Ensures(!object.ReferenceEquals(Contract.Result<DirectoryPath>(), this));
			Contract.Ensures(Contract.Result<DirectoryPath>().IsAbsolute);
			return ToAbsolutePath(CurrentDirectory);
		}

		/// <summary>
		/// Converts the <see cref="DirectoryPath"/> to an absolute path using the specified <paramref name="rootDirectory"/>
		/// as the base path. If the <see cref="DirectoryPath"/> is already an absolute path, an exception is thrown.
		/// </summary>
		/// <param name="rootDirectory">The absolute <see cref="DirectoryPath"/> that will form the root of the returned <see cref="DirectoryPath"/>.</param>
		/// <returns>A copy of the current <see cref="DirectoryPath"/> that is absolute, not relative.</returns>
		public DirectoryPath ToAbsolutePath(DirectoryPath rootDirectory) {
			Contract.Requires(rootDirectory != null, "rootDirectory");
			Contract.Requires<ArgumentException>(rootDirectory.IsAbsolute, "Must be an absolute directory path");

			// justification: if the path is already absolute, it can't be converted to an absolute
			// path w/ the rootDirectory as the base. Returning the path unmodified would violate
			// the expectation that the returned path has the rootDirectory as its base.
			Contract.Requires<InvalidOperationException>(this.IsRelative, "Cannot make a DirectoryPath absolute when it is already absolute.");

			Contract.Ensures(Contract.Result<DirectoryPath>() != null);
			Contract.Ensures(!object.ReferenceEquals(Contract.Result<DirectoryPath>(), this));
			Contract.Ensures(Contract.Result<DirectoryPath>().IsAbsolute);

			var rootDir = RemoveLocalDirectoryPaths(rootDirectory.FullPath);
			var thisFullPath = RemoveLocalDirectoryPaths(FullPath);
			return Parse(PathStub.Combine(rootDir, thisFullPath));
		}

		private static string RemoveLocalDirectoryPaths(string path) {
			Contract.Requires(!String.IsNullOrEmpty(path));
			// this method can't ensure that the result isn't empty, because
			// the 'path' string might just be equivalent to @".\"
			Contract.Ensures(Contract.Result<string>() != null);

			var items = path.Split(new[] { PathStub.AltDirectorySeparatorChar, PathStub.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
				.Where(p => p != ".")
				.ToArray();

			if (items.Length == 1 && PathStub.IsPathRooted(items[0]))
				return items[0] + @"\";

			return string.Join(PathStub.DirectorySeparatorChar.ToString(), items);
		}

		/// <summary>
		/// Converts the <see cref="DirectoryPath"/> to its' string representation.
		/// </summary>
		/// <returns>The value of the <see cref="DirectoryPath"/>'s <see cref="FullPath"/>.</returns>
		public override string ToString() {
			Contract.Ensures(!String.IsNullOrEmpty(Contract.Result<string>()));

			return FullPath;
		}

		/// <summary>
		/// Attempts to convert the <paramref name="potentialDirPath"/> into a <see cref="DirectoryPath"/>.
		/// If the conversion is successful, the created <see cref="DirectoryPath"/> is returned.
		/// If the conversion is not successful, then an error message describing the failure is returned.
		/// No exception is thrown.
		/// </summary>
		/// <param name="potentialDirPath">A string that might or might not be a valid filesystem directory path.</param>
		/// <returns>A tuple that contains the new <see cref="DirectoryPath"/> if the conversion was successful, or an error message if it was not.</returns>
		public static ErrorTuple<DirectoryPath> TryParse(string potentialDirPath) {
			Contract.Ensures(Contract.Result<ErrorTuple<DirectoryPath>>() != null);

			if (potentialDirPath.IsNullOrWhitespace()) {
				return new ErrorTuple<DirectoryPath>("The potential directory path was empty.");
			}

			try {
				return ErrorTuple.Create(DirectoryPath.Parse(potentialDirPath));
			}
			catch {
				return new ErrorTuple<DirectoryPath>("The path '{0}' is not a legal directory path.", potentialDirPath);
			}
		}

	}
}
