using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Common.SystemStubs;
using System.Security.AccessControl;

namespace System.IO {
	public static class DirectoryStub {
#pragma warning disable 0649 // justification: this field is set by reflection
		private static IDirectoryStub _interfaceStub;
#pragma warning restore
		///<summary>
		///Creates all directories and subdirectories as specified by path.
		///</summary>
		///
		///<param name="path">
		///The directory path to create.
		///</param>
		///<returns>
		///A System.IO.DirectoryInfo as specified by path.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///The directory specified by path is read-only.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///-or- path is prefixed with, or contains only a colon character (:).
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path contains a colon character (:) that is not part of a drive label ("C:\").
		///</exception>
		public static DirectoryInfo CreateDirectory(string path) {
			if (_interfaceStub != null) {
				return _interfaceStub.CreateDirectory(path);
			}
			else {
				return Directory.CreateDirectory(path);
			}
		}
		///<summary>
		///Creates all the directories in the specified path, applying the specified
		///Windows security.
		///</summary>
		///
		///<param name="path">
		///The directory to create.
		///</param>
		///<param name="directorySecurity">
		///The access control to apply to the directory.
		///</param>
		///<returns>
		///A System.IO.DirectoryInfo object representing the newly created directory.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///The directory specified by path is read-only.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///-or- path is prefixed with, or contains only a colon character (:).
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path contains a colon character (:) that is not part of a drive label ("C:\").
		///</exception>
		public static DirectoryInfo CreateDirectory(string path, DirectorySecurity directorySecurity) {
			if (_interfaceStub != null) {
				return _interfaceStub.CreateDirectory(path, directorySecurity);
			}
			else {
				return Directory.CreateDirectory(path, directorySecurity);
			}
		}
		///<summary>
		///Deletes an empty directory from a specified path.
		///</summary>
		///
		///<param name="path">
		///The name of the empty directory to remove. This directory must be writable
		///or empty.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///A file with the same name and location specified by path exists.  -or- The
		///directory is the application's current working directory.  -or- The directory
		///specified by path is not empty.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path does not exist or could not be found.  -or- The specified
		///path is invalid (for example, it is on an unmapped drive).
		///</exception>
		public static void Delete(string path) {
			if (_interfaceStub != null) {
				_interfaceStub.Delete(path);
			}
			else {
				Directory.Delete(path);
			}
		}
		///<summary>
		///Deletes the specified directory and, if indicated, any subdirectories in
		///the directory.
		///</summary>
		///
		///<param name="path">
		///The name of the directory to remove.
		///</param>
		///<param name="recursive">
		///true to remove directories, subdirectories, and files in path; otherwise,
		///false.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///A file with the same name and location specified by path exists.  -or- The
		///directory specified by path is read-only, or recursive is false and path
		///is not an empty directory.  -or- The directory is the application's current
		///working directory.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path does not exist or could not be found.  -or- The specified
		///path is invalid (for example, it is on an unmapped drive).
		///</exception>
		public static void Delete(string path, bool recursive) {
			if (_interfaceStub != null) {
				_interfaceStub.Delete(path, recursive);
			}
			else {
				Directory.Delete(path, recursive);
			}
		}
		///<summary>
		///Determines whether the given path refers to an existing directory on disk.
		///</summary>
		///
		///<param name="path">
		///The path to test.
		///</param>
		///<returns>
		///true if path refers to an existing directory; otherwise, false.
		///</returns>
		public static bool Exists(string path) {
			if (_interfaceStub != null) {
				return _interfaceStub.Exists(path);
			}
			else {
				return Directory.Exists(path);
			}
		}
		///<summary>
		///Gets a System.Security.AccessControl.DirectorySecurity object that encapsulates
		///the access control list (ACL) entries for a specified directory.
		///</summary>
		///
		///<param name="path">
		///The path to a directory containing a System.Security.AccessControl.DirectorySecurity
		///object that describes the file's access control list (ACL) information.
		///</param>
		///<returns>
		///A System.Security.AccessControl.DirectorySecurity object that encapsulates
		///the access control rules for the file described by the path parameter.
		///</returns>
		///
		///<exception cref="System.ArgumentNullException">
		///The path parameter is null.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the directory.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///The current operating system is not Microsoft Windows 2000 or later.
		///</exception>
		///<exception cref="System.SystemException">
		///The directory could not be found.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The path parameter specified a directory that is read-only.  -or- This operation
		///is not supported on the current platform.  -or- The caller does not have
		///the required permission.
		///</exception>
		public static DirectorySecurity GetAccessControl(string path) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetAccessControl(path);
			}
			else {
				return Directory.GetAccessControl(path);
			}
		}
		///<summary>
		///Gets a System.Security.AccessControl.DirectorySecurity object that encapsulates
		///the specified type of access control list (ACL) entries for a specified directory.
		///</summary>
		///
		///<param name="path">
		///The path to a directory containing a System.Security.AccessControl.DirectorySecurity
		///object that describes the file's access control list (ACL) information.
		///</param>
		///<param name="includeSections">
		///One of the System.Security.AccessControl.AccessControlSections values that
		///specifies the type of access control list (ACL) information to receive.
		///</param>
		///<returns>
		///A System.Security.AccessControl.DirectorySecurity object that encapsulates
		///the access control rules for the file described by the path parameter.
		///</returns>
		///
		///<exception cref="System.ArgumentNullException">
		///The path parameter is null.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the directory.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///The current operating system is not Microsoft Windows 2000 or later.
		///</exception>
		///<exception cref="System.SystemException">
		///The directory could not be found.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The path parameter specified a directory that is read-only.  -or- This operation
		///is not supported on the current platform.  -or- The caller does not have
		///the required permission.
		///</exception>
		public static DirectorySecurity GetAccessControl(string path, AccessControlSections includeSections) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetAccessControl(path, includeSections);
			}
			else {
				return Directory.GetAccessControl(path, includeSections);
			}
		}
		///<summary>
		///Gets the creation date and time of a directory.
		///</summary>
		///
		///<param name="path">
		///The path of the directory.
		///</param>
		///<returns>
		///A System.DateTime structure set to the creation date and time for the specified
		///directory. This value is expressed in local time.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		public static DateTime GetCreationTime(string path) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetCreationTime(path);
			}
			else {
				return Directory.GetCreationTime(path);
			}
		}
		///<summary>
		///Gets the creation date and time, in Coordinated Universal Time (UTC) format,
		///of a directory.
		///</summary>
		///
		///<param name="path">
		///The path of the directory.
		///</param>
		///<returns>
		///A System.DateTime structure set to the creation date and time for the specified
		///directory. This value is expressed in UTC time.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		public static DateTime GetCreationTimeUtc(string path) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetCreationTimeUtc(path);
			}
			else {
				return Directory.GetCreationTimeUtc(path);
			}
		}
		///<summary>
		///Gets the current working directory of the application.
		///</summary>
		///<returns>
		///A string containing the path of the current working directory.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///The operating system is Windows CE, which does not have current directory
		///functionality.  This method is available in the .NET Compact Framework, but
		///is not currently supported.
		///</exception>
		public static string GetCurrentDirectory() {
			if (_interfaceStub != null) {
				return _interfaceStub.GetCurrentDirectory();
			}
			else {
				return Directory.GetCurrentDirectory();
			}
		}
		///<summary>
		///Gets the names of subdirectories in the specified directory.
		///</summary>
		///
		///<param name="path">
		///The path for which an array of subdirectory names is returned.
		///</param>
		///<returns>
		///An array of type String containing the names of subdirectories in path.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.IOException">
		///path is a file name.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		public static string[] GetDirectories(string path) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetDirectories(path);
			}
			else {
				return Directory.GetDirectories(path);
			}
		}
		///<summary>
		///Gets an array of directories matching the specified search pattern from the
		///current directory.
		///</summary>
		///
		///<param name="path">
		///The path to search.
		///</param>
		///<param name="searchPattern">
		///The search string to match against the names of files in path. The parameter
		///cannot end in two periods ("..") or contain two periods ("..") followed by
		///System.IO.Path.DirectorySeparatorChar or System.IO.Path.AltDirectorySeparatorChar,
		///nor can it contain any of the characters in System.IO.Path.InvalidPathChars.
		///</param>
		///<returns>
		///A String array of directories matching the search pattern.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///-or- searchPattern does not contain a valid pattern.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path or searchPattern is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.IOException">
		///path is a file name.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		public static string[] GetDirectories(string path, string searchPattern) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetDirectories(path, searchPattern);
			}
			else {
				return Directory.GetDirectories(path, searchPattern);
			}
		}
		///<summary>
		///Gets an array of directories matching the specified search pattern from the
		///current directory, using a value to determine whether to search subdirectories.
		///</summary>
		///
		///<param name="path">
		///The path to search.
		///</param>
		///<param name="searchPattern">
		///The search string to match against the names of files in path. The parameter
		///cannot end in two periods ("..") or contain two periods ("..") followed by
		///System.IO.Path.DirectorySeparatorChar or System.IO.Path.AltDirectorySeparatorChar,
		///nor can it contain any of the characters in System.IO.Path.InvalidPathChars.
		///</param>
		///<param name="searchOption">
		///One of the System.IO.SearchOption values that specifies whether the search
		///operation should include all subdirectories or only the current directory.
		///</param>
		///<returns>
		///A String array of directories matching the search pattern.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///-or- searchPattern does not contain a valid pattern.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path or searchPattern is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.IOException">
		///path is a file name.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		public static string[] GetDirectories(string path, string searchPattern, SearchOption searchOption) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetDirectories(path, searchPattern, searchOption);
			}
			else {
				return Directory.GetDirectories(path, searchPattern, searchOption);
			}
		}
		///<summary>
		///Returns the volume information, root information, or both for the specified
		///path.
		///</summary>
		///
		///<param name="path">
		///The path of a file or directory.
		///</param>
		///<returns>
		///A string containing the volume information, root information, or both for
		///the specified path.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		public static string GetDirectoryRoot(string path) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetDirectoryRoot(path);
			}
			else {
				return Directory.GetDirectoryRoot(path);
			}
		}
		///<summary>
		///Returns the names of files in the specified directory.
		///</summary>
		///
		///<param name="path">
		///The directory from which to retrieve the files.
		///</param>
		///<returns>
		///A String array of file names in the specified directory.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///path is a file name.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		public static string[] GetFiles(string path) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetFiles(path);
			}
			else {
				return Directory.GetFiles(path);
			}
		}
		///<summary>
		///Returns the names of files in the specified directory that match the specified
		///search pattern.
		///</summary>
		///
		///<param name="path">
		///The directory to search.
		///</param>
		///<param name="searchPattern">
		///The search string to match against the names of files in path. The parameter
		///cannot end in two periods ("..") or contain two periods ("..") followed by
		///System.IO.Path.DirectorySeparatorChar or System.IO.Path.AltDirectorySeparatorChar,
		///nor can it contain any of the characters in System.IO.Path.InvalidPathChars.
		///</param>
		///<returns>
		///A String array containing the names of files in the specified directory that
		///match the specified search pattern.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///-or- searchPattern does not contain a valid pattern.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path or searchPattern is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		public static string[] GetFiles(string path, string searchPattern) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetFiles(path, searchPattern);
			}
			else {
				return Directory.GetFiles(path, searchPattern);
			}
		}
		///<summary>
		///Returns the names of files in the specified directory that match the specified
		///search pattern, using a value to determine whether to search subdirectories.
		///</summary>
		///
		///<param name="path">
		///The directory to search.
		///</param>
		///<param name="searchPattern">
		///The search string to match against the names of files in path. The parameter
		///cannot end in two periods ("..") or contain two periods ("..") followed by
		///System.IO.Path.DirectorySeparatorChar or System.IO.Path.AltDirectorySeparatorChar,
		///nor can it contain any of the characters in System.IO.Path.InvalidPathChars.
		///</param>
		///<param name="searchOption">
		///One of the System.IO.SearchOption values that specifies whether the search
		///operation should include all subdirectories or only the current directory.
		///</param>
		///<returns>
		///A String array containing the names of files in the specified directory that
		///match the specified search pattern. File names include the full path.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///path is a file name.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///-or- searchPattern does not contain a valid pattern.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path or searchpattern is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		public static string[] GetFiles(string path, string searchPattern, SearchOption searchOption) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetFiles(path, searchPattern, searchOption);
			}
			else {
				return Directory.GetFiles(path, searchPattern, searchOption);
			}
		}
		///<summary>
		///Returns the names of all files and subdirectories in the specified directory.
		///</summary>
		///
		///<param name="path">
		///The directory for which file and subdirectory names are returned.
		///</param>
		///<returns>
		///A String array containing the names of file system entries in the specified
		///directory.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.IOException">
		///path is a file name.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		public static string[] GetFileSystemEntries(string path) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetFileSystemEntries(path);
			}
			else {
				return Directory.GetFileSystemEntries(path);
			}
		}
		///<summary>
		///Returns an array of file system entries matching the specified search criteria.
		///</summary>
		///
		///<param name="path">
		///The path to be searched.
		///</param>
		///<param name="searchPattern">
		///The search string to match against the names of files in path. The searchPattern
		///parameter cannot end in two periods ("..") or contain two periods ("..")
		///followed by System.IO.Path.DirectorySeparatorChar or System.IO.Path.AltDirectorySeparatorChar,
		///nor can it contain any of the characters in System.IO.Path.InvalidPathChars.
		///</param>
		///<returns>
		///A String array of file system entries matching the search criteria.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///-or- searchPattern does not contain a valid pattern.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path or searchPattern is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.IOException">
		///path is a file name.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		public static string[] GetFileSystemEntries(string path, string searchPattern) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetFileSystemEntries(path, searchPattern);
			}
			else {
				return Directory.GetFileSystemEntries(path, searchPattern);
			}
		}
		///<summary>
		///Returns the date and time the specified file or directory was last accessed.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to obtain access date and time information.
		///</param>
		///<returns>
		///A System.DateTime structure set to the date and time the specified file or
		///directory was last accessed. This value is expressed in local time.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///The path parameter is in an invalid format.
		///</exception>
		public static DateTime GetLastAccessTime(string path) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetLastAccessTime(path);
			}
			else {
				return Directory.GetLastAccessTime(path);
			}
		}
		///<summary>
		///Returns the date and time, in Coordinated Universal Time (UTC) format, that
		///the specified file or directory was last accessed.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to obtain access date and time information.
		///</param>
		///<returns>
		///A System.DateTime structure set to the date and time the specified file or
		///directory was last accessed. This value is expressed in UTC time.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///The path parameter is in an invalid format.
		///</exception>
		public static DateTime GetLastAccessTimeUtc(string path) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetLastAccessTimeUtc(path);
			}
			else {
				return Directory.GetLastAccessTimeUtc(path);
			}
		}
		///<summary>
		///Returns the date and time the specified file or directory was last written
		///to.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to obtain modification date and time information.
		///</param>
		///<returns>
		///A System.DateTime structure set to the date and time the specified file or
		///directory was last written to. This value is expressed in local time.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		public static DateTime GetLastWriteTime(string path) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetLastWriteTime(path);
			}
			else {
				return Directory.GetLastWriteTime(path);
			}
		}
		///<summary>
		///Returns the date and time, in Coordinated Universal Time (UTC) format, that
		///the specified file or directory was last written to.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to obtain modification date and time information.
		///</param>
		///<returns>
		///A System.DateTime structure set to the date and time the specified file or
		///directory was last written to. This value is expressed in UTC time.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		public static DateTime GetLastWriteTimeUtc(string path) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetLastWriteTimeUtc(path);
			}
			else {
				return Directory.GetLastWriteTimeUtc(path);
			}
		}
		///<summary>
		///Retrieves the names of the logical drives on this computer in the form "<drive
		///letter>:\".
		///</summary>
		///<returns>
		///The logical drives on this computer.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occured (for example, a disk error).
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		public static string[] GetLogicalDrives() {
			if (_interfaceStub != null) {
				return _interfaceStub.GetLogicalDrives();
			}
			else {
				return Directory.GetLogicalDrives();
			}
		}
		///<summary>
		///Retrieves the parent directory of the specified path, including both absolute
		///and relative paths.
		///</summary>
		///
		///<param name="path">
		///The path for which to retrieve the parent directory.
		///</param>
		///<returns>
		///The parent directory, or null if path is the root directory, including the
		///root of a UNC server or share name.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///The directory specified by path is read-only.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path was not found.
		///</exception>
		public static DirectoryInfo GetParent(string path) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetParent(path);
			}
			else {
				return Directory.GetParent(path);
			}
		}
		///<summary>
		///Moves a file or a directory and its contents to a new location.
		///</summary>
		///
		///<param name="sourceDirName">
		///The path of the file or directory to move.
		///</param>
		///<param name="destDirName">
		///The path to the new location for sourceDirName. If sourceDirName is a file,
		///then destDirName must also be a file name.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An attempt was made to move a directory to a different volume. -or- destDirName
		///already exists. -or- The sourceDirName and destDirName parameters refer to
		///the same file or directory.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///sourceDirName or destDirName is a zero-length string, contains only white
		///space, or contains one or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///sourceDirName or destDirName is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The path specified by sourceDirName is invalid (for example, it is on an
		///unmapped drive).
		///</exception>
		public static void Move(string sourceDirName, string destDirName) {
			if (_interfaceStub != null) {
				_interfaceStub.Move(sourceDirName, destDirName);
			}
			else {
				Directory.Move(sourceDirName, destDirName);
			}
		}
		///<summary>
		///Applies access control list (ACL) entries described by a System.Security.AccessControl.DirectorySecurity
		///object to the specified directory.
		///</summary>
		///
		///<param name="path">
		///A directory to add or remove access control list (ACL) entries from.
		///</param>
		///<param name="directorySecurity">
		///A System.Security.AccessControl.DirectorySecurity object that describes an
		///ACL entry to apply to the directory described by the path parameter.
		///</param>
		///
		///<exception cref="System.ArgumentNullException">
		///The directorySecurity parameter is null.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The directory could not be found.
		///</exception>
		///<exception cref="System.ArgumentException">
		///The path was invalid.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The current process does not have access to open the file.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///The current operating system is not Microsoft Windows 2000 or later.
		///</exception>
		public static void SetAccessControl(string path, DirectorySecurity directorySecurity) {
			if (_interfaceStub != null) {
				_interfaceStub.SetAccessControl(path, directorySecurity);
			}
			else {
				Directory.SetAccessControl(path, directorySecurity);
			}
		}
		///<summary>
		///Sets the creation date and time for the specified file or directory.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to set the creation date and time information.
		///</param>
		///<param name="creationTime">
		///A System.DateTime containing the value to set for the creation date and time
		///of path. This value is expressed in local time.
		///</param>
		///
		///<exception cref="System.IO.FileNotFoundException">
		///The specified path was not found.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///creationTime specifies a value outside the range of dates or times permitted
		///for this operation.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///The current operating system is not Microsoft Windows NT or later.
		///</exception>
		public static void SetCreationTime(string path, DateTime creationTime) {
			if (_interfaceStub != null) {
				_interfaceStub.SetCreationTime(path, creationTime);
			}
			else {
				Directory.SetCreationTime(path, creationTime);
			}
		}
		///<summary>
		///Sets the creation date and time, in Coordinated Universal Time (UTC) format,
		///for the specified file or directory.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to set the creation date and time information.
		///</param>
		///<param name="creationTimeUtc">
		///A System.DateTime containing the value to set for the creation date and time
		///of path. This value is expressed in UTC time.
		///</param>
		///
		///<exception cref="System.IO.FileNotFoundException">
		///The specified path was not found.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///creationTime specifies a value outside the range of dates or times permitted
		///for this operation.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///The current operating system is not Microsoft Windows NT or later.
		///</exception>
		public static void SetCreationTimeUtc(string path, DateTime creationTimeUtc) {
			if (_interfaceStub != null) {
				_interfaceStub.SetCreationTimeUtc(path, creationTimeUtc);
			}
			else {
				Directory.SetCreationTimeUtc(path, creationTimeUtc);
			}
		}
		///<summary>
		///Sets the application's current working directory to the specified directory.
		///</summary>
		///
		///<param name="path">
		///The path to which the current working directory is set.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An IO error occurred.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission to access unmanaged code.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The specified path was not found.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified directory was not found.
		///</exception>
		public static void SetCurrentDirectory(string path) {
			if (_interfaceStub != null) {
				_interfaceStub.SetCurrentDirectory(path);
			}
			else {
				Directory.SetCurrentDirectory(path);
			}
		}
		///<summary>
		///Sets the date and time the specified file or directory was last accessed.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to set the access date and time information.
		///</param>
		///<param name="lastAccessTime">
		///A System.DateTime containing the value to set for the access date and time
		///of path. This value is expressed in local time.
		///</param>
		///
		///<exception cref="System.IO.FileNotFoundException">
		///The specified path was not found.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///The current operating system is not Microsoft Windows NT or later.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///lastAccessTime specifies a value outside the range of dates or times permitted
		///for this operation.
		///</exception>
		public static void SetLastAccessTime(string path, DateTime lastAccessTime) {
			if (_interfaceStub != null) {
				_interfaceStub.SetLastAccessTime(path, lastAccessTime);
			}
			else {
				Directory.SetLastAccessTime(path, lastAccessTime);
			}
		}
		///<summary>
		///Sets the date and time, in Coordinated Universal Time (UTC) format, that
		///the specified file or directory was last accessed.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to set the access date and time information.
		///</param>
		///<param name="lastAccessTimeUtc">
		///A System.DateTime containing the value to set for the access date and time
		///of path. This value is expressed in UTC time.
		///</param>
		///
		///<exception cref="System.IO.FileNotFoundException">
		///The specified path was not found.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///The current operating system is not Microsoft Windows NT or later.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///lastAccessTimeUtc specifies a value outside the range of dates or times permitted
		///for this operation.
		///</exception>
		public static void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) {
			if (_interfaceStub != null) {
				_interfaceStub.SetLastAccessTimeUtc(path, lastAccessTimeUtc);
			}
			else {
				Directory.SetLastAccessTimeUtc(path, lastAccessTimeUtc);
			}
		}
		///<summary>
		///Sets the date and time a directory was last written to.
		///</summary>
		///
		///<param name="path">
		///The path of the directory.
		///</param>
		///<param name="lastWriteTime">
		///The date and time the directory was last written to. This value is expressed
		///in local time.
		///</param>
		///
		///<exception cref="System.IO.FileNotFoundException">
		///The specified path was not found.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///The current operating system is not Microsoft Windows NT or later.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///lastWriteTime specifies a value outside the range of dates or times permitted
		///for this operation.
		///</exception>
		public static void SetLastWriteTime(string path, DateTime lastWriteTime) {
			if (_interfaceStub != null) {
				_interfaceStub.SetLastWriteTime(path, lastWriteTime);
			}
			else {
				Directory.SetLastWriteTime(path, lastWriteTime);
			}
		}
		///<summary>
		///Sets the date and time, in Coordinated Universal Time (UTC) format, that
		///a directory was last written to.
		///</summary>
		///
		///<param name="path">
		///The path of the directory.
		///</param>
		///<param name="lastWriteTimeUtc">
		///The date and time the directory was last written to. This value is expressed
		///in UTC time.
		///</param>
		///
		///<exception cref="System.IO.FileNotFoundException">
		///The specified path was not found.
		///</exception>
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///The current operating system is not Microsoft Windows NT or later.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///lastWriteTimeUtc specifies a value outside the range of dates or times permitted
		///for this operation.
		///</exception>
		public static void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc) {
			if (_interfaceStub != null) {
				_interfaceStub.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
			}
			else {
				Directory.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
			}
		}
	}
}
