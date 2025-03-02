using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Configuration;
using UGMVR;
using UGMVR.UnrealVR;

namespace UEVR
{
	/// <summary>
	/// Interaction logic for UnrealInjectorView.xaml
	/// </summary>
	public partial class UnrealInjectorView : UserControl
	{
		private ExecutableFilter m_executableFilter = new ExecutableFilter();
		
		private	CommandLineOptions commandLineParser;
		private string? m_commandLineAttachExe = null;
		

		public static bool IsUnrealRunning = false;

		private IConfiguration? m_currentConfig = null;
		private string? m_currentConfigPath = null;
		private bool m_isFirstProcessFill = true;
		
		private string m_lastDisplayedWarningProcess = "";

		private string excludedProcessesFile = "excluded.txt";
		private string? m_lastDefaultProcessListName = null;
		private string m_lastSelectedProcessName = new string("");
		private int m_lastSelectedProcessId = 0;
		private List<Process> m_processList = new List<Process>();

		private SemaphoreSlim m_processSemaphore = new SemaphoreSlim(1, 1); // create a semaphore with initial count of 1 and max count of 1
		//private ProcessMonitor processMonitor;
		
		private InjectorManager injectorManager = new InjectorManager();

		private DateTime m_lastAutoInjectTime = DateTime.MinValue;
		private DateTime lastInjectorStatusUpdate = DateTime.MinValue;
		private DateTime lastFrontendSignal = DateTime.MinValue;
		
		private bool runExecutableOnStartup = false;

		public UnrealInjectorView ()
		{
			InitializeComponent ();
		}

		internal void OnMainInit ()
		{
			// Grab the command-line arguments
			commandLineParser = new CommandLineOptions(Environment.GetCommandLineArgs());
			runExecutableOnStartup = commandLineParser.RunOnStartup;
			m_commandLineAttachExe = commandLineParser.ExecutablePath;

			m_autoInject.Checked += (sender, e) => RefreshStates ();
			m_autoInject.Unchecked += (sender, e) => RefreshStates ();
			RefreshStates ();
		}
		
		private async void FillProcessList ()
		{
			// Allow the previous running FillProcessList task to finish first
			if (m_processSemaphore.CurrentCount == 0) {
				return;
			}

			await m_processSemaphore.WaitAsync ();

			try {
				m_processList.Clear ();
				m_processListBox.Items.Clear ();

				await Task.Run (() => {
					// get the list of processes
					Process[] processList = Process.GetProcesses();

					// loop through the list of processes
					foreach (Process process in processList) {
						if (!UEVRHelper.IsInjectableProcess (process, excludedProcessesFile, m_executableFilter)) {
							continue;
						}

						Application.Current.Dispatcher.Invoke (() => {
							m_processList.Add (process);
							m_processList.Sort ((a, b) => a.ProcessName.CompareTo (b.ProcessName));
							RefreshProcessComboBox ();
						});
					}

					Application.Current.Dispatcher.Invoke (() => {
						RefreshProcessComboBox ();
					});
				});
			} finally {
				m_processSemaphore.Release ();
			}
		}

  //      internal async Task AttachToProcessAsync() 
		//{
  //          ExtendedMainWindow.skipAlertMessages = true;
		//	string processName = Path.GetFileNameWithoutExtension(commandLineParser.ExecutablePath);
       
  //          var processCandidate = ProcessHelper.WaitForProcess(commandLineParser.ExecutablePath, commandLineParser.Arguments);
			
		//	var timeToWait = 15000;
		//	await ProcessHelper.WaitForProcessToStartAsync(processName, timeToWait);
		//	InitializeConfig(processCandidate.ProcessName);
		//	InjectProcess (processCandidate);
  //          Update_InjectorConnectionStatus();

  //          ExtendedMainWindow.skipAlertMessages = false;
  //      }

		private void RefreshStates()
		{
			// autoinject checkbox
			if ((bool)m_autoInject.IsChecked) {
				m_injectAfterPanel.Visibility = Visibility.Visible;
				m_processListBox.Visibility = m_injectButton.Visibility = Visibility.Collapsed;
				
			} else {
				m_injectAfterPanel.Visibility = Visibility.Collapsed;
				m_processListBox.Visibility = m_injectButton.Visibility = Visibility.Visible;
			}
		}

		public void MainUpdate()
		{
			Update_InjectorConnectionStatus();
			Update_InjectStatus ();
		}
		
		public void ImportConfig()
		{
			var importPath = GameConfig.BrowseForImport(AppSettings.GetGlobalDirPath());

			if (importPath == null) {
				return;
			}

			var gameName = System.IO.Path.GetFileNameWithoutExtension(importPath);
			if (gameName == null) {
				MessageBox.Show ("Invalid filename");
				return;
			}

			var globalDir = AppSettings.GetGlobalDirPath();
			var gameGlobalDir = globalDir + "\\" + gameName;

			try {
				if (!Directory.Exists (gameGlobalDir)) {
					Directory.CreateDirectory (gameGlobalDir);
				}

				bool wantsExtract = true;

				if (GameConfig.ZipContainsDLL (importPath)) {
					string message = "The selected config file includes a DLL (plugin), which may execute actions on your system.\n" +
									 "Only import configs with DLLs from trusted sources to avoid potential risks.\n" +
									 "Do you still want to proceed with the import?";
					var dialog = new YesNoDialog("DLL Warning", message);
					dialog.ShowDialog ();

					wantsExtract = dialog.DialogResultYes;
				}

				if (wantsExtract) {
					var finalGameName = GameConfig.ExtractZipToDirectory(importPath, gameGlobalDir, gameName);

					if (finalGameName == null) {
						MessageBox.Show ("Failed to extract the ZIP file.");
						return;
					}

					var finalDirectory = System.IO.Path.Combine(globalDir, finalGameName);
					DirectoryHelper.NavigateToDirectory (finalDirectory);

					RefreshCurrentConfig ();

					if (m_connected) {
						SharedMemory.SendCommand (SharedMemory.Command.ReloadConfig);
					}
				}
			} catch (Exception ex) {
				MessageBox.Show ("An error occurred: " + ex.Message);
			}
		}

		public void ExportConfig()
		{
			if (!m_connected) {
				MessageBox.Show ("Inject into a game first!");
				return;
			}

			if (m_lastSharedData == null) {
				MessageBox.Show ("No game connection detected.");
				return;
			}

			var dir = AppEnvironment.GetGlobalGameDir(m_lastSelectedProcessName);
			if (dir == null) {
				return;
			}

			if (!Directory.Exists (dir)) {
				MessageBox.Show ("Directory does not exist.");
				return;
			}

			var exportedConfigsDir = AppSettings.GetGlobalDirPath() + "\\ExportedConfigs";

			if (!Directory.Exists (exportedConfigsDir)) {
				Directory.CreateDirectory (exportedConfigsDir);
			}

			GameConfig.CreateZipFromDirectory (dir, exportedConfigsDir + "\\" + m_lastSelectedProcessName + ".zip");
			DirectoryHelper.NavigateToDirectory (exportedConfigsDir);
		}

		ExtendedMainWindow ExtendedWindow => (ExtendedMainWindow)Window.GetWindow (this);

		private void Update_InjectorConnectionStatus ()
		{
			var data = SharedMemory.GetData();
			DateTime now = DateTime.Now;
			TimeSpan oneSecond = TimeSpan.FromSeconds(1);

			if (data != null) {
				m_connectionStatus.Text = UEVRConnectionStatus.Connected;
				m_connectionStatus.Text += ": " + data?.path;
				m_connectionStatus.Text += "\nThread ID: " + data?.mainThreadId.ToString ();
				m_lastSharedData = data;
				m_connected = true;
				ExtendedWindow.ShowConnectionOptions (true);

				if (data?.signalFrontendConfigSetup == true && (now - lastFrontendSignal > oneSecond)) {
					SharedMemory.SendCommand (SharedMemory.Command.ConfigSetupAcknowledged);
					RefreshCurrentConfig ();

					lastFrontendSignal = now;
				}
			} else {
				if (m_connected && !string.IsNullOrEmpty (m_commandLineAttachExe)) {
					// If we launched with an attached game exe, we shut ourselves down once that game closes.
					Application.Current.Shutdown ();
					return;
				}
				m_connectionStatus.Text = UEVRConnectionStatus.NoInstanceDetected;
				m_connected = false;
				ExtendedWindow.ShowConnectionOptions (false);
			}

			lastInjectorStatusUpdate = now;
		}

		private bool m_connected = false;
		private SharedMemory.Data? m_lastSharedData = null;

		private bool TermintateCurrentProcessInjectedIfExists ()
		{
			// "Terminate Connected Process"
			if (m_connected) {
				var pid = m_lastSharedData?.pid;
				if (pid != null) {
					ProcessManager.Kill((int)pid);
				}
				return true;
			}
			return false;
		}

		private void Inject_Clicked (object sender, RoutedEventArgs e)
		{
			// "Terminate Connected Process"
			if (TermintateCurrentProcessInjectedIfExists ())
				return;

			var selectedProcessName = m_processListBox.SelectedItem;
			if (selectedProcessName == null) {
				return;
			}

			var index = m_processListBox.SelectedIndex;
			var process = m_processList[index];

			if (process == null) {
				return;
			}

			if (TryGetNewProcess (out var newProcess)) {
				process = newProcess;
				m_processList[index] = newProcess;
				m_processListBox.Items[index] = ProcessHelper.GenerateProcessName (newProcess);
				m_processListBox.SelectedIndex = index;
			}
			if (process == null) {
				throw new Exception ("Process is null");
			}
			injectorManager.Inject(process);
		}

		private void RefreshProcessComboBox ()
		{
			m_processListBox.Items.Clear ();

			foreach (Process p in m_processList) {
				string processName = ProcessHelper.GenerateProcessName(p);
				m_processListBox.Items.Add (processName);

				if (m_processListBox.SelectedItem == null && m_processListBox.Items.Count > 0) {
					if (m_lastDefaultProcessListName == null || m_lastDefaultProcessListName == processName) {
						m_processListBox.SelectedItem = m_processListBox.Items[m_processListBox.Items.Count - 1];
						m_lastDefaultProcessListName = processName;
					}
				}
			}
		}
		
		private void Update_InjectStatus ()
		{
			if (m_connected) {
				m_injectButton.Content = "Terminate Connected Process";
				return;
			}

			DateTime now = DateTime.Now;
			TimeSpan oneSecond = TimeSpan.FromSeconds(1);

			if (m_commandLineAttachExe == null) {
				if (m_lastSelectedProcessId == 0) {
					m_injectButton.Content = "Inject";
					return;
				}

				try {
					var verifyProcess = Process.GetProcessById(m_lastSelectedProcessId);

					if (verifyProcess == null || verifyProcess.HasExited || verifyProcess.ProcessName != m_lastSelectedProcessName) {
						var processes = Process.GetProcessesByName(m_lastSelectedProcessName);

						if (processes == null || processes.Length == 0 || !UEVRHelper.AnyInjectableProcesses (processes, excludedProcessesFile, m_executableFilter)) {
							m_injectButton.Content = "Waiting for Process";
							return;
						}
					}

					m_injectButton.Content = "Inject";
				} catch (ArgumentException) {
					var processes = Process.GetProcessesByName(m_lastSelectedProcessName);

					if (processes == null || processes.Length == 0 || !UEVRHelper.AnyInjectableProcesses (processes, excludedProcessesFile, m_executableFilter)) {
						m_injectButton.Content = "Waiting for Process";
						return;
					}

					m_injectButton.Content = "Inject";
				}
			} else {
				m_injectButton.Content = "Waiting for " + m_commandLineAttachExe.ToLower () + "...";

				var processes = Process.GetProcessesByName(m_commandLineAttachExe.ToLower().Replace(".exe", ""));

				if (processes.Count () == 0) {
					return;
				}

				Process? process = null;

				foreach (Process p in processes) {
					if (UEVRHelper.IsInjectableProcess (p, excludedProcessesFile, m_executableFilter)) {
						m_lastSelectedProcessId = p.Id;
						m_lastSelectedProcessName = p.ProcessName;
						process = p;
					}
				}

				if (process == null) {
					return;
				}

				if (now - m_lastAutoInjectTime > oneSecond) {
					if (injectorManager.Inject(process)) {
						InitializeConfig (process.ProcessName);
					}

					m_lastAutoInjectTime = now;
					m_commandLineAttachExe = null; // no need anymore.
					FillProcessList ();
				}
			}
		}

		private void RefreshCurrentConfig ()
		{
			if (m_currentConfig == null || m_currentConfigPath == null) {
				return;
			}

			InitializeConfig_FromPath (m_currentConfigPath);
		}
		
		private void InitializeConfig (string gameName)
		{
			var configDir =AppEnvironment.GetGlobalGameDir(gameName);
			var configPath = configDir + "\\config.txt";

			InitializeConfig_FromPath (configPath);
		}
		
		private string[] m_discouragedPlugins = {
			"OpenVR",
			"OpenXR",
			"Oculus"
		};
		
		private string IniToString (IConfiguration config)
		{
			string result = "";

			foreach (var kv in config.AsEnumerable ()) {
				result += kv.Key + "=" + kv.Value + "\n";
			}

			return result;
		}

		private void SaveCurrentConfig ()
		{
			try {
				if (m_currentConfig == null || m_currentConfigPath == null) {
					return;
				}

				var iniStr = IniToString(m_currentConfig);
				Debug.Print (iniStr);

				File.WriteAllText (m_currentConfigPath, iniStr);

				if (m_connected) {
					SharedMemory.SendCommand (SharedMemory.Command.ReloadConfig);
				}
			} catch (Exception ex) {
				MessageBox.Show (ex.ToString ());
			}
		}

		private string? AreVRPluginsPresent_InEngineDir (string enginePath)
		{
			string pluginsPath = enginePath + "\\Binaries\\ThirdParty";

			if (!Directory.Exists (pluginsPath)) {
				return null;
			}

			foreach (string discouragedPlugin in m_discouragedPlugins) {
				string pluginPath = pluginsPath + "\\" + discouragedPlugin;

				if (Directory.Exists (pluginPath)) {
					return pluginsPath;
				}
			}

			return null;
		}

		private string? AreVRPluginsPresent (string gameDirectory)
		{
			try {
				var parentPath = gameDirectory;

				for (int i = 0; i < 10; ++i) {
					parentPath = System.IO.Path.GetDirectoryName (parentPath);

					if (parentPath == null) {
						return null;
					}

					if (Directory.Exists (parentPath + "\\Engine")) {
						return AreVRPluginsPresent_InEngineDir (parentPath + "\\Engine");
					}
				}
			} catch (Exception ex) {
				Console.WriteLine ($"Exception caught: {ex}");
			}

			return null;
		}

		private void ComboBox_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
			//ComboBoxItem comboBoxItem = ((sender as ComboBox).SelectedItem as ComboBoxItem);

			try {
				var box = (sender as ComboBox);
				if (box == null || box.SelectedIndex < 0 || box.SelectedIndex > m_processList.Count) {
					return;
				}

				var p = m_processList[box.SelectedIndex];
				if (p == null || p.HasExited) {
					return;
				}

				m_lastSelectedProcessName = p.ProcessName;
				m_lastSelectedProcessId = p.Id;

				// Search for the VR plugins inside the game directory
				// and warn the user if they exist.
				if (m_lastDisplayedWarningProcess != m_lastSelectedProcessName && p.MainModule != null) {
					m_lastDisplayedWarningProcess = m_lastSelectedProcessName;

					var gamePath = p.MainModule.FileName;

					if (gamePath != null) {
						var gameDirectory = System.IO.Path.GetDirectoryName(gamePath);

						if (gameDirectory != null) {
							var pluginsDir = AreVRPluginsPresent(gameDirectory);

							if (pluginsDir != null) {
								MessageBox.Show ("VR plugins have been detected in the game install directory.\n" +
												"You may want to delete or rename these as they will cause issues with the mod.\n" +
												"You may also want to pass -nohmd as a command-line option to the game. This can sometimes work without deleting anything.");
								var result = MessageBox.Show("Do you want to open the plugins directory now?", "Confirmation", MessageBoxButton.YesNo);

								switch (result) {
									case MessageBoxResult.Yes:
										DirectoryHelper.NavigateToDirectory (pluginsDir);
										break;
									case MessageBoxResult.No:
										break;
								};
							}

							ExtendedWindow.Check_VirtualDesktop ();

							m_iniListView.ItemsSource = null; // Because we are switching processes.
							InitializeConfig (p.ProcessName);

							if (!UEVRHelper.IsUnrealEngineGame (gameDirectory, m_lastSelectedProcessName) && !m_isFirstProcessFill) {
								MessageBox.Show ("Warning: " + m_lastSelectedProcessName + " does not appear to be an Unreal Engine title");
							}
						}

						m_lastDefaultProcessListName = ProcessHelper.GenerateProcessName (p);
					}
				}
			} catch (Exception ex) {
				Console.WriteLine ($"Exception caught: {ex}");
			}
		}
		
		public bool TryGetNewProcess (out Process? process)
		{
			process = null;

			// Double check that the process we want to inject into exists
			// this can happen if the user presses inject again while
			// the previous combo entry is still selected but the old process
			// has died.
			try {
				var verifyProcess = Process.GetProcessById(m_lastSelectedProcessId);

				if (verifyProcess == null || verifyProcess.HasExited || verifyProcess.ProcessName != m_lastSelectedProcessName) {

					var processes = Process.GetProcessesByName(m_lastSelectedProcessName);
					if (processes == null || processes.Length == 0 || 
						!UEVRHelper.AnyInjectableProcesses (processes, excludedProcessesFile, m_executableFilter)) {
						return false;
					}

					foreach (var candidate in processes) {
						if (UEVRHelper.IsInjectableProcess (candidate, excludedProcessesFile, m_executableFilter)) {
							process = candidate;
							return true;
						}
					}
					return true;
				}
			} catch (Exception ex) {
				if (!ExtendedMainWindow.skipAlertMessages) {
					MessageBox.Show (ex.Message);
				}
			}
			return false;
		}

		private void ComboBox_DropDownOpened (object sender, System.EventArgs e)
		{
			m_lastSelectedProcessName = "";
			m_lastSelectedProcessId = 0;

			FillProcessList ();
			Update_InjectStatus ();

			m_isFirstProcessFill = false;
		}
		
		private void TextChanged_Value (object sender, RoutedEventArgs e)
		{
			try {
				if (m_currentConfig == null || m_currentConfigPath == null) {
					return;
				}

				var textBox = (TextBox)sender;
				var keyValuePair = (GameSettingEntry)textBox.DataContext;

				// For some reason the TextBox.text is updated but thne keyValuePair.Value isn't at this point.
				bool changed = m_currentConfig[keyValuePair.Key] != textBox.Text || keyValuePair.Value != textBox.Text;
				var newValue = textBox.Text;

				if (changed) {
					RefreshCurrentConfig ();
				}

				m_currentConfig[keyValuePair.Key] = newValue;
				RefreshConfigUI ();

				if (changed) {
					SaveCurrentConfig ();
				}
			} catch (Exception ex) {
				Console.WriteLine (ex.ToString ());
			}
		}
		private void ComboChanged_Value (object sender, RoutedEventArgs e)
		{
			try {
				if (m_currentConfig == null || m_currentConfigPath == null) {
					return;
				}

				var comboBox = (ComboBox)sender;
				var keyValuePair = (GameSettingEntry)comboBox.DataContext;

				bool changed = m_currentConfig[keyValuePair.Key] != keyValuePair.Value;
				var newValue = keyValuePair.Value;

				if (changed) {
					RefreshCurrentConfig ();
				}

				m_currentConfig[keyValuePair.Key] = newValue;
				RefreshConfigUI ();

				if (changed) {
					SaveCurrentConfig ();
				}
			} catch (Exception ex) {
				Console.WriteLine (ex.ToString ());
			}
		}

		private void CheckChanged_Value (object sender, RoutedEventArgs e)
		{
			try {
				if (m_currentConfig == null || m_currentConfigPath == null) {
					return;
				}

				var checkbox = (CheckBox)sender;
				var keyValuePair = (GameSettingEntry)checkbox.DataContext;

				bool changed = m_currentConfig[keyValuePair.Key] != keyValuePair.Value;
				string newValue = keyValuePair.Value;

				if (changed) {
					RefreshCurrentConfig ();
				}

				m_currentConfig[keyValuePair.Key] = newValue;
				RefreshConfigUI ();

				if (changed) {
					SaveCurrentConfig ();
				}
			} catch (Exception ex) {
				Console.WriteLine (ex.ToString ());
			}
		}

		bool IsUnrealGame(UEVRGameRowView? selectedItem)
		{
			if (selectedItem == null) {
				return false;
			}
			return selectedItem.Engine == Engines.Unreal;
		}

		internal void SelectionChanged (UEVRGameRowView? selectedItem)
		{
			// refresh
			if (IsUnrealGame(selectedItem)) {
				RefreshStates();
			} 
		}

		internal void OnMainLoaded ()
		{
			m_nullifyVRPluginsCheckbox.IsChecked = ExtendedMainWindow.MainSettings.NullifyVRPluginsCheckbox;
			m_focusGameOnInjectionCheckbox.IsChecked = ExtendedMainWindow.MainSettings.FocusGameOnInjection;

			processMonitor = new ProcessMonitor ();
			FillProcessList ();
		}

		internal void OpenGameDir ()
		{
			if (m_lastSharedData == null) {
				return;
			}

			var directory = System.IO.Path.GetDirectoryName(m_lastSharedData?.path);
			if (directory == null) {
				return;
			}

			DirectoryHelper.NavigateToDirectory (directory);
		}
		
		private void InitializeConfig_FromPath (string configPath)
		{
			var builder = new ConfigurationBuilder().AddIniFile(configPath, optional: true, reloadOnChange: false);

			m_currentConfig = builder.Build ();
			m_currentConfigPath = configPath;

			foreach (var entry in MandatoryConfig.Entries) {
				if (m_currentConfig.AsEnumerable ().ToList ().FindAll (v => v.Key == entry.Key).Count () == 0) {
					m_currentConfig[entry.Key] = entry.Value;
				}
			}

			RefreshConfigUI ();
		}

		private void RefreshConfigUI ()
		{
			if (m_currentConfig == null) {
				return;
			}

			var vanillaList = m_currentConfig.AsEnumerable().ToList();
			vanillaList.Sort ((a, b) => a.Key.CompareTo (b.Key));

			List<GameSettingEntry> newList = new List<GameSettingEntry>();

			foreach (var kv in vanillaList) {
				if (!string.IsNullOrEmpty (kv.Key) && !string.IsNullOrEmpty (kv.Value)) {
					Dictionary<string, string> comboValues = new Dictionary<string, string>();
					string tooltip = "";

					if (ComboMapping.KeyEnums.ContainsKey (kv.Key)) {
						var valueList = ComboMapping.KeyEnums[kv.Key];

						if (valueList != null && valueList.ContainsKey (kv.Value)) {
							comboValues = valueList;
						}
					}

					if (GameSettingTooltips.Entries.ContainsKey (kv.Key)) {
						tooltip = GameSettingTooltips.Entries[kv.Key];
					}

					newList.Add (new GameSettingEntry { Key = kv.Key, Value = kv.Value, ComboValues = comboValues, Tooltip = tooltip });
				}
			}

			if (m_iniListView.ItemsSource == null) {
				m_iniListView.ItemsSource = newList;
			} else {
				foreach (var kv in newList) {
					var source = (List<GameSettingEntry>)m_iniListView.ItemsSource;

					var elements = source.FindAll(el => el.Key == kv.Key);

					if (elements.Count () == 0) {
						// Just set the entire list, we don't care.
						m_iniListView.ItemsSource = newList;
						break;
					} else {
						elements[0].Value = kv.Value;
						elements[0].ComboValues = kv.ComboValues;
						elements[0].Tooltip = kv.Tooltip;
						//elements[0].ExtraInfo = "kv.Tooltip;";
					}
				}
			}

			m_iniListView.Visibility = Visibility.Visible;
		}

		internal void OnMainClosing ()
		{
			ExtendedMainWindow.MainSettings.NullifyVRPluginsCheckbox = m_nullifyVRPluginsCheckbox.IsChecked == true;
			ExtendedMainWindow.MainSettings.FocusGameOnInjection = m_focusGameOnInjectionCheckbox.IsChecked == true;
		}

		internal void OnSizeChanged (Window w, SizeChangedEventArgs e)
		{
			ContentScrollView.MaxHeight = Math.Max (w.ActualHeight - 100, 100);
		}

		internal ProcessAction? GetProcessAction (UEVRGameRowView? selectedItem)
		{
			if (selectedItem != null && selectedItem.Engine == Engines.Unreal) {
				
			}
			return null;
		}
	}
}
