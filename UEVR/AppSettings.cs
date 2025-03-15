using System;
using System.IO;

namespace UEVR
{
	class AppSettings
	{
		public static string GetExportedConfigs ()
		{
			return Path.Combine(GetGlobalDirPath(), ".config");
		}

		public static string GetCmdDirPath ()
		{
			return Path.Combine(GetGlobalDirPath(), ".cmd");
		}

		public static string GetGlobalDirPath ()
		{
			string directory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

			directory += "\\AlvrGameManager";
			return directory;
		}
	}
}