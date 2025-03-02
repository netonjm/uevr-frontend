using System.Diagnostics;

namespace UEVR
{
	public interface IProcessRunner
	{
		public void Start ();

		public void Stop ();
	}

	class SurrealStreamerProcess : IProcessRunner
	{
		public string ExecutablePath { get; set; } = "C:\\Users\\Simulador\\Downloads\\surreal_streamer_windows_0.10.2\\Surreal Streamer.exe";

		public void Start ()
		{
			var processStart = new ProcessStartInfo(ExecutablePath);
			processStart.WorkingDirectory = System.IO.Path.GetDirectoryName (ExecutablePath);
			Process.Start (processStart);
		}

		public void Stop ()
		{
			var process = Process.GetProcessesByName("Surreal Streamer");
			if (process.Length > 0) {
				process[0].Kill ();
			}
		}
	}
}