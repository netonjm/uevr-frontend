using System;
using System.Diagnostics;
using System.Windows;

namespace UEVR
{
	public enum VrRuntime
	{
		OpenXR,
		OpenVR,
		Oculus
	}

	class InjectorManager
	{
		public VrRuntime Runtime { get; set; } = VrRuntime.OpenXR;

		[System.Runtime.InteropServices.DllImport ("user32.dll")]
		public static extern void SwitchToThisWindow (IntPtr hWnd, bool fAltTab);

		public bool UseNullifyVrPlugins { get; set; } = true;

		public bool SkipAlertMessages { get; set; } = false;

		public bool FocusGameOnInjection { get; set; } = true;

		string GetDllName ()
		{
			switch (Runtime) {
				case VrRuntime.OpenXR:
					return "openxr_loader.dll";
			}
			return "openvr_api.dll";
		}

		public bool Inject (Process process)
		{
			try {
				string runtimeName = GetDllName();

				if (UseNullifyVrPlugins) {
					IntPtr nullifierBase;
					if (Injector.InjectDll (process.Id, "UEVRPluginNullifier.dll", out nullifierBase) && nullifierBase.ToInt64 () > 0) {
						if (!Injector.CallFunctionNoArgs (process.Id, "UEVRPluginNullifier.dll", nullifierBase, "nullify", true)) {
							if (!SkipAlertMessages)
								MessageBox.Show ("Failed to nullify VR plugins.");
						}
					} else {
						if (!SkipAlertMessages)
							MessageBox.Show ("Failed to inject plugin nullifier.");
					}
				}

				if (Injector.InjectDll (process.Id, runtimeName)) {
					Injector.InjectDll (process.Id, "UEVRBackend.dll");
				}

				if (FocusGameOnInjection) {
					SwitchToThisWindow (process.MainWindowHandle, true);
				}
				return true;
			} catch (Exception ex) {
				if (!SkipAlertMessages)
					MessageBox.Show ("Failed to inject plugin nullifier." + ex.Message);
				else {
					Console.WriteLine ($"Exception while injecting: {ex}");
				}
				return false;
			}
		}
	}
}
