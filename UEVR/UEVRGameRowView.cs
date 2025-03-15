using System;
using System.Windows.Media;
using UGMVR;

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

		public bool IsUnrealGame => game.IsUnrealGame;

		public void SetGame (GameInfo game)
		{
			this.game = game;

			try 
			{
				var platform = AppEnvironment.GetSdkPlatform(game.Wrapper);
				this.Provider = platform.Name.ToUpper();
				// this.Engine = this.game.Executable.Engine.Brand;

				this.Image = AppEnvironment.GetEngineImage (game); ;
			} catch (Exception ex) {

				Console.WriteLine ("");
			}

			IsLinked = true;

			// this.Version = this.game.Executable.Engine.VersionString?.ToString() ?? string.Empty;
			//this.Image = ImageHelper.GetImage(this.game.ThumbnailUrl ?? "C:\\Program Files (x86)\\Steam\\appcache\\librarycache\\80_library_hero_blur");
		}

		public bool IsLinked { get; set; }

		//Lazy loading
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

		public string Name => game.Title;
		public string Description => game.DetailedDescription;
		public string Tooltip => game.Title;
			//               ThumbnailUrl = GetImageIconFilePath(steamGame)
		public string Provider {get; private set; }

		public string Developer => game.Developer;
		public string Engine => game?.Engine ?? string.Empty;
		public string EngineVersion => game?.EngineVersion ?? string.Empty;
		public string Version { get; set; }
		public ImageSource Image { get; set; }
	}

}