using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UGMVR.Sdks.Oculus;
using UGMVR.Sdks.Steam;
using UGMVR.SGE.Providers;
using UGMVR.SGE;
using UGMVR.UnrealVR;
using UGMVR.VirtualDesktop;
using System.IO;
using UGMVR.Sdks;
using UGMVR;
using System.Windows.Media.Imaging;
using UGMVR.Sdks.Xbox;
using System.Net;

namespace UEVR
{
	internal static class AppEnvironment
	{
		public static event EventHandler FinishedLoading;

		public static List<GameInfo> Games = new List<GameInfo>();
		const string Eee = @"C:\Users\Simulador\Downloads\UEVR - Universal Unreal Engine VR Injector from praydog - Testing Log.csv";
		const string ConfigurationFilePath = "C:\\Users\\Simulador\\source\\repos\\SteamParser\\config.json";

		static async Task LoadConfiguration ()
		{
			var config = await GamesConfiguration.FromFilePathAsync(ConfigurationFilePath);
			Games.Clear ();
			Games.AddRange (config.Games);
		}

		private static OculusSdkPlatform oculusSdkPlatform;
		private static XboxSdkPlatform xboxSdkPlatform;
		private static SteamSdkPlatform steamSdkPlatform;
		private static ISdkPlatform[] SdksEnvironments;
		private static SearchGameEngine searchEngine;
		private static VDGameSettings VDGameSettings;

		private static UEVRCompatibilityManifest CompatibilityManifest;

		internal static async Task Init ()
		{
			Logger.Info ("Init environment...");

			try {
				configurationFileExists = File.Exists (ConfigurationFilePath);
				await LoadConfiguration ();
			} catch (Exception ex) {
				configurationFileExists = false;
			}

			Logger.Info ("Loading Oculus sdk...");
			oculusSdkPlatform = new OculusSdkPlatform ();
			oculusSdkPlatform.ManifestAdded += OnManifestAdded;

			Logger.Info ("Loading Steam sdk...");
			steamSdkPlatform = new SteamSdkPlatform ();
			steamSdkPlatform.ManifestAdded += OnManifestAdded;

			

			xboxSdkPlatform = new XboxSdkPlatform ();
			xboxSdkPlatform.ManifestAdded += OnManifestAdded;

			SdksEnvironments = [
				//oculusSdkPlatform, 
                steamSdkPlatform,
                //xboxSdkPlatform
                ];

			searchEngine = new SearchGameEngine ();
			searchEngine.Providers.Add (new UnrealSearchGameEngineProvider ());
			searchEngine.Providers.Add (new UnitySearchGameEngineProvider ());
			searchEngine.Providers.Add (new RESearchGameEngineProvider ());


			VDGameSettings = await VDGameSettings.FromDefaultPathAsync ();
			CompatibilityManifest = await UEVRCompatibilityManifest.FromUEVRUrlAsync ();

			foreach (var sdk in SdksEnvironments) {
				// initialize sdk
				await sdk.InitializeAsync ();

				//var gamess = await steamSdkPlatform.Api.GetSteamAppGameListAsync();

				// sdk setup
				await sdk.SetupAsync (CompatibilityManifest);
			}

			FinishedLoading?.Invoke (null, EventArgs.Empty);
		}

		private static GameInfo OnPluginCreateGameInfo ()
		{
			return new GameInfo ();
		}

		static bool configurationFileExists = false;

		private static async void OnManifestAdded (object sender, IGameManifestWrapper e)
		{
			try {
				//si existe configuration skipeamos
				if (configurationFileExists)
					return;

				var platform = GetSdkPlatform(e);
				Logger.Debug ($"Adding {platform.Name} Game {e.Title}...");

				var newGame = OnPluginCreateGameInfo();
				newGame.Id = e.Id;
				newGame.AppId = e.AppId;
				newGame.Properties.Add (nameof (GameInfo.AppId), newGame.AppId);

				newGame.Title = e.Title;
				newGame.Wrapper = e;
				newGame.Platform = platform.Name;

				var directoryPath = platform.GetGameDirectoryPath(e);
				var searchResult = await searchEngine.SearchAsync(directoryPath);

				if (searchResult != null) {
					newGame.Engine = searchResult.Engine;
					newGame.EngineVersion = searchResult.Version;
				}
				
				if (newGame.Engine == Engines.Unreal) {
					//TODO: Enable this
					//try {
					//	var executablePath = platform.GetGameExecutablePath(e);
					//	var metadata = MetadataHelper.GetMetadataFromExecutableFilePath(executablePath);
					//	foreach (var data in metadata) {
					//		newGame.Properties.Add (data.Key, data.Value);
					//	}
					//} catch (Exception ex) {
					//	Console.WriteLine (ex.ToString ());
					//}
				}

				OnPluginManifestAdded (newGame);

				if (platform.Name == SteamSdk.SdkIdentifier) {
					//RefreshGameInfoFromSteamId(newGame, newGame.AppId);
				}

				Games.Add (newGame);
			} catch (Exception ex) {
				Console.WriteLine ("Error");
			}

		}

		static void RefreshGameInfoFromSteamId (GameInfo newGame, string steamAppId)
		{
			try {
				var details = steamSdkPlatform.Api.GetSteamAppGameDetails(steamAppId)
			  .FirstOrDefault().Value?.Data;
				if (details != null) {
					newGame.DetailedDescription = details.DetailedDescription;
					newGame.ShortDescription = details.ShortDescription;
					newGame.Properties.Add (nameof (GameInfo.ShortDescription), newGame.ShortDescription);

					newGame.Developer = details.Developers.FirstOrDefault ();
					newGame.Properties.Add (nameof (GameInfo.Developer), newGame.Developer);

					newGame.Publisher = details.Publishers.FirstOrDefault ();
					newGame.Properties.Add (nameof (GameInfo.Publisher), newGame.Publisher);

					newGame.Screenshots = [];
					foreach (var screen in details.Screenshots) {
						newGame.Screenshots.Add (screen.PathThumbnail);
					}

					newGame.Genres = [];

					if (details.Genres != null) {
						foreach (var genre in details.Genres) {
							newGame.Genres.Add (genre.Id, genre.Description);
						}
						newGame.Properties.Add (nameof (GameInfo.Genres), string.Join (',', newGame.Genres.Values));
					}

					newGame.Categories = [];
					if (details.Categories != null) {
						foreach (var category in details.Categories) {
							newGame.Categories.Add (category.Id, category.Description);
						}
						newGame.Properties.Add (nameof (GameInfo.Categories), string.Join (',', newGame.Categories.Values));
					}

					newGame.SupportedLanguages = details.SupportedLanguages;

					newGame.Recomendations = details.Recommendations?.Total ?? 0;
					newGame.Properties.Add (nameof (GameInfo.Recomendations), newGame.Recomendations.ToString ());

					newGame.Website = details.Website;
					newGame.Properties.Add (nameof (GameInfo.Website), newGame.Website);

					newGame.IsFree = details.IsFree;
					newGame.Properties.Add (nameof (GameInfo.IsFree), newGame.IsFree.ToString ());

					newGame.Type = details.Type;
				}
			} catch (Exception ex) {
				Console.WriteLine (ex.ToString () + $" {steamAppId}");
			}
		}


		public static string GetGameIdentifier (IGameManifestWrapper game)
		{
			if (game is SteamGameManifestWrapper steamGame) {
				return $"steam_{steamGame.AppId}";
			}
			throw new NotImplementedException ("");
		}

		private static string GetEGlobalImageDir ()
		{
			string directory = Path.Combine(GetEGlobalCacheDir(), "images");

			if (!Directory.Exists (directory))
				Directory.CreateDirectory (directory);
			return directory;
		}

		private static string GetEGlobalCacheDir ()
		{
			string directory = Path.Combine(GetEGlobalDir(), "cache");

			if (!Directory.Exists (directory))
				Directory.CreateDirectory (directory);
			return directory;
		}

		private static string GetEGlobalDir ()
		{
			string directory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			directory += "\\UnrealVR";

			if (!Directory.Exists (directory))
				Directory.CreateDirectory (directory);

			return directory;
		}

		[Obsolete ("Use GetEGlobalGameDir")]
		private static string GetGlobalDir ()
		{
			string directory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

			directory += "\\UnrealVRMod";

			if (!System.IO.Directory.Exists (directory)) {
				System.IO.Directory.CreateDirectory (directory);
			}

			return directory;
		}

		[Obsolete ("Use GetEGlobalGameDir")]
		public static string GetGlobalGameDir (string gameName)
		{
			string directory = GetGlobalDir() + "\\" + gameName;

			if (!System.IO.Directory.Exists (directory)) {
				System.IO.Directory.CreateDirectory (directory);
			}

			return directory;
		}

		public static string GetImageIconFilePath (IGameManifestWrapper game)
		{
			if (game is SteamGameManifestWrapper steamGame) {
				var expectedPath = Path.Combine(GetEGlobalImageDir(), $"{GetGameIdentifier(game)}_header.jpg");
				if (!File.Exists (expectedPath)) {
					using (WebClient webClient = new WebClient ()) {
						try {
							webClient.DownloadFile ($"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.AppId}/capsule_231x87.jpg", expectedPath);
						} catch (Exception ex) {
							Console.WriteLine ($"Error al descargar la imagen: {ex.Message}");
						}
					}
				}
				return expectedPath;
			}
			throw new NotImplementedException ("");
		}

		public static string GetGameUrl (IGameManifestWrapper manifest)
		{
			if (manifest is SteamGameManifestWrapper wrapper) {
				return steamSdkPlatform.GetGameUrl (wrapper);
			}
			throw new Exception ("type not supported");
		}

		public static string GetGameExecutablePath (IGameManifestWrapper manifest)
		{
			return GetSdkPlatform(manifest).GetGameExecutablePath(manifest); 
		}

		public static string GetGameDirectoryPath (IGameManifestWrapper manifest)
		{
			return GetSdkPlatform(manifest).GetGameDirectoryPath(manifest);
		}

		public static void LaunchGame (IGameManifestWrapper manifest)
		{
			GetSdkPlatform(manifest).LaunchGame(manifest);
			throw new Exception ("type not supported");
		}


		private static UEVRGameCompatibility GetGameCompatibility (GameInfo gameInfo)
		{
			if (gameInfo.Wrapper is SteamGameManifestWrapper steamGameManifestWrapper) {
				var compatibilityGame = CompatibilityManifest.Games.FirstOrDefault(s => s.IsSteamGame
	  && s.SteamId == steamGameManifestWrapper.manifest.AppId.ToString());
				return compatibilityGame;
			}
			return null;
		}

		public static ISdkPlatform GetSdkPlatform (IGameManifestWrapper gameManifest)
		{
			if (gameManifest is OculusGameManifestWrapper)
				return oculusSdkPlatform;
			if (gameManifest is SteamGameManifestWrapper)
				return steamSdkPlatform;
			if (gameManifest is XboxManifestWrapper)
				return xboxSdkPlatform;

			throw new NotImplementedException (gameManifest.GetType ().FullName);
		}

		public static ISdkPlatform GetSdkPlatform (string platformName)
		{
			if (platformName == oculusSdkPlatform.Name)
				return oculusSdkPlatform;
			if (platformName == steamSdkPlatform.Name)
				return steamSdkPlatform;
			if (platformName == xboxSdkPlatform.Name)
				return xboxSdkPlatform;

			throw new NotImplementedException (platformName);
		}

		readonly static Dictionary<GameInfo, UEVRGameCompatibility> compatibility = [];


		internal static BitmapImage? GetEngineImage (GameInfo model)
		{
			var platform = GetSdkPlatform(model.Wrapper);
			if (platform != null) {
				var imagePath = platform.GetGameLogoResourcePath(model.Wrapper);
				if (File.Exists(imagePath))
					return ImageSourceHelper.LoadBitmapImage(imagePath);
			}

//			if (model.Engine == "Unity")
//				return new BitmapImage (
//new Uri ("pack://application:,,,/UEVR;component/Images/Unity.png"));


//			if (model.Engine == "Unreal") {
//				if (compatibility.TryGetValue (model, out var com)) {
//					return new BitmapImage (
//new Uri ("pack://application:,,,/UEVR;component/Images/x"));

//				} else {
//					return new BitmapImage (
//new Uri ("pack://application:,,,/UEVR;component/Images/Unreal.png"));

//				}
			//}
	//	//
	//		if (model.Engine == "REEngine")
	//			return new BitmapImage (
 //new Uri ("pack://application:,,,/UEVR;component/Images/REEngine.png"));

			return null;
		}


		static void OnPluginManifestAdded (GameInfo gameInfo)
		{
			UEVRGameCompatibility compatibilityGame = GetGameCompatibility(gameInfo);
			if (compatibilityGame != null) {
				compatibility.Add (gameInfo, compatibilityGame);

				Logger.Debug ($"Compatibility game found.");

				//gameInfo.Properties.Add(nameof(UEVRGameCompatibility.SteamId), compatibilityGame.SteamId);
				gameInfo.Properties.Add (nameof (UEVRGameCompatibility.Playability), compatibilityGame.Playability.ToString ());

				gameInfo.Properties.Add (nameof (UEVRGameCompatibility.UEmajor), compatibilityGame.UEmajor.ToString ());
				gameInfo.Properties.Add (nameof (UEVRGameCompatibility.UEversion), compatibilityGame.UEversion.ToString ());

				gameInfo.Properties.Add (nameof (UEVRGameCompatibility.View1st), compatibilityGame.View1st.ToString ());
				gameInfo.Properties.Add (nameof (UEVRGameCompatibility.View3rd), compatibilityGame.View3rd.ToString ());
				gameInfo.Properties.Add (nameof (UEVRGameCompatibility.num3DOFmotioncontrols), compatibilityGame.num3DOFmotioncontrols.ToString ());
				gameInfo.Properties.Add (nameof (UEVRGameCompatibility.num6DOFmotioncontrolsUObjectHook), compatibilityGame.num6DOFmotioncontrolsUObjectHook.ToString ());
				gameInfo.Properties.Add (nameof (UEVRGameCompatibility.AntiCheat), compatibilityGame.AntiCheat.ToString ());
				gameInfo.Properties.Add (nameof (UEVRGameCompatibility.DateTested), compatibilityGame.DateTested.ToString ("d"));
				gameInfo.Properties.Add (nameof (UEVRGameCompatibility.ReleaseDate), compatibilityGame.ReleaseDate.ToString ("d"));
			}
		}
	}
}
