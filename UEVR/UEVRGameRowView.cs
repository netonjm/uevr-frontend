using System;
using System.Windows.Media;

namespace UEVR
{
	// Clase de ejemplo para representar los datos
	internal class UEVRGameRowView
	{
		internal GameInfo game;

		public UEVRGameRowView ()
		{

		}

		public UEVRGameRowView (GameInfo hify)
		{
			SetGame (hify);
		}

		public void SetGame (GameInfo game)
		{

			//var executable = new Executable()
			//         {
			//             //Architecture = "x64",
			//             OperatingSystem = "Windows",
			//             //Path = "F:\\XboxGames\\Hi-Fi RUSH\\Content\\Hibiki\\Binaries\\WinGDK\\Hi-Fi-RUSH.exe",
			//             Engine = new Engine()
			//             {
			//                 Brand = "Unreal",
			//                 Version = new Version(4, 3, 21)
			//             }
			//         };

			//         var gameManifest = new SteamGameManifest()
			//         {
			//             AppId = (int)steamGame.manifest.AppId,
			//             Description = steamGame.Title,
			//             Executable = executable
			//         };

			//       AppEnvironment.Games
			//                     Name = steamGame.Title,
			//                     ProviderId = "Steam",
			//                     GameInfo = game,
			//GameManifest = gameManifest,
			//                     Id = Guid.NewGuid().ToString(),
			//
			//
			//               ThumbnailUrl = GetImageIconFilePath(steamGame)
			this.game = game;

			this.Name = this.game.Title;


			try {

				var platform = AppEnvironment.GetSdkPlatform(game.Wrapper);
				this.Provider = platform.Name;
				// this.Description = "This is a test";
				// this.Engine = this.game.Executable.Engine.Brand;

				this.Image = AppEnvironment.GetEngineImage (game); ;
			} catch (Exception ex) {

				Console.WriteLine ("");
			}


			// this.Version = this.game.Executable.Engine.VersionString?.ToString() ?? string.Empty;
			//this.Image = ImageHelper.GetImage(this.game.ThumbnailUrl ?? "C:\\Program Files (x86)\\Steam\\appcache\\librarycache\\80_library_hero_blur");
		}

		string exec;
		public string Executable
		{
			get
			{
				if (exec == null) {
					try {
						exec = AppEnvironment.GetGameExecutablePath (this.game.Wrapper);

					} catch (Exception ex) {
						Console.WriteLine (ex.Message);
					}

				}
				return exec;
			}
		}

		public string Name { get; set; }
		public string Description { get; set; }
		public string Tooltip { get; set; }
		public string Provider { get; set; }
		public string Engine => game?.Engine ?? string.Empty;
		public string Version { get; set; }
		public ImageSource Image { get; set; }
	}

}