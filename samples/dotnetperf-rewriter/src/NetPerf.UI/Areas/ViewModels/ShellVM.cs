using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using MetadataDashboard.NetperfUI.Areas.Profiling.ViewModels;
using MetadataDashboard.NetperfUI.Infrastructure;
using MetadataDashboard.NetperfUI.Infrastructure.Utilities;
using NetPerf.Common;
using NetPerf.Common.Configuration;
using MetadataDashboard.NetperfUI.Areas.UISettings.ViewModels;

namespace MetadataDashboard.NetperfUI.Areas.ViewModels {
	internal class ShellVM : Conductor<Screen> {

		private readonly IAppConfig _config;
		private readonly IWindowManagerEx _winManager;
		private readonly IEventAggregator _events;
		private readonly string _baseDisplayName;
		private IAutoUpdateAdaptor _autoUpdate;
		private InstrumentationConfigVM _currentSettings;

		private InstrumentationConfigVM CurrentSettings {
			get {
				return _currentSettings;
			}
			set {
				if (_currentSettings != value) {
					if (_currentSettings != null) {
						_currentSettings.PropertyChanged -= new PropertyChangedEventHandler(_currentSettings_PropertyChanged);
					}
					
					_currentSettings = value;

					if (_currentSettings != null) {
						_currentSettings.PropertyChanged += new PropertyChangedEventHandler(_currentSettings_PropertyChanged);
					}
				}
			}
		}

		void _currentSettings_PropertyChanged(object sender, PropertyChangedEventArgs e) {
			if (e.PropertyName == "IsDirty" || e.PropertyName == "") {
				UpdateDisplayName();
			}
		}


		public double? ScreenLeft {
			get {
				if (this.ScreenState == WindowState.Normal) {
					if (double.IsNaN(_config.MainWindowPosition.ScreenLeft)) {
						return Math.Abs(System.Windows.SystemParameters.PrimaryScreenWidth - _config.MainWindowPosition.ScreenWidth) / 2;
					}

					return _config.MainWindowPosition.ScreenLeft;
				}

				return null;
			}
			set {
				if (this.ScreenState == WindowState.Normal && _config.MainWindowPosition.ScreenLeft != value && value.HasValue) {
					_config.MainWindowPosition.ScreenLeft = value.Value;
					NotifyOfPropertyChange(() => ScreenLeft);
				}
			}
		}

		public double? ScreenTop {
			get {
				if (this.ScreenState == WindowState.Normal) {
					if (double.IsNaN(_config.MainWindowPosition.ScreenTop)) {
						return Math.Abs(System.Windows.SystemParameters.PrimaryScreenHeight - _config.MainWindowPosition.ScreenHeight) / 2;
					}

					return _config.MainWindowPosition.ScreenTop;
				}

				return null;
			}
			set {
				if (this.ScreenState == WindowState.Normal && _config.MainWindowPosition.ScreenTop != value && value.HasValue) {
					_config.MainWindowPosition.ScreenTop = value.Value;
					NotifyOfPropertyChange(() => ScreenTop);
				}
			}
		}

		public double ScreenHeight {
			get {
				return _config.MainWindowPosition.ScreenHeight;
			}
			set {
				if (_config.MainWindowPosition.ScreenHeight != value) {
					_config.MainWindowPosition.ScreenHeight = value;
					NotifyOfPropertyChange(() => ScreenHeight);
				}
			}
		}

		public double ScreenWidth {
			get {
				return _config.MainWindowPosition.ScreenWidth;
			}
			set {
				if (_config.MainWindowPosition.ScreenWidth != value) {
					_config.MainWindowPosition.ScreenWidth = value;
					NotifyOfPropertyChange(() => ScreenWidth);
				}
			}
		}

		public int ScreenMinHeight {
			get {
				return 550;
			}
		}

		public int ScreenMinWidth {
			get {
				return 600;
			}
		}

		public WindowState ScreenState {
			get {
				var state = _config.MainWindowPosition.ScreenState;
				if (Enum.IsDefined(typeof(WindowState), state)) {
					return (WindowState)state;
				}

				return WindowState.Normal;
			}
			set {
				var curState = _config.MainWindowPosition.ScreenState;
				if ((int)value != curState) {
					_config.MainWindowPosition.ScreenState = (int)value;
					NotifyOfPropertyChange(() => ScreenState);
				}
			}
		}

		public BindableCollection<MenuItemVM> MenuItems {
			get;
			private set;
		}

		public StatusBarVM NotificationArea {
			get;
			private set;
		}

		public ShellVM(IAppConfig config, IWindowManagerEx windowManager, IEventAggregator events) {
			Contract.Requires(config != null);
			Contract.Requires(windowManager != null);
			Contract.Requires(events != null);

			_config = config;
			_winManager = windowManager;
			_events = events;
			_events.Subscribe(this);
			_baseDisplayName = string.Concat(
					"Netperf v",
					Utility.NetperfVersion(),
					Utility.Is64Bit() ? " x64 " : " x86 ",
					Utility.IsAdministrator()
						? " (Administrator) "
						: " "
					);

			DisplayName = _baseDisplayName;
			MenuItems = new BindableCollection<MenuItemVM> {
				new MenuItemVM("File") {
						new MenuItemVM("New", onClick: ExecuteNewConfig ),
						new MenuItemVM("Config Wizard", onClick: ExecuteInstrumentWizard),
						new MenuItemVM("Open", onClick: ExecuteOpenConfig),
						new MenuItemVM("Open Recent", onClick: ExecuteOpenRecent, canClick: CanOpenRecent),
						new MenuItemVM("Save", onClick: ExecuteSaveConfig, canClick: CanSaveConfig),
						new MenuItemVM("Save As...", onClick: ExecuteSaveConfigAs, canClick: CanSaveConfig),
						new MenuItemVM(),
						new MenuItemVM("Execute", onClick: ExecuteRunActiveConfig, canClick: CanSaveConfig),
						new MenuItemVM(),
						new MenuItemVM("Exit", onClick: ExecuteExitApplication)
					},
				new MenuItemVM("Execute", onClick: ExecuteRunActiveConfig, canClick: CanSaveConfig),
				new MenuItemVM("Help") {
					new MenuItemVM("Check for Updates", onClick: ExecuteCheckForUpdates, canClick: CanCheckForUpdates),
					new MenuItemVM("User Options", onClick: ExecuteShowUserOptions),
					new MenuItemVM(),
					new MenuItemVM("About Netperf", onClick: ExecuteShowAbout)
				}
			};

			MenuItems[0].Single(mi => mi.Text == "Open Recent")
				.AddRange(from f in _config.RecentSettingFiles
						  select FilePathToMenuItem(f));

			NotificationArea = new StatusBarVM();

			var latestConfig = _config.RecentSettingFiles.FirstOrDefault();
			if (latestConfig != null) {
				OpenConfigFile(latestConfig);
			}

			// runs if the latest profiling configuration failed to load
			// or if there is no latest profiling configuration
			if (CurrentSettings == null) {
				ExecuteNewConfig(null);
			}
		}

		public void SetAutoUpdate(IAutoUpdateAdaptor updater) {
			if (_autoUpdate != null) {
				_autoUpdate.Dispose();
				_autoUpdate = null;
			}

			_autoUpdate = updater;
		}

		protected override void OnViewLoaded(object view) {
			base.OnViewLoaded(view);

			if (_autoUpdate != null && _config.AutoUpdateOption != UpdateOption.Never) {
				// this is the autoupdate check, so we will do whatever the user
				// decided to do for autoupdates
				_autoUpdate.StartUpdateCheck(_config.AutoUpdateOption);
			}
		}

		public override void DeactivateItem(Screen item, bool close) {
			base.DeactivateItem(item, close);

			if (item.GetType() != typeof(StartInstructionsVM)) {
				ActivateItem(IoC.Get<StartInstructionsVM>());
			}
		}

		protected override void OnDeactivate(bool close) {
			if (close) {
				_events.Unsubscribe(this);
			}
			base.OnDeactivate(close);
		}

		private MenuItemVM FilePathToMenuItem(FilePath fromPath) {
			string displayPath;

			if (fromPath.FullPath.Length < 40) {
				displayPath = fromPath;
			}
			else {
				var pathSections = fromPath.FullPath.SplitAndRemoveEmpty(PathStub.DirectorySeparatorChar, PathStub.AltDirectorySeparatorChar);
				var endPath = pathSections.Reverse()
					.Aggregate(new List<string>(), (soFar, next) => {
						if (soFar.Sum(s => s.Length) < 30) {
							soFar.Add(next);
						}

						return soFar;
					});

				endPath.Add("...");
				endPath.Add(PathStub.GetPathRoot(fromPath));

				endPath.Reverse();

				displayPath = PathStub.Combine(endPath.ToArray());
			}

			return new MenuItemVM(displayPath, fromPath, fromPath, ExecuteOpenRecentConfig);
		}

		private void ExecuteNewConfig(object notUsed) {
			GuardSaveUnsavedConfig();

			CurrentSettings = new InstrumentationConfigVM(_winManager, InstrumentationSettingsParser.DefaultSettings);
			ActivateItem(CurrentSettings);
			CurrentSettings.IsDirty = false;
			UpdateDisplayName();

			// use the wizard to configure the Instrumentation Settings
			//Execute.BeginOnUIThread(() => ExecuteInstrumentWizard(notUsed));
		}

		private void ExecuteInstrumentWizard(object notUsed) {
			var wizard = new InstrumentWizardVM(_winManager, CurrentSettings);
			_winManager.ShowDialog(wizard);
		}

		private void GuardSaveUnsavedConfig() {
			if (CurrentSettings != null && CurrentSettings.IsDirty) {
				var result = _winManager.Confirm("The current profiling configuration has some unsaved changes. Would you like to save the changes first?");
				if (result) {
					ExecuteSaveConfig(null);
				}
			}
		}

		private void ExecuteOpenConfig(object notUsed) {
			GuardSaveUnsavedConfig();

			var paths = _winManager.OpenFiles(filterFormat: "Netperf Configuration File (*.npconfig)|*.npconfig", canChooseMultiple: false);

			if (paths != null && paths.Length > 0) {
				OpenConfigFile(paths[0]);
			}
		}

		private void ExecuteOpenRecentConfig(object filePath) {
			GuardSaveUnsavedConfig();

			var fp = filePath as FilePath;
			if (fp == null) {
				_winManager.ShowMessage("Invalid or unsupported/unknown Netperf Configuration file format.");
			}

			OpenConfigFile(fp);
		}

		private void OpenConfigFile(FilePath fromFile) {

			var result = InstrumentationSettingsParser.Read(fromFile);
			if (result.HasError) {
				ReplaceRecentMenuItem(fromFile, null);

				// Note: uncommenting out the line below will cause the application to crash if we can't load the config file on startup
				//_winManager.ShowMessage("Invalid or unsupported/unknown Netperf Configuration file format.");
			}
			else {
				CurrentSettings = new InstrumentationConfigVM(_winManager, result.Data, fromFile);
				ActivateItem(CurrentSettings);

				ReplaceRecentMenuItem(fromFile, fromFile);
				CurrentSettings.IsDirty = false;
				UpdateDisplayName();
			}
		}

		private bool CanOpenRecent(object notused) {
			return _config != null && _config.RecentSettingFiles != null && _config.RecentSettingFiles.Count > 0;
		}

		private void ExecuteOpenRecent(object notUsed) {
			// do nothing. this method is here so that CanOpenRecent will work
		}

		private void UpdateDisplayName() {
			DisplayName = CurrentSettings.ConfigFilePath == null
				? (CurrentSettings.IsDirty ? "* " : "") + "New Configuration - " + _baseDisplayName
				: (CurrentSettings.IsDirty ? "* " : "") + CurrentSettings.ConfigFilePath.FileName + " in " + CurrentSettings.ConfigFilePath.ParentDirectory + " - " + _baseDisplayName;
		}

		/// <summary>
		/// Removes the <paramref name="previousPath"/> from the recent file list,
		/// and places the <paramref name="newPath"/> on the top of the list.
		/// </summary>
		/// <param name="previousPath">The path that will be removed. If it is null, no paths are removed from the MRU list.</param>
		/// <param name="newPath">The path to place at the top of the MRU list. If it is null, no path is added to the MRU list.</param>
		private void ReplaceRecentMenuItem(FilePath previousPath, FilePath newPath) {
			var recentMenu = MenuItems[0].Single(mi => mi.Text == "Open Recent");
			// move the file to the top of the recent items list by removing all
			// instances of it in the list, and then adding it back at the top
			var menuItem = recentMenu.Where(mi => mi.Tag.Equals(previousPath)).ToArray(); //in case we have duplicates

			if (menuItem != null && menuItem.Length > 0) {
				for (int i = 0; i < menuItem.Length; i++) {
					recentMenu.Remove(menuItem[i]);	
				}
			}

			if (newPath != null) {
				var newItem = FilePathToMenuItem(newPath);
				recentMenu.Insert(0, newItem);

				var toRemove = recentMenu.Skip(10).ToArray();
				recentMenu.RemoveRange(toRemove);

				// keep the application configuration in sync.
				// removing everything and adding it back again is a little expensive, 
				// but hopefully configuration files aren't being opened very often
				_config.RecentSettingFiles.Clear();

				_config.RecentSettingFiles.AddRange(from mi in recentMenu
													select (FilePath)mi.Tag);
			}
		}

		private bool CanSaveConfig(object notUsed) {
			return CurrentSettings != null && CurrentSettings.IsValid;
		}

		private void ExecuteSaveConfig(object notUsed) {
			if (CurrentSettings.ConfigFilePath == null) {
				ExecuteSaveConfigAs(notUsed);
				return;
			}

			SaveActiveConfig(CurrentSettings.ConfigFilePath);
		}

		private void ExecuteSaveConfigAs(object notUsed) {
			_winManager.SaveFile(path => SaveActiveConfig(path), "Netperf Configuration File (*.npconfig)|*.npconfig", CurrentSettings.ConfigFilePath ?? string.Empty);
		}

		private void SaveActiveConfig(FilePath toFile) {
			if (!CurrentSettings.IsValid) {
				// this code shouldn't ever run, but in case the user ever gets in here
				// w/o the Can* method running, it's better to show a message box
				// then let _currentSettings.ExtractCurrentSettings() throw an exception.
				_winManager.ShowMessage("The current settings are invalid. Please correct the errors before saving the current settings.");
				return;
			}

			var result = InstrumentationSettingsParser.Write(toFile, CurrentSettings.ExtractProfilingConfig());
			if (result.WasSuccess) {
				// if the saved file isn't already on the MRU list, then add it
				if (CurrentSettings.ConfigFilePath != toFile) {
					// pass null so that the previous name isn't removed from the MRU list
					ReplaceRecentMenuItem(null, toFile);
				}

				CurrentSettings.ConfigFilePath = toFile;
				CurrentSettings.IsDirty = false;
				UpdateDisplayName();
			}
			else {
				_winManager.ShowMessage(string.Format("Failed to save the profiling options. Error: {0}", result.ErrorMessage));
			}
		}

		private void ExecuteRunActiveConfig(object notUsed) {

			var actions = new List<IResult> {
				new DelegateResult(aec => {
					_winManager.ShowNotification("Running external process. Please wait...");
				}),

				new BackgroundResult(aec => {

					// figure out where netperf console lives
					var netperfPath = Utility.NetperfConsole;
					if (netperfPath == null || !FileStub.Exists(netperfPath))
						return;

					if (CurrentSettings == null || !CurrentSettings.IsValid)
						return;

					var configPath = CurrentSettings.ConfigFilePath ?? FilePath.Parse(CurrentSettings.ApplicationToRun);
					if (configPath == null)
						return;

					configPath = configPath.ChangeExtension(".npconfig").ChangeFileNameWithoutExtension(PathStub.GetRandomFileName());
					var result = InstrumentationSettingsParser.Write(configPath, CurrentSettings.ExtractProfilingConfig());
					if (result.HasError)
						return;

					try {
						string runParams = string.Empty;
						runParams += "useconfig \"" + configPath + "\"";

						if (!CurrentSettings.WorkingFolder.IsNullOrWhitespace()) {
							var path = CurrentSettings.WorkingFolder;
							if (path.EndsWith(@"\")) {
								path = path.Substring(0, path.Length - 1);
							}
							runParams += " basedir \"" + path + "\"";
						}

						//added 4/14/2011 so we can see what causes the failure.
						runParams += " pauseonerror";


						var procInfo = new ProcessStartInfo(netperfPath.FullPath, runParams);

						using (var proc = Process.Start(procInfo)) {
							;
							proc.WaitForExit();
						}
					} finally {
						if (FileStub.Exists(configPath)) {
							// delete the temporary profiling configuration file
							ExceptionHelper.WrapFileOperation(() => FileStub.Delete(configPath));
						}
					}
				}),

				new DelegateResult(aec => {
						_winManager.CloseNotification();
				})
			};

			Coroutine.BeginExecute(actions.GetEnumerator());
		}

		private void ExecuteShowAbout(object notUsed) {
			_winManager.ShowDialog(IoC.Get<AboutVM>());
		}

		private void ExecuteShowUserOptions(object notUsed) {
			_winManager.ShowDialog(IoC.Get<ApplicationOptionsVM>());
		}

		private void ExecuteCheckForUpdates(object notUsed) {
			if (_autoUpdate != null) {
				// the user forced this check from the menu, so we will check for updates
				// but not do anything else
				_autoUpdate.StartUpdateCheck(UpdateOption.CheckOnly);
			}
		}

		private bool CanCheckForUpdates(object notUsed) {
			return _autoUpdate != null;
		}

		private void ExecuteExitApplication(object notUsed) {
			this.TryClose();
		}
	}
}
