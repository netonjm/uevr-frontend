using System.Security.Principal;

namespace UEVR
{
	class WindowsIdentityHelper
		{
			public static bool IsAdministrator ()
			{
				WindowsIdentity identity = WindowsIdentity.GetCurrent();
				WindowsPrincipal principal = new WindowsPrincipal(identity);
				return principal.IsInRole (WindowsBuiltInRole.Administrator);
			}
		}
}