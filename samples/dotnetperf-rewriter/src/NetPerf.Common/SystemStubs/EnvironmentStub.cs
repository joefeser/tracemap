using System.Collections;
using System.Security;
using NetPerf.Common.SystemStubs;

namespace System {
	public static class EnvironmentStub {
#pragma warning disable 0649 // justification: this field is set by reflection
		private static IEnvironmentStub _interfaceStub;
#pragma warning restore
		///<summary>
		///Gets the command line for this process.
		///</summary>
		///<returns>
		///A string containing command-line arguments.
		///</returns>
		public static string CommandLine {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.CommandLine;
				}
				else {
					return Environment.CommandLine;
				}
			}
		}
		///<summary>
		///Gets or sets the fully qualified path of the current working directory.
		///</summary>
		///<returns>
		///A string containing a directory path.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///Attempted to set to an empty string ("").
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///Attempted to set to null.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		///<exception cref="System.IO.DirectoryNotFoundException">
		///Attempted to set a local path that cannot be found.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the appropriate permission.
		///</exception>
		public static string CurrentDirectory {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.CurrentDirectory;
				}
				else {
					return Environment.CurrentDirectory;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.CurrentDirectory = value;
				}
				else {
					Environment.CurrentDirectory = value;
				}
			}
		}
		///<summary>
		///Gets or sets the exit code of the process.
		///</summary>
		///<returns>
		///A 32-bit signed integer containing the exit code. The default value is zero.
		///</returns>
		public static int ExitCode {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.ExitCode;
				}
				else {
					return Environment.ExitCode;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.ExitCode = value;
				}
				else {
					Environment.ExitCode = value;
				}
			}
		}
		///<summary>
		///Gets a value indicating whether the common language runtime (CLR) is shutting
		///down.
		///</summary>
		///<returns>
		///true if the common language runtime is shutting down; otherwise, false.
		///</returns>
		public static bool HasShutdownStarted {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.HasShutdownStarted;
				}
				else {
					return Environment.HasShutdownStarted;
				}
			}
		}
		///<summary>
		///Gets the NetBIOS name of this local computer.
		///</summary>
		///<returns>
		///A string containing the name of this computer.
		///</returns>
		///
		///<exception cref="System.InvalidOperationException">
		///The name of this computer cannot be obtained.
		///</exception>
		public static string MachineName {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.MachineName;
				}
				else {
					return Environment.MachineName;
				}
			}
		}
		///<summary>
		///Gets the newline string defined for this environment.
		///</summary>
		///<returns>
		///A string containing "\r\n" for non-Unix platforms, or a string containing
		///"\n" for Unix platforms.
		///</returns>
		public static string NewLine {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.NewLine;
				}
				else {
					return Environment.NewLine;
				}
			}
		}
		///<summary>
		///Gets an System.OperatingSystem object that contains the current platform
		///identifier and version number.
		///</summary>
		///<returns>
		///An object that contains the platform identifier and version number.
		///</returns>
		///
		///<exception cref="System.InvalidOperationException">
		///This property was unable to obtain the system version.-or- The obtained platform
		///identifier is not a member of System.PlatformID.
		///</exception>
		public static OperatingSystem OSVersion {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.OSVersion;
				}
				else {
					return Environment.OSVersion;
				}
			}
		}
		///<summary>
		///Gets the number of processors on the current machine.
		///</summary>
		///<returns>
		///The 32-bit signed integer that specifies the number of processors on the
		///current machine. There is no default.
		///</returns>
		public static int ProcessorCount {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.ProcessorCount;
				}
				else {
					return Environment.ProcessorCount;
				}
			}
		}
		///<summary>
		///Gets current stack trace information.
		///</summary>
		///<returns>
		///A string containing stack trace information. This value can be System.String.Empty.
		///</returns>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///The requested stack trace information is out of range.
		///</exception>
		public static string StackTrace {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.StackTrace;
				}
				else {
					return Environment.StackTrace;
				}
			}
		}
		///<summary>
		///Gets the fully qualified path of the system directory.
		///</summary>
		///<returns>
		///A string containing a directory path.
		///</returns>
		public static string SystemDirectory {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.SystemDirectory;
				}
				else {
					return Environment.SystemDirectory;
				}
			}
		}
		///<summary>
		///Gets the number of milliseconds elapsed since the system started.
		///</summary>
		///<returns>
		///A 32-bit signed integer containing the amount of time in milliseconds that
		///has passed since the last time the computer was started.
		///</returns>
		public static int TickCount {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.TickCount;
				}
				else {
					return Environment.TickCount;
				}
			}
		}
		///<summary>
		///Gets the network domain name associated with the current user.
		///</summary>
		///<returns>
		///The network domain name associated with the current user.
		///</returns>
		///
		///<exception cref="System.PlatformNotSupportedException">
		///The operating system does not support retrieving the network domain name.
		///</exception>
		///<exception cref="System.InvalidOperationException">
		///The network domain name cannot be retrieved.
		///</exception>
		public static string UserDomainName {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.UserDomainName;
				}
				else {
					return Environment.UserDomainName;
				}
			}
		}
		///<summary>
		///Gets a value indicating whether the current process is running in user interactive
		///mode.
		///</summary>
		///<returns>
		///true if the current process is running in user interactive mode; otherwise,
		///false.
		///</returns>
		public static bool UserInteractive {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.UserInteractive;
				}
				else {
					return Environment.UserInteractive;
				}
			}
		}
		///<summary>
		///Gets the user name of the person who is currently logged on to the Windows
		///operating system.
		///</summary>
		///<returns>
		///The user name of the person who is logged on to Windows.
		///</returns>
		public static string UserName {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.UserName;
				}
				else {
					return Environment.UserName;
				}
			}
		}
		///<summary>
		///Gets a System.Version object that describes the major, minor, build, and
		///revision numbers of the common language runtime.
		///</summary>
		///<returns>
		///An object that displays the version of the common language runtime.
		///</returns>
		public static Version Version {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.Version;
				}
				else {
					return Environment.Version;
				}
			}
		}
		///<summary>
		///Gets the amount of physical memory mapped to the process context.
		///</summary>
		///<returns>
		///A 64-bit signed integer containing the number of bytes of physical memory
		///mapped to the process context.
		///</returns>
		public static long WorkingSet {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.WorkingSet;
				}
				else {
					return Environment.WorkingSet;
				}
			}
		}

		///<summary>
		///Terminates this process and gives the underlying operating system the specified
		///exit code.
		///</summary>
		///
		///<param name="exitCode">
		///Exit code to be given to the operating system.
		///</param>
		///
		///<exception cref="System.Security.SecurityException">
		///The caller does not have sufficient security permission to perform this function.
		///</exception>
		[SecuritySafeCritical]
		public static void Exit(int exitCode) {
			if (_interfaceStub != null) {
				_interfaceStub.Exit(exitCode);
			}
			else {
				Environment.Exit(exitCode);
			}
		}
		///<summary>
		///Replaces the name of each environment variable embedded in the specified
		///string with the string equivalent of the value of the variable, then returns
		///the resulting string.
		///</summary>
		///
		///<param name="name">
		///A string containing the names of zero or more environment variables. Each
		///environment variable is quoted with the percent sign character (%).
		///</param>
		///<returns>
		///A string with each environment variable replaced by its value.
		///</returns>
		///
		///<exception cref="System.ArgumentNullException">
		///name is null.
		///</exception>
		[SecuritySafeCritical]
		public static string ExpandEnvironmentVariables(string name) {
			if (_interfaceStub != null) {
				return _interfaceStub.ExpandEnvironmentVariables(name);
			}
			else {
				return Environment.ExpandEnvironmentVariables(name);
			}
		}
		///<summary>
		///Immediately terminates a process after writing a message to the Windows Application
		///event log, and then includes the message in error reporting to Microsoft.
		///</summary>
		///
		///<param name="message">
		///A message that explains why the process was terminated, or null if no explanation
		///is provided.
		///</param>
		[SecurityCritical]
		public static void FailFast(string message) {
			if (_interfaceStub != null) {
				_interfaceStub.FailFast(message);
			}
			else {
				Environment.FailFast(message);
			}
		}
		///<summary>
		///Returns a string array containing the command-line arguments for the current
		///process.
		///</summary>
		///<returns>
		///An array of string where each element contains a command-line argument. The
		///first element is the executable file name, and the following zero or more
		///elements contain the remaining command-line arguments.
		///</returns>
		///
		///<exception cref="System.NotSupportedException">
		///The system does not support command-line arguments.
		///</exception>
		[SecuritySafeCritical]
		public static string[] GetCommandLineArgs() {
			if (_interfaceStub != null) {
				return _interfaceStub.GetCommandLineArgs();
			}
			else {
				return Environment.GetCommandLineArgs();
			}
		}
		///<summary>
		///Retrieves the value of an environment variable from the current process.
		///</summary>
		///
		///<param name="variable">
		///The name of the environment variable.
		///</param>
		///<returns>
		///The value of the environment variable specified by variable, or null if the
		///environment variable is not found.
		///</returns>
		///
		///<exception cref="System.ArgumentNullException">
		///variable is null.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission to perform this operation.
		///</exception>
		[SecuritySafeCritical]
		public static string GetEnvironmentVariable(string variable) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetEnvironmentVariable(variable);
			}
			else {
				return Environment.GetEnvironmentVariable(variable);
			}
		}
		///<summary>
		///Retrieves the value of an environment variable from the current process or
		///from the Windows operating system registry key for the current user or local
		///machine.
		///</summary>
		///
		///<param name="variable">
		///The name of an environment variable.
		///</param>
		///<param name="target">
		///One of the System.EnvironmentVariableTarget values.
		///</param>
		///<returns>
		///The value of the environment variable specified by the variable and target
		///parameters, or null if the environment variable is not found.
		///</returns>
		///
		///<exception cref="System.ArgumentNullException">
		///variable is null.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///target is System.EnvironmentVariableTarget.User or System.EnvironmentVariableTarget.Machine
		///and the current operating system is Windows 95, Windows 98, or Windows Me.
		///</exception>
		///<exception cref="System.ArgumentException">
		///target is not a valid System.EnvironmentVariableTarget value.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission to perform this operation.
		///</exception>
		[SecuritySafeCritical]
		public static string GetEnvironmentVariable(string variable, EnvironmentVariableTarget target) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetEnvironmentVariable(variable, target);
			}
			else {
				return Environment.GetEnvironmentVariable(variable, target);
			}
		}
		///<summary>
		///Retrieves all environment variable names and their values from the current
		///process.
		///</summary>
		///<returns>
		///A dictionary that contains all environment variable names and their values;
		///otherwise, an empty dictionary if no environment variables are found.
		///</returns>
		///
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission to perform this operation.
		///</exception>
		///<exception cref="System.OutOfMemoryException">
		///The buffer is out of memory.
		///</exception>
		[SecuritySafeCritical]
		public static IDictionary GetEnvironmentVariables() {
			if (_interfaceStub != null) {
				return _interfaceStub.GetEnvironmentVariables();
			}
			else {
				return Environment.GetEnvironmentVariables();
			}
		}
		///<summary>
		///Retrieves all environment variable names and their values from the current
		///process, or from the Windows operating system registry key for the current
		///user or local machine.
		///</summary>
		///
		///<param name="target">
		///One of the System.EnvironmentVariableTarget values.
		///</param>
		///<returns>
		///A dictionary that contains all environment variable names and their values
		///from the source specified by the target parameter; otherwise, an empty dictionary
		///if no environment variables are found.
		///</returns>
		///
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission to perform this operation
		///for the specified value of target.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///This method cannot be used on Windows 95 or Windows 98 platforms.
		///</exception>
		///<exception cref="System.ArgumentException">
		///target contains an illegal value.
		///</exception>
		[SecuritySafeCritical]
		public static IDictionary GetEnvironmentVariables(EnvironmentVariableTarget target) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetEnvironmentVariables(target);
			}
			else {
				return Environment.GetEnvironmentVariables(target);
			}
		}
		///<summary>
		///Gets the path to the system special folder that is identified by the specified
		///enumeration.
		///</summary>
		///
		///<param name="folder">
		///An enumerated constant that identifies a system special folder.
		///</param>
		///<returns>
		///The path to the specified system special folder, if that folder physically
		///exists on your computer; otherwise, an empty string ("").A folder will not
		///physically exist if the operating system did not create it, the existing
		///folder was deleted, or the folder is a virtual directory, such as My Computer,
		///which does not correspond to a physical path.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///folder is not a member of System.Environment.SpecialFolder.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///The current platform is not supported.
		///</exception>
		[SecuritySafeCritical]
		public static string GetFolderPath(Environment.SpecialFolder folder) {
			if (_interfaceStub != null) {
				return _interfaceStub.GetFolderPath(folder);
			}
			else {
				return Environment.GetFolderPath(folder);
			}
		}
		///<summary>
		///Returns an array of string containing the names of the logical drives on
		///the current computer.
		///</summary>
		///<returns>
		///An array of strings where each element contains the name of a logical drive.
		///For example, if the computer's hard drive is the first logical drive, the
		///first element returned is "C:\".
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurs.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permissions.
		///</exception>
		[SecuritySafeCritical]
		public static string[] GetLogicalDrives() {
			if (_interfaceStub != null) {
				return _interfaceStub.GetLogicalDrives();
			}
			else {
				return Environment.GetLogicalDrives();
			}
		}
		///<summary>
		///Creates, modifies, or deletes an environment variable stored in the current
		///process.
		///</summary>
		///
		///<param name="variable">
		///The name of an environment variable.
		///</param>
		///<param name="value">
		///A value to assign to variable.
		///</param>
		///
		///<exception cref="System.ArgumentNullException">
		///variable is null.
		///</exception>
		///<exception cref="System.ArgumentException">
		///variable contains a zero-length string, an initial hexadecimal zero character
		///(0x00), or an equal sign ("="). -or-The length of variable or value is greater
		///than or equal to 32,767 characters.-or-An error occurred during the execution
		///of this operation.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission to perform this operation.
		///</exception>
		[SecuritySafeCritical]
		public static void SetEnvironmentVariable(string variable, string value) {
			if (_interfaceStub != null) {
				_interfaceStub.SetEnvironmentVariable(variable, value);
			}
			else {
				Environment.SetEnvironmentVariable(variable, value);
			}
		}
		///<summary>
		///Creates, modifies, or deletes an environment variable stored in the current
		///process or in the Windows operating system registry key reserved for the
		///current user or local machine.
		///</summary>
		///
		///<param name="variable">
		///The name of an environment variable.
		///</param>
		///<param name="value">
		///A value to assign to variable.
		///</param>
		///<param name="target">
		///One of the System.EnvironmentVariableTarget values.
		///</param>
		///
		///<exception cref="System.ArgumentNullException">
		///variable is null.
		///</exception>
		///<exception cref="System.ArgumentException">
		///variable contains a zero-length string, an initial hexadecimal zero character
		///(0x00), or an equal sign ("="). -or-The length of variable is greater than
		///or equal to 32,767 characters.-or-target is not a member of the System.EnvironmentVariableTarget
		///enumeration. -or-target is System.EnvironmentVariableTarget.Machine or System.EnvironmentVariableTarget.User
		///and the length of variable is greater than or equal to 255.-or-target is
		///System.EnvironmentVariableTarget.Process and the length of value is greater
		///than or equal to 32,767 characters. -or-An error occurred during the execution
		///of this operation.
		///</exception>
		///<exception cref="System.NotSupportedException">
		///target is System.EnvironmentVariableTarget.User or System.EnvironmentVariableTarget.Machine
		///and the current operating system is Windows 95, Windows 98, or Windows Me.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission to perform this operation.
		///</exception>
		[SecuritySafeCritical]
		public static void SetEnvironmentVariable(string variable, string value, EnvironmentVariableTarget target) {
			if (_interfaceStub != null) {
				_interfaceStub.SetEnvironmentVariable(variable, value, target);
			}
			else {
				Environment.SetEnvironmentVariable(variable, value, target);
			}
		}

	}
}
