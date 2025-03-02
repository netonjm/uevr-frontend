using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UEVR
{
	internal class DirectoryHelper
	{
		public static void NavigateToDirectory (string directory)
		{
			string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
			string explorerPath = System.IO.Path.Combine(windowsDirectory, "explorer.exe");
			Process.Start (explorerPath, "\"" + directory + "\"");
		}
	}
}
