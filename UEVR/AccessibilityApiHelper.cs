using System;
using System.Diagnostics;
using System.Windows.Automation;

namespace UEVR
{
	
public static class AccessibilityApiHelper
{
    /// <summary>
    /// Obtiene la ventana principal de un proceso dado.
    /// </summary>
    /// <param name="process">Proceso de la aplicación</param>
    /// <returns>Elemento de automatización de la ventana</returns>
    public static AutomationElement GetMainWindow(this Process process)
    {
        if (process == null || process.MainWindowHandle == IntPtr.Zero)
        {
            Console.WriteLine("El proceso no tiene una ventana principal.");
            return null;
        }

        return AutomationElement.FromHandle(process.MainWindowHandle);
    }

    /// <summary>
    /// Busca un botón en la ventana de la aplicación usando AutomationId o Name.
    /// </summary>
    /// <param name="windowElement">Ventana de la aplicación</param>
    /// <param name="automationId">ID de automatización del botón (opcional)</param>
    /// <param name="name">Nombre del botón (opcional)</param>
    /// <returns>Elemento del botón si se encuentra</returns>
    public static AutomationElement FindAutomationElement(this AutomationElement windowElement, string automationId = null, string name = null)
    {
        if (windowElement == null)
        {
            Console.WriteLine("Ventana no encontrada.");
            return null;
        }

        Condition condition = null;

        if (!string.IsNullOrEmpty(automationId))
        {
            condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
        }
        else if (!string.IsNullOrEmpty(name))
        {
            condition = new PropertyCondition(AutomationElement.NameProperty, name);
        }
        else
        {
            Console.WriteLine("Debe proporcionar un AutomationId o un Name.");
            return null;
        }

        return windowElement.FindFirst(TreeScope.Descendants, condition);
    }

       /// <summary>
    /// Verifica si un botón está habilitado.
    /// </summary>
    /// <param name="buttonElement">Elemento del botón</param>
    /// <returns>True si está habilitado, False si está deshabilitado</returns>
    public static bool IsButtonEnabled(this AutomationElement buttonElement)
    {
        if (buttonElement == null)
        {
            Console.WriteLine("Botón no encontrado.");
            return false;
        }

        return buttonElement.Current.IsEnabled;
    }

        /// <summary>
/// Obtiene el RuntimeId de un AutomationElement.
/// </summary>
/// <param name="element">Elemento de UI Automation</param>
/// <returns>Array de enteros con el RuntimeId</returns>
public static int[] GetRuntimeId(this AutomationElement element)
{
    if (element == null)
    {
        Console.WriteLine("El elemento es nulo.");
        return null;
    }

    object runtimeIdObj = element.GetCurrentPropertyValue(AutomationElement.RuntimeIdProperty);

    if (runtimeIdObj is int[] runtimeId)
    {
        return runtimeId;
    }

    Console.WriteLine("No se pudo obtener el RuntimeId.");
    return null;
}

        /// <summary>
/// Busca un AutomationElement basado en los dos últimos valores del RuntimeId.
/// </summary>
/// <param name="root">Elemento raíz (por ejemplo, la ventana principal)</param>
/// <param name="lastRuntimeId1">Penúltimo valor del RuntimeId</param>
/// <param name="lastRuntimeId2">Último valor del RuntimeId</param>
/// <returns>El AutomationElement encontrado o null si no existe</returns>
public static AutomationElement FindElementByLastTwoRuntimeId(this AutomationElement root, int lastRuntimeId1, int lastRuntimeId2)
{
    if (root == null)
    {
        Console.WriteLine("El elemento raíz es nulo.");
        return null;
    }

    // Buscar en toda la jerarquía de la UI
    AutomationElementCollection allElements = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);

    foreach (AutomationElement element in allElements)
    {
        object runtimeIdObj = element.GetCurrentPropertyValue(AutomationElement.RuntimeIdProperty);

        if (runtimeIdObj is int[] runtimeId && runtimeId.Length >= 2)
        {
            int last1 = runtimeId[runtimeId.Length - 2];
            int last2 = runtimeId[runtimeId.Length - 1];

            if (last1 == lastRuntimeId1 && last2 == lastRuntimeId2)
            {
                Console.WriteLine("Elemento encontrado.");
                return element;
            }
        }
    }

    Console.WriteLine("Elemento no encontrado.");
    return null;
}

    /// <summary>
    /// Lista todos los AutomationId de los hijos de un AutomationElement y los escribe en la consola.
    /// </summary>
    /// <param name="parentElement">Elemento del cual listar los hijos</param>
    public static void ListChildrenAutomationIds(this AutomationElement parentElement)
    {
        if (parentElement == null)
        {
            Console.WriteLine("El elemento padre es nulo.");
            return;
        }

        AutomationElementCollection children = parentElement.FindAll(TreeScope.Children, Condition.TrueCondition);

        Console.WriteLine($"Se encontraron {children.Count} elementos hijos:");

        foreach (AutomationElement child in children)
        {
            string automationId = child.Current.AutomationId;
            string name = child.Current.Name;
            string controlType = child.Current.ControlType.ProgrammaticName;
            var runtime = GetRuntimeId(child);
            Console.WriteLine($"AutomationId: {automationId}, Name: {name}, ControlType: {controlType}");
        }
    }

    /// <summary>
    /// Simula un clic en un botón.
    /// </summary>
    /// <param name="buttonElement">Elemento del botón</param>
    public static void ClickButton(this AutomationElement buttonElement)
    {
        if (buttonElement == null)
        {
            Console.WriteLine("Botón no encontrado.");
            return;
        }

        if (buttonElement.TryGetCurrentPattern(InvokePattern.Pattern, out object pattern))
        {
            ((InvokePattern)pattern).Invoke();
            Console.WriteLine("Botón pulsado correctamente.");
        }
        else
        {
            Console.WriteLine("El botón no admite la acción de clic.");
        }
    }

		internal static string GetLabelText (this AutomationElement label1)
		{
            try {
                return label1?.Current.Name ?? string.Empty;
            } catch (Exception) {
                return string.Empty;
            }
		}

		internal static void CloseWindow (AutomationElement window)
		{
			
		}
	}
}
