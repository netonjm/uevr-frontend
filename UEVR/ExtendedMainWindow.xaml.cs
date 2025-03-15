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
using System.Windows.Data;
using GameFinder.RegistryUtils;
using GameFinder.StoreHandlers.Steam;
using System.Windows.Media;
using GameFinder.StoreHandlers.Steam.Services;
using NexusMods.Paths;
using System.Windows.Navigation;
using System.Windows.Documents;
using UGMVR.UnrealVR;
using UGMVR;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using UGMVR.Sdks.Steam;

namespace UEVR
{
	class GameSettingEntry : INotifyPropertyChanged
	{
		private string _key = "";
		private string _value = "";
		private string _tooltip = "";
		private string _extraInfo = "";

		public string Key { get => _key; set => SetProperty (ref _key, value); }
		public string Value
		{
			get => _value;
			set
			{
				SetProperty (ref _value, value);
				OnPropertyChanged (nameof (ValueAsBool));
			}
		}

		public string Tooltip { get => _tooltip; set => SetProperty (ref _tooltip, value); }

		public int KeyAsInt { get { return Int32.Parse (Key); } set { Key = value.ToString (); } }
		public bool ValueAsBool
		{
			get => Boolean.Parse (Value);
			set
			{
				Value = value.ToString ().ToLower ();
			}
		}

		public Dictionary<string, string> ComboValues { get; set; } = new Dictionary<string, string> ();

		public event PropertyChangedEventHandler? PropertyChanged;

		protected bool SetProperty<T> (ref T storage, T value, [CallerMemberName] string? propertyName = null)
		{
			if (Equals (storage, value)) return false;
			if (propertyName == null) return false;

			storage = value;
			OnPropertyChanged (propertyName);
			return true;
		}

		protected virtual void OnPropertyChanged (string propertyName)
		{
			PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propertyName));
		}
	};

	enum RenderingMethod
	{
		[Description("Native Stereo")]
		NativeStereo = 0,
		[Description("Synced Sequential")]
		SyncedSequential = 1,
		[Description("Alternating/AFR")]
		Alternating = 2
	};

	enum SyncedSequentialMethods
	{
		SkipTick = 0,
		SkipDraw = 1,
	};

	class ComboMapping
	{

		public static Dictionary<string, string> RenderingMethodValues = new Dictionary<string, string>(){
			{"0", "Native Stereo" },
			{"1", "Synced Sequential" },
			{"2", "Alternating/AFR" }
		};

		public static Dictionary<string, string> SyncedSequentialMethodValues = new Dictionary<string, string>(){
			{"0", "Skip Tick" },
			{"1", "Skip Draw" },
		};

		public static Dictionary<string, Dictionary<string, string>> KeyEnums = new Dictionary<string, Dictionary<string, string>>() {
			{ "VR_RenderingMethod", RenderingMethodValues },
			{ "VR_SyncedSequentialMethod", SyncedSequentialMethodValues },
		};
	};

	class MandatoryConfig
	{
		public static Dictionary<string, string> Entries = new Dictionary<string, string>() {
			{ "VR_RenderingMethod", ((int)RenderingMethod.NativeStereo).ToString() },
			{ "VR_SyncedSequentialMethod", ((int)SyncedSequentialMethods.SkipDraw).ToString() },
			{ "VR_UncapFramerate", "true" },
			{ "VR_Compatibility_SkipPostInitProperties", "false" }
		};
	};

	class GameSettingTooltips
	{
		public static string VR_RenderingMethod =
		"Native Stereo: The default, most performant, and best looking rendering method (when it works). Runs through the native UE stereo pipeline. Can cause rendering bugs or crashes on some games.\n" +
		"Synced Sequential: A form of AFR. Can fix many rendering bugs. It is fully synchronized with none of the usual AFR artifacts. Causes TAA/temporal effect ghosting.\n" +
		"Alternating/AFR: The most basic form of AFR with all of the usual desync/artifacts. Should generally not be used unless the other two are causing issues.";

		public static string VR_SyncedSequentialMethod =
		"Requires \"Synced Sequential\" rendering to be enabled.\n" +
		"Skip Tick: Skips the engine tick on the next frame. Usually works well but sometimes causes issues.\n" +
		"Skip Draw: Skips the viewport draw on the next frame. Works with least issues but particle effects can play slower in some cases.\n";

		public static Dictionary<string, string> Entries = new Dictionary<string, string>() {
			{ "VR_RenderingMethod", VR_RenderingMethod },
			{ "VR_SyncedSequentialMethod", VR_SyncedSequentialMethod },
		};
	}

	public class ValueTemplateSelector : DataTemplateSelector
	{
		public DataTemplate? ComboBoxTemplate { get; set; }
		public DataTemplate? TextBoxTemplate { get; set; }
		public DataTemplate? CheckboxTemplate { get; set; }

		public override DataTemplate? SelectTemplate (object item, DependencyObject container)
		{
			var keyValuePair = (GameSettingEntry)item;
			if (ComboMapping.KeyEnums.ContainsKey (keyValuePair.Key)) {
				return ComboBoxTemplate;
			} else if (keyValuePair.Value.ToLower ().Contains ("true") || keyValuePair.Value.ToLower ().Contains ("false")) {
				return CheckboxTemplate;
			} else {
				return TextBoxTemplate;
			}
		}
	}


	public partial class ExtendedMainWindow : Window
	{
		internal static bool skipAlertMessages = false;

		private DispatcherTimer m_updateTimer = new DispatcherTimer {
			Interval = new TimeSpan(0, 0, 1)
		};

		private bool m_ignoreFutureVDWarnings = false;

		private bool m_virtualDesktopWarned = false;
		private bool m_virtualDesktopChecked = false;
		private bool m_steamVR;

		private List<UEVRGameRowView> GameRows = new();
		private List<UEVRGameRowView> FilteredGameRows = new();

		internal static MainWindowSettings MainSettings = new MainWindowSettings();

		private UEVRGameRowView selectedItem;
		private SurrealStreamerProcess surrealStreamerRunner = new();
		private AccessibilitySurrealStreamingWindow surrealWindow;
		private ProcessManager processManager = new();

		public ExtendedMainWindow ()
		{
			InitializeComponent ();

			AppEnvironment.FinishedLoading += AppEnvironment_FinishedLoading;
			UnrealPanel.OnMainInit ();
		}

		private void AppEnvironment_FinishedLoading (object? sender, EventArgs e)
		{
			// Custom Games

			Refresh ();

		}

		enum RunningStates
		{
			Start,
			Stop
		}

		private void MainWindow_Loaded (object sender, RoutedEventArgs e)
		{
			if (!WindowsIdentityHelper.IsAdministrator ()) {
				m_nNotificationsGroupBox.Visibility = Visibility.Visible;
				m_restartAsAdminButton.Visibility = Visibility.Visible;
				m_adminExplanation.Visibility = Visibility.Visible;
			}

			UnrealPanel.OnMainLoaded ();

			processManager.Finished += (s, e) => {
				oLaunch.Content = RunningStates.Start.ToString ();
			};

			m_UEvRFilter.Checked += (s, e) => Refresh ();
			m_UEvRFilter.Unchecked += (s, e) => Refresh ();

			//m_openvrRadio.IsChecked = m_mainWindowSettings.OpenVRRadio;
			//m_openxrRadio.IsChecked = m_mainWindowSettings.OpenXRRadio;

			m_ignoreFutureVDWarnings = MainSettings.IgnoreFutureVDWarnings;

			m_updateTimer.Tick += (sender, e) => Dispatcher.Invoke (MainWindow_Update);
			m_updateTimer.Start ();
		}

		void Clean ()
		{
			selectedItem = null;
			GameRows.Clear ();
			UnrealPanel.Clean ();
		}

		void Refresh ()
		{
			Clean ();
			AddSomeRandomGames ();
			AddEngineGames ();
			RefreshGameList ();

		}

		private void AddEngineGames ()
		{
			bool ueFilter = (bool)m_UEvRFilter.IsChecked;

			var fileSystem = FileSystem.Shared;
			var registry = WindowsRegistry.Shared;

			//iterate between all the games
			foreach (var game in AppEnvironment.Games) {
				if (ueFilter && (game.IsVR || !game.IsUnrealGame)) {
					continue;
				}

				var rowView = new UEVRGameRowView(game);
				GameRows.Add (rowView);
			}
		}

		private void AddSomeRandomGames ()
		{
			//var hify = new UEVRGame()
			//{
			//    Name = "Hi-Fi-RUSH",
			//    ProviderId = "Manual",
			//    Id = "1231214123123412",
			//    GameManifest = new SteamGameManifest()
			//    {
			//        AppId = 2323123,
			//        AppType = "",
			//        Description = "Hi-Fi-RUSH",
			//        Executable = new Executable()
			//        {
			//            Architecture = "x86",
			//            OperatingSystem = "Windows",
			//            Path = "F:\\XboxGames\\Hi-Fi RUSH\\Content\\Hibiki\\Binaries\\WinGDK\\Hi-Fi-RUSH.exe",
			//            Engine = new Engine()
			//            {
			//                Brand = "Unreal",
			//                Version = new Version(4, 3, 21)
			//            }
			//        }
			//    },
			//    ThumbnailUrl = "C:\\Program Files (x86)\\Steam\\appcache\\librarycache\\10_header.jpg"
			//};

			//Games.Add(hify);
			//GameRows.Add(new UEVRGameRowView(hify));
		}

		private void RefreshGameList ()
		{
			ResultListView.ItemsSource = null;
			FilteredGameRows.Clear ();
			FilteredGameRows.AddRange (GameRows.Where (s => s.Name.Contains (SearchField.Text, StringComparison.InvariantCultureIgnoreCase)));
			ResultListView.ItemsSource = FilteredGameRows;
			RefreshGameData ();
		}

		private void ListViewItem_PreviewMouseLeftButtonDown (object sender, MouseButtonEventArgs e)
		{
			var item = sender as ListViewItem;
			if (item != null && item.IsSelected) {
				//Do your stuff
			}
		}

		private void RestartAsAdminButton_Click (object sender, RoutedEventArgs e)
		{
			ProcessHelper.RestartAsAdmin (skipAlertMessages);
		}

		internal void ShowConnectionOptions (bool value)
		{
			m_openGameDirectoryBtn.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
		}

		private void TitleBar_MouseLeftButtonDown (object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed) {
				this.DragMove ();
			}
		}

		private void CloseButton_Click (object sender, RoutedEventArgs e)
		{
			this.Close ();
		}

		#region Top Menu

		protected void OpenGlobalDir_Clicked (object sender, MouseButtonEventArgs e)
		{
			string directory = AppSettings.GetGlobalDirPath();

			if (!System.IO.Directory.Exists (directory)) {
				System.IO.Directory.CreateDirectory (directory);
			}

			DirectoryHelper.NavigateToDirectory (directory);
		}

		protected void OpenGameDir_Clicked (object sender, MouseButtonEventArgs e)
		{
			UnrealPanel.OpenGameDir ();
		}

		private void ExportConfig_Clicked (object sender, MouseButtonEventArgs e)
		{
			UnrealPanel.ExportConfig ();
		}

		private void ImportConfig_Clicked (object sender, MouseButtonEventArgs e)
		{
			UnrealPanel.ImportConfig ();
		}

		#endregion

		internal void Check_VirtualDesktop ()
		{
			if (m_virtualDesktopWarned || m_ignoreFutureVDWarnings) {
				return;
			}

			//TODO: QUITAR
			return;

			if (ProcessHelper.IsExecutableRunning ("Surreal Streamer")) {
				m_virtualDesktopWarned = true;
				var dialog = new VDWarnDialog();
				dialog.Topmost = true;
				dialog.ShowDialog ();

				if (dialog.DialogResultOK) {
					if (dialog.HideFutureWarnings) {
						m_ignoreFutureVDWarnings = true;
					}
				}
			}
		}

		private void UpdateStreamingStatus ()
		{
			// Updater
			var surrealStreamer = ProcessHelper.GetExecutableRunning("Surreal Streamer");
			var m_virtualDesktopWarned = surrealStreamer != null;
			ss_connectedLabel.Visibility = m_virtualDesktopWarned ? Visibility.Visible : Visibility.Collapsed;
			ss_circle.Fill = m_virtualDesktopWarned ? Brushes.Green : Brushes.Red;
			Hyperlink vddLink = (Hyperlink)ss_openSurreal.Inlines.FirstInline;
			vddLink.Inlines.Clear ();
			vddLink.Inlines.Add (m_virtualDesktopWarned ? "Stop Service" : "Start Service");

			if (surrealStreamer != null && surrealWindow == null) {
				surrealWindow = new AccessibilitySurrealStreamingWindow (surrealStreamer);
			}

			surrealWindow?.RefreshFromProcess ();

			bool m_steamVR = false;

			if (surrealWindow != null) {
				m_steamVR = surrealWindow.IsSteamVrConnected ();
			}

			sv_connectedLabel.Visibility = m_steamVR ? Visibility.Visible : Visibility.Collapsed;
			sv_circle.Fill = m_steamVR ? Brushes.Green : Brushes.Red;
			Hyperlink steamVRLink = (Hyperlink)sv_openSteam.Inlines.FirstInline;
			steamVRLink.Inlines.Clear ();
			steamVRLink.Inlines.Add (m_steamVR ? "Stop Service" : "Start Service");
		}


		bool isStarting = false;

		private void MainWindow_Update ()
		{
			// Actualizar todos los paneles
			UnrealPanel.MainUpdate ();

			// Update Streaming Status
			UpdateStreamingStatus ();

			//if (!isStarting)
			//	processManager.CheckZombieProcessAndKill ();

			// Check Virtual Desktop
			if (m_virtualDesktopChecked == false) {
				m_virtualDesktopChecked = true;
				Check_VirtualDesktop ();
			}
		}

		private void OpenSurrealStreamer (object sender, RequestNavigateEventArgs e)
		{
			if (ProcessHelper.IsExecutableRunning ("Surreal Streamer")) {
				surrealStreamerRunner.Stop ();
				surrealWindow?.Dispose ();
				surrealWindow = null;

			} else {
				surrealStreamerRunner.Start ();
			}
		}

		private void OpenSteamVR (object sender, RequestNavigateEventArgs e)
		{
			if (surrealWindow != null) {
				surrealWindow.Connect ();
			}
			e.Handled = true;
		}

		private void MainWindow_Closing (object sender, System.ComponentModel.CancelEventArgs e)
		{
			//m_mainWindowSettings.OpenXRRadio = m_openxrRadio.IsChecked == true;
			//m_mainWindowSettings.OpenVRRadio = m_openvrRadio.IsChecked == true;
			MainSettings.IgnoreFutureVDWarnings = m_ignoreFutureVDWarnings;

			UnrealPanel.OnMainClosing ();

			MainSettings.Save ();
		}

		private void Donate_Clicked (object sender, RoutedEventArgs e)
		{
			Process.Start (new ProcessStartInfo ("https://patreon.com/praydog") { UseShellExecute = true });
		}

		private void Documentation_Clicked (object sender, RoutedEventArgs e)
		{
			Process.Start (new ProcessStartInfo ("https://praydog.github.io/uevr-docs/") { UseShellExecute = true });
		}

		private void Discord_Clicked (object sender, RoutedEventArgs e)
		{
			Process.Start (new ProcessStartInfo ("http://flat2vr.com") { UseShellExecute = true });
		}

		private void GitHub_Clicked (object sender, RoutedEventArgs e)
		{
			Process.Start (new ProcessStartInfo ("https://github.com/praydog/UEVR") { UseShellExecute = true });
		}

		private void GridViewColumnHeader_Click (object sender, RoutedEventArgs e)
		{
			var list = (ListView)sender;
			GridViewColumnHeader colHeader = (GridViewColumnHeader)e.OriginalSource;
			string colName = colHeader.Content.ToString();

			CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(list.ItemsSource);
			view.SortDescriptions.Add (new SortDescription (colName, ListSortDirection.Ascending));

			view.Refresh ();
		}

		private void ResultListView_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count > 0 && e.AddedItems[0] is UEVRGameRowView r) {
				SelectGame (r);
			} else {
				SelectGame (null);
			}

		}

		private void RefreshGameData ()
		{
			// Visibility 
			if (selectedItem != null) {
				if (selectedItem.game.Platform == SteamSdk.SdkIdentifier) {
					oLaunch.IsEnabled = true;
				} else {
					oLaunch.IsEnabled = selectedItem != null && File.Exists (selectedItem.Executable);
				}

				if (selectedItem.IsUnrealGame && !selectedItem.game.IsVR) {
					BtnImportConfig.Visibility = BtnExportConfig.Visibility = Visibility.Visible;
					UnrealPanel.Visibility = Visibility.Visible;
				} else {
					BtnImportConfig.Visibility = BtnExportConfig.Visibility = Visibility.Collapsed;
					UnrealPanel.Visibility = Visibility.Collapsed;
				}
			} else {
				UnrealPanel.Visibility = Visibility.Collapsed;
			}
		}

		private void Window_SizeChanged (object sender, SizeChangedEventArgs e)
		{
			Window w = (Window)sender;
			GameListScrollView.MaxHeight = w.ActualHeight - 110;
			UnrealPanel.OnSizeChanged (w, e);
		}

		private void TextBox_TextChanged (object sender, TextChangedEventArgs e)
		{
			RefreshGameList ();
		}


		async Task LaunchGame (UEVRGameRowView selected)
		{
			if (selected == null)
				return;

			isStarting = true;
			//skipAlertMessages = true;
			

			if (processManager.IsRunning) {
				await processManager.StopAsync ();
				oLaunch.Content = RunningStates.Start.ToString ();
			} else {
				await processManager.StopAsync ();
				oLaunch.Content = RunningStates.Stop.ToString ();
				await processManager.StartAsync(selected.game);
			}
			isStarting = false;
		}

		private void oLaunch_Click (object sender, RoutedEventArgs e)
		{
			LaunchGame (selectedItem);
		}

		private void ResultListView_SizeChanged (object sender, SizeChangedEventArgs e)
		{
			if (ResultListView.View is GridView gridView) {
				double totalWidth = ResultListView.ActualWidth; // Ancho total del ListView
				double fixedColumnsWidth =  gridView.Columns[2].Width + gridView.Columns[3].Width + gridView.Columns[0].Width; // Ancho de las columnas fijas + margen (Scrollbar)

				// Calcular el ancho restante para la columna Name
				double newWidth = totalWidth - fixedColumnsWidth;

				gridView.Columns[1].Width = newWidth;
				//if (newWidth > 100) // Para evitar que sea demasiado pequeño
				//{
				//	gridView.Columns[1].Width = newWidth;
				//}
			}
		}

		private void Image_MouseDown (object sender, MouseButtonEventArgs e)
		{
			//Profile Manager
			var profileManager = new ProfileManagerDialog();
			profileManager.ShowDialog ();
		}

		private void MenuLaunch_Click (object sender, RoutedEventArgs e)
		{
			if (sender is MenuItem menuItem) {
				var selectedItem = GetFromMenuItem(menuItem);
				if (selectedItem != null) {
					SelectGame (selectedItem);
					LaunchGame (selectedItem);
				}
			}
		}

		private void MenuShorcut_Click (object sender, RoutedEventArgs e)
		{

		}

		UEVRGameRowView GetFromMenuItem (MenuItem menuItem)
		{
			// Obtener el ContextMenu asociado al MenuItem
			ContextMenu contextMenu = menuItem.Parent as ContextMenu;

			if (contextMenu != null) {
				// Obtener el ListViewItem al que pertenece el ContextMenu
				ListViewItem listViewItem = contextMenu.PlacementTarget as ListViewItem;

				if (listViewItem != null) {
					// Obtener el objeto de datos asociado (ejemplo: MyItem)
					var selectedItem = listViewItem.DataContext as UEVRGameRowView;
					return selectedItem;

				}
			}
			return null;
		}

		void SelectGame (UEVRGameRowView game)
		{
			if (game == selectedItem)
				return;
			// tenemos que seleccionar el post runner handler
			selectedItem = game;

			var executionHandler = UnrealPanel.GetPostExecutionHandler (selectedItem);
			if (executionHandler != null) {
				processManager.PostExecutionHandler = executionHandler;
			} else {
				// configurar
				processManager.PostExecutionHandler = null;
			}
			UnrealPanel.SelectionChanged (selectedItem);

			RefreshGameData ();
		}

		private void MenuOpenDirectory_Click (object sender, RoutedEventArgs e)
		{
			if (sender is MenuItem menuItem) {
				var selectedItem = GetFromMenuItem(menuItem);
				if (selectedItem != null) {
					SelectGame (selectedItem);
					var directoryPath = selectedItem.GetGameDirectoryPath();
					DirectoryHelper.NavigateToDirectory (directoryPath);
				}
			}

		}

		MenuItem GetMenuItem (ContextMenu context, string name)
		{
			foreach (var item in context.Items) {
				if (item is MenuItem menuitem && menuitem.Name == name) {
					return menuitem;
				}

			}
			return null;
		}

		private void OnListViewItem_ContextMenuOpening (object sender, ContextMenuEventArgs e)
		{
			if (sender is ListViewItem listViewItem) {
				var selectedItem = listViewItem.DataContext as UEVRGameRowView;
				if (selectedItem != null) {
					// Obtener el ContextMenu del ListViewItem
					ContextMenu contextMenu = listViewItem.ContextMenu;

					if (contextMenu != null) {
						// Obtener los MenuItems
						MenuItem menuLaunch = GetMenuItem(contextMenu, "MenuLaunch");
						menuLaunch.Click -= MenuLaunch_Click;
						menuLaunch.Click += MenuLaunch_Click;
						MenuItem menuShorcut = GetMenuItem(contextMenu, "MenuShorcut");
						menuShorcut.Click -= MenuShorcut_Click;
						menuShorcut.Click += MenuShorcut_Click;
						MenuItem openDirectory = GetMenuItem(contextMenu,"MenuOpenDirectory");
						openDirectory.Click -= MenuOpenDirectory_Click;
						openDirectory.Click += MenuOpenDirectory_Click;

						// Condiciones para ocultar o mostrar opciones
						//abrirItem.Visibility = selectedItem.Name.Contains("MenuLaunch") ? Visibility.Visible : Visibility.Collapsed;
						//eliminarItem.Visibility = selectedItem.Name.StartsWith("MenuShorcut") ? Visibility.Collapsed : Visibility.Visible;
						//detallesItem.Visibility = Visibility.Visible; // Siempre visible
					}
				}
			}
		}


		//private void OnListViewItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		//{
		//	if (sender is ListViewItem mi)
		//				{
		//						switch (mi.Name) {
		//							case "MenuLaunch":
		//								//mi.Click += MenuLaunch_Click;
		//							break;
		//							case "MenuShorcut":
		//								//mi.Click += MenuShorcut_Click;
		//								break;
		//							case "MenuOpenDirectory":
		//								//mi.Click += MenuOpenDirectory_Click;
		//								break;
		//						}
		//				}

		//	// var selectedTrack = (sender as ListViewItem).DataContext as TrackItem; // Cast to your data context type if required.
		//}
	}

	static class menuItemExgtensins
	{
		public static string GetGameDirectoryPath (this UEVRGameRowView game)
		{
			var platform = AppEnvironment.GetSdkPlatform(game.game.Wrapper);
			return platform.GetGameDirectoryPath (game.game.Wrapper);
		}
	}

	// Implementación de otra acción: Lanzar otro proceso
		//public class LaunchProcessAction : ProcessPostExecutionHandler
		//{
		//	public string NewProcessPath { get; set; }

		//	public override Task<bool> ExecuteAsync (Process process)
		//	{
		//	if (NewProcessPath == null) {
		//		throw new InvalidOperationException ("NewProcessPath no puede ser nulo.");
		//	}
		//		Console.WriteLine ($"[LaunchProcess] Intentando lanzar {NewProcessPath}...");

		//		try {
		//			Process.Start (NewProcessPath);
		//			Console.WriteLine ("[LaunchProcess] Proceso lanzado exitosamente.");
		//			return Task.FromResult(true);
		//		} catch (Exception ex) {
		//			Console.WriteLine ($"[LaunchProcess] Error al lanzar proceso: {ex.Message}");
		//			return Task.FromResult(false);
		//		}
		//	}
		//}
}