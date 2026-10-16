using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics.Contracts;
using System.Xml.Serialization;

namespace NetPerf.Common {

	/// <summary>
	/// A strongly typed representation of a file's location within a directory hierarchy.
	/// </summary>
	[Pure] // b/c FilePath is supposed to function like string
	[System.Diagnostics.DebuggerDisplay("{FullPath}")]
	public sealed class FilePath : ICloneable<FilePath>, IEquatable<FilePath> {

		/// <summary>
		/// The file's extension. This is all the characters following the last period
		/// ('.') in the filename, as well as the period itself.
		/// </summary>
		public string Extension {
			get {
				return Path.GetExtension(FileName) ?? string.Empty;
			}
		}

		/// <summary>
		/// The file's filename. This does not include any directory information, but does include the extension.
		/// </summary>
		public string FileName {
			get;
			private set;
		}

		/// <summary>
		/// The file's filename without its extension.
		/// </summary>
		public string FileNameWithoutExtension {
			get {
				return Path.GetFileNameWithoutExtension(FileName) ?? string.Empty;
			}
		}

		/// <summary>
		/// The file's full filesystem path, including its directory information, name, and extension.
		/// </summary>
		[XmlElement]
		public string FullPath {
			get;
			private set;
		}

		/// <summary>
		/// Whether the file's <see cref="ParentDirectory"/> is absolute.
		/// </summary>
		public bool IsAbsolute {
			get {
				return ParentDirectory.IsAbsolute;
			}
		}

		/// <summary>
		/// Whether the file's <see cref="ParentDirectory"/> is relative.
		/// </summary>
		public bool IsRelative {
			get {
				return ParentDirectory.IsRelative;
			}
		}

		/// <summary>
		/// A strongly-typed representation of the directory the file resides in.
		/// </summary>
		public DirectoryPath ParentDirectory {
			get;
			private set;
		}

		/// <summary>
		/// Converts a <see cref="FilePath"/> into a <see cref="System.String"/>,
		/// using the <see cref="FilePath"/>'s <see cref="FullName"/> property.
		/// </summary>
		/// <param name="item">The <see cref="FilePath"/> to convert.</param>
		/// <returns>The <see cref="FullName"/> of the <see cref="FilePath"/>.</returns>
		public static implicit operator string(FilePath item) {
			Contract.Requires(item != null);
			Contract.Requires(!String.IsNullOrEmpty(item.FullPath));

			Contract.Ensures(!String.IsNullOrEmpty(Contract.Result<string>()));
			return item.FullPath;
		}

		/// <summary>
		/// Compares two <see cref="FilePath"/>'s for equality. Returns True if their <see cref="FullPath"/>'s
		/// are equal, False otherwise.
		/// </summary>
		/// <param name="left">One of the <see cref="FilePath"/>'s to compare.</param>
		/// <param name="right">One of the <see cref="FilePath"/>'s to compare.</param>
		/// <returns>True if the <see cref="FilePath"/>'s are equal, False otherwise.</returns>
		public static bool operator ==(FilePath left, FilePath right) {
			if (object.ReferenceEquals(left, null) && object.ReferenceEquals(right, null))
				return true;

			return object.ReferenceEquals(left, null) ? right.Equals(left) : left.Equals(right);
		}

		/// <summary>
		/// Compares two <see cref="FilePath"/>'s for inequality. Returns True if their <see cref="FullPath"/>'s
		/// are not equal, False otherwise.
		/// </summary>
		/// <param name="left">One of the <see cref="FilePath"/>'s to compare.</param>
		/// <param name="right">One of the <see cref="FilePath"/>'s to compare.</param>
		/// <returns>True if the <see cref="FilePath"/>'s are not equal, False otherwise.</returns>
		public static bool operator !=(FilePath left, FilePath right) {
			if (object.ReferenceEquals(left, null) && object.ReferenceEquals(right, null))
				return false;

			return !(object.ReferenceEquals(left, null) ? right.Equals(left) : left.Equals(right));
		}

		[ContractInvariantMethod]
		void ObjectInvariant() {
			Contract.Invariant(!String.IsNullOrEmpty(FileName));
			Contract.Invariant(ParentDirectory != null);
			Contract.Invariant(!String.IsNullOrEmpty(FullPath));
			Contract.Invariant(Extension != null);
			Contract.Invariant(FileNameWithoutExtension != null);
		}

		/// <summary>
		/// Changes the file's extension, without changing its filename or directory path.
		/// </summary>
		/// <param name="newExtension">The desired extension, excluding the preceeding period ('.')</param>
		/// <returns>A new <see cref="FilePath"/> that is a copy of the current one, except with the extension changed.</returns>
		public FilePath ChangeExtension(string newExtension) {
			Contract.Requires(!String.IsNullOrEmpty(newExtension));

			Contract.Ensures(Contract.Result<FilePath>() != null);
			Contract.Ensures(!object.ReferenceEquals(this, Contract.Result<FilePath>()));

			return new FilePath(Path.ChangeExtension(FileName, newExtension), ParentDirectory.Clone());
		}

		/// <summary>
		/// Changes the file's filename, without changing its extension or directory path.
		/// </summary>
		/// <param name="newFileName">The new desired filename. Any part that looks like an extension will be treated as part of the filename, not as its extension.</param>
		/// <returns>A new <see cref="FilePath"/> that is a copy of the current one, except with the filename changed.</returns>
		public FilePath ChangeFileNameWithoutExtension(string newFileName) {
			Contract.Requires(!String.IsNullOrEmpty(newFileName));

			Contract.Ensures(Contract.Result<FilePath>() != null);
			Contract.Ensures(!object.ReferenceEquals(this, Contract.Result<FilePath>()));

			var ext = Extension;
			return new FilePath(newFileName + ext, ParentDirectory.Clone());
		}

		/// <summary>
		/// Changes the file's <see cref="ParentDirectory"/>, but not its filename or extension.
		/// </summary>
		/// <param name="newParentDirectory">The desired <see cref="ParentDirectory"/>.</param>
		/// <returns>A new <see cref="FilePath"/> that is a copy of the current one, except with a different <see cref="ParentDirectory"/>.</returns>
		public FilePath ChangeParentDirectory(DirectoryPath newParentDirectory) {
			Contract.Requires<ArgumentNullException>(newParentDirectory != null);

			Contract.Ensures(Contract.Result<FilePath>() != null);
			Contract.Ensures(!object.ReferenceEquals(this, Contract.Result<FilePath>()));

			return new FilePath(FileName, newParentDirectory);
		}

		/// <summary>
		/// Clones the current <see cref="FilePath"/>.
		/// </summary>
		/// <returns>A copy of the current <see cref="FilePath"/>.</returns>
		public FilePath Clone() {
			Contract.Ensures(Contract.Result<FilePath>() != null);
			Contract.Ensures(!object.ReferenceEquals(this, Contract.Result<FilePath>()));
			return new FilePath(FileName, ParentDirectory.Clone());
		}

		object ICloneable.Clone() {
			Contract.Ensures(Contract.Result<object>() != null);
			Contract.Ensures(!object.ReferenceEquals(this, Contract.Result<object>()));
			return Clone();
		}

		/// <summary>
		/// Compares two <see cref="FilePath"/>'s for equality. Returns True if the two <see cref="FilePath"/>'s
		/// are equal, False otherwise. Also returns false if the <paramref name="obj"/> is null.
		/// </summary>
		/// <param name="obj">The <see cref="FilePath"/> to compare this one to.</param>
		/// <returns>True if the current instance is equal to the other, False otherwise.</returns>
		public override bool Equals(object obj) {
			if (!(obj is FilePath))
				return false;

			return Equals((FilePath)obj);
		}

		/// <summary>
		/// Compares two <see cref="FilePath"/>'s for equality. Returns True if the two <see cref="FilePath"/>'s
		/// are equal, False otherwise.
		/// </summary>
		/// <param name="other">The <see cref="FilePath"/> to compare this one to.</param>
		/// <returns>True if the current instance is equal to the other, False otherwise.</returns>
		public bool Equals(FilePath other) {
			if (other == null)
				return false;

			if (object.ReferenceEquals(this, other))
				return true;

			return FileName.IgnoreCaseEq(other.FileName) && ParentDirectory.Equals(other.ParentDirectory);
		}

		private FilePath(string fileName, DirectoryPath parentDir) {
			Contract.Requires(!String.IsNullOrEmpty(fileName));
			Contract.Requires(parentDir != null);
			Contract.Requires(!String.IsNullOrEmpty(parentDir.FullPath));

			Contract.Ensures(!String.IsNullOrEmpty(FileName));
			Contract.Ensures(ParentDirectory != null);
			Contract.Ensures(!string.IsNullOrEmpty(FullPath));

			FileName = fileName;
			ParentDirectory = parentDir;
			FullPath = Path.Combine(ParentDirectory.FullPath, FileName);
		}

		/// <summary>
		/// Serves as a hash function for the <see cref="FilePath"/>.
		/// </summary>
		/// <returns>A hash value based on the <see cref="FullPath"/></returns>
		public override int GetHashCode() {
			return FullPath.GetHashCode();
		}

		/// <summary>
		/// Converts the <see cref="FilePath"/> to a path relative to the <paramref name="newRootPath"/>,
		/// but only if both are on the same drive.
		/// </summary>
		/// <param name="newRootPath"></param>
		/// <returns></returns>
		public FilePath MakeRelativeTo(DirectoryPath newRootPath) {
			Contract.Requires(newRootPath != null);
			Contract.Ensures(Contract.Result<FilePath>() != null);
			Contract.Ensures(!object.ReferenceEquals(this, Contract.Result<FilePath>()));
			return new FilePath(FileName, ParentDirectory.MakeRelativeTo(newRootPath));
		}

		/// <summary>
		/// Converts the <paramref name="initialFilePath"/> into a <see cref="FilePath"/>. 
		/// If the <paramref name="initialFilePath"/> cannot be converted to a valid <see cref="FilePath"/>,
		/// an exception is thrown.
		/// </summary>
		/// <param name="initialFilePath">The path to convert.</param>
		/// <returns>A new <see cref="FilePath"/> created from the <paramref name="initialFilePath"/>.</returns>
		public static FilePath Parse(string initialFilePath) {
			Contract.Requires(!initialFilePath.IsNullOrWhitespace());

			Contract.Ensures(Contract.Result<FilePath>() != null);

			var fileName = Path.GetFileName(initialFilePath);
			if (string.IsNullOrEmpty(fileName))
				throw new ArgumentOutOfRangeException("initialFilePath", "The path information '" + initialFilePath + "' is missing a filename.");

			var dirPath = Path.GetDirectoryName(initialFilePath);
			var dir = string.IsNullOrEmpty(dirPath)
				? DirectoryPath.LocalDirectory
				: DirectoryPath.Parse(dirPath);

			return new FilePath(fileName, dir);
		}

		/// <summary>
		/// Creates a new <see cref="FilePath"/> from the given <paramref name="fileName"/> and <paramref name="directory"/>.
		/// </summary>
		/// <param name="fileName">The name of the file.</param>
		/// <param name="directory">The directory the file is located in.</param>
		/// <returns>A new <see cref="FilePath"/></returns>
		public static FilePath Create(string fileName, DirectoryPath directory) {
			Contract.Requires(!fileName.IsNullOrWhitespace());
			Contract.Requires(directory != null);

			Contract.Ensures(Contract.Result<FilePath>() != null);

			return Parse(Path.Combine(directory.FullPath, fileName));
		}

		/// <summary>
		/// Converts the <see cref="FilePath"/> to an absolute path using the current local directory as the base path.
		/// If the <see cref="FilePath"/> is already absolute, an exception is thrown.
		/// </summary>
		/// <returns>A copy of the current <see cref="FilePath"/> that is absolute, not relative.</returns>
		public FilePath ToAbsolutePath() {
			Contract.Ensures(Contract.Result<FilePath>() != null);
			Contract.Ensures(!object.ReferenceEquals(this, Contract.Result<FilePath>()));
			return new FilePath(FileName, ParentDirectory.ToAbsolutePath());
		}

		/// <summary>
		/// Converts the <see cref="FilePath"/> to an absolute path using the specified <paramref name="rootDirectory"/> as the base path.
		/// If it is already an absolute path, an exception is thrown.
		/// </summary>
		/// <param name="rootDirectory">The absolute <see cref="DirectoryPath"/> that will form the root of the returned <see cref="FilePath"/>.</param>
		/// <returns>A copy of the current <see cref="FilePath"/> that is absolute, not relative.</returns>
		public FilePath ToAbsolutePath(DirectoryPath rootDirectory) {
			Contract.Requires(rootDirectory != null);

			Contract.Ensures(Contract.Result<FilePath>() != null);
			Contract.Ensures(!object.ReferenceEquals(this, Contract.Result<FilePath>()));
			return new FilePath(FileName, ParentDirectory.ToAbsolutePath(rootDirectory));
		}

		/// <summary>
		/// Converts the <see cref="FilePath"/> to its string representation.
		/// </summary>
		/// <returns>The <see cref="FilePath"/>'s <see cref="FullPath"/>.</returns>
		public override string ToString() {
			Contract.Ensures(!String.IsNullOrEmpty(Contract.Result<string>()));

			return FullPath;
		}

		/// <summary>
		/// Attempts to convert the <paramref name="potentialFilePath"/> into a <see cref="FilePath"/>.
		/// If the conversion is successful, the created <see cref="FilePath"/> is returned.
		/// If the conversion is not successful, then an error message describing the failure is returned.
		/// No exception is thrown.
		/// </summary>
		/// <param name="potentialFilePath">A string that might or might not be a valid filesystem path.</param>
		/// <returns>A tuple that contains the new <see cref="FilePath"/> if the conversion was successful, or an error message if it was not.</returns>
		public static ErrorTuple<FilePath> TryParse(string potentialFilePath) {
			Contract.Ensures(Contract.Result<ErrorTuple<FilePath>>() != null);

			if (potentialFilePath.IsNullOrWhitespace()) {
				return new ErrorTuple<FilePath>("The potential file path was empty.");
			}

			try {
				return ErrorTuple.Create(FilePath.Parse(potentialFilePath));
			}
			catch {
				return new ErrorTuple<FilePath>("The path '{0}' is not a legal file path.", potentialFilePath);
			}
		}

	}
}
