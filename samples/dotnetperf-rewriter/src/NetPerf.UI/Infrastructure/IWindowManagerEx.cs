using System;
using Caliburn.Micro;
using NetPerf.Common;

namespace MetadataDashboard.NetperfUI.Infrastructure {
	public interface IWindowManagerEx : IWindowManager {

		/// <summary>
		/// Opens a model, non-re-sizable foreground window that displays a simple text message.
		/// </summary>
		/// <param name="message">The message to display</param>
		/// <param name="title">The window's title.</param>
		/// <param name="context">The context</param>
		void ShowNotification(string message, string title = null, object context = null);

		/// <summary>
		/// Closes the notification window opened by the <see cref="ShowNotification"/> method.
		/// </summary>
		void CloseNotification();

		/// <summary>
		/// Displays a modal dialog box that displays a short message.
		/// </summary>
		/// <param name="message">The message to show.</param>
		void ShowMessage(string message);

		bool Confirm(string message, string caption = "");

		/// <summary>
		/// Displays a modal dialog box asking the user to 'open' a single folder.
		/// </summary>
		/// <param name="originalPath">if set, the Open Folder dialog will attempt to show this folder when it opens.</param>
		/// <param name="title">The title to show in the open folder window.</param>
		/// <returns><see cref="null"/> if no folder was chosen, otherwise the full file system path of the chosen folder.</returns>
		string OpenFolder(string originalPath, string title = "Choose a folder to open");

		/// <summary>
		/// Displays a modal dialog box asking the user to 'open' a single file.
		/// If a file is selected, its path is passed to <paramref name="resultAssigner"/>.
		/// </summary>
		/// <param name="originalPath">Used to set the initial directory of the dialog.</param>
		/// <param name="filterFormat">
		/// Describes the extensions of the files the user is allowed to choose. Used to specify
		/// that e.g. only html or xml files should be opened. The format is described in full on
		/// http://msdn.microsoft.com/en-us/library/system.windows.controls.openfiledialog.filter(VS.95).aspx
		/// but the general format is
		/// 'user friendly name|extension;extension;extension|user friendly name|extension'
		/// </param>
		/// <param name="resultAssigner">Only called if a file was chosen. If one was, the full path of the chosen file is passed in.</param>
		/// <param name="title">The title to show in the open file window</param>
		void PickAFile(string originalPath, string filterFormat, Action<string> resultAssigner, string title = "Choose file(s) to open");

		/// <summary>
		/// Displays a modal dialog box asking the user to 'open' a file (or several files if <paramref name="canChooseMultiple"/> is true).
		/// </summary>
		/// <param name="title">The title to show in the open file window</param>
		/// <param name="canChooseMultiple">True if multiple file selections are allowed, False if they are not.</param>
		/// <param name="originalPath">Used to set the initial directory of the dialog.</param>
		/// <param name="filterFormat">
		/// Describes the extensions of the files the user is allowed to choose. Used to specify
		/// that e.g. only html or xml files should be opened. The format is described in full on
		/// http://msdn.microsoft.com/en-us/library/system.windows.controls.openfiledialog.filter(VS.95).aspx
		/// but the general format is
		/// 'user friendly name|extension;extension;estension|user friendly name|extension'
		/// </param>
		/// <returns>An array of all the files selected, or an empty array if no files were selected.</returns>
		FilePath[] OpenFiles(string title = "Choose file(s) to open", bool canChooseMultiple = false, string originalPath = null, string filterFormat = "All Files (*.*)|*.*");

		/// <summary>
		/// Displays a modal dialog asking the user where they want to save a file.
		/// </summary>
		/// <param name="suggestedPath">Used to set the initial directory of the dialog, as well as a suggested dave file name.</param>
		/// <param name="filterFormat">
		/// Describes the extensions of the files the user is allowed to choose. Used to specify
		/// that e.g. only html or xml files should be opened. The format is described in full on
		/// http://msdn.microsoft.com/en-us/library/system.windows.controls.openfiledialog.filter(VS.95).aspx
		/// but the general format is
		/// 'user friendly name|extension;extension;estension|user friendly name|extension'
		/// </param>
		/// <param name="title">The title to show in the save file window</param>
		/// <returns>The save path, or null if none was selected.</returns>
		FilePath SaveFile(string suggestedPath = null, string filterFormat = "All Files (*.*)|*.*", string title = "Save a File");

		/// <summary>
		/// Displays a modal dialog asking the user where they want to save a file.
		/// If a file name is selected, its path is passed to <paramref name="performSaveWork"/>.
		/// </summary>
		/// <param name="performSaveWork">Only called if a file was chosen. If one was, its path is passed in.</param>
		/// <param name="filterFormat">
		/// Describes the extensions of the files the user is allowed to choose. Used to specify
		/// that e.g. only html or xml files should be opened. The format is described in full on
		/// http://msdn.microsoft.com/en-us/library/system.windows.controls.openfiledialog.filter(VS.95).aspx
		/// but the general format is
		/// 'user friendly name|extension;extension;estension|user friendly name|extension'
		/// </param>
		/// <param name="suggestedPath">Used to set the initial directory of the dialog, as well as a suggested dave file name.</param>
		/// <param name="title">The title to show in the save file window</param>
		void SaveFile(Action<FilePath> performSaveWork, string filterFormat = "All Files (*.*)|*.*", string suggestedPath = null, string title = "Save a File");
	}
}
