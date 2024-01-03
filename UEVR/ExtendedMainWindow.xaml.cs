using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;
using System.ComponentModel;
using System.Security.Principal;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using GameFinder.RegistryUtils;
using GameFinder.StoreHandlers.Steam;
using System.Windows.Media;
using GameFinder.StoreHandlers.Steam.Services;
using NexusMods.Paths;
using System.Net;
using GameFinder.Common;
using PeNet;
using System.Windows.Controls.Primitives;
using System.Reflection;
using System.Security;
using System.Management;
using UGMVR.Sdks.Steam;

namespace UEVR
{
    public partial class ExtendedMainWindow : Window {
        // variables
        // process list
        private List<Process> m_processList = new List<Process>();
        private MainWindowSettings m_mainWindowSettings = new MainWindowSettings();

        private string m_lastSelectedProcessName = new string("");
        private int m_lastSelectedProcessId = 0;

        private SharedMemory.Data? m_lastSharedData = null;
        private bool m_connected = false;

        private DispatcherTimer m_updateTimer = new DispatcherTimer {
            Interval = new TimeSpan(0, 0, 1)
        };

        private IConfiguration? m_currentConfig = null;
        private string? m_currentConfigPath = null;

        private ExecutableFilter m_executableFilter = new ExecutableFilter();
        private string? m_commandLineAttachExe = null;
        private bool m_ignoreFutureVDWarnings = false;

        public ExtendedMainWindow() {
            InitializeComponent();

            // Grab the command-line arguments
            string[] args = Environment.GetCommandLineArgs();

            // Parse and handle arguments
            foreach (string arg in args) {
                if (arg.StartsWith("--attach=")) {
                    m_commandLineAttachExe = arg.Split('=')[1];
                }
            }
        }

        public static bool IsAdministrator() {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e) {

            processMonitor = new ProcessMonitor();

            if (!IsAdministrator()) {
                m_nNotificationsGroupBox.Visibility = Visibility.Visible;
                m_restartAsAdminButton.Visibility = Visibility.Visible;
                m_adminExplanation.Visibility = Visibility.Visible;
            }

            FillProcessList();
            m_openvrRadio.IsChecked = m_mainWindowSettings.OpenVRRadio;
            m_openxrRadio.IsChecked = m_mainWindowSettings.OpenXRRadio;
            m_nullifyVRPluginsCheckbox.IsChecked = m_mainWindowSettings.NullifyVRPluginsCheckbox;
            m_ignoreFutureVDWarnings = m_mainWindowSettings.IgnoreFutureVDWarnings;

            m_updateTimer.Tick += (sender, e) => Dispatcher.Invoke(MainWindow_Update);
            m_updateTimer.Start();

            // Custom Games
            AddSomeRandomGames();
            AddSteamGames();

            RefreshGameList();
        }

        private string GetIdentifier(IGame game)
        {
            if (game is SteamGame steamGame)
            {
                return $"steam_{steamGame.AppId}";
            }
            throw new NotImplementedException("");
        }

        public string GetImageIconFilePath(SteamGame game)
        {
            var expectedPath = Path.Combine(GetEGlobalImageDir(), $"{GetIdentifier(game)}_header.jpg");
            if (!File.Exists(expectedPath))
            {
                using (WebClient webClient = new WebClient())
                {
                    try
                    {
                        webClient.DownloadFile($"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.AppId}/capsule_231x87.jpg", expectedPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error al descargar la imagen: {ex.Message}");
                    }
                }
            }
            return expectedPath;
        }
      
        void AddSteamGames()
        {
            // elimino juegos

            //itero en el proveedor y añador
            var fileSystem = FileSystem.Shared;
            var registry = WindowsRegistry.Shared;


            var steamHandler = new SteamHandler(fileSystem, registry);

            var steamDirectoryPath = SteamLocationFinder.FindSteam(fileSystem, registry).Value.ToString();
            var steamManifests = SteamAppInfoFile.FromSteamDirectory(steamDirectoryPath);

            var steamGames = Games.Where(s => s.ProviderId == "Steam");

            //iterate between all the games
            foreach (var game in steamHandler.FindAllGames())
            {
                if (game.Value is SteamGame steamGame)
                {
                    // si no lo encuentra lo añade
                    var uEVRGame = steamGames.FirstOrDefault(s => s.GameManifest is SteamGameManifest manifest && manifest.AppId == steamGame.AppId.Value);
                    if (uEVRGame == null)
                    {
                        var executable = new Executable()
                        {
                            //Architecture = "x64",
                            OperatingSystem = "Windows",
                            //Path = "F:\\XboxGames\\Hi-Fi RUSH\\Content\\Hibiki\\Binaries\\WinGDK\\Hi-Fi-RUSH.exe",
                            Engine = new Engine()
                            {
                                Brand = "Unreal",
                                Version = new Version(4, 3, 21)
                            }
                        };

                        var gameManifest = new SteamGameManifest()
                        {
                            AppId = (int)steamGame.AppId.Value,
                            Description = steamGame.Name,
                            Executable = executable
                        };

                        uEVRGame = new UEVRGame()
                        {
                            Name = steamGame.Name,
                            ProviderId = "Steam",
                            GameManifest = gameManifest,
                            Id = Guid.NewGuid().ToString(),
                            ThumbnailUrl = GetImageIconFilePath(steamGame)
                        };

                        var steamApp = steamManifests.Apps.FirstOrDefault(s => s.AppId == steamGame.AppId);
                        if (steamApp != null)
                        {
                            //var appInfoDictionary = steamApp.GetDictionary("appinfo");
                            //var extendedDictionary = appInfoDictionary.GetDictionary("extended");
                            //var commonDictionary = appInfoDictionary.GetDictionary("common");
                            //var configDictionary = appInfoDictionary.GetDictionary("config");

                            //uEVRGame.Description = appInfoDictionary.GetValue<string>("description");

                            //uEVRGame.Name = commonDictionary.GetValue<string>("name");
                            //uEVRGame.Language = commonDictionary.GetDictionaryKeyValues("languages");
                            //uEVRGame.Developer = extendedDictionary.GetValue<string>("developer");
                            //uEVRGame.Publisher = extendedDictionary.GetValue<string>("publisher");
                            //uEVRGame.IsFree = extendedDictionary.GetValue<int>("isfreeapp") == 1;

                            //uEVRGame.ReleaseDate = commonDictionary.GetValue<int>("steam_release_date");
                            //uEVRGame.OriginalReleaseDate = commonDictionary.GetValue<int>("original_release_date");

                            //xecutable
                            //var launchConfiguration = configDictionary.GetDictionary("launch", "0");
                           
                            //uEVRGame.IsVR = launchConfiguration.GetValue<string>("type") == "vr";
                            //uEVRGame.OsList = launchConfiguration.GetValue<string>("config", "oslist") ?? commonDictionary.GetValue<string>("oslist");
                            //uEVRGame.OsArch = launchConfiguration.GetValue<string>("config", "osarch") ?? commonDictionary.GetValue<string>("betakey");

                            //var executableUrl = launchConfiguration.GetValue<string>("executable");
                            //if (Path.HasExtension(executableUrl))
                            //{
                            //    executableUrl = Path.Combine(steamGame.Path.ToString().Replace('/', '\\'), executableUrl);
                            //}
                            //executable.Path = executableUrl;
                        }

                        // is unreal
                        //if (UnrealEngineUtils.IsUnrealExe(executable.Path))
                        //{
                        //    UnrealEngineUtils.PopulateExecutableMetadata(uEVRGame);
                        //    //var fileMetadata =  MetadataHelper.GetMetadataFromExecutableFilePath(executable.Path);
                        //    Console.WriteLine("");

                        //    // versioning
                          
                        //}

                        Games.Add(uEVRGame);
                        GameRows.Add(new UEVRGameRowView(uEVRGame));
                    }
                }
            }
            //mark from deleteion non existing games
        }



        bool IsUnrealGameFolder(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                if (Directory.Exists(Path.Combine(directoryPath, "Engine", "Binaries")))
                {
                    return true;
                }
            }
            return false;
        }

        List<UEVRGame> Games = new();
        List<UEVRGameRowView> GameRows = new();
        List<UEVRGameRowView> FilteredGameRows = new();

        void AddSomeRandomGames()
        {
            var hify = new UEVRGame()
            {
                Name = "Hi-Fi-RUSH",
                ProviderId = "Manual",
                Id = "1231214123123412",
                GameManifest = new SteamGameManifest()
                {
                    AppId = 2323123,
                    AppType = "",
                    Description = "Hi-Fi-RUSH",
                    Executable = new Executable()
                    {
                        Architecture = "x86",
                        OperatingSystem = "Windows",
                        Path = "F:\\XboxGames\\Hi-Fi RUSH\\Content\\Hibiki\\Binaries\\WinGDK\\Hi-Fi-RUSH.exe",
                        Engine = new Engine()
                        {
                            Brand = "Unreal",
                            Version = new Version(4, 3, 21)
                        }
                    }
                },
                ThumbnailUrl = "C:\\Program Files (x86)\\Steam\\appcache\\librarycache\\10_header.jpg"
            };
            
            Games.Add(hify);
            GameRows.Add(new UEVRGameRowView(hify));
        }

        void RefreshGameList()
        {
            ResultListView.ItemsSource = null;
            FilteredGameRows.Clear();
            FilteredGameRows.AddRange(GameRows.Where(s => s.Name.Contains(SearchField.Text, StringComparison.InvariantCultureIgnoreCase)));
            ResultListView.ItemsSource = FilteredGameRows;
        }

        private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = sender as ListViewItem;
            if (item != null && item.IsSelected)
            {
                //Do your stuff
            }
        }
        private static bool IsExecutableRunning(string executableName) {
            return Process.GetProcesses().Any(p => p.ProcessName.Equals(executableName, StringComparison.OrdinalIgnoreCase));
        }

        private void RestartAsAdminButton_Click(object sender, RoutedEventArgs e) {
            // Get the path of the current executable
            var mainModule = Process.GetCurrentProcess().MainModule;
            if (mainModule == null) {
                return;
            }

            var exePath = mainModule.FileName;
            if (exePath == null) {
                return;
            }

            // Create a new process with administrator privileges
            var processInfo = new ProcessStartInfo {
                FileName = exePath,
                Verb = "runas",
                UseShellExecute = true,
            };

            try {
                // Attempt to start the process
                Process.Start(processInfo);
            } catch (Win32Exception ex) {

                // Handle the case when the user cancels the UAC prompt or there's an error
                if (!silent)
                MessageBox.Show($"Error: {ex.Message}\n\nThe application will continue running without administrator privileges.", "Failed to Restart as Admin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Close the current application instance
            Application.Current.Shutdown();
        }

        private DateTime m_lastAutoInjectTime = DateTime.MinValue;

        private void Update_InjectStatus() {
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

                        if (processes == null || processes.Length == 0 || !AnyInjectableProcesses(processes)) {
                            m_injectButton.Content = "Waiting for Process";
                            return;
                        }
                    }

                    m_injectButton.Content = "Inject";
                } catch (ArgumentException) {
                    var processes = Process.GetProcessesByName(m_lastSelectedProcessName);

                    if (processes == null || processes.Length == 0 || !AnyInjectableProcesses(processes)) {
                        m_injectButton.Content = "Waiting for Process";
                        return;
                    }

                    m_injectButton.Content = "Inject";
                }
            } else {
                m_injectButton.Content = "Waiting for " + m_commandLineAttachExe.ToLower() + "...";

                var processes = Process.GetProcessesByName(m_commandLineAttachExe.ToLower().Replace(".exe", ""));

                if (processes.Count() == 0) {
                    return;
                }

                Process? process = null;

                foreach (Process p in processes) {
                    if (IsInjectableProcess(p)) {
                        m_lastSelectedProcessId = p.Id;
                        m_lastSelectedProcessName = p.ProcessName;
                        process = p;
                    }
                }

                if (process == null) {
                    return;
                }

                if (now - m_lastAutoInjectTime > oneSecond) {
                    string runtimeName;

                    if (m_openvrRadio.IsChecked == true) {
                        runtimeName = "openvr_api.dll";
                    } else if (m_openxrRadio.IsChecked == true) {
                        runtimeName = "openxr_loader.dll";
                    } else {
                        runtimeName = "openvr_api.dll";
                    }

                    if (Injector.InjectDll(process.Id, runtimeName)) {
                        InitializeConfig(process.ProcessName);
                        Injector.InjectDll(process.Id, "UEVRBackend.dll");
                    }

                    m_lastAutoInjectTime = now;
                    m_commandLineAttachExe = null; // no need anymore.
                    FillProcessList();
                }
            }
        }

        private void Hide_ConnectionOptions() {
            m_openGameDirectoryBtn.Visibility = Visibility.Collapsed;
        }

        private void Show_ConnectionOptions() {
            m_openGameDirectoryBtn.Visibility = Visibility.Visible;
        }

        private DateTime lastInjectorStatusUpdate = DateTime.MinValue;
        private DateTime lastFrontendSignal = DateTime.MinValue;

        private void Update_InjectorConnectionStatus() {
            var data = SharedMemory.GetData();
            DateTime now = DateTime.Now;
            TimeSpan oneSecond = TimeSpan.FromSeconds(1);

            if (data != null) {
                m_connectionStatus.Text = UEVRConnectionStatus.Connected;
                m_connectionStatus.Text += ": " + data?.path;
                m_connectionStatus.Text += "\nThread ID: " + data?.mainThreadId.ToString();
                m_lastSharedData = data;
                m_connected = true;
                Show_ConnectionOptions();

                if (data?.signalFrontendConfigSetup == true && (now - lastFrontendSignal > oneSecond)) {
                    SharedMemory.SendCommand(SharedMemory.Command.ConfigSetupAcknowledged);
                    RefreshCurrentConfig();

                    lastFrontendSignal = now;
                }
            } else {
                if (m_connected && !string.IsNullOrEmpty(m_commandLineAttachExe))
                {
                    // If we launched with an attached game exe, we shut ourselves down once that game closes.
                    Application.Current.Shutdown();
                    return;
                }
                
                m_connectionStatus.Text = UEVRConnectionStatus.NoInstanceDetected;
                m_connected = false;
                Hide_ConnectionOptions();
            }

            lastInjectorStatusUpdate = now;
        }

        private string GetEGlobalImageDir()
        {
            string directory = Path.Combine(GetEGlobalCacheDir(), "images");

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            return directory;
        }

        private string GetEGlobalCacheDir()
        {
            string directory = Path.Combine(GetEGlobalDir(), "cache");

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            return directory;
        }

        private string GetEGlobalDir()
        {
            string directory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            directory += "\\UnrealVR";

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            return directory;
        }

        [Obsolete("Use GetEGlobalGameDir")]
        private string GetGlobalDir() {
            string directory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            directory += "\\UnrealVRMod";

            if (!System.IO.Directory.Exists(directory)) {
                System.IO.Directory.CreateDirectory(directory);
            }

            return directory;
        }

        private string GetEGlobalGameDir(string gameName)
        {
            string directory = Path.Combine(GetEGlobalCacheDir(), gameName);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            return directory;
        }

        [Obsolete("Use GetEGlobalGameDir")]
        private string GetGlobalGameDir(string gameName) {
            string directory = GetGlobalDir() + "\\" + gameName;

            if (!System.IO.Directory.Exists(directory)) {
                System.IO.Directory.CreateDirectory(directory);
            }

            return directory;
        }

        private void NavigateToDirectory(string directory) {
            string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string explorerPath = System.IO.Path.Combine(windowsDirectory, "explorer.exe");
            Process.Start(explorerPath, "\"" + directory + "\"");
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) {
                //this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }

        private string GetGlobalDirPath() {
            string directory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            directory += "\\UnrealVRMod";
            return directory;
        }

        protected void OpenGlobalDir_Clicked(object sender, MouseButtonEventArgs e) {
            string directory = GetGlobalDirPath();

            if (!System.IO.Directory.Exists(directory)) {
                System.IO.Directory.CreateDirectory(directory);
            }

            NavigateToDirectory(directory);
        }

        protected void OpenGameDir_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (m_lastSharedData == null) {
                return;
            }

            var directory = System.IO.Path.GetDirectoryName(m_lastSharedData?.path);
            if (directory == null) {
                return;
            }

            NavigateToDirectory(directory);
        }
        private void ExportConfig_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (!m_connected) {
                MessageBox.Show("Inject into a game first!");
                return;
            }

            if (m_lastSharedData == null) {
                MessageBox.Show("No game connection detected.");
                return;
            }

            var dir = GetGlobalGameDir(m_lastSelectedProcessName);
            if (dir == null) {
                return;
            }

            if (!Directory.Exists(dir)) {
                MessageBox.Show("Directory does not exist.");
                return;
            }

            var exportedConfigsDir = GetGlobalDirPath() + "\\ExportedConfigs";

            if (!Directory.Exists(exportedConfigsDir)) {
                Directory.CreateDirectory(exportedConfigsDir);
            }

            GameConfig.CreateZipFromDirectory(dir, exportedConfigsDir + "\\" + m_lastSelectedProcessName + ".zip");
            NavigateToDirectory(exportedConfigsDir);
        }

        private void ImportConfig_Clicked(object sender, MouseButtonEventArgs e)
        {
            var importPath = GameConfig.BrowseForImport(GetGlobalDirPath());

            if (importPath == null) {
                return;
            }

            var gameName = System.IO.Path.GetFileNameWithoutExtension(importPath);
            if (gameName == null) {
                MessageBox.Show("Invalid filename");
                return;
            }

            var globalDir = GetGlobalDirPath();
            var gameGlobalDir = globalDir + "\\" + gameName;

            try {
                if (!Directory.Exists(gameGlobalDir)) {
                    Directory.CreateDirectory(gameGlobalDir);
                }

                var finalGameName = GameConfig.ExtractZipToDirectory(importPath, gameGlobalDir, gameName);

                if (finalGameName == null) {
                    MessageBox.Show("Failed to extract the ZIP file.");
                    return;
                }

                var finalDirectory = System.IO.Path.Combine(globalDir, finalGameName);
                NavigateToDirectory(finalDirectory);


                if (m_connected) {
                    SharedMemory.SendCommand(SharedMemory.Command.ReloadConfig);
                }
            } catch (Exception ex) {
                MessageBox.Show("An error occurred: " + ex.Message);
            }
        }

        private bool m_virtualDesktopWarned = false;
        private bool m_virtualDesktopChecked = false;
        private void Check_VirtualDesktop() {
            if (m_virtualDesktopWarned || m_ignoreFutureVDWarnings) {
                return;
            }

            if (IsExecutableRunning("VirtualDesktop.Streamer")) {
                m_virtualDesktopWarned = true;
                var dialog = new VDWarnDialog();
                dialog.Topmost = true;
                dialog.ShowDialog();
              
                if (dialog.DialogResultOK) {
                    if (dialog.HideFutureWarnings) {
                        m_ignoreFutureVDWarnings = true;
                    }
                }
            }
        }

        private void MainWindow_Update() {
            Update_InjectorConnectionStatus();
            Update_InjectStatus();

            if (m_virtualDesktopChecked == false) {
                m_virtualDesktopChecked = true;
                Check_VirtualDesktop();
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            m_mainWindowSettings.OpenXRRadio = m_openxrRadio.IsChecked == true;
            m_mainWindowSettings.OpenVRRadio = m_openvrRadio.IsChecked == true;
            m_mainWindowSettings.NullifyVRPluginsCheckbox = m_nullifyVRPluginsCheckbox.IsChecked == true;
            m_mainWindowSettings.IgnoreFutureVDWarnings = m_ignoreFutureVDWarnings;

            m_mainWindowSettings.Save();
        }

        private string m_lastDisplayedWarningProcess = "";
        private string[] m_discouragedPlugins = {
            "OpenVR",
            "OpenXR",
            "Oculus"
        };

        private string? AreVRPluginsPresent_InEngineDir(string enginePath) {
            string pluginsPath = enginePath + "\\Binaries\\ThirdParty";

            if (!Directory.Exists(pluginsPath)) {
                return null;
            }

            foreach (string discouragedPlugin in m_discouragedPlugins) {
                string pluginPath = pluginsPath + "\\" + discouragedPlugin;

                if (Directory.Exists(pluginPath)) {
                    return pluginsPath;
                }
            }

            return null;
        }

        private string? AreVRPluginsPresent(string gameDirectory) {
            try {
                var parentPath = gameDirectory;

                for (int i = 0; i < 10; ++i) {
                    parentPath = System.IO.Path.GetDirectoryName(parentPath);

                    if (parentPath == null) {
                        return null;
                    }

                    if (Directory.Exists(parentPath + "\\Engine")) {
                        return AreVRPluginsPresent_InEngineDir(parentPath + "\\Engine");
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine($"Exception caught: {ex}");
            }

            return null;
        }

        private bool IsUnrealEngineGame(string gameDirectory, string targetName) {
            try {
                if (targetName.ToLower().EndsWith("-win64-shipping")) {
                    return true;
                }

                if (targetName.ToLower().EndsWith("-wingdk-shipping")) {
                    return true;
                }

                // Check if going up the parent directories reveals the directory "\Engine\Binaries\ThirdParty".
                var parentPath = gameDirectory;
                for (int i = 0; i < 10; ++i) {  // Limit the number of directories to move up to prevent endless loops.
                    if (parentPath == null) {
                        return false;
                    }

                    if (Directory.Exists(parentPath + "\\Engine\\Binaries\\ThirdParty")) {
                        return true;
                    }

                    if (Directory.Exists(parentPath + "\\Engine\\Binaries\\Win64")) {
                        return true;
                    }

                    parentPath = System.IO.Path.GetDirectoryName(parentPath);
                }
            } catch (Exception ex) {
                Console.WriteLine($"Exception caught: {ex}");
            }

            return false;
        }

        private string IniToString(IConfiguration config) {
            string result = "";

            foreach (var kv in config.AsEnumerable()) {
                result += kv.Key + "=" + kv.Value + "\n";
            }

            return result;
        }

        private void SaveCurrentConfig() {
            try {
                if (m_currentConfig == null || m_currentConfigPath == null) {
                    return;
                }

                var iniStr = IniToString(m_currentConfig);
                Debug.Print(iniStr);

                File.WriteAllText(m_currentConfigPath, iniStr);

                if (m_connected) {
                    SharedMemory.SendCommand(SharedMemory.Command.ReloadConfig);
                }
            } catch(Exception ex) {
                MessageBox.Show(ex.ToString());
            }
        }

        private void TextChanged_Value(object sender, RoutedEventArgs e) {
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
                    RefreshCurrentConfig();
                }

                m_currentConfig[keyValuePair.Key] = newValue;
                RefreshConfigUI();

                if (changed) {
                    SaveCurrentConfig();
                }
            } catch(Exception ex) { 
                Console.WriteLine(ex.ToString()); 
            }
        }

        private void ComboChanged_Value(object sender, RoutedEventArgs e) {
            try {
                if (m_currentConfig == null || m_currentConfigPath == null) {
                    return;
                }

                var comboBox = (ComboBox)sender;
                var keyValuePair = (GameSettingEntry)comboBox.DataContext;

                bool changed = m_currentConfig[keyValuePair.Key] != keyValuePair.Value;
                var newValue = keyValuePair.Value;

                if (changed) {
                    RefreshCurrentConfig();
                }

                m_currentConfig[keyValuePair.Key] = newValue;
                RefreshConfigUI();

                if (changed) {
                    SaveCurrentConfig();
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        private void CheckChanged_Value(object sender, RoutedEventArgs e) {
            try {
                if (m_currentConfig == null || m_currentConfigPath == null) {
                    return;
                }

                var checkbox = (CheckBox)sender;
                var keyValuePair = (GameSettingEntry)checkbox.DataContext;

                bool changed = m_currentConfig[keyValuePair.Key] != keyValuePair.Value;
                string newValue = keyValuePair.Value;

                if (changed) {
                    RefreshCurrentConfig();
                }

                m_currentConfig[keyValuePair.Key] = newValue;
                RefreshConfigUI();

                if (changed) {
                    SaveCurrentConfig();
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        private void RefreshCurrentConfig() {
            if (m_currentConfig == null || m_currentConfigPath == null) {
                return;
            }

            InitializeConfig_FromPath(m_currentConfigPath);
        }

        private void RefreshConfigUI() {
            if (m_currentConfig == null) {
                return;
            }

            var vanillaList = m_currentConfig.AsEnumerable().ToList();
            vanillaList.Sort((a, b) => a.Key.CompareTo(b.Key));

            List<GameSettingEntry> newList = new List<GameSettingEntry>();

            foreach (var kv in vanillaList) {
                if (!string.IsNullOrEmpty(kv.Key) && !string.IsNullOrEmpty(kv.Value)) {
                    Dictionary<string, string> comboValues = new Dictionary<string, string>();
                    string tooltip = "";

                    if (ComboMapping.KeyEnums.ContainsKey(kv.Key)) {
                        var valueList = ComboMapping.KeyEnums[kv.Key];

                        if (valueList != null && valueList.ContainsKey(kv.Value)) {
                            comboValues = valueList;
                        }
                    }

                    if (GameSettingTooltips.Entries.ContainsKey(kv.Key)) {
                        tooltip = GameSettingTooltips.Entries[kv.Key];
                    }

                    newList.Add(new GameSettingEntry { Key = kv.Key, Value = kv.Value, ComboValues = comboValues, Tooltip = tooltip });
                }
            }

            if (m_iniListView.ItemsSource == null) {
                m_iniListView.ItemsSource = newList;
            } else {
                foreach (var kv in newList) {
                    var source = (List<GameSettingEntry>)m_iniListView.ItemsSource;

                    var elements = source.FindAll(el => el.Key == kv.Key);

                    if (elements.Count() == 0) {
                        // Just set the entire list, we don't care.
                        m_iniListView.ItemsSource = newList;
                        break;
                    } else {
                        elements[0].Value = kv.Value;
                        elements[0].ComboValues = kv.ComboValues;
                        elements[0].Tooltip = kv.Tooltip;
                    }
                }
            }

            m_iniListView.Visibility = Visibility.Visible;
        }

        private void InitializeConfig_FromPath(string configPath) {
            var builder = new ConfigurationBuilder().AddIniFile(configPath, optional: true, reloadOnChange: false);

            m_currentConfig = builder.Build();
            m_currentConfigPath = configPath;

            foreach (var entry in MandatoryConfig.Entries) {
                if (m_currentConfig.AsEnumerable().ToList().FindAll(v => v.Key == entry.Key).Count() == 0) {
                    m_currentConfig[entry.Key] = entry.Value;
                }
            }

            RefreshConfigUI();
        }

        private void InitializeConfig(string gameName) {
            var configDir = GetGlobalGameDir(gameName);
            var configPath = configDir + "\\config.txt";

            InitializeConfig_FromPath(configPath);
        }

        private bool m_isFirstProcessFill = true;

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
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
                                MessageBox.Show("VR plugins have been detected in the game install directory.\n" +
                                                "You may want to delete or rename these as they will cause issues with the mod.\n" +
                                                "You may also want to pass -nohmd as a command-line option to the game. This can sometimes work without deleting anything.");
                                var result = MessageBox.Show("Do you want to open the plugins directory now?", "Confirmation", MessageBoxButton.YesNo);

                                switch (result) {
                                    case MessageBoxResult.Yes:
                                        NavigateToDirectory(pluginsDir);
                                        break;
                                    case MessageBoxResult.No:
                                        break;
                                };
                            }

                            Check_VirtualDesktop();

                            m_iniListView.ItemsSource = null; // Because we are switching processes.
                            InitializeConfig(p.ProcessName);

                            if (!IsUnrealEngineGame(gameDirectory, m_lastSelectedProcessName) && !m_isFirstProcessFill) {
                                MessageBox.Show("Warning: " + m_lastSelectedProcessName + " does not appear to be an Unreal Engine title");
                            }
                        }

                        m_lastDefaultProcessListName = GenerateProcessName(p);
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine($"Exception caught: {ex}");
            }
        }

        private void ComboBox_DropDownOpened(object sender, System.EventArgs e) {
            m_lastSelectedProcessName = "";
            m_lastSelectedProcessId = 0;

            FillProcessList();
            Update_InjectStatus();

            m_isFirstProcessFill = false;
        }

        private void Donate_Clicked(object sender, RoutedEventArgs e) {
            Process.Start(new ProcessStartInfo("https://patreon.com/praydog") { UseShellExecute = true });
        }

        private void Documentation_Clicked(object sender, RoutedEventArgs e) {
            Process.Start(new ProcessStartInfo("https://praydog.github.io/uevr-docs/") { UseShellExecute = true });
        }
        private void Discord_Clicked(object sender, RoutedEventArgs e) {
            Process.Start(new ProcessStartInfo("http://flat2vr.com") { UseShellExecute = true });
        }
        private void GitHub_Clicked(object sender, RoutedEventArgs e) {
            Process.Start(new ProcessStartInfo("https://github.com/praydog/UEVR") { UseShellExecute = true });
        }

        private void Inject_Clicked(object sender, RoutedEventArgs e) {
            // "Terminate Connected Process"
            if (m_connected) {
                try {
                    var pid = m_lastSharedData?.pid;

                    if (pid != null) {
                        var target = Process.GetProcessById((int)pid);
                        target.CloseMainWindow();
                        target.Kill();
                    }
                } catch(Exception) {

                }

                return;
            }

            var selectedProcessName = m_processListBox.SelectedItem;
            if (selectedProcessName == null) {
                return;
            }

            var index = m_processListBox.SelectedIndex;
            var process = m_processList[index];

            if (process == null)
            {
                return;
            }

            if (TryGetNewProcess(out var newProcess))
            {
                process = newProcess;
                m_processList[index] = newProcess;
                m_processListBox.Items[index] = GenerateProcessName(newProcess);
                m_processListBox.SelectedIndex = index;
            }

            InjectProcess(process);
        }

        bool silent = false;
        void InjectProcess(Process process)
        {
            string runtimeName;

            if (m_openvrRadio.IsChecked == true)
            {
                runtimeName = "openvr_api.dll";
            }
            else if (m_openxrRadio.IsChecked == true)
            {
                runtimeName = "openxr_loader.dll";
            }
            else
            {
                runtimeName = "openvr_api.dll";
            }

            if (m_nullifyVRPluginsCheckbox.IsChecked == true)
            {
                IntPtr nullifierBase;
                if (Injector.InjectDll(process.Id, "UEVRPluginNullifier.dll", out nullifierBase) && nullifierBase.ToInt64() > 0)
                {
                    if (!Injector.CallFunctionNoArgs(process.Id, "UEVRPluginNullifier.dll", nullifierBase, "nullify", true))
                    {
                        if (!silent)
                        MessageBox.Show("Failed to nullify VR plugins.");
                    }
                }
                else
                {
                    if (!silent)
                    MessageBox.Show("Failed to inject plugin nullifier.");
                }
            }

            if (Injector.InjectDll(process.Id, runtimeName))
            {
                Injector.InjectDll(process.Id, "UEVRBackend.dll");
            }
        }

        bool TryGetNewProcess(out Process process)
        {
            process = default;

            // Double check that the process we want to inject into exists
            // this can happen if the user presses inject again while
            // the previous combo entry is still selected but the old process
            // has died.
            try
            {
                var verifyProcess = Process.GetProcessById(m_lastSelectedProcessId);

                if (verifyProcess == null || verifyProcess.HasExited || verifyProcess.ProcessName != m_lastSelectedProcessName)
                {
                    var processes = Process.GetProcessesByName(m_lastSelectedProcessName);

                    if (processes == null || processes.Length == 0 || !AnyInjectableProcesses(processes))
                    {
                        return false;
                    }

                    foreach (var candidate in processes)
                    {
                        if (IsInjectableProcess(candidate))
                        {
                            process = candidate;
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            return false;
        }

        private string GenerateProcessName(Process p) {
            return p.ProcessName + " (pid: " + p.Id + ")" + " (" + p.MainWindowTitle + ")";
        }

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool wow64Process);

        private bool IsInjectableProcess(Process process) {
            try {
                if (Environment.Is64BitOperatingSystem) {
                    try {
                        bool isWow64 = false;
                        if (IsWow64Process(process.Handle, out isWow64) && isWow64) {
                            return false;
                        }
                    } catch {
                        // If we threw an exception here, then the process probably can't be accessed anyways.
                        return false;
                    }
                }

                if (process.MainWindowTitle.Length == 0) {
                    return false;
                }

                if (process.Id == Process.GetCurrentProcess().Id) {
                    return false;
                }

                if (!m_executableFilter.IsValidExecutable(process.ProcessName.ToLower())) {
                    return false;
                }

                foreach (ProcessModule module in process.Modules) {
                    if (module.ModuleName == null) {
                        continue;
                    }

                    string moduleLow = module.ModuleName.ToLower();
                    if (moduleLow == "d3d11.dll" || moduleLow == "d3d12.dll") {
                        return true ;
                    }
                }

                return false;
            } catch(Exception ex) {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        private bool AnyInjectableProcesses(Process[] processList) {
            foreach (Process process in processList) {
                if (IsInjectableProcess(process)) {
                    return true;
                }
            }

            return false;
        }
        private SemaphoreSlim m_processSemaphore = new SemaphoreSlim(1, 1); // create a semaphore with initial count of 1 and max count of 1
        private string? m_lastDefaultProcessListName = null;

        private async void FillProcessList() {
            // Allow the previous running FillProcessList task to finish first
            if (m_processSemaphore.CurrentCount == 0) {
                return;
            }

            await m_processSemaphore.WaitAsync();

            try {
                m_processList.Clear();
                m_processListBox.Items.Clear();

                await Task.Run(() => {
                    // get the list of processes
                    Process[] processList = Process.GetProcesses();

                    // loop through the list of processes
                    foreach (Process process in processList) {
                        if (!IsInjectableProcess(process)) {
                            continue;
                        }

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            m_processList.Add(process);
                            m_processList.Sort((a, b) => a.ProcessName.CompareTo(b.ProcessName));
                            RefreshProcessComboBox();
                        });
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RefreshProcessComboBox();
                    });
                });
            } finally {
                m_processSemaphore.Release();
            }
        }

        private void RefreshProcessComboBox()
        {
            m_processListBox.Items.Clear();

            foreach (Process p in m_processList)
            {
                string processName = GenerateProcessName(p);
                m_processListBox.Items.Add(processName);

                if (m_processListBox.SelectedItem == null && m_processListBox.Items.Count > 0)
                {
                    if (m_lastDefaultProcessListName == null || m_lastDefaultProcessListName == processName)
                    {
                        m_processListBox.SelectedItem = m_processListBox.Items[m_processListBox.Items.Count - 1];
                        m_lastDefaultProcessListName = processName;
                    }
                }
            }
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var list = (ListView)sender;
            GridViewColumnHeader colHeader = (GridViewColumnHeader)e.OriginalSource;
            string colName = colHeader.Content.ToString();

            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(list.ItemsSource);
            view.SortDescriptions.Add(new SortDescription(colName, ListSortDirection.Ascending));

            view.Refresh();
        }

        UEVRGameRowView selectedItem;

        private void ResultListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems[0] is UEVRGameRowView r)
            {
                selectedItem = r;
                RefreshGameData();
            }
        }

        private void RefreshGameData()
        {
            oLaunch.IsEnabled = selectedItem != null && File.Exists(selectedItem.Executable);

        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Window w = (Window)sender;
            GameListScrollView.MaxHeight = w.ActualHeight - 110;
            ContentScrollView.MaxHeight = Math.Max(w.ActualHeight - 100, 100);
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshGameList();
        }
        
        bool skipAlertMessages = false;

        private Process? WaitForProcess(string processName, int maxRetry = 1, int retryTimeInMiliseconds = 1000)
        {
            for (int i = 0; i < maxRetry && !m_connected; i++)
            {
                if (i > 0)
                {
                    Thread.Sleep(retryTimeInMiliseconds);
                }

                foreach (var p in Process.GetProcesses())
                {
                    if (!IsInjectableProcess(p))
                    {
                        continue;
                    }
                    if (processName == p.ProcessName)
                    {
                        return p;
                    }
                }
            }
            return null;
        }

        class ProcessMonitor
        {
            public event EventHandler<Process> NewProcessDetected;
            ManagementEventWatcher processStartWatcher;
            public void StartMonitoring(string Path)
            {
                // Crear un objeto para seguir los eventos de inicio de nuevos procesos
                processStartWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
                processStartWatcher.EventArrived += ProcessStarted;

                // Comenzar a monitorear
                processStartWatcher.Start();
            }

            void Stop()
            {
                processStartWatcher.Stop();
            }

            private void ProcessStarted(object sender, EventArrivedEventArgs e)
            {
                Console.WriteLine($"Nuevo proceso detectado: {e.NewEvent}");

                // Lanzar el evento de nuevo proceso detectado
                //OnNewProcessDetected(e.NewEvent.);
            }

            //protected virtual void OnNewProcessDetected(Proc processName)
            //{
            //    // Verificar si hay manejadores de eventos suscritos
            //    if (NewProcessDetected != null)
            //    {
            //        // Lanzar el evento
            //        NewProcessDetected.Invoke(this, processName);
            //    }
            //}
        }

        ProcessMonitor processMonitor;

        private void oLaunch_Click(object sender, RoutedEventArgs e)
        {
            var selected = selectedItem;
            if (selected == null)
                return;

            skipAlertMessages = true;

            //var processCandidate = WaitForProcess(arguments.ProcessName);
            //processCandidate?.CloseMainWindow();

            Process p = new();
            p.StartInfo.FileName = selected.Executable;
            p.StartInfo.Arguments = "";
            p.StartInfo.WorkingDirectory = Path.GetDirectoryName(selected.Executable);
            p.Start();

            //var processCandidate = WaitForProcess(arguments.ProcessName, maxRetry: 10);
            //if (processCandidate != null)
            //{
            //    m_lastDefaultProcessListName = GenerateProcessName(processCandidate);
            //}

            //if (processCandidate != null)
            //{
            //    int max = 10;
            //    for (int i = 0; i < max && !m_connected; i++)
            //    {
            //        processCandidate = WaitForProcess(arguments.ProcessName);
            //        if (processCandidate == null)
            //        {
            //            continue;
            //        }

            //        InitializeConfig(processCandidate.ProcessName);
            //        InjectProcess(processCandidate);

            //        Update_InjectorConnectionStatus();
            //    }
            //}
            skipAlertMessages = false;

            //Button OpenPopupButton = (Button)sender;

            //// Crear un nuevo Popup
            //Popup popup = new Popup();

            //// Crear contenido para el Popup (puede ser cualquier control)
            //TextBlock popupContent = new TextBlock();
            //popupContent.Text = "¡Hola, este es un Popup!";
            //popupContent.Margin = new Thickness(10);

            //// Establecer el contenido del Popup
            //popup.Child = popupContent;

            //// Establecer propiedades adicionales del Popup según sea necesario
            //popup.PlacementTarget = sender as UIElement;
            //popup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            //popup.HorizontalOffset = OpenPopupButton.ActualWidth/2 - 70; // Centrar horizontalmente

            //popup.IsOpen = true;
        }
    }

    internal static class ImageHelper
    {
        public static ImageSource? GetImage(string imagePath)
        {
            if (!File.Exists(imagePath)) { return null; };
            // Crea una nueva instancia de BitmapImage
            BitmapImage bitmapImage = new BitmapImage();
            // Asigna la URI de la imagen al BitmapImage
            bitmapImage.BeginInit();
            bitmapImage.UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);
            bitmapImage.EndInit();
            return bitmapImage;
        }
    }

    // Clase de ejemplo para representar los datos
    internal class UEVRGameRowView
    {
        internal UEVRGame game;

        public UEVRGameRowView()
        {

        }

        public UEVRGameRowView(UEVRGame hify)
        {
            SetGame(hify);
        }

        public void SetGame(UEVRGame game)
        {
            this.game = game;

            this.Name = this.game.Name;
            this.Provider = this.game.ProviderId;
            this.Description = "This is a test";
            this.Engine = this.game.GameManifest.Executable.Engine.Brand;
            this.Version = this.game.GameManifest.Executable.Engine.VersionString?.ToString() ?? string.Empty;
            this.Image = ImageHelper.GetImage(this.game.ThumbnailUrl ?? "C:\\Program Files (x86)\\Steam\\appcache\\librarycache\\80_library_hero_blur");
            this.Executable = this.game.GameManifest.Executable.Path;
        }

        public string Executable { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public string Tooltip { get; set; }
        public string Provider { get; set; }
        public string Engine { get; set; }
        public string Version { get; set; }
        public ImageSource Image { get; set; }
    }

}