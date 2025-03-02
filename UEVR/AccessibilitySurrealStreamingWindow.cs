using System;
using System.Diagnostics;
using System.Windows.Automation;

namespace UEVR
{
	class AccessibilitySurrealStreamingWindow : IDisposable
	{
		AutomationElement window;
		AutomationElement? label1, label2;
		AutomationElement? button;

		public AccessibilitySurrealStreamingWindow (Process surrealStreamer)
		{
			window = AccessibilityApiHelper.GetMainWindow (surrealStreamer);
		}

		public void RefreshFromProcess ()
		{
			RefreshButtons ();
		}

		void RefreshButtons ()
		{
			label1 = window.FindElementByLastTwoRuntimeId (-108303344, -1110231814);
			label2 = window.FindElementByLastTwoRuntimeId (-2068978114, -1699728301);
			button = window.FindElementByLastTwoRuntimeId (1141999025, 1201985768);
		}

		public bool IsSteamVrConnected () => (label1?.GetLabelText () ?? string.Empty) == "Connected";
		public bool IsHeadsetConnected () => (label2?.GetLabelText () ?? string.Empty) == "Connected";

		public void Connect () => AccessibilityApiHelper.ClickButton (button);

		public void Close ()
		{
			// Close the window
			AccessibilityApiHelper.CloseWindow (window);
		}

		public void Dispose ()
		{

		}
	}
}