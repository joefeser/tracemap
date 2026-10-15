using System;
using System.IO;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using MetadataDashboard.NetperfUI.Areas.ViewModels;
using Microsoft.Win32;
using NetPerf.Common;

namespace MetadataDashboard.NetperfUI.Infrastructure {
	internal class WindowManagerEx : WindowManager, IWindowManagerEx {

		private IScreen _notificationWin;
		private object _notificationContext;
		private Window m_ownerWin;

		public void ShowNotification(string message, string title = null, object context = null) {
			// only one of these can be active at a time
			CloseNotification();

			_notificationContext = context;
			_notificationWin = new MessageWindowVM(message, title);
			var notifyWin = CreateWindow(_notificationWin, true, _notificationContext, null);

			// disable the parent window partly to simulate a modal window,
			// and partly because the user shouldn't be able to do anything with
			// the parent once this window is shown.
			m_ownerWin = InferOwnerOf(notifyWin);
			if (m_ownerWin != null) {
				m_ownerWin.IsEnabled = false;
			}

			notifyWin.Show();
		}

		public void CloseNotification() {
			if (_notificationWin != null) {

				// re-enable the parent window (undo the disable that was done in ShowNotification)
				if (m_ownerWin != null) {
					m_ownerWin.IsEnabled = true;
				}

				_notificationWin.TryClose();
				_notificationWin = null;
				_notificationContext = null;
				m_ownerWin = null;
			}
		}

		public void ShowMessage(string message) {
			MessageBox.Show(GetActiveWindow(), message);
		}

		public bool Confirm(string message, string caption = "") {
			return MessageBoxResult.Yes == MessageBox.Show(GetActiveWindow(), message, caption, MessageBoxButton.YesNo);
		}

		/// <summary>
		/// Displays a modal dialog box asking the user to 'open' a single folder.
		/// </summary>
		/// <param name="originalPath">if set, the Open Folder dialog will attempt to show this folder when it opens.</param>
		/// <param name="title">The title to show in the open folder window.</param>
		/// <returns><see cref="null"/> if no folder was chosen, otherwise the full filesystem path of the chosen folder.</returns>
		public string OpenFolder(string originalPath = null, string title = "Choose a folder to open") {

			// we're going to try to show the same "open folder" dialog that Visual Studio uses,
			// but if we can't show it we will degrade gracefully to the standard "open folder" dialog
			if (Environment.OSVersion.Version.Major >= 6) {
				var r = new Reflector("System.Windows.Forms");


				var ofd = new System.Windows.Forms.OpenFileDialog();

				ofd.Filter = "Folders|\n";
				ofd.AddExtension = false;
				ofd.CheckFileExists = false;
				ofd.DereferenceLinks = true;
				ofd.Multiselect = false;
				ofd.InitialDirectory = string.IsNullOrEmpty(originalPath) ? Environment.CurrentDirectory : originalPath;
				ofd.RestoreDirectory = true;

				Type typeIFileDialog = r.GetType("FileDialogNative.IFileDialog");
				object dialog = r.Call(ofd, "CreateVistaDialog");
				r.Call(ofd, "OnBeforeVistaDialog", dialog);

				uint options = (uint)r.CallAs(typeof(System.Windows.Forms.FileDialog), ofd, "GetOptions");
				options |= (uint)r.GetEnum("FileDialogNative.FOS", "FOS_PICKFOLDERS");
				r.CallAs(typeIFileDialog, dialog, "SetOptions", options);

				uint num = 0;
				object pfde = r.New("FileDialog.VistaDialogEvents", ofd);
				object[] parameters = new object[] { pfde, num };
				r.CallAs2(typeIFileDialog, dialog, "Advise", parameters);
				num = (uint)parameters[1];
				try {
					var owner = GetActiveWindow();
					var winId = new System.Windows.Interop.WindowInteropHelper(owner).Handle;

					if (0 == (int)r.CallAs(typeIFileDialog, dialog, "Show", winId)) {
						return ofd.FileName;
					}
					else {
						return null;
					}
				}
				finally {
					r.CallAs(typeIFileDialog, dialog, "Unadvise", num);
					GC.KeepAlive(pfde);
				}
			}
			else {
				var prevDir = Environment.CurrentDirectory;
				try {
					var fbd = new System.Windows.Forms.FolderBrowserDialog();
					fbd.Description = title;
					fbd.SelectedPath = originalPath;
					fbd.ShowNewFolderButton = false;
					if (fbd.ShowDialog() != System.Windows.Forms.DialogResult.OK) {
						return null;
					}
					else {
						return fbd.SelectedPath;
					}
				}
				finally {
					// restore the current directory
					Environment.CurrentDirectory = prevDir;
				}
			}
		}

		public void PickAFile(string originalPath, string filterFormat, Action<string> resultAssigner, string title = "Choose file(s) to open") {
			var path = OpenFiles(
				title: title,
				canChooseMultiple: false,
				originalPath: originalPath,
				filterFormat: filterFormat);

			if (path.Length > 0) {
				resultAssigner(path[0].FullPath);
			}
		}

		public FilePath[] OpenFiles(string title = "Choose file(s) to open", bool canChooseMultiple = false, string originalPath = null, string filterFormat = "All Files (*.*)|*.*") {
			var initialDir = originalPath.IsNullOrWhitespace()
				? null
				: (PathStub.HasExtension(originalPath)
					? PathStub.GetDirectoryName(originalPath)
					: originalPath);

			var x = new OpenFileDialog() {
				AddExtension = true,
				CheckFileExists = true,
				CheckPathExists = true,
				RestoreDirectory = true,
				Multiselect = canChooseMultiple,
				Title = title,
				InitialDirectory = initialDir,
				Filter = filterFormat
			};

			var result = x.ShowDialog(GetActiveWindow());
			if (result.GetValueOrDefault()) {
				return x.FileNames.Select(n => FilePath.Parse(n)).ToArray();
			}

			return new FilePath[0];
		}

		public void SaveFile(Action<FilePath> performSaveWork, string filterFormat = "All Files (*.*)|*.*", string suggestedPath = null, string title = "Save a File") {
			var savePath = SaveFile(suggestedPath, filterFormat, title);

			if (savePath != null) {
				performSaveWork(savePath);
			}
		}

		public FilePath SaveFile(string suggestedPath = null, string filterFormat = "All Files (*.*)|*.*", string title = "Save a File") {
			var initialDir = suggestedPath.IsNullOrWhitespace()
				? null
				: (PathStub.HasExtension(suggestedPath)
					? PathStub.GetDirectoryName(suggestedPath)
					: suggestedPath);

			var suggestedName = suggestedPath.IsNullOrWhitespace()
				? null
				: (PathStub.HasExtension(suggestedPath)
					? PathStub.GetFileName(suggestedPath)
					: null);

			var x = new SaveFileDialog() {
				CheckFileExists = false,
				CheckPathExists = false,
				CreatePrompt = false, // if a file that doesn't exist is specified, dont ask the user to confirm
				Filter = filterFormat,
				InitialDirectory = initialDir,
				RestoreDirectory = true,
				OverwritePrompt = true,
				Title = title,
				FileName = suggestedName
			};

			var result = x.ShowDialog(GetActiveWindow());
			if (result.GetValueOrDefault()) {
				return FilePath.Parse(x.FileName);
			}

			return null;
		}

		private Window GetActiveWindow() {
			if (Application.Current == null) {
				return null;
			}

			Window active = null;
			for (int i = 0; i < Application.Current.Windows.Count; i++) {
				if (Application.Current.Windows[i].IsActive) {
					active = Application.Current.Windows[i];
					break;
				}
			}

			return active ?? Application.Current.MainWindow;
		}

	}
}
