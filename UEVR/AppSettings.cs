using System;

namespace UEVR
{
	class AppSettings
		{
			public static string GetGlobalDirPath ()
			{
				string directory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

				directory += "\\UnrealVRMod";
				return directory;
			}
		}
}