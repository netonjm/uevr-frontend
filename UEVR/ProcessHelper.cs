using System;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using UGMVR.UnrealVR;
using System.ComponentModel;
using System.Management;
using GameFinder.Common;
using UGMVR.Sdks.Steam;

namespace UEVR
{
	static class ProcessHelper
	{
		
    public  static Process GetProcessByPath(string executablePath)
    {
        return Process.GetProcesses()
                      .FirstOrDefault(proc =>
                      {
                          try
                          {
                              return proc.MainModule?.FileName.Equals(executablePath, StringComparison.OrdinalIgnoreCase) == true;
                          }
                          catch
                          {
                              return false; // Algunos procesos pueden lanzar excepción por permisos
                          }
                      });
    }

		
		public static Process GetCandidateProcess (string processName)
		{
			return Process.GetProcessesByName (processName).FirstOrDefault ();
		}

		public static async Task<bool> WaitForStabilityAsync (Process proc, int waitTimeMs)
		{
			int pid = proc.Id;
			await Task.Delay (waitTimeMs); // Espera asíncrona

			// Verificar si el proceso sigue en ejecución
			return Process.GetProcesses ().Any (p => p.Id == pid);
		}


	public static async Task<Process> FindStableProcessAsync(string executablePath, int retries = 3)
    {
        string processName = Path.GetFileNameWithoutExtension(executablePath);
        Console.WriteLine($"Buscando proceso con ruta '{executablePath}' o nombre '{processName}'...");

        for (int attempt = 1; attempt <= retries; attempt++)
        {
            Process candidate = GetProcessByPath(executablePath) ?? GetCandidateProcess(processName);

            if (candidate != null)
            {
                Console.WriteLine($"[{attempt}/{retries}] Proceso encontrado: {candidate.ProcessName} (PID: {candidate.Id})");

                if (await WaitForStabilityAsync(candidate, 5000))
                {
                    Console.WriteLine($"Proceso estable. Retornando proceso con PID {candidate.Id}");
                    return candidate; // Retorna el proceso estable
                }
                else
                {
                    Console.WriteLine($"El proceso se cerró. Intento {attempt} de {retries} fallido.");
                }
            }
            else
            {
                Console.WriteLine($"Intento {attempt} de {retries}: No se encontró un proceso con la ruta '{executablePath}' o nombre '{processName}'.");
            }

            if (attempt < retries)
            {
                await Task.Delay(1000); // Espera antes de reintentar
            }
        }

        Console.WriteLine("No se encontró un proceso estable después de varios intentos.");
        return null; // Retorna null si después de los intentos no encuentra un proceso estable
    }

		public static Task<Process> GetProcessCandidateAsync(this GameInfo game, int retries = 3)
		{
			var platform = AppEnvironment.GetSdkPlatform(game.Wrapper);
			var expectedExecutable = platform.GetGameExecutablePath(game.Wrapper);
				
			return FindStableProcessAsync(expectedExecutable, retries);
		}

		 public static int ParentProcessId(this Process process)
    {
        try
        {
            using (var query = new ManagementObjectSearcher($"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {process.Id}"))
            {
                var results = query.Get().Cast<ManagementObject>().FirstOrDefault();
                return results != null ? Convert.ToInt32(results["ParentProcessId"]) : -1;
            }
        }
        catch
        {
            return -1; // Error al obtener el proceso padre
        }
    }

		public static string GenerateProcessName (Process p)
		{
			return p.ProcessName + " (pid: " + p.Id + ")" + " (" + p.MainWindowTitle + ")";
		}

		public static Process ExecuteSelectedGame(string executablePath, string arguments)
		{
			Process p = new();
			p.StartInfo.FileName = executablePath;
			p.StartInfo.Arguments = arguments;
			p.StartInfo.WorkingDirectory = Path.GetDirectoryName (executablePath);
			p.Start ();
			return p;
		}

		public static bool IsExecutableRunning (string executableName)
		{
			return GetExecutableRunning(executableName) != null;
		}

		public static Process? GetExecutableRunning (string executableName)
		{
			return Process.GetProcessesByName (executableName).FirstOrDefault(p =>  p.ProcessName.Equals (executableName, StringComparison.OrdinalIgnoreCase));
		}


		/// <summary>
		/// Espera hasta que un proceso con el nombre dado aparezca en ejecución dentro del timeout.
		/// </summary>
		public static async Task<Process> WaitForProcessToStartAsync (string processName, int timeoutMilliseconds)
		{
			int elapsedTime = 0;
			int checkInterval = 500; // Intervalo de verificación en milisegundos

			while (elapsedTime < timeoutMilliseconds) {
				Process foundProcess = Process.GetProcessesByName(processName).FirstOrDefault();
				if (foundProcess != null) {
					Console.WriteLine ($"Proceso '{processName}' detectado después de {elapsedTime}ms.");
					return foundProcess;
				}

				await Task.Delay (checkInterval);
				elapsedTime += checkInterval;
			}

			Console.WriteLine ($"Tiempo de espera agotado ({timeoutMilliseconds}ms). No se detectó el proceso.");
			return null;
		}

		public static Process? WaitForProcess (string processName, bool m_connected, Func<Process, bool> IsInjectableProcessHandler, int maxRetry = 1, int retryTimeInMiliseconds = 1000)
		{
			for (int i = 0; i < maxRetry && !m_connected; i++) {
				if (i > 0) {
					Thread.Sleep (retryTimeInMiliseconds);
				}

				foreach (var p in Process.GetProcesses ()) {
					if (!IsInjectableProcessHandler.Invoke(p)) {
						continue;
					}
					if (processName == p.ProcessName) {
						return p;
					}
				}
			}
			return null;
		}

		public static Process WaitForProcess (string executablePath, string arguments, int timeoutMilliseconds = 3000)
		{
			string processName = Path.GetFileNameWithoutExtension(executablePath);
			Process existingProcess = Process.GetProcessesByName(processName).FirstOrDefault();

			if (existingProcess != null) {
				Console.WriteLine ($"Proceso '{processName}' ya está en ejecución.");
				return existingProcess;
			}

			Console.WriteLine ($"Proceso '{processName}' no encontrado. Iniciando...");

			Process newProcess = new Process
		{
				StartInfo = new ProcessStartInfo
			{
					FileName = executablePath,
					Arguments = arguments,
					UseShellExecute = false
				}
			};

			try {
				newProcess.Start ();
				return newProcess;
			} catch (Exception ex) {
				Console.WriteLine ($"Error al iniciar el proceso: {ex.Message}");
				return null;
			}


		}

		public static bool TryGetParentProcessFilename (Process process, out string parentProcess)
		{
			try {
				// Consultamos el ID del proceso padre (PPID) utilizando WMI
				using var query = new System.Management.ManagementObjectSearcher(
					$"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {process.Id}");
				var results = query.Get();
				var queryObj = results.OfType<System.Management.ManagementObject>().FirstOrDefault();
				if (queryObj != null) {
					var parentProcessId = (int)(uint)queryObj["ParentProcessId"];
					parentProcess = Process.GetProcessById (parentProcessId)?.MainModule?.FileName;
					return parentProcess != null;
				}
			} catch (Exception) {
			}
			parentProcess = null;
			return false;
		}

		internal static void RestartAsAdmin (bool skipAlertMessages)
		{
			// Get the path of the current executable
			var mainModule = Process.GetCurrentProcess().MainModule;
			if (mainModule == null) {
				return;
			}

			var exePath = mainModule.FileName;
			if (exePath == null) {
				return;
			}

			// Create a new process with administrator privileges
			var processInfo = new ProcessStartInfo {
				FileName = exePath,
				Verb = "runas",
				UseShellExecute = true,
			};

			try {
				// Attempt to start the process
				Process.Start (processInfo);
			} catch (Win32Exception ex) {

				// Handle the case when the user cancels the UAC prompt or there's an error
				if (!skipAlertMessages)
					MessageBox.Show ($"Error: {ex.Message}\n\nThe application will continue running without administrator privileges.", "Failed to Restart as Admin", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			// Close the current application instance
			Application.Current.Shutdown ();
		}
	}
}