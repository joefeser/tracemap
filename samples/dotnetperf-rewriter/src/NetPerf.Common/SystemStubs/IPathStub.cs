using System;
using System.Security;
using System.IO;

namespace NetPerf.Common.SystemStubs {
	[MockForClass(typeof(PathStub))]
	public interface IPathStub {
		///<summary>
		///Provides a platform-specific alternate character used to separate directory
		///levels in a path string that reflects a hierarchical file system organization.
		///</summary>
		char AltDirectorySeparatorChar {
			get;
		}
		///<summary>
		///Provides a platform-specific character used to separate directory levels
		///in a path string that reflects a hierarchical file system organization.
		///</summary>
		char DirectorySeparatorChar {
			get;
		}
		///<summary>
		///Provides a platform-specific array of characters that cannot be specified
		///in path string arguments passed to members of the System.IO.Path class.
		///</summary>
		///<returns>
		///A character array of invalid path characters for the current platform.
		///</returns>
		[Obsolete("Please use GetInvalidPathChars or GetInvalidFileNameChars instead.")]
		char[] InvalidPathChars {
			get;
		}
		///<summary>
		///A platform-specific separator character used to separate path strings in
		///environment variables.
		///</summary>
		char PathSeparator {
			get;
		}
		///<summary>
		///Provides a platform-specific volume separator character.
		///</summary>
		char VolumeSeparatorChar {
			get;
		}

		///<summary>
		///Changes the extension of a path string.
		///</summary>
		///
		///<param name="path">
		///The path information to modify. The path cannot contain any of the characters
		///defined in System.IO.Path.GetInvalidPathChars().
		///</param>
		///<param name="extension">
		///The new extension (with or without a leading period). Specify null to remove
		///an existing extension from path.
		///</param>
		///<returns>
		///A string containing the modified path information.On Windows-based desktop
		///platforms, if path is null or an empty string (""), the path information
		///is returned unmodified. If extension is null, the returned string contains
		///the specified path with its extension removed. If path has no extension,
		///and extension is not null, the returned path string contains extension appended
		///to the end of path.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///path contains one or more of the invalid characters defined in System.IO.Path.GetInvalidPathChars().
		///</exception>
		string ChangeExtension(string path, string extension);
		///<summary>
		///Combines two strings into a path.
		///</summary>
		///
		///<param name="path1">
		///The first path to combine.
		///</param>
		///<param name="path2">
		///The second path to combine.
		///</param>
		///<returns>
		///A string that contains the combined paths. If one of the specified paths
		///is a zero-length string, this method returns the other path. If path2 contains
		///an absolute path, this method returns path2.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///path1 or path2 contains one or more of the invalid characters defined in
		///System.IO.Path.GetInvalidPathChars().
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path1 or path2 is null.
		///</exception>
		string Combine(string path1, string path2);
		///<summary>
		///Returns the directory information for the specified path string.
		///</summary>
		///
		///<param name="path">
		///The path of a file or directory.
		///</param>
		///<returns>
		///A System.String containing directory information for path, or null if path
		///denotes a root directory, is the empty string (""), or is null. Returns System.String.Empty
		///if path does not contain directory information.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///The path parameter contains invalid characters, is empty, or contains only
		///white spaces.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The path parameter is longer than the system-defined maximum length.
		///</exception>
		[SecuritySafeCritical]
		string GetDirectoryName(string path);
		///<summary>
		///Returns the extension of the specified path string.
		///</summary>
		///
		///<param name="path">
		///The path string from which to get the extension.
		///</param>
		///<returns>
		///A System.String containing the extension of the specified path (including
		///the "."), null, or System.String.Empty. If path is null, GetExtension returns
		///null. If path does not have extension information, GetExtension returns Empty.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///path contains one or more of the invalid characters defined in System.IO.Path.GetInvalidPathChars().
		///</exception>
		string GetExtension(string path);
		///<summary>
		///Returns the file name and extension of the specified path string.
		///</summary>
		///
		///<param name="path">
		///The path string from which to obtain the file name and extension.
		///</param>
		///<returns>
		///A System.String consisting of the characters after the last directory character
		///in path. If the last character of path is a directory or volume separator
		///character, this method returns System.String.Empty. If path is null, this
		///method returns null.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///path contains one or more of the invalid characters defined in System.IO.Path.GetInvalidPathChars().
		///</exception>
		string GetFileName(string path);
		///<summary>
		///Returns the file name of the specified path string without the extension.
		///</summary>
		///
		///<param name="path">
		///The path of the file.
		///</param>
		///<returns>
		///A System.String containing the string returned by System.IO.Path.GetFileName(System.String),
		///minus the last period (.) and all characters following it.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///path contains one or more of the invalid characters defined in System.IO.Path.GetInvalidPathChars().
		///</exception>
		string GetFileNameWithoutExtension(string path);
		///<summary>
		///Returns the absolute path for the specified path string.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to obtain absolute path information.
		///</param>
		///<returns>
		///A string containing the fully qualified location of path, such as "C:\MyFile.txt".
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more of the invalid characters defined in System.IO.Path.GetInvalidPathChars().-or-
		///The system could not retrieve the absolute path.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permissions.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path contains a colon (":") that is not part of a volume identifier (for
		///example, "c:\").
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		[SecuritySafeCritical]
		string GetFullPath(string path);
		///<summary>
		///Gets an array containing the characters that are not allowed in file names.
		///</summary>
		///<returns>
		///An array containing the characters that are not allowed in file names.
		///</returns>
		char[] GetInvalidFileNameChars();
		///<summary>
		///Gets an array containing the characters that are not allowed in path names.
		///</summary>
		///<returns>
		///An array containing the characters that are not allowed in path names.
		///</returns>
		char[] GetInvalidPathChars();
		///<summary>
		///Gets the root directory information of the specified path.
		///</summary>
		///
		///<param name="path">
		///The path from which to obtain root directory information.
		///</param>
		///<returns>
		///A string containing the root directory of path, such as "C:\", or null if
		///path is null, or an empty string if path does not contain root directory
		///information.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///path contains one or more of the invalid characters defined in System.IO.Path.GetInvalidPathChars().-or-
		///System.String.Empty was passed to path.
		///</exception>
		[SecuritySafeCritical]
		string GetPathRoot(string path);
		///<summary>
		///Returns a random folder name or file name.
		///</summary>
		///<returns>
		///A random folder name or file name.
		///</returns>
		string GetRandomFileName();
		///<summary>
		///Creates a uniquely named, zero-byte temporary file on disk and returns the
		///full path of that file.
		///</summary>
		///<returns>
		///A System.String containing the full path of the temporary file.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurs, such as no unique temporary file name is available.-
		///or -This method was unable to create a temporary file.
		///</exception>
		[SecuritySafeCritical]
		string GetTempFileName();
		///<summary>
		///Returns the path of the current system's temporary folder.
		///</summary>
		///<returns>
		///A System.String containing the path information of a temporary directory.
		///</returns>
		///
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permissions.
		///</exception>
		[SecuritySafeCritical]
		string GetTempPath();
		///<summary>
		///Determines whether a path includes a file name extension.
		///</summary>
		///
		///<param name="path">
		///The path to search for an extension.
		///</param>
		///<returns>
		///true if the characters that follow the last directory separator (\\ or /)
		///or volume separator (:) in the path include a period (.) followed by one
		///or more characters; otherwise, false.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///path contains one or more of the invalid characters defined in System.IO.Path.GetInvalidPathChars().
		///</exception>
		bool HasExtension(string path);
		///<summary>
		///Gets a value indicating whether the specified path string contains absolute
		///or relative path information.
		///</summary>
		///
		///<param name="path">
		///The path to test.
		///</param>
		///<returns>
		///true if path contains an absolute path; otherwise, false.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///path contains one or more of the invalid characters defined in System.IO.Path.GetInvalidPathChars().
		///</exception>
		bool IsPathRooted(string path);
	}
}
