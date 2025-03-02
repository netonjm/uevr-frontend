using System;
using System.Diagnostics;
using System.Management;

namespace UEVR
{
	class ProcessMonitor
		{
			public event EventHandler<Process> NewProcessDetected;
			ManagementEventWatcher processStartWatcher;
			public void StartMonitoring (string Path)
			{
				// Crear un objeto para seguir los eventos de inicio de nuevos procesos
				processStartWatcher = new ManagementEventWatcher (new WqlEventQuery ("SELECT * FROM Win32_ProcessStartTrace"));
				processStartWatcher.EventArrived += ProcessStarted;

				// Comenzar a monitorear
				processStartWatcher.Start ();
			}

			void Stop ()
			{
				processStartWatcher.Stop ();
			}

			private void ProcessStarted (object sender, EventArrivedEventArgs e)
			{
				Console.WriteLine ($"Nuevo proceso detectado: {e.NewEvent}");

				// Lanzar el evento de nuevo proceso detectado
				//OnNewProcessDetected(e.NewEvent.);
			}

			//protected virtual void OnNewProcessDetected(Proc processName)
			//{
			//    // Verificar si hay manejadores de eventos suscritos
			//    if (NewProcessDetected != null)
			//    {
			//        // Lanzar el evento
			//        NewProcessDetected.Invoke(this, processName);
			//    }
			//}
		}

}