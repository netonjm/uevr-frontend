using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using GameFinder.Common;
using GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using NLog.Targets;
using UGMVR.Sdks.Steam;

namespace UEVR
{
	// Clase abstracta para definir acciones sobre un proceso
	public abstract class ProcessPostExecutionHandler
	{
		public abstract Task<bool> ExecuteAsync (GameInfo process);
	}

	// Clase que maneja la ejecución de un proceso y ejecuta la acción correspondiente
	public class ProcessManager
	{
		public bool IsRunning => currentProcess != null;
		public event EventHandler Finished;
		private  string _exePath;
		private  string _processName;
		public ProcessPostExecutionHandler? PostExecutionHandler { get; set; }

		Process currentProcess;

		public static bool Kill (Process target)
		{
			if (target == null)
				return true;

			//if (target == null || target.HasExited) {
			//	return true;
			//}

			try {

				var processName = target.ProcessName;
				SharedMemory.SendCommand (SharedMemory.Command.Quit);


				//target = Process.GetProcessesByName (processName).FirstOrDefault();
				target.Kill (true);

				
				target.WaitForInputIdle (100);

				if (target.WaitForExit (2000)) {
					return true;
				}
			} catch (Exception) {
				return false;
			}
			return true;
		}

		public static bool Kill (int pid)
		{
			var target = Process.GetProcessById(pid);
			return Kill (target);
		}

		/// <summary>
		/// Obtiene un proceso candidato para la inyección dentro de los procesos hijos del ejecutable original.
		/// </summary>
		private async Task<Process> ObtenerProcesoCandidato (int parentPid)
		{
			for (int i = 0; i < 5; i++) {
				var childProcesses = Process.GetProcesses()
				.Where(p => ObtenerProcesoPadre(p.Id) == parentPid)
				.ToList();

				if (childProcesses.Any ()) {
					// Seleccionar un proceso candidato (puede ajustarse según necesidad)
					return childProcesses.FirstOrDefault ();
				}

				//await Task.Delay (1000); // Esperar antes de volver a comprobar
			}
			return null;
		}

		/// <summary>
		/// Obtiene el PID del proceso padre de un proceso dado.
		/// </summary>
		private int ObtenerProcesoPadre (int pid)
		{
			try {
				using (var process = Process.GetProcessById (pid)) {
					return process.ParentProcessId ();
				}
			} catch {
				return -1; // Si no se puede obtener, asumimos que es un proceso huérfano
			}
		}

		private async Task StartedAsync ()
		{
			// wait some time to get the child process
			//await Task.Delay (5000);

			//// si existe un proceso candidato, lo seleccionamos
			//var candidate = await ObtenerProcesoCandidato(currentProcess.Id);
			//if (candidate != null) {
			//	currentProcess = candidate;
			//}

			
			// comprobamos el proceso que está enfocadd si no es el mismo que se espera
			// es el proceso enfocado?


			//currentProcess.Exited -= CurrentProcess_Exited;
			//currentProcess.Exited += CurrentProcess_Exited;

			PostExecutionHandler?.ExecuteAsync(currentGame);
		}

		GameInfo currentGame;

		public async Task StopAsync()
		{
			if (currentProcess != null && !currentProcess.HasExited) {
				currentProcess.Kill(true);
			}

			if (currentGame != null) 
			{
				currentProcess = await currentGame.GetProcessCandidateAsync ();
				currentProcess?.Kill(true);
				currentGame = null;
			}
			currentProcess = null;
		}

		public async Task StartAsync(GameInfo game)
		{	
			currentGame = game;

			var platform = AppEnvironment.GetSdkPlatform(game.Wrapper);

			if (platform.Name == SteamSdk.SdkIdentifier) {
				Process.Start (new ProcessStartInfo ($"steam://run/{game.AppId}") { UseShellExecute = true });
				currentProcess = await game.GetProcessCandidateAsync();
			} else {
				var executablePath = platform.GetGameExecutablePath(game.Wrapper);
				Console.WriteLine ($"Buscando el proceso '{executablePath}'...");

				_exePath = executablePath;
				_processName = System.IO.Path.GetFileNameWithoutExtension (executablePath); // Obtener solo el nombre del proceso

				currentProcess = GetExistingProcess ();

				if (currentProcess != null && !currentProcess.HasExited) {
					//kill
					KillCurrentProcess ();
					currentProcess = null;
				}

				if (currentProcess == null) {
					ProcessStartInfo startInfo = new ProcessStartInfo
					{
						FileName = executablePath,
						Arguments = null,
						UseShellExecute = true, // Necesario para ejecutables con GUI
						CreateNoWindow = false
					};
					currentProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
					currentProcess.Start ();
				}
			}
			await StartedAsync ();
		}

		private void CurrentProcess_Exited (object? sender, EventArgs e)
		{
			currentProcess = null;

			if (!skipFireEvent)
				Finished?.Invoke (this, EventArgs.Empty);
		}

		private Process? GetExistingProcess ()
		{
			var processes = Process.GetProcessesByName(_processName);
			return processes.FirstOrDefault ();
		}

		private Process? StartNewProcess ()
		{
			try {
				var process = new Process
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = _exePath,
						UseShellExecute = false
					}
				};

				if (process.Start ()) {
					return process;
				}
			} catch (Exception ex) {
				Console.WriteLine ($"Error al iniciar el proceso: {ex.Message}");
			}
			return null;
		}

		bool skipFireEvent = false;
		public void KillCurrentProcess (bool fireEvent = true)
		{
			skipFireEvent = true;
			Kill (currentProcess);
			skipFireEvent = false;
		}

		internal void CheckZombieProcessAndKill ()
		{
			if (currentProcess != null && currentProcess.HasExited) {
				KillCurrentProcess ();
			}
		}
	}
}
