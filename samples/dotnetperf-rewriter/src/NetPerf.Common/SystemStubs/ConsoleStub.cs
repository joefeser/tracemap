using System.Text;
using NetPerf.Common.SystemStubs;
using System.IO;

namespace System {
	///<summary>
	///Represents the standard input, output, and error streams for console applications.
	///This class cannot be inherited.
	///</summary>
	public static class ConsoleStub {
#pragma warning disable 649
		private static IConsoleStub _interfaceStub;
#pragma warning restore 649

		///<summary>
		///Gets or sets the background color of the console.
		///</summary>
		///<returns>
		///A System.ConsoleColor that specifies the background color of the console;
		///that is, the color that appears behind each character. The default is black.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///The color specified in a set operation is not a valid member of System.ConsoleColor.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static ConsoleColor BackgroundColor {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.BackgroundColor;
				}
				else {
					return Console.BackgroundColor;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.BackgroundColor = value;
				}
				else {
					Console.BackgroundColor = value;
				}
			}
		}
		///<summary>
		///Gets or sets the height of the buffer area.
		///</summary>
		///<returns>
		///The current height, in rows, of the buffer area.
		///</returns>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///The value in a set operation is less than or equal to zero.  -or- The value
		///in a set operation is greater than or equal to System.Int16.MaxValue.  -or-
		///The value in a set operation is less than System.Console.WindowTop + System.Console.WindowHeight.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static int BufferHeight {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.BufferHeight;
				}
				else {
					return Console.BufferHeight;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.BufferHeight = value;
				}
				else {
					Console.BufferHeight = value;
				}
			}
		}
		///<summary>
		///Gets or sets the width of the buffer area.
		///</summary>
		///<returns>
		///The current width, in columns, of the buffer area.
		///</returns>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///The value in a set operation is less than or equal to zero.  -or- The value
		///in a set operation is greater than or equal to System.Int16.MaxValue.  -or-
		///The value in a set operation is less than System.Console.WindowLeft + System.Console.WindowWidth.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static int BufferWidth {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.BufferWidth;
				}
				else {
					return Console.BufferWidth;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.BufferWidth = value;
				}
				else {
					Console.BufferWidth = value;
				}
			}
		}
		///<summary>
		///Gets a value indicating whether the CAPS LOCK keyboard toggle is turned on
		///or turned off.
		///</summary>
		///<returns>
		///true if CAPS LOCK is turned on; false if CAPS LOCK is turned off.
		///</returns>
		public static bool CapsLock {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.CapsLock;
				}
				else {
					return Console.CapsLock;
				}
			}
		}
		///<summary>
		///Gets or sets the column position of the cursor within the buffer area.
		///</summary>
		///<returns>
		///The current position, in columns, of the cursor.
		///</returns>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///The value in a set operation is less than zero.  -or- The value in a set
		///operation is greater than or equal to System.Console.BufferWidth.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static int CursorLeft {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.CursorLeft;
				}
				else {
					return Console.CursorLeft;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.CursorLeft = value;
				}
				else {
					Console.CursorLeft = value;
				}
			}
		}
		///<summary>
		///Gets or sets the height of the cursor within a character cell.
		///</summary>
		///<returns>
		///The size of the cursor expressed as a percentage of the height of a character
		///cell. The property value ranges from 1 to 100.
		///</returns>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///The value specified in a set operation is less than 1 or greater than 100.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static int CursorSize {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.CursorSize;
				}
				else {
					return Console.CursorSize;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.CursorSize = value;
				}
				else {
					Console.CursorSize = value;
				}
			}
		}
		///<summary>
		///Gets or sets the row position of the cursor within the buffer area.
		///</summary>
		///<returns>
		///The current position, in rows, of the cursor.
		///</returns>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///The value in a set operation is less than zero.  -or- The value in a set
		///operation is greater than or equal to System.Console.BufferHeight.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static int CursorTop {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.CursorTop;
				}
				else {
					return Console.CursorTop;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.CursorTop = value;
				}
				else {
					Console.CursorTop = value;
				}
			}
		}
		///<summary>
		///Gets or sets a value indicating whether the cursor is visible.
		///</summary>
		///<returns>
		///true if the cursor is visible; otherwise, false.
		///</returns>
		///
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static bool CursorVisible {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.CursorVisible;
				}
				else {
					return Console.CursorVisible;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.CursorVisible = value;
				}
				else {
					Console.CursorVisible = value;
				}
			}
		}
		///<summary>
		///Gets the standard error output stream.
		///</summary>
		///<returns>
		///A System.IO.TextWriter that represents the standard error output stream.
		///</returns>
		public static TextWriter Error {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.Error;
				}
				else {
					return Console.Error;
				}
			}
		}
		///<summary>
		///Gets or sets the foreground color of the console.
		///</summary>
		///<returns>
		///A System.ConsoleColor that specifies the foreground color of the console;
		///that is, the color of each character that is displayed. The default is gray.
		///</returns>
		///
		///<exception cref="System.ArgumentException">
		///The color specified in a set operation is not a valid member of System.ConsoleColor.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static ConsoleColor ForegroundColor {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.ForegroundColor;
				}
				else {
					return Console.ForegroundColor;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.ForegroundColor = value;
				}
				else {
					Console.ForegroundColor = value;
				}
			}
		}
		///<summary>
		///Gets the standard input stream.
		///</summary>
		///<returns>
		///A System.IO.TextReader that represents the standard input stream.
		///</returns>
		public static TextReader In {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.In;
				}
				else {
					return Console.In;
				}
			}
		}
		///<summary>
		///Gets or sets the encoding the console uses to read input.
		///</summary>
		///<returns>
		///The encoding used to read console input.
		///</returns>
		///
		///<exception cref="System.ArgumentNullException">
		///The property value in a set operation is null.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///This property's set operation is not supported on Windows 98, Windows 98
		///Second Edition, or Windows Millennium Edition.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An error occurred during the execution of this operation.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///Your application does not have permission to perform this operation.
		///</exception>
		public static Encoding InputEncoding {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.InputEncoding;
				}
				else {
					return Console.InputEncoding;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.InputEncoding = value;
				}
				else {
					Console.InputEncoding = value;
				}
			}
		}
		///<summary>
		///Gets a value indicating whether a key press is available in the input stream.
		///</summary>
		///<returns>
		///true if a key press is available; otherwise, false.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		///<exception cref="System.InvalidOperationException">
		///Standard input is redirected to a file instead of the keyboard.
		///</exception>
		public static bool KeyAvailable {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.KeyAvailable;
				}
				else {
					return Console.KeyAvailable;
				}
			}
		}
		///<summary>
		///Gets the largest possible number of console window rows, based on the current
		///font and screen resolution.
		///</summary>
		///<returns>
		///The height of the largest possible console window measured in rows.
		///</returns>
		public static int LargestWindowHeight {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.LargestWindowHeight;
				}
				else {
					return Console.LargestWindowHeight;
				}
			}
		}
		///<summary>
		///Gets the largest possible number of console window columns, based on the
		///current font and screen resolution.
		///</summary>
		///<returns>
		///The width of the largest possible console window measured in columns.
		///</returns>
		public static int LargestWindowWidth {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.LargestWindowWidth;
				}
				else {
					return Console.LargestWindowWidth;
				}
			}
		}
		///<summary>
		///Gets a value indicating whether the NUM LOCK keyboard toggle is turned on
		///or turned off.
		///</summary>
		///<returns>
		///true if NUM LOCK is turned on; false if NUM LOCK is turned off.
		///</returns>
		public static bool NumberLock {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.NumberLock;
				}
				else {
					return Console.NumberLock;
				}
			}
		}
		///<summary>
		///Gets the standard output stream.
		///</summary>
		///<returns>
		///A System.IO.TextWriter that represents the standard output stream.
		///</returns>
		public static TextWriter Out {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.Out;
				}
				else {
					return Console.Out;
				}
			}
		}
		///<summary>
		///Gets or sets the encoding the console uses to write output.
		///</summary>
		///<returns>
		///The encoding used to write console output.
		///</returns>
		///
		///<exception cref="System.ArgumentNullException">
		///The property value in a set operation is null.
		///</exception>
		///<exception cref="System.PlatformNotSupportedException">
		///This property's set operation is not supported on Windows 98, Windows 98
		///Second Edition, or Windows Millennium Edition.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An error occurred during the execution of this operation.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///Your application does not have permission to perform this operation.
		///</exception>
		public static Encoding OutputEncoding {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.OutputEncoding;
				}
				else {
					return Console.OutputEncoding;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.OutputEncoding = value;
				}
				else {
					Console.OutputEncoding = value;
				}
			}
		}
		///<summary>
		///Gets or sets the title to display in the console title bar.
		///</summary>
		///<returns>
		///The string to be displayed in the title bar of the console. The maximum length
		///of the title string is 24500 characters.
		///</returns>
		///
		///<exception cref="System.InvalidOperationException">
		///In a get operation, the retrieved title is longer than 24500 characters.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///In a set operation, the specified title is longer than 24500 characters.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///In a set operation, the specified title is null.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static string Title {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.Title;
				}
				else {
					return Console.Title;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.Title = value;
				}
				else {
					Console.Title = value;
				}
			}
		}
		///<summary>
		///Gets or sets a value indicating whether the combination of the System.ConsoleModifiers.Control
		///modifier key and System.ConsoleKey.C console key (CTRL+C) is treated as ordinary
		///input or as an interruption that is handled by the operating system.
		///</summary>
		///<returns>
		///true if CTRL+C is treated as ordinary input; otherwise, false.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///Unable to get or set the input mode of the console input buffer.
		///</exception>
		public static bool TreatControlCAsInput {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.TreatControlCAsInput;
				}
				else {
					return Console.TreatControlCAsInput;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.TreatControlCAsInput = value;
				}
				else {
					Console.TreatControlCAsInput = value;
				}
			}
		}
		///<summary>
		///Gets or sets the height of the console window area.
		///</summary>
		///<returns>
		///The height of the console window measured in rows.
		///</returns>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///The value of the System.Console.WindowWidth property or the value of the
		///System.Console.WindowHeight property is less than or equal to 0.  -or- The
		///value of the System.Console.WindowHeight property plus the value of the System.Console.WindowTop
		///property is greater than or equal to System.Int16.MaxValue.  -or- The value
		///of the System.Console.WindowWidth property or the value of the System.Console.WindowHeight
		///property is greater than the largest possible window width or height for
		///the current screen resolution and console font.
		///</exception>
		///<exception cref="System.IO.IOException">
		///Error reading or writing information.
		///</exception>
		public static int WindowHeight {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.WindowHeight;
				}
				else {
					return Console.WindowHeight;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.WindowHeight = value;
				}
				else {
					Console.WindowHeight = value;
				}
			}
		}
		///<summary>
		///Gets or sets the leftmost position of the console window area relative to
		///the screen buffer.
		///</summary>
		///<returns>
		///The leftmost console window position measured in columns.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///Error reading or writing information.
		///</exception>
		public static int WindowLeft {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.WindowLeft;
				}
				else {
					return Console.WindowLeft;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.WindowLeft = value;
				}
				else {
					Console.WindowLeft = value;
				}
			}
		}
		///<summary>
		///Gets or sets the top position of the console window area relative to the
		///screen buffer.
		///</summary>
		///<returns>
		///The uppermost console window position measured in rows.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///Error reading or writing information.
		///</exception>
		public static int WindowTop {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.WindowTop;
				}
				else {
					return Console.WindowTop;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.WindowTop = value;
				}
				else {
					Console.WindowTop = value;
				}
			}
		}
		///<summary>
		///Gets or sets the width of the console window.
		///</summary>
		///<returns>
		///The width of the console window measured in columns.
		///</returns>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///The value of the System.Console.WindowWidth property or the value of the
		///System.Console.WindowHeight property is less than or equal to 0.  -or- The
		///value of the System.Console.WindowHeight property plus the value of the System.Console.WindowTop
		///property is greater than or equal to System.Int16.MaxValue.  -or- The value
		///of the System.Console.WindowWidth property or the value of the System.Console.WindowHeight
		///property is greater than the largest possible window width or height for
		///the current screen resolution and console font.
		///</exception>
		///<exception cref="System.IO.IOException">
		///Error reading or writing information.
		///</exception>
		public static int WindowWidth {
			get {
				if (_interfaceStub != null) {
					return _interfaceStub.WindowWidth;
				}
				else {
					return Console.WindowWidth;
				}
			}
			set {
				if (_interfaceStub != null) {
					_interfaceStub.WindowWidth = value;
				}
				else {
					Console.WindowWidth = value;
				}
			}
		}

#pragma warning disable 67
		///<summary>
		///Occurs when the System.ConsoleModifiers.Control modifier key (CTRL) and System.ConsoleKey.C
		///console key (C) are pressed simultaneously (CTRL+C).
		///</summary>
		public static event ConsoleCancelEventHandler CancelKeyPress;
#pragma warning restore 67

		///<summary>
		///Plays the sound of a beep through the console speaker.
		///</summary>
		///
		///<exception cref="System.Security.HostProtectionException">
		///This method was executed on a server, such as SQL Server, that does not permit
		///access to a user interface.
		///</exception>
		public static void Beep() {
			if (_interfaceStub != null) {
				_interfaceStub.Beep();
			}
			else {
				Console.Beep();
			}
		}
		///<summary>
		///Plays the sound of a beep of a specified frequency and duration through the
		///console speaker.
		///</summary>
		///
		///<param name="frequency">
		///The frequency of the beep, ranging from 37 to 32767 hertz.
		///</param>
		///<param name="duration">
		///The duration of the beep measured in milliseconds.
		///</param>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///frequency is less than 37 or more than 32767 hertz.  -or- duration is less
		///than or equal to zero.
		///</exception>
		///<exception cref="System.Security.HostProtectionException">
		///This method was executed on a server, such as SQL Server, that does not permit
		///access to the console.
		///</exception>
		public static void Beep(int frequency, int duration) {
			if (_interfaceStub != null) {
				_interfaceStub.Beep(frequency, duration);
			}
			else {
				Console.Beep(frequency, duration);
			}
		}
		///<summary>
		///Clears the console buffer and corresponding console window of display information.
		///</summary>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void Clear() {
			if (_interfaceStub != null) {
				_interfaceStub.Clear();
			}
			else {
				Console.Clear();
			}
		}
		///<summary>
		///Copies a specified source area of the screen buffer to a specified destination
		///area.
		///</summary>
		///
		///<param name="sourceLeft">
		///The leftmost column of the source area.
		///</param>
		///<param name="sourceTop">
		///The topmost row of the source area.
		///</param>
		///<param name="sourceWidth">
		///The number of columns in the source area.
		///</param>
		///<param name="sourceHeight">
		///The number of rows in the source area.
		///</param>
		///<param name="targetLeft">
		///The leftmost column of the destination area.
		///</param>
		///<param name="targetTop">
		///The topmost row of the destination area.
		///</param>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///One or more of the parameters is less than zero.  -or- sourceLeft or targetLeft
		///is greater than or equal to System.Console.BufferWidth.  -or- sourceTop or
		///targetTop is greater than or equal to System.Console.BufferHeight.  -or-
		///sourceTop + sourceHeight is greater than or equal to System.Console.BufferHeight.
		///-or- sourceLeft + sourceWidth is greater than or equal to System.Console.BufferWidth.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void MoveBufferArea(int sourceLeft, int sourceTop, int sourceWidth, int sourceHeight, int targetLeft, int targetTop) {
			if (_interfaceStub != null) {
				_interfaceStub.MoveBufferArea(sourceLeft, sourceTop, sourceWidth, sourceHeight, targetLeft, targetTop);
			}
			else {
				Console.MoveBufferArea(sourceLeft, sourceTop, sourceWidth, sourceHeight, targetLeft, targetTop);
			}
		}
		///<summary>
		///Copies a specified source area of the screen buffer to a specified destination
		///area.
		///</summary>
		///
		///<param name="sourceLeft">
		///The leftmost column of the source area.
		///</param>
		///<param name="sourceTop">
		///The topmost row of the source area.
		///</param>
		///<param name="sourceWidth">
		///The number of columns in the source area.
		///</param>
		///<param name="sourceHeight">
		///The number of rows in the source area.
		///</param>
		///<param name="targetLeft">
		///The leftmost column of the destination area.
		///</param>
		///<param name="targetTop">
		///The topmost row of the destination area.
		///</param>
		///<param name="sourceChar">
		///The character used to fill the source area.
		///</param>
		///<param name="sourceForeColor">
		///The foreground color used to fill the source area.
		///</param>
		///<param name="sourceBackColor">
		///The background color used to fill the source area.
		///</param>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///One or more of the parameters is less than zero.  -or- sourceLeft or targetLeft
		///is greater than or equal to System.Console.BufferWidth.  -or- sourceTop or
		///targetTop is greater than or equal to System.Console.BufferHeight.  -or-
		///sourceTop + sourceHeight is greater than or equal to System.Console.BufferHeight.
		///-or- sourceLeft + sourceWidth is greater than or equal to System.Console.BufferWidth.
		///</exception>
		///<exception cref="System.ArgumentException">
		///One or both of the color parameters is not a member of the System.ConsoleColor
		///enumeration.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void MoveBufferArea(int sourceLeft, int sourceTop, int sourceWidth, int sourceHeight, int targetLeft, int targetTop, char sourceChar, ConsoleColor sourceForeColor, ConsoleColor sourceBackColor) {
			if (_interfaceStub != null) {
				_interfaceStub.MoveBufferArea(sourceLeft, sourceTop, sourceWidth, sourceHeight, targetLeft, targetTop, sourceChar, sourceForeColor, sourceBackColor);
			}
			else {
				Console.MoveBufferArea(sourceLeft, sourceTop, sourceWidth, sourceHeight, targetLeft, targetTop, sourceChar, sourceForeColor, sourceBackColor);
			}
		}
		///<summary>
		///Acquires the standard error stream.
		///</summary>
		///<returns>
		///The standard error stream.
		///</returns>
		public static Stream OpenStandardError() {
			if (_interfaceStub != null) {
				return _interfaceStub.OpenStandardError();
			}
			else {
				return Console.OpenStandardError();
			}
		}
		///<summary>
		///Acquires the standard error stream, which is set to a specified buffer size.
		///</summary>
		///
		///<param name="bufferSize">
		///The internal stream buffer size.
		///</param>
		///<returns>
		///The standard error stream.
		///</returns>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///bufferSize is less than or equal to zero.
		///</exception>
		public static Stream OpenStandardError(int bufferSize) {
			if (_interfaceStub != null) {
				return _interfaceStub.OpenStandardError(bufferSize);
			}
			else {
				return Console.OpenStandardError(bufferSize);
			}
		}
		///<summary>
		///Acquires the standard input stream.
		///</summary>
		///<returns>
		///The standard input stream.
		///</returns>
		public static Stream OpenStandardInput() {
			if (_interfaceStub != null) {
				return _interfaceStub.OpenStandardInput();
			}
			else {
				return Console.OpenStandardInput();
			}
		}
		///<summary>
		///Acquires the standard input stream, which is set to a specified buffer size.
		///</summary>
		///
		///<param name="bufferSize">
		///The internal stream buffer size.
		///</param>
		///<returns>
		///The standard input stream.
		///</returns>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///bufferSize is less than or equal to zero.
		///</exception>
		public static Stream OpenStandardInput(int bufferSize) {
			if (_interfaceStub != null) {
				return _interfaceStub.OpenStandardInput(bufferSize);
			}
			else {
				return Console.OpenStandardInput(bufferSize);
			}
		}
		///<summary>
		///Acquires the standard output stream.
		///</summary>
		///<returns>
		///The standard output stream.
		///</returns>
		public static Stream OpenStandardOutput() {
			if (_interfaceStub != null) {
				return _interfaceStub.OpenStandardOutput();
			}
			else {
				return Console.OpenStandardOutput();
			}
		}
		///<summary>
		///Acquires the standard output stream, which is set to a specified buffer size.
		///</summary>
		///
		///<param name="bufferSize">
		///The internal stream buffer size.
		///</param>
		///<returns>
		///The standard output stream.
		///</returns>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///bufferSize is less than or equal to zero.
		///</exception>
		public static Stream OpenStandardOutput(int bufferSize) {
			if (_interfaceStub != null) {
				return _interfaceStub.OpenStandardOutput(bufferSize);
			}
			else {
				return Console.OpenStandardOutput(bufferSize);
			}
		}
		///<summary>
		///Reads the next character from the standard input stream.
		///</summary>
		///<returns>
		///The next character from the input stream, or negative one (-1) if there are
		///currently no more characters to be read.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static int Read() {
			if (_interfaceStub != null) {
				return _interfaceStub.Read();
			}
			else {
				return Console.Read();
			}
		}
		///<summary>
		///Obtains the next character or function key pressed by the user. The pressed
		///key is displayed in the console window.
		///</summary>
		///<returns>
		///A System.ConsoleKeyInfo object that describes the System.ConsoleKey constant
		///and Unicode character, if any, that correspond to the pressed console key.
		///The System.ConsoleKeyInfo object also describes, in a bitwise combination
		///of System.ConsoleModifiers values, whether one or more SHIFT, ALT, or CTRL
		///modifier keys was pressed simultaneously with the console key.
		///</returns>
		///
		///<exception cref="System.InvalidOperationException">
		///The System.Console.In property is redirected from some stream other than
		///the console.
		///</exception>
		public static ConsoleKeyInfo ReadKey() {
			if (_interfaceStub != null) {
				return _interfaceStub.ReadKey();
			}
			else {
				return Console.ReadKey();
			}
		}
		///<summary>
		///Obtains the next character or function key pressed by the user. The pressed
		///key is optionally displayed in the console window.
		///</summary>
		///
		///<param name="intercept">
		///Determines whether to display the pressed key in the console window. true
		///to not display the pressed key; otherwise, false.
		///</param>
		///<returns>
		///A System.ConsoleKeyInfo object that describes the System.ConsoleKey constant
		///and Unicode character, if any, that correspond to the pressed console key.
		///The System.ConsoleKeyInfo object also describes, in a bitwise combination
		///of System.ConsoleModifiers values, whether one or more SHIFT, ALT, or CTRL
		///modifier keys was pressed simultaneously with the console key.
		///</returns>
		///
		///<exception cref="System.InvalidOperationException">
		///The System.Console.In property is redirected from some stream other than
		///the console.
		///</exception>
		public static ConsoleKeyInfo ReadKey(bool intercept) {
			if (_interfaceStub != null) {
				return _interfaceStub.ReadKey(intercept);
			}
			else {
				return Console.ReadKey(intercept);
			}
		}
		///<summary>
		///Reads the next line of characters from the standard input stream.
		///</summary>
		///<returns>
		///The next line of characters from the input stream, or null if no more lines
		///are available.
		///</returns>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		///<exception cref="System.OutOfMemoryException">
		///There is insufficient memory to allocate a buffer for the returned string.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///The number of characters in the next line of characters is greater than System.Int32.MaxValue.
		///</exception>
		public static string ReadLine() {
			if (_interfaceStub != null) {
				return _interfaceStub.ReadLine();
			}
			else {
				return Console.ReadLine();
			}
		}
		///<summary>
		///Sets the foreground and background console colors to their defaults.
		///</summary>
		///
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void ResetColor() {
			if (_interfaceStub != null) {
				_interfaceStub.ResetColor();
			}
			else {
				Console.ResetColor();
			}
		}
		///<summary>
		///Sets the height and width of the screen buffer area to the specified values.
		///</summary>
		///
		///<param name="width">
		///The width of the buffer area measured in columns.
		///</param>
		///<param name="height">
		///The height of the buffer area measured in rows.
		///</param>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///height or width is less than or equal to zero.  -or- height or width is greater
		///than or equal to System.Int16.MaxValue.  -or- width is less than System.Console.WindowLeft
		///+ System.Console.WindowWidth.  -or- height is less than System.Console.WindowTop
		///+ System.Console.WindowHeight.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void SetBufferSize(int width, int height) {
			if (_interfaceStub != null) {
				_interfaceStub.SetBufferSize(width, height);
			}
			else {
				Console.SetBufferSize(width, height);
			}
		}
		///<summary>
		///Sets the position of the cursor.
		///</summary>
		///
		///<param name="left">
		///The column position of the cursor.
		///</param>
		///<param name="top">
		///The row position of the cursor.
		///</param>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///left or top is less than zero.  -or- left is greater than or equal to System.Console.BufferWidth.
		///-or- top is greater than or equal to System.Console.BufferHeight.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void SetCursorPosition(int left, int top) {
			if (_interfaceStub != null) {
				_interfaceStub.SetCursorPosition(left, top);
			}
			else {
				Console.SetCursorPosition(left, top);
			}
		}
		///<summary>
		///Sets the System.Console.Error property to the specified System.IO.TextWriter
		///object.
		///</summary>
		///
		///<param name="newError">
		///A System.IO.TextWriter stream that is the new standard error output.
		///</param>
		///
		///<exception cref="System.ArgumentNullException">
		///newError is null.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		public static void SetError(TextWriter newError) {
			if (_interfaceStub != null) {
				_interfaceStub.SetError(newError);
			}
			else {
				Console.SetError(newError);
			}
		}
		///<summary>
		///Sets the System.Console.In property to the specified System.IO.TextReader
		///object.
		///</summary>
		///
		///<param name="newIn">
		///A System.IO.TextReader stream that is the new standard input.
		///</param>
		///
		///<exception cref="System.ArgumentNullException">
		///newIn is null.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		public static void SetIn(TextReader newIn) {
			if (_interfaceStub != null) {
				_interfaceStub.SetIn(newIn);
			}
			else {
				Console.SetIn(newIn);
			}
		}
		///<summary>
		///Sets the System.Console.Out property to the specified System.IO.TextWriter
		///object.
		///</summary>
		///
		///<param name="newOut">
		///A System.IO.TextWriter stream that is the new standard output.
		///</param>
		///
		///<exception cref="System.ArgumentNullException">
		///newOut is null.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The caller does not have the required permission.
		///</exception>
		public static void SetOut(TextWriter newOut) {
			if (_interfaceStub != null) {
				_interfaceStub.SetOut(newOut);
			}
			else {
				Console.SetOut(newOut);
			}
		}
		///<summary>
		///Sets the position of the console window relative to the screen buffer.
		///</summary>
		///
		///<param name="left">
		///The column position of the upper left corner of the console window.
		///</param>
		///<param name="top">
		///The row position of the upper left corner of the console window.
		///</param>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///left or top is less than zero.  -or- left + System.Console.WindowWidth is
		///greater than System.Console.BufferWidth.  -or- top + System.Console.WindowHeight
		///is greater than System.Console.BufferHeight.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void SetWindowPosition(int left, int top) {
			if (_interfaceStub != null) {
				_interfaceStub.SetWindowPosition(left, top);
			}
			else {
				Console.SetWindowPosition(left, top);
			}
		}
		///<summary>
		///Sets the height and width of the console window to the specified values.
		///</summary>
		///
		///<param name="width">
		///The width of the console window measured in columns.
		///</param>
		///<param name="height">
		///The height of the console window measured in rows.
		///</param>
		///
		///<exception cref="System.ArgumentOutOfRangeException">
		///width or height is less than or equal to zero.  -or- width plus System.Console.WindowLeft
		///or height plus System.Console.WindowTop is greater than or equal to System.Int16.MaxValue.
		///-or- width or height is greater than the largest possible window width or
		///height for the current screen resolution and console font.
		///</exception>
		///<exception cref="System.Security.SecurityException">
		///The user does not have permission to perform this action.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void SetWindowSize(int width, int height) {
			if (_interfaceStub != null) {
				_interfaceStub.SetWindowSize(width, height);
			}
			else {
				Console.SetWindowSize(width, height);
			}
		}
		///<summary>
		///Writes the text representation of the specified Boolean value to the standard
		///output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void Write(bool value) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(value);
			}
			else {
				Console.Write(value);
			}
		}
		///<summary>
		///Writes the specified Unicode character value to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void Write(char value) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(value);
			}
			else {
				Console.Write(value);
			}
		}
		///<summary>
		///Writes the specified array of Unicode characters to the standard output stream.
		///</summary>
		///
		///<param name="buffer">
		///A Unicode character array.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void Write(char[] buffer) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(buffer);
			}
			else {
				Console.Write(buffer);
			}
		}
		///<summary>
		///Writes the text representation of the specified System.Decimal value to the
		///standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void Write(decimal value) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(value);
			}
			else {
				Console.Write(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified double-precision floating-point
		///value to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void Write(double value) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(value);
			}
			else {
				Console.Write(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified single-precision floating-point
		///value to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void Write(float value) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(value);
			}
			else {
				Console.Write(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified 32-bit signed integer value
		///to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void Write(int value) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(value);
			}
			else {
				Console.Write(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified 64-bit signed integer value
		///to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void Write(long value) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(value);
			}
			else {
				Console.Write(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified object to the standard output
		///stream.
		///</summary>
		///
		///<param name="value">
		///The value to write, or null.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void Write(object value) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(value);
			}
			else {
				Console.Write(value);
			}
		}
		///<summary>
		///Writes the specified string value to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void Write(string value) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(value);
			}
			else {
				Console.Write(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified 32-bit unsigned integer value
		///to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		[CLSCompliant(false)]
		public static void Write(uint value) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(value);
			}
			else {
				Console.Write(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified 64-bit unsigned integer value
		///to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		[CLSCompliant(false)]
		public static void Write(ulong value) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(value);
			}
			else {
				Console.Write(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified object to the standard output
		///stream using the specified format information.
		///</summary>
		///
		///<param name="format">
		///A composite format string.
		///</param>
		///<param name="arg0">
		///An object to write using format.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///format is null.
		///</exception>
		///<exception cref="System.FormatException">
		///The format specification in format is invalid.
		///</exception>
		public static void Write(string format, object arg0) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(format, arg0);
			}
			else {
				Console.Write(format, arg0);
			}
		}
		///<summary>
		///Writes the text representation of the specified array of objects to the standard
		///output stream using the specified format information.
		///</summary>
		///
		///<param name="format">
		///A composite format string.
		///</param>
		///<param name="arg">
		///An array of objects to write using format.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///format or arg is null.
		///</exception>
		///<exception cref="System.FormatException">
		///The format specification in format is invalid.
		///</exception>
		public static void Write(string format, params object[] arg) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(format, arg);
			}
			else {
				Console.Write(format, arg);
			}
		}
		///<summary>
		///Writes the specified subarray of Unicode characters to the standard output
		///stream.
		///</summary>
		///
		///<param name="buffer">
		///An array of Unicode characters.
		///</param>
		///<param name="index">
		///The starting position in buffer.
		///</param>
		///<param name="count">
		///The number of characters to write.
		///</param>
		///
		///<exception cref="System.ArgumentNullException">
		///buffer is null.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///index or count is less than zero.
		///</exception>
		///<exception cref="System.ArgumentException">
		///index plus count specify a position that is not within buffer.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void Write(char[] buffer, int index, int count) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(buffer, index, count);
			}
			else {
				Console.Write(buffer, index, count);
			}
		}
		///<summary>
		///Writes the text representation of the specified objects to the standard output
		///stream using the specified format information.
		///</summary>
		///
		///<param name="format">
		///A composite format string.
		///</param>
		///<param name="arg0">
		///The first object to write using format.
		///</param>
		///<param name="arg1">
		///The second object to write using format.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///format is null.
		///</exception>
		///<exception cref="System.FormatException">
		///The format specification in format is invalid.
		///</exception>
		public static void Write(string format, object arg0, object arg1) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(format, arg0, arg1);
			}
			else {
				Console.Write(format, arg0, arg1);
			}
		}
		///<summary>
		///Writes the text representation of the specified objects to the standard output
		///stream using the specified format information.
		///</summary>
		///
		///<param name="format">
		///A composite format string.
		///</param>
		///<param name="arg0">
		///The first object to write using format.
		///</param>
		///<param name="arg1">
		///The second object to write using format.
		///</param>
		///<param name="arg2">
		///The third object to write using format.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///format is null.
		///</exception>
		///<exception cref="System.FormatException">
		///The format specification in format is invalid.
		///</exception>
		public static void Write(string format, object arg0, object arg1, object arg2) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(format, arg0, arg1, arg2);
			}
			else {
				Console.Write(format, arg0, arg1, arg2);
			}
		}
		///<summary>
		///Writes the text representation of the specified objects and variable-length
		///parameter list to the standard output stream using the specified format information.
		///</summary>
		///
		///<param name="format">
		///A composite format string.
		///</param>
		///<param name="arg0">
		///The first object to write using format.
		///</param>
		///<param name="arg1">
		///The second object to write using format.
		///</param>
		///<param name="arg2">
		///The third object to write using format.
		///</param>
		///<param name="arg3">
		///The fourth object to write using format.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///format is null.
		///</exception>
		///<exception cref="System.FormatException">
		///The format specification in format is invalid.
		///</exception>
		[CLSCompliant(false)]
		public static void Write(string format, object arg0, object arg1, object arg2, object arg3) {
			if (_interfaceStub != null) {
				_interfaceStub.Write(format, arg0, arg1, arg2, arg3);
			}
			else {
				Console.Write(format, arg0, arg1, arg2, arg3);
			}
		}
		///<summary>
		///Writes the current line terminator to the standard output stream.
		///</summary>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void WriteLine() {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine();
			}
			else {
				Console.WriteLine();
			}
		}
		///<summary>
		///Writes the text representation of the specified Boolean value, followed by
		///the current line terminator, to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void WriteLine(bool value) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(value);
			}
			else {
				Console.WriteLine(value);
			}
		}
		///<summary>
		///Writes the specified Unicode character, followed by the current line terminator,
		///value to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void WriteLine(char value) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(value);
			}
			else {
				Console.WriteLine(value);
			}
		}
		///<summary>
		///Writes the specified array of Unicode characters, followed by the current
		///line terminator, to the standard output stream.
		///</summary>
		///
		///<param name="buffer">
		///A Unicode character array.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void WriteLine(char[] buffer) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(buffer);
			}
			else {
				Console.WriteLine(buffer);
			}
		}
		///<summary>
		///Writes the text representation of the specified System.Decimal value, followed
		///by the current line terminator, to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void WriteLine(decimal value) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(value);
			}
			else {
				Console.WriteLine(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified double-precision floating-point
		///value, followed by the current line terminator, to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void WriteLine(double value) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(value);
			}
			else {
				Console.WriteLine(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified single-precision floating-point
		///value, followed by the current line terminator, to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void WriteLine(float value) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(value);
			}
			else {
				Console.WriteLine(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified 32-bit signed integer value,
		///followed by the current line terminator, to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void WriteLine(int value) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(value);
			}
			else {
				Console.WriteLine(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified 64-bit signed integer value,
		///followed by the current line terminator, to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void WriteLine(long value) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(value);
			}
			else {
				Console.WriteLine(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified object, followed by the current
		///line terminator, to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void WriteLine(object value) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(value);
			}
			else {
				Console.WriteLine(value);
			}
		}
		///<summary>
		///Writes the specified string value, followed by the current line terminator,
		///to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void WriteLine(string value) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(value);
			}
			else {
				Console.WriteLine(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified 32-bit unsigned integer value,
		///followed by the current line terminator, to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		[CLSCompliant(false)]
		public static void WriteLine(uint value) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(value);
			}
			else {
				Console.WriteLine(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified 64-bit unsigned integer value,
		///followed by the current line terminator, to the standard output stream.
		///</summary>
		///
		///<param name="value">
		///The value to write.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		[CLSCompliant(false)]
		public static void WriteLine(ulong value) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(value);
			}
			else {
				Console.WriteLine(value);
			}
		}
		///<summary>
		///Writes the text representation of the specified object, followed by the current
		///line terminator, to the standard output stream using the specified format
		///information.
		///</summary>
		///
		///<param name="format">
		///A composite format string.
		///</param>
		///<param name="arg0">
		///An object to write using format.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///format is null.
		///</exception>
		///<exception cref="System.FormatException">
		///The format specification in format is invalid.
		///</exception>
		public static void WriteLine(string format, object arg0) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(format, arg0);
			}
			else {
				Console.WriteLine(format, arg0);
			}
		}
		///<summary>
		///Writes the text representation of the specified array of objects, followed
		///by the current line terminator, to the standard output stream using the specified
		///format information.
		///</summary>
		///
		///<param name="format">
		///A composite format string.
		///</param>
		///<param name="arg">
		///An array of objects to write using format.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///format or arg is null.
		///</exception>
		///<exception cref="System.FormatException">
		///The format specification in format is invalid.
		///</exception>
		public static void WriteLine(string format, params object[] arg) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(format, arg);
			}
			else {
				Console.WriteLine(format, arg);
			}
		}
		///<summary>
		///Writes the specified subarray of Unicode characters, followed by the current
		///line terminator, to the standard output stream.
		///</summary>
		///
		///<param name="buffer">
		///An array of Unicode characters.
		///</param>
		///<param name="index">
		///The starting position in buffer.
		///</param>
		///<param name="count">
		///The number of characters to write.
		///</param>
		///
		///<exception cref="System.ArgumentNullException">
		///buffer is null.
		///</exception>
		///<exception cref="System.ArgumentOutOfRangeException">
		///index or count is less than zero.
		///</exception>
		///<exception cref="System.ArgumentException">
		///index plus count specify a position that is not within buffer.
		///</exception>
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		public static void WriteLine(char[] buffer, int index, int count) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(buffer, index, count);
			}
			else {
				Console.WriteLine(buffer, index, count);
			}
		}
		///<summary>
		///Writes the text representation of the specified objects, followed by the
		///current line terminator, to the standard output stream using the specified
		///format information.
		///</summary>
		///
		///<param name="format">
		///A composite format string.
		///</param>
		///<param name="arg0">
		///The first object to write using format.
		///</param>
		///<param name="arg1">
		///The second object to write using format.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///format is null.
		///</exception>
		///<exception cref="System.FormatException">
		///The format specification in format is invalid.
		///</exception>
		public static void WriteLine(string format, object arg0, object arg1) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(format, arg0, arg1);
			}
			else {
				Console.WriteLine(format, arg0, arg1);
			}
		}
		///<summary>
		///Writes the text representation of the specified objects, followed by the
		///current line terminator, to the standard output stream using the specified
		///format information.
		///</summary>
		///
		///<param name="format">
		///A composite format string.
		///</param>
		///<param name="arg0">
		///The first object to write using format.
		///</param>
		///<param name="arg1">
		///The second object to write using format.
		///</param>
		///<param name="arg2">
		///The third object to write using format.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///format is null.
		///</exception>
		///<exception cref="System.FormatException">
		///The format specification in format is invalid.
		///</exception>
		public static void WriteLine(string format, object arg0, object arg1, object arg2) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(format, arg0, arg1, arg2);
			}
			else {
				Console.WriteLine(format, arg0, arg1, arg2);
			}
		}
		///<summary>
		///Writes the text representation of the specified objects and variable-length
		///parameter list, followed by the current line terminator, to the standard
		///output stream using the specified format information.
		///</summary>
		///
		///<param name="format">
		///A composite format string.
		///</param>
		///<param name="arg0">
		///The first object to write using format.
		///</param>
		///<param name="arg1">
		///The second object to write using format.
		///</param>
		///<param name="arg2">
		///The third object to write using format.
		///</param>
		///<param name="arg3">
		///The fourth object to write using format.
		///</param>
		///
		///<exception cref="System.IO.IOException">
		///An I/O error occurred.
		///</exception>
		///<exception cref="System.ArgumentNullException">
		///format is null.
		///</exception>
		///<exception cref="System.FormatException">
		///The format specification in format is invalid.
		///</exception>
		[CLSCompliant(false)]
		public static void WriteLine(string format, object arg0, object arg1, object arg2, object arg3) {
			if (_interfaceStub != null) {
				_interfaceStub.WriteLine(format, arg0, arg1, arg2, arg3);
			}
			else {
				Console.WriteLine(format, arg0, arg1, arg2, arg3);
			}
		}
	}
}
