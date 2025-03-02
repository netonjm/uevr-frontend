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

namespace UEVR
{
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
		private	List<UEVRGameRowView> FilteredGameRows = new();
		
		internal static MainWindowSettings MainSettings = new MainWindowSettings();
		
		private UEVRGameRowView selectedItem;
		private SurrealStreamerProcess surrealStreamerRunner = new SurrealStreamerProcess ();
		private AccessibilitySurrealStreamingWindow surrealWindow;
		
		public ExtendedMainWindow ()
		{
			InitializeComponent ();
			AppEnvironment.FinishedLoading += AppEnvironment_FinishedLoading;
			UnrealPanel.OnMainInit();
		}

		private void AppEnvironment_FinishedLoading (object? sender, EventArgs e)
		{
			// Custom Games
			AddSomeRandomGames ();
			AddGames ();
			RefreshGameList ();
		}

		private void MainWindow_Loaded (object sender, RoutedEventArgs e)
		{
			if (!WindowsIdentityHelper.IsAdministrator ()) {
				m_nNotificationsGroupBox.Visibility = Visibility.Visible;
				m_restartAsAdminButton.Visibility = Visibility.Visible;
				m_adminExplanation.Visibility = Visibility.Visible;
			}

			UnrealPanel.OnMainLoaded();
			
			//m_openvrRadio.IsChecked = m_mainWindowSettings.OpenVRRadio;
			//m_openxrRadio.IsChecked = m_mainWindowSettings.OpenXRRadio;
			
			m_ignoreFutureVDWarnings = MainSettings.IgnoreFutureVDWarnings;

			

			m_updateTimer.Tick += (sender, e) => Dispatcher.Invoke (MainWindow_Update);
			m_updateTimer.Start ();
		}

		private void AddGames ()
		{
			var fileSystem = FileSystem.Shared;
			var registry = WindowsRegistry.Shared;
			//iterate between all the games
			foreach (var game in AppEnvironment.Games) {
				//if (!IsUnrealGameFolder(AppEnvironment.GetGameDirectoryPath(game.Wrapper))) 
				//{
				//    continue;
				//}

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

			RefreshGameData();
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
			ProcessHelper.RestartAsAdmin(skipAlertMessages);
		}
		
		internal void Hide_ConnectionOptions ()
		{
			m_openGameDirectoryBtn.Visibility = Visibility.Collapsed;
		}

		internal void Show_ConnectionOptions ()
		{
			m_openGameDirectoryBtn.Visibility = Visibility.Visible;
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
			UnrealPanel.OpenGameDir();
		}

		private void ExportConfig_Clicked (object sender, MouseButtonEventArgs e)
		{
			UnrealPanel.ExportConfig();
		}

		private void ImportConfig_Clicked (object sender, MouseButtonEventArgs e)
		{
			UnrealPanel.ImportConfig();
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

		private void MainWindow_Update ()
		{
			// Actualizar todos los paneles
			UnrealPanel.MainUpdate();

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
				m_steamVR = surrealWindow.IsSteamVrConnected();
			}

			sv_connectedLabel.Visibility = m_steamVR ? Visibility.Visible : Visibility.Collapsed;
				sv_circle.Fill = m_steamVR ? Brushes.Green : Brushes.Red;
				Hyperlink steamVRLink = (Hyperlink)sv_openSteam.Inlines.FirstInline;
				steamVRLink.Inlines.Clear ();
				steamVRLink.Inlines.Add (m_steamVR ? "Stop Service" : "Start Service");

			if (m_virtualDesktopChecked == false) {
				m_virtualDesktopChecked = true;
				Check_VirtualDesktop ();
			}
		}

		private void OpenSurrealStreamer (object sender, RequestNavigateEventArgs e)
		{
			if (ProcessHelper.IsExecutableRunning ("Surreal Streamer")) {
				surrealStreamerRunner.Stop();
				surrealWindow?.Dispose();
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

			UnrealPanel.OnMainClosing();

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
				selectedItem = r;
			} else {
				selectedItem = null;
			}

			UnrealPanel.SelectionChanged(selectedItem);
			RefreshGameData ();
		}

		private void RefreshGameData ()
		{
			if (selectedItem != null) {
				UnrealPanel.Visibility = selectedItem.Engine == "Unreal" ? Visibility.Visible : Visibility.Collapsed;
			} else {
				UnrealPanel.Visibility = Visibility.Collapsed;
			}
			oLaunch.IsEnabled = selectedItem != null && File.Exists (selectedItem.Executable);
		}

		private void Window_SizeChanged (object sender, SizeChangedEventArgs e)
		{
			Window w = (Window)sender;
			GameListScrollView.MaxHeight = w.ActualHeight - 110;
			UnrealPanel.OnSizeChanged(w, e);
		}

		private void TextBox_TextChanged (object sender, TextChangedEventArgs e)
		{
			RefreshGameList ();
		}

		private void oLaunch_Click (object sender, RoutedEventArgs e)
		{
			var selected = selectedItem;
			if (selected == null)
				return;

			skipAlertMessages = true;

			//var processCandidate = WaitForProcess(arguments.ProcessName);
			//processCandidate?.CloseMainWindow();
			ProcessHelper.ExecuteSelectedGame(selected.Executable, "");

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
	}
}