using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace UEVR {
    public partial class VDWarnDialog : Window {

        private const string OpenXRRegistryKey = @"SOFTWARE\Khronos\OpenXR\1";
        private const string AvailableRuntimesKey = @"SOFTWARE\Khronos\OpenXR\1\AvailableRuntimes";
        private const string ActiveRuntimeValue = "ActiveRuntime";


        public bool HideFutureWarnings { get; private set; }
        public bool DialogResultOK { get; private set; }

        public VDWarnDialog() {
            InitializeComponent();

            //CheckOpenXRRuntime();
        }
        
        private void CheckOpenXRRuntime()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(OpenXRRegistryKey, false))
                {
                    if (key != null)
                    {
                        string currentRuntime = key.GetValue(ActiveRuntimeValue) as string;
                        string steamRuntime = GetSteamVRRuntimePath();

                        if (!string.IsNullOrEmpty(currentRuntime) && currentRuntime.Equals(steamRuntime, StringComparison.OrdinalIgnoreCase))
                        {
                            MessageBox.Show("SteamVR ya está configurado como OpenXR Runtime.", "OpenXR", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            AskToSetSteamVRAsOpenXR(steamRuntime);
                        }
                    }
                    else
                    {
                        MessageBox.Show("No se encontró la clave de OpenXR en el registro.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al leer el registro: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetSteamVRRuntimePath()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(AvailableRuntimesKey, false))
                {
                    if (key != null)
                    {
                        foreach (var valueName in key.GetValueNames())
                        {
                            if (valueName.ToLower().Contains("steamvr"))
                            {
                                return valueName; // La clave es la ruta del runtime de SteamVR
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al buscar SteamVR en AvailableRuntimes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null; // No encontrado
        }

        private void AskToSetSteamVRAsOpenXR(string steamRuntime)
        {
            if (string.IsNullOrEmpty(steamRuntime))
            {
                MessageBox.Show("No se encontró SteamVR en AvailableRuntimes. Asegúrate de que está instalado.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                "SteamVR no está configurado como OpenXR Runtime.\n\n¿Quieres cambiarlo ahora?",
                "Configurar OpenXR",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SetSteamVRAsOpenXR(steamRuntime);
            }
        }

        private void SetSteamVRAsOpenXR(string steamRuntime)
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(OpenXRRegistryKey, true))
                {
                    if (key != null)
                    {
                        key.SetValue(ActiveRuntimeValue, steamRuntime, RegistryValueKind.String);
                        MessageBox.Show("SteamVR ha sido configurado como OpenXR Runtime.\n\nReinicia SteamVR para aplicar los cambios.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("No se pudo acceder a la clave de OpenXR en el registro.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al modificar el registro: {ex.Message}\n\nEjecuta como Administrador.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e) {
            HideFutureWarnings = chkHideWarning.IsChecked ?? false;
            DialogResultOK = true;
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) {
            DialogResultOK = false;
            this.Close();
        }


        public static void ModifySteamVRSettings(string configPath)
    {
        try
        {
            // Leer el archivo JSON existente
            string json = File.ReadAllText(configPath);
            JObject config = JObject.Parse(json);

            // Asegurar que la sección "steamvr" existe
            if (config["steamvr"] == null)
            {
                config["steamvr"] = new JObject();
            }

            // Modificar solo las opciones necesarias
            config["steamvr"]["enableHomeApp"] = false;
            config["steamvr"]["doNotFadeToGrid"] = true;

            // Guardar los cambios en el archivo
            File.WriteAllText(configPath, config.ToString());
            Console.WriteLine("Configuración de SteamVR modificada correctamente.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al modificar SteamVR settings: {ex.Message}");
        }
    }



        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) {
                this.DragMove();
            }
        }
    }
}
