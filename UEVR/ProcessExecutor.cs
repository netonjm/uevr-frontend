using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog.Targets;

namespace UEVR
{
	// Clase abstracta para definir acciones sobre un proceso
	public abstract class ProcessAction
	{
		public virtual int DelayMs { get; } = 3000;  // Tiempo antes de ejecutar la acción (por defecto 3s)
		public virtual int MaxRetries { get; } = 3;  // Número máximo de intentos (por defecto 3)
		public virtual int RetryDelayMs { get; } = 2000; // Espera entre intentos (por defecto 2s)

		public abstract bool Execute (Process process);
	}

	// Clase que maneja la ejecución de un proceso y ejecuta la acción correspondiente
	public class ProcessManager
	{
		public bool IsRunning => currentProcess != null;
		public event EventHandler Finished;
		private  string _exePath;
		private  string _processName;
		public ProcessAction? ProcessAction { get; set; }

		Process currentProcess;

		public static bool Kill (Process target)
		{
			try {
				if (target == null || target.HasExited) {
					return true;
				}
				target.WaitForInputIdle (100);

				SharedMemory.SendCommand (SharedMemory.Command.Quit);

				if (target.WaitForExit (2000)) {
					return true;
				}

				target.Kill ();

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

		public void Start (string exePath)
		{
			_exePath = exePath;
			_processName = System.IO.Path.GetFileNameWithoutExtension (exePath); // Obtener solo el nombre del proceso

			currentProcess = GetExistingProcess ();
			if (currentProcess != null && currentProcess.HasExited) {
				//kill
				KillCurrentProcess();
				currentProcess = null;
			}

			if (currentProcess == null) {
				currentProcess = StartNewProcess ();
			}

			if (currentProcess == null) {
				Console.WriteLine ("No se pudo iniciar ni obtener el proceso.");
				return;
			}

			currentProcess.Exited -= CurrentProcess_Exited;
			currentProcess.Exited += CurrentProcess_Exited;

			Console.WriteLine ($"Proceso en ejecución: {currentProcess.ProcessName} (PID: {currentProcess.Id})");

			// Si no hay acción, simplemente ejecutar el proceso sin hacer nada más
			if (ProcessAction == null) {
				Console.WriteLine ("No se ha definido ninguna acción. El proceso continuará ejecutándose normalmente.");
				//process.WaitForExit();
				return;
			}

			// Ejecutar la acción después del delay configurado en la acción
			Task.Run (async () => {
				await Task.Delay (ProcessAction.DelayMs);
				ExecuteWithRetries (currentProcess);
			});
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

		private void ExecuteWithRetries (Process process)
		{
			int attempt = 0;
			while (attempt < ProcessAction!.MaxRetries) {
				attempt++;
				Console.WriteLine ($"Intento {attempt}/{ProcessAction.MaxRetries} de ejecutar la acción...");

				if (ProcessAction.Execute (process)) {
					Console.WriteLine ("Acción ejecutada exitosamente.");
					return;
				}

				if (attempt < ProcessAction.MaxRetries) {
					Console.WriteLine ($"Fallo en la ejecución. Reintentando en {ProcessAction.RetryDelayMs}ms...");
					Thread.Sleep (ProcessAction.RetryDelayMs);
				} else {
					Console.WriteLine ("Se agotaron los intentos. La acción no pudo completarse.");
				}
			}
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

	// Implementación de una acción: Inyectar una DLL
	public class InjectDLLAction : ProcessAction
	{
		public override int DelayMs { get; } = 5000;  // Sobrescribir: Espera antes de inyectar (5s)
		public override int MaxRetries { get; } = 3;  // Máximo 3 intentos
		public override int RetryDelayMs { get; } = 2000; // 2 segundos entre intentos
		private readonly string _dllPath;

		public InjectDLLAction (string dllPath)
		{
			_dllPath = dllPath;
		}

		public override bool Execute (Process process)
		{
			Console.WriteLine ($"[InjectDLL] Intentando inyectar {_dllPath} en {process.ProcessName} (PID: {process.Id})...");

			// Simular fallo aleatorio en la inyección
			if (new Random ().Next (0, 2) == 0) {
				Console.WriteLine ("[InjectDLL] Inyección fallida.");
				return false;
			}

			Console.WriteLine ("[InjectDLL] Inyección exitosa.");
			return true;
		}
	}

	// Implementación de otra acción: Lanzar otro proceso
	public class LaunchProcessAction : ProcessAction
	{
		public override int DelayMs { get; } = 3000;  // Sobrescribir: Espera 3 segundos antes de ejecutar
		public override int MaxRetries { get; } = 5;  // Máximo 5 intentos
		public override int RetryDelayMs { get; } = 1000; // 1 segundo entre intentos
		private readonly string _newProcessPath;

		public LaunchProcessAction (string newProcessPath)
		{
			_newProcessPath = newProcessPath;
		}

		public override bool Execute (Process process)
		{
			Console.WriteLine ($"[LaunchProcess] Intentando lanzar {_newProcessPath}...");

			try {
				Process.Start (_newProcessPath);
				Console.WriteLine ("[LaunchProcess] Proceso lanzado exitosamente.");
				return true;
			} catch (Exception ex) {
				Console.WriteLine ($"[LaunchProcess] Error al lanzar proceso: {ex.Message}");
				return false;
			}
		}
	}
}
