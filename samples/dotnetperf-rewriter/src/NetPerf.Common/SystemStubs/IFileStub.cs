using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.AccessControl;
using System.Text;

namespace NetPerf.Common.SystemStubs {
	[MockForClass(typeof(FileStub))]
	public interface IFileStub {

		///<summary>
		///Opens a file, appends the specified string to the file, and then closes the
		///file. If the file does not exist, this method creates a file, writes the
		///specified string to the file, then closes the file.
		///</summary>
		///
		///<param name="path">
		///The file to append the specified string to.
		///</param>
		///<param name="contents">
		///The string to append to the file.
		///</param>
		///
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only.-or- This operation is not supported
		///on the current platform.-or- path specified a directory.-or- The caller does
		///not have the required permission.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		void AppendAllText(string path, string contents);
		///<summary>
		///Appends the specified string to the file, creating the file if it does not
		///already exist.
		///</summary>
		///
		///<param name="path">
		///The file to append the specified string to.
		///</param>
		///<param name="contents">
		///The string to append to the file.
		///</param>
		///<param name="encoding">
		///The character encoding to use.
		///</param>
		///
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only.-or- This operation is not supported
		///on the current platform.-or- path specified a directory.-or- The caller does
		///not have the required permission.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		void AppendAllText(string path, string contents, Encoding encoding);
		///<summary>
		///Creates a System.IO.StreamWriter that appends UTF-8 encoded text to an existing
		///file.
		///</summary>
		///
		///<param name="path">
		///The path to the file to append to.
		///</param>
		///<returns>
		///A StreamWriter that appends UTF-8 encoded text to an existing file.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		StreamWriter AppendText(string path);
		///<summary>
		///Copies an existing file to a new file. Overwriting a file of the same name
		///is not allowed.
		///</summary>
		///
		///<param name="sourceFileName">
		///The file to copy.
		///</param>
		///<param name="destFileName">
		///The name of the destination file. This cannot be a directory or an existing
		///file.
		///</param>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentException">
		///sourceFileName or destFileName is a zero-length string, contains only white
		///space, or contains one or more invalid characters as defined by System.IO.Path.InvalidPathChars.-or-
		///sourceFileName or destFileName specifies a directory.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///sourceFileName or destFileName is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The path specified in sourceFileName or destFileName is invalid (for example,
		///it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///sourceFileName was not found.
		///</exception>
		///<exception cref="System.IO.IOException">
		///destFileName exists.-or- An I/O error has occurred.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///sourceFileName or destFileName is in an invalid format.
		///</exception>
		void Copy(string sourceFileName, string destFileName);
		///<summary>
		///Copies an existing file to a new file. Overwriting a file of the same name
		///is allowed.
		///</summary>
		///
		///<param name="sourceFileName">
		///The file to copy.
		///</param>
		///<param name="destFileName">
		///The name of the destination file. This cannot be a directory.
		///</param>
		///<param name="overwrite">
		///true if the destination file can be overwritten; otherwise, false.
		///</param>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission. -or-destFileName is read-only.
		///</exception>
		///<exception cref="System.ArgumentException">
		///sourceFileName or destFileName is a zero-length string, contains only white
		///space, or contains one or more invalid characters as defined by System.IO.Path.InvalidPathChars.-or-
		///sourceFileName or destFileName specifies a directory.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///sourceFileName or destFileName is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The path specified in sourceFileName or destFileName is invalid (for example,
		///it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///sourceFileName was not found.
		///</exception>
		///<exception cref="System.IO.IOException">
		///destFileName exists and overwrite is false.-or- An I/O error has occurred.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///sourceFileName or destFileName is in an invalid format.
		///</exception>
		void Copy(string sourceFileName, string destFileName, bool overwrite);
		///<summary>
		///Creates or overwrites a file in the specified path.
		///</summary>
		///
		///<param name="path">
		///The path and name of the file to create.
		///</param>
		///<returns>
		///A System.IO.FileStream that provides read/write access to the file specified
		///in path.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.-or- path specified a file
		///that is read-only.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while creating the file.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		FileStream Create(string path);
		///<summary>
		///Creates or overwrites the specified file.
		///</summary>
		///
		///<param name="path">
		///The name of the file.
		///</param>
		///<param name="bufferSize">
		///The number of bytes buffered for reads and writes to the file.
		///</param>
		///<returns>
		///A System.IO.FileStream with the specified buffer size that provides read/write
		///access to the file specified in path.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.-or- path specified a file
		///that is read-only.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while creating the file.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		FileStream Create(string path, int bufferSize);
		///<summary>
		///Creates or overwrites the specified file, specifying a buffer size and a
		///System.IO.FileOptions value that describes how to create or overwrite the
		///file.
		///</summary>
		///
		///<param name="path">
		///The name of the file.
		///</param>
		///<param name="bufferSize">
		///The number of bytes buffered for reads and writes to the file.
		///</param>
		///<param name="options">
		///One of the System.IO.FileOptions values that describes how to create or overwrite
		///the file.
		///</param>
		///<returns>
		///A new file with the specified buffer size.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.-or- path specified a file
		///that is read-only. -or-System.IO.FileOptions.Encrypted is specified for options
		///and file encryption is not supported on the current platform.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while creating the file.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.-or- path specified a file
		///that is read-only.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.-or- path specified a file
		///that is read-only.
		///</exception>
		[SecuritySafeCritical]
		FileStream Create(string path, int bufferSize, FileOptions options);
		///<summary>
		///Creates or overwrites the specified file with the specified buffer size,
		///file options, and file security.
		///</summary>
		///
		///<param name="path">
		///The name of the file.
		///</param>
		///<param name="bufferSize">
		///The number of bytes buffered for reads and writes to the file.
		///</param>
		///<param name="options">
		///One of the System.IO.FileOptions values that describes how to create or overwrite
		///the file.
		///</param>
		///<param name="fileSecurity">
		///One of the System.Security.AccessControl.FileSecurity values that determines
		///the access control and audit security for the file.
		///</param>
		///<returns>
		///A new file with the specified buffer size, file options, and file security.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.-or- path specified a file
		///that is read-only.-or-System.IO.FileOptions.Encrypted is specified for options
		///and file encryption is not supported on the current platform.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while creating the file.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.-or- path specified a file
		///that is read-only.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.-or- path specified a file
		///that is read-only.
		///</exception>
		FileStream Create(string path, int bufferSize, FileOptions options, FileSecurity fileSecurity);
		///<summary>
		///Creates or opens a file for writing UTF-8 encoded text.
		///</summary>
		///
		///<param name="path">
		///The file to be opened for writing.
		///</param>
		///<returns>
		///A System.IO.StreamWriter that writes to the specified file using UTF-8 encoding.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		StreamWriter CreateText(string path);
		///<summary>
		///Decrypts a file that was encrypted by the current account using the System.IO.File.Encrypt(System.String)
		///method.
		///</summary>
		///
		///<param name="path">
		///A path that describes a file to decrypt.
		///</param>
		///
		///<exception cref="System.ArgumentException">
		///The path parameter is a zero-length string, contains only white space, or
		///contains one or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///The path parameter is null.
		///</exception>
		///<exception cref="System.IO.DriveNotFoundException">
		///An invalid drive was specified.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file described by the path parameter could not be found.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file. For example, the encrypted
		///file is already open. -or-This operation is not supported on the current
		///platform.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///The current operating system is not Windows NT or later.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///The file system is not NTFS.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The path parameter specified a file that is read-only.-or- This operation
		///is not supported on the current platform.-or- The path parameter specified
		///a directory.-or- The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		void Decrypt(string path);
		///<summary>
		///Deletes the specified file. An exception is not thrown if the specified file
		///does not exist.
		///</summary>
		///
		///<param name="path">
		///The name of the file to be deleted. Wildcard characters are not supported.
		///</param>
		///
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid, (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///The specified file is in use.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.-or- path is a directory.-or-
		///path specified a read-only file.
		///</exception>
		[SecuritySafeCritical]
		void Delete(string path);
		///<summary>
		///Encrypts a file so that only the account used to encrypt the file can decrypt
		///it.
		///</summary>
		///
		///<param name="path">
		///A path that describes a file to encrypt.
		///</param>
		///
		///<exception cref="System.ArgumentException">
		///The path parameter is a zero-length string, contains only white space, or
		///contains one or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///The path parameter is null.
		///</exception>
		///<exception cref="System.IO.DriveNotFoundException">
		///An invalid drive was specified.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file described by the path parameter could not be found.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.-or-This operation is not supported
		///on the current platform.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///The current operating system is not Windows NT or later.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///The file system is not NTFS.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The path parameter specified a file that is read-only.-or- This operation
		///is not supported on the current platform.-or- The path parameter specified
		///a directory.-or- The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		void Encrypt(string path);
		///<summary>
		///Determines whether the specified file exists.
		///</summary>
		///
		///<param name="path">
		///The file to check.
		///</param>
		///<returns>
		///true if the caller has the required permissions and path contains the name
		///of an existing file; otherwise, false. This method also returns false if
		///path is null, an invalid path, or a zero-length string. If the caller does
		///not have sufficient permissions to read the specified file, no exception
		///is thrown and the method returns false regardless of the existence of path.
		///</returns>
		[SecuritySafeCritical]
		bool Exists(string path);
		///<summary>
		///Gets a System.Security.AccessControl.FileSecurity object that encapsulates
		///the access control list (ACL) entries for a specified file.
		///</summary>
		///
		///<param name="path">
		///The path to a file containing a System.Security.AccessControl.FileSecurity
		///object that describes the file's access control list (ACL) information.
		///</param>
		///<returns>
		///A System.Security.AccessControl.FileSecurity object that encapsulates the
		///access control rules for the file described by the path parameter.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.Runtime.InteropServices.SEHException">
		///The path parameter is null.
		///</exception>
		///<exception cref="System.SystemException">
		///The file could not be found.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The path parameter specified a file that is read-only.-or- This operation
		///is not supported on the current platform.-or- The path parameter specified
		///a directory.-or- The caller does not have the required permission.
		///</exception>
		FileSecurity GetAccessControl(string path);
		///<summary>
		///Gets a System.Security.AccessControl.FileSecurity object that encapsulates
		///the specified type of access control list (ACL) entries for a particular
		///file.
		///</summary>
		///
		///<param name="path">
		///The path to a file containing a System.Security.AccessControl.FileSecurity
		///object that describes the file's access control list (ACL) information.
		///</param>
		///<param name="includeSections">
		///One of the System.Security.AccessControl.AccessControlSections values that
		///specifies the type of access control list (ACL) information to receive.
		///</param>
		///<returns>
		///A System.Security.AccessControl.FileSecurity object that encapsulates the
		///access control rules for the file described by the path parameter.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.Runtime.InteropServices.SEHException">
		///The path parameter is null.
		///</exception>
		///<exception cref="System.SystemException">
		///The file could not be found.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The path parameter specified a file that is read-only.-or- This operation
		///is not supported on the current platform.-or- The path parameter specified
		///a directory.-or- The caller does not have the required permission.
		///</exception>
		FileSecurity GetAccessControl(string path, AccessControlSections includeSections);
		///<summary>
		///Gets the System.IO.FileAttributes of the file on the path.
		///</summary>
		///
		///<param name="path">
		///The path to the file.
		///</param>
		///<returns>
		///The System.IO.FileAttributes of the file on the path.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///path is empty, contains only white spaces, or contains invalid characters.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///path represents a file and is invalid, such as being on an unmapped drive,
		///or the file cannot be found.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///path represents a directory and is invalid, such as being on an unmapped
		///drive, or the directory cannot be found.
		///</exception>
		[SecuritySafeCritical]
		FileAttributes GetAttributes(string path);
		///<summary>
		///Returns the creation date and time of the specified file or directory.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to obtain creation date and time information.
		///</param>
		///<returns>
		///A System.DateTime structure set to the creation date and time for the specified
		///file or directory. This value is expressed in local time.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		DateTime GetCreationTime(string path);
		///<summary>
		///Returns the creation date and time, in coordinated universal time (UTC),
		///of the specified file or directory.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to obtain creation date and time information.
		///</param>
		///<returns>
		///A System.DateTime structure set to the creation date and time for the specified
		///file or directory. This value is expressed in UTC time.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		DateTime GetCreationTimeUtc(string path);
		///<summary>
		///Returns the date and time the specified file or directory was last accessed.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to obtain access date and time information.
		///</param>
		///<returns>
		///A System.DateTime structure set to the date and time that the specified file
		///or directory was last accessed. This value is expressed in local time.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		DateTime GetLastAccessTime(string path);
		///<summary>
		///Returns the date and time, in coordinated universal time (UTC), that the
		///specified file or directory was last accessed.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to obtain access date and time information.
		///</param>
		///<returns>
		///A System.DateTime structure set to the date and time that the specified file
		///or directory was last accessed. This value is expressed in UTC time.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		DateTime GetLastAccessTimeUtc(string path);
		///<summary>
		///Returns the date and time the specified file or directory was last written
		///to.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to obtain write date and time information.
		///</param>
		///<returns>
		///A System.DateTime structure set to the date and time that the specified file
		///or directory was last written to. This value is expressed in local time.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		DateTime GetLastWriteTime(string path);
		///<summary>
		///Returns the date and time, in coordinated universal time (UTC), that the
		///specified file or directory was last written to.
		///</summary>
		///
		///<param name="path">
		///The file or directory for which to obtain write date and time information.
		///</param>
		///<returns>
		///A System.DateTime structure set to the date and time that the specified file
		///or directory was last written to. This value is expressed in UTC time.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		DateTime GetLastWriteTimeUtc(string path);
		///<summary>
		///Moves a specified file to a new location, providing the option to specify
		///a new file name.
		///</summary>
		///
		///<param name="sourceFileName">
		///The name of the file to move.
		///</param>
		///<param name="destFileName">
		///The new path for the file.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///The destination file already exists.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///sourceFileName or destFileName is null.
		///</exception>
		///<exception cref="System.ArgumentException">
		///sourceFileName or destFileName is a zero-length string, contains only white
		///space, or contains invalid characters as defined in System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///sourceFileName was not found.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The path specified in sourceFileName or destFileName is invalid, (for example,
		///it is on an unmapped drive).
		///</exception>
		///<exception cref="System.NotSupportedException">
		///sourceFileName or destFileName is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		void Move(string sourceFileName, string destFileName);
		///<summary>
		///Opens a System.IO.FileStream on the specified path with read/write access.
		///</summary>
		///
		///<param name="path">
		///The file to open.
		///</param>
		///<param name="mode">
		///A System.IO.FileMode value that specifies whether a file is created if one
		///does not exist, and determines whether the contents of existing files are
		///retained or overwritten.
		///</param>
		///<returns>
		///A System.IO.FileStream opened in the specified mode and path, with read/write
		///access and not shared.
		///</returns>
		///
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid, (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only.-or- This operation is not supported
		///on the current platform.-or- path specified a directory.-or- The caller does
		///not have the required permission. -or-mode is System.IO.FileMode.Create and
		///the specified file is a hidden file.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///mode specified an invalid value.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		FileStream Open(string path, FileMode mode);
		///<summary>
		///Opens a System.IO.FileStream on the specified path, with the specified mode
		///and access.
		///</summary>
		///
		///<param name="path">
		///The file to open.
		///</param>
		///<param name="mode">
		///A System.IO.FileMode value that specifies whether a file is created if one
		///does not exist, and determines whether the contents of existing files are
		///retained or overwritten.
		///</param>
		///<param name="access">
		///A System.IO.FileAccess value that specifies the operations that can be performed
		///on the file.
		///</param>
		///<returns>
		///An unshared System.IO.FileStream that provides access to the specified file,
		///with the specified mode and access.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.-or-
		///access specified Read and mode specified Create, CreateNew, Truncate, or
		///Append.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid, (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only and access is not Read.-or- path
		///specified a directory.-or- The caller does not have the required permission.
		///-or-mode is System.IO.FileMode.Create and the specified file is a hidden
		///file.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///mode or access specified an invalid value.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		FileStream Open(string path, FileMode mode, FileAccess access);
		///<summary>
		///Opens a System.IO.FileStream on the specified path, having the specified
		///mode with read, write, or read/write access and the specified sharing option.
		///</summary>
		///
		///<param name="path">
		///The file to open.
		///</param>
		///<param name="mode">
		///A System.IO.FileMode value that specifies whether a file is created if one
		///does not exist, and determines whether the contents of existing files are
		///retained or overwritten.
		///</param>
		///<param name="access">
		///A System.IO.FileAccess value that specifies the operations that can be performed
		///on the file.
		///</param>
		///<param name="share">
		///A System.IO.FileShare value specifying the type of access other threads have
		///to the file.
		///</param>
		///<returns>
		///A System.IO.FileStream on the specified path, having the specified mode with
		///read, write, or read/write access and the specified sharing option.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.-or-
		///access specified Read and mode specified Create, CreateNew, Truncate, or
		///Append.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid, (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only and access is not Read.-or- path
		///specified a directory.-or- The caller does not have the required permission.
		///-or-mode is System.IO.FileMode.Create and the specified file is a hidden
		///file.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///mode, access, or share specified an invalid value.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		FileStream Open(string path, FileMode mode, FileAccess access, FileShare share);
		///<summary>
		///Opens an existing file for reading.
		///</summary>
		///
		///<param name="path">
		///The file to be opened for reading.
		///</param>
		///<returns>
		///A read-only System.IO.FileStream on the specified path.
		///</returns>
		///
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid, (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a directory.-or- The caller does not have the required permission.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		FileStream OpenRead(string path);
		///<summary>
		///Opens an existing UTF-8 encoded text file for reading.
		///</summary>
		///
		///<param name="path">
		///The file to be opened for reading.
		///</param>
		///<returns>
		///A System.IO.StreamReader on the specified path.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid, (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		StreamReader OpenText(string path);
		///<summary>
		///Opens an existing file for writing.
		///</summary>
		///
		///<param name="path">
		///The file to be opened for writing.
		///</param>
		///<returns>
		///An unshared System.IO.FileStream object on the specified path with System.IO.FileAccess.Write
		///access.
		///</returns>
		///
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.-or- path specified a read-only
		///file or directory.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid, (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		FileStream OpenWrite(string path);
		///<summary>
		///Opens a binary file, reads the contents of the file into a byte array, and
		///then closes the file.
		///</summary>
		///
		///<param name="path">
		///The file to open for reading.
		///</param>
		///<returns>
		///A byte array containing the contents of the file.
		///</returns>
		///
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///This operation is not supported on the current platform.-or- path specified
		///a directory.-or- The caller does not have the required permission.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		byte[] ReadAllBytes(string path);
		///<summary>
		///Opens a text file, reads all lines of the file, and then closes the file.
		///</summary>
		///
		///<param name="path">
		///The file to open for reading.
		///</param>
		///<returns>
		///A string array containing all lines of the file.
		///</returns>
		///
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only.-or- This operation is not supported
		///on the current platform.-or- path specified a directory.-or- The caller does
		///not have the required permission.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		string[] ReadAllLines(string path);
		///<summary>
		///Opens a file, reads all lines of the file with the specified encoding, and
		///then closes the file.
		///</summary>
		///
		///<param name="path">
		///The file to open for reading.
		///</param>
		///<param name="encoding">
		///The encoding applied to the contents of the file.
		///</param>
		///<returns>
		///A string array containing all lines of the file.
		///</returns>
		///
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only.-or- This operation is not supported
		///on the current platform.-or- path specified a directory.-or- The caller does
		///not have the required permission.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		string[] ReadAllLines(string path, Encoding encoding);
		///<summary>
		///Opens a text file, reads all lines of the file, and then closes the file.
		///</summary>
		///
		///<param name="path">
		///The file to open for reading.
		///</param>
		///<returns>
		///A string containing all lines of the file.
		///</returns>
		///
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only.-or- This operation is not supported
		///on the current platform.-or- path specified a directory.-or- The caller does
		///not have the required permission.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		string ReadAllText(string path);
		///<summary>
		///Opens a file, reads all lines of the file with the specified encoding, and
		///then closes the file.
		///</summary>
		///
		///<param name="path">
		///The file to open for reading.
		///</param>
		///<param name="encoding">
		///The encoding applied to the contents of the file.
		///</param>
		///<returns>
		///A string containing all lines of the file.
		///</returns>
		///
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only.-or- This operation is not supported
		///on the current platform.-or- path specified a directory.-or- The caller does
		///not have the required permission.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		string ReadAllText(string path, Encoding encoding);
		///<summary>
		///Replaces the contents of a specified file with the contents of another file,
		///deleting the original file, and creating a backup of the replaced file.
		///</summary>
		///
		///<param name="sourceFileName">
		///The name of a file that replaces the file specified by destinationFileName.
		///</param>
		///<param name="destinationFileName">
		///The name of the file being replaced.
		///</param>
		///<param name="destinationBackupFileName">
		///The name of the backup file.
		///</param>
		///
		///<exception cref="System.ArgumentException">
		///The path described by the destinationFileName parameter was not of a legal
		///form.-or-The path described by the destinationBackupFileName parameter was
		///not of a legal form.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///The destinationFileName parameter is null.
		///</exception>
		///<exception cref="System.IO.DriveNotFoundException">
		///An invalid drive was specified.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file described by the current System.IO.FileInfo object could not be
		///found.-or-The file described by the destinationBackupFileName parameter could
		///not be found.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.- or -The sourceFileName and
		///destinationFileName parameters specify the same file.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///The operating system is Windows 98 Second Edition or earlier and the files
		///system is not NTFS.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The sourceFileName or destinationFileName parameter specifies a file that
		///is read-only.-or- This operation is not supported on the current platform.-or-
		///Source or destination parameters specify a directory instead of a file.-or-
		///The caller does not have the required permission.
		///</exception>
		void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName);
		///<summary>
		///Replaces the contents of a specified file with the contents of another file,
		///deleting the original file, and creating a backup of the replaced file and
		///optionally ignores merge errors.
		///</summary>
		///
		///<param name="sourceFileName">
		///The name of a file that replaces the file specified by destinationFileName.
		///</param>
		///<param name="destinationFileName">
		///The name of the file being replaced.
		///</param>
		///<param name="destinationBackupFileName">
		///The name of the backup file.
		///</param>
		///<param name="ignoreMetadataErrors">
		///true to ignore merge errors (such as attributes and access control lists
		///(ACLs)) from the replaced file to the replacement file; otherwise, false.
		///</param>
		///
		///<exception cref="System.ArgumentException">
		///The path described by the destinationFileName parameter was not of a legal
		///form.-or-The path described by the destinationBackupFileName parameter was
		///not of a legal form.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///The destinationFileName parameter is null.
		///</exception>
		///<exception cref="System.IO.DriveNotFoundException">
		///An invalid drive was specified.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file described by the current System.IO.FileInfo object could not be
		///found.-or-The file described by the destinationBackupFileName parameter could
		///not be found.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.- or -The sourceFileName and
		///destinationFileName parameters specify the same file.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///The operating system is Windows 98 Second Edition or earlier and the files
		///system is not NTFS.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The sourceFileName or destinationFileName parameter specifies a file that
		///is read-only.-or- This operation is not supported on the current platform.-or-
		///Source or destination parameters specify a directory instead of a file.-or-
		///The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName, bool ignoreMetadataErrors);
		///<summary>
		///Applies access control list (ACL) entries described by a System.Security.AccessControl.FileSecurity
		///object to the specified file.
		///</summary>
		///
		///<param name="path">
		///A file to add or remove access control list (ACL) entries from.
		///</param>
		///<param name="fileSecurity">
		///A System.Security.AccessControl.FileSecurity object that describes an ACL
		///entry to apply to the file described by the path parameter.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.Runtime.InteropServices.SEHException">
		///The path parameter is null.
		///</exception>
		///<exception cref="System.SystemException">
		///The file could not be found.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The path parameter specified a file that is read-only.-or- This operation
		///is not supported on the current platform.-or- The path parameter specified
		///a directory.-or- The caller does not have the required permission.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///The fileSecurity parameter is null.
		///</exception>
		[SecuritySafeCritical]
		void SetAccessControl(string path, FileSecurity fileSecurity);
		///<summary>
		///Sets the specified System.IO.FileAttributes of the file on the specified
		///path.
		///</summary>
		///
		///<param name="path">
		///The path to the file.
		///</param>
		///<param name="fileAttributes">
		///The desired System.IO.FileAttributes, such as Hidden, ReadOnly, Normal, and
		///Archive.
		///</param>
		///
		///<exception cref="System.ArgumentException">
		///path is empty, contains only white spaces, contains invalid characters, or
		///the file attribute is invalid.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid, (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file cannot be found.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only.-or- This operation is not supported
		///on the current platform.-or- path specified a directory.-or- The caller does
		///not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		void SetAttributes(string path, FileAttributes fileAttributes);
		///<summary>
		///Sets the date and time the file was created.
		///</summary>
		///
		///<param name="path">
		///The file for which to set the creation date and time information.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while performing the operation.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///creationTime specifies a value outside the range of dates, times, or both
		///permitted for this operation.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		void SetCreationTime(string path, DateTime creationTime);
		///<summary>
		///Sets the date and time, in coordinated universal time (UTC), that the file
		///was created.
		///</summary>
		///
		///<param name="path">
		///The file for which to set the creation date and time information.
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while performing the operation.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///creationTime specifies a value outside the range of dates, times, or both
		///permitted for this operation.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		[SecuritySafeCritical]
		void SetCreationTimeUtc(string path, DateTime creationTimeUtc);
		///<summary>
		///Sets the date and time the specified file was last accessed.
		///</summary>
		///
		///<param name="path">
		///The file for which to set the access date and time information.
		///</param>
		///<param name="lastAccessTime">
		///A System.DateTime containing the value to set for the last access date and
		///time of path. This value is expressed in local time.
		///</param>
		///
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The specified path was not found.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///lastAccessTime specifies a value outside the range of dates or times permitted
		///for this operation.
		///</exception>
		void SetLastAccessTime(string path, DateTime lastAccessTime);
		///<summary>
		///Sets the date and time, in coordinated universal time (UTC), that the specified
		///file was last accessed.
		///</summary>
		///
		///<param name="path">
		///The file for which to set the access date and time information.
		///</param>
		///<param name="lastAccessTimeUtc">
		///A System.DateTime containing the value to set for the last access date and
		///time of path. This value is expressed in UTC time.
		///</param>
		///
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The specified path was not found.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///lastAccessTimeUtc specifies a value outside the range of dates or times permitted
		///for this operation.
		///</exception>
		[SecuritySafeCritical]
		void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc);
		///<summary>
		///Sets the date and time that the specified file was last written to.
		///</summary>
		///
		///<param name="path">
		///The file for which to set the date and time information.
		///</param>
		///<param name="lastWriteTime">
		///A System.DateTime containing the value to set for the last write date and
		///time of path. This value is expressed in local time.
		///</param>
		///
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The specified path was not found.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///lastWriteTime specifies a value outside the range of dates or times permitted
		///for this operation.
		///</exception>
		void SetLastWriteTime(string path, DateTime lastWriteTime);
		///<summary>
		///Sets the date and time, in coordinated universal time (UTC), that the specified
		///file was last written to.
		///</summary>
		///
		///<param name="path">
		///The file for which to set the date and time information.
		///</param>
		///<param name="lastWriteTimeUtc">
		///A System.DateTime containing the value to set for the last write date and
		///time of path. This value is expressed in UTC time.
		///</param>
		///
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
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The specified path was not found.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///lastWriteTimeUtc specifies a value outside the range of dates or times permitted
		///for this operation.
		///</exception>
		[SecuritySafeCritical]
		void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc);
		///<summary>
		///Creates a new file, writes the specified byte array to the file, and then
		///closes the file. If the target file already exists, it is overwritten.
		///</summary>
		///
		///<param name="path">
		///The file to write to.
		///</param>
		///<param name="bytes">
		///The bytes to write to the file.
		///</param>
		///
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null or the byte array is empty.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only.-or- This operation is not supported
		///on the current platform.-or- path specified a directory.-or- The caller does
		///not have the required permission.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		void WriteAllBytes(string path, byte[] bytes);
		///<summary>
		///Creates a new file, writes a collection of strings to the file, and then
		///closes the file.
		///</summary>
		///
		///<param name="path">
		///The file to write to.
		///</param>
		///<param name="contents">
		///The lines to write to the file.
		///</param>
		///
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters defined by the System.IO.Path.GetInvalidPathChars()
		///method.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///Either path or contents is null.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified by path was not found.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///path exceeds the system-defined maximum length. For example, on Windows-based
		///platforms, paths must be less than 248 characters and file names must be
		///less than 260 characters.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specifies a file that is read-only.-or-This operation is not supported
		///on the current platform.-or-path is a directory.-or-The caller does not have
		///the required permission.
		///</exception>
		[SecuritySafeCritical]
		void WriteAllLines(string path, IEnumerable<string> contents);
		///<summary>
		///Creates a new file, write the specified string array to the file, and then
		///closes the file.
		///</summary>
		///
		///<param name="path">
		///The file to write to.
		///</param>
		///<param name="contents">
		///The string array to write to the file.
		///</param>
		///
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///Either path or contents is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only.-or- This operation is not supported
		///on the current platform.-or- path specified a directory.-or- The caller does
		///not have the required permission.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		void WriteAllLines(string path, string[] contents);
		///<summary>
		///Creates a new file by using the specified encoding, writes a collection of
		///strings to the file, and then closes the file.
		///</summary>
		///
		///<param name="path">
		///The file to write to.
		///</param>
		///<param name="contents">
		///The lines to write to the file.
		///</param>
		///<param name="encoding">
		///The character encoding to use.
		///</param>
		///
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters defined by the System.IO.Path.GetInvalidPathChars()
		///method.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///Either path, contents, or encoding is null.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified by path was not found.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///path exceeds the system-defined maximum length. For example, on Windows-based
		///platforms, paths must be less than 248 characters and file names must be
		///less than 260 characters.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specifies a file that is read-only.-or-This operation is not supported
		///on the current platform.-or-path is a directory.-or-The caller does not have
		///the required permission.
		///</exception>
		[SecuritySafeCritical]
		void WriteAllLines(string path, IEnumerable<string> contents, Encoding encoding);
		///<summary>
		///Creates a new file, writes the specified string array to the file by using
		///the specified encoding, and then closes the file.
		///</summary>
		///
		///<param name="path">
		///The file to write to.
		///</param>
		///<param name="contents">
		///The string array to write to the file.
		///</param>
		///<param name="encoding">
		///An System.Text.Encoding object that represents the character encoding applied
		///to the string array.
		///</param>
		///
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///Either path or contents is null.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only.-or- This operation is not supported
		///on the current platform.-or- path specified a directory.-or- The caller does
		///not have the required permission.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		void WriteAllLines(string path, string[] contents, Encoding encoding);
		///<summary>
		///Creates a new file, writes the specified string to the file, and then closes
		///the file. If the target file already exists, it is overwritten.
		///</summary>
		///
		///<param name="path">
		///The file to write to.
		///</param>
		///<param name="contents">
		///The string to write to the file.
		///</param>
		///
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null or contents is empty.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only.-or- This operation is not supported
		///on the current platform.-or- path specified a directory.-or- The caller does
		///not have the required permission.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		void WriteAllText(string path, string contents);
		///<summary>
		///Creates a new file, writes the specified string to the file using the specified
		///encoding, and then closes the file. If the target file already exists, it
		///is overwritten.
		///</summary>
		///
		///<param name="path">
		///The file to write to.
		///</param>
		///<param name="contents">
		///The string to write to the file.
		///</param>
		///<param name="encoding">
		///The encoding to apply to the string.
		///</param>
		///
		///<exception cref="System.ArgumentException">
		///path is a zero-length string, contains only white space, or contains one
		///or more invalid characters as defined by System.IO.Path.InvalidPathChars.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///path is null or contents is empty.
		///</exception>
		///<exception cref="System.IO.PathTooLongException">
		///The specified path, file name, or both exceed the system-defined maximum
		///length. For example, on Windows-based platforms, paths must be less than
		///248 characters, and file names must be less than 260 characters.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///The specified path is invalid (for example, it is on an unmapped drive).
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred while opening the file.
		///</exception>
		///<exception cref="System.UnauthorizedAccessException">
		///path specified a file that is read-only.-or- This operation is not supported
		///on the current platform.-or- path specified a directory.-or- The caller does
		///not have the required permission.
		///</exception>
		///<exception cref="System.IO.FileNotFoundException">
		///The file specified in path was not found.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///path is in an invalid format.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		[SecuritySafeCritical]
		void WriteAllText(string path, string contents, Encoding encoding);
	}


}
