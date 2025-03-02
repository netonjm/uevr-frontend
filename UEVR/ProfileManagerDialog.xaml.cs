using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace UEVR
{
	public enum CodecType
    {
        H264,
        HEVC
    }

	/// <summary>
	/// Interaction logic for ProfileManagerDialog.xaml
	/// </summary>
	public partial class ProfileManagerDialog : Window
	{
		public ProfileManagerDialog ()
        {
            InitializeComponent ();
              ProfileSelector.SelectedIndex = 1; // Default to Medium
            currentProfile = "Medium";
            ApplyProfileSettings(currentProfile);
        }

        private string currentProfile;

        private void ProfileSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileSelector.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedProfile = selectedItem.Content.ToString();
                ApplyProfileSettings(selectedProfile);
            }
        }

        private void ApplyProfileSettings(string profile)
        {
            switch (profile)
            {
                case "Low":
                    ResolutionBox.SelectedIndex = 0;
                    BitrateBox.SelectedIndex = 0;
                    FPSBox.SelectedIndex = 0;
                    CodecBox.SelectedIndex = 0;
                    break;
                case "Medium":
                    ResolutionBox.SelectedIndex = 1;
                    BitrateBox.SelectedIndex = 1;
                    FPSBox.SelectedIndex = 1;
                    CodecBox.SelectedIndex = 0;
                    break;
                case "High":
                    ResolutionBox.SelectedIndex = 2;
                    BitrateBox.SelectedIndex = 2;
                    FPSBox.SelectedIndex = 2;
                    CodecBox.SelectedIndex = 1;
                    break;
                case "Ultra":
                    ResolutionBox.SelectedIndex = 3;
                    BitrateBox.SelectedIndex = 3;
                    FPSBox.SelectedIndex = 3;
                    CodecBox.SelectedIndex = 1;
                    break;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "¿Desea modificar la configuración actual? Se recomienda hacer un respaldo.", 
                "Confirmación", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                BackupCurrentSettings();
                MessageBox.Show("Configuración modificada y respaldo guardado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BackupCurrentSettings()
        {
            // Simulación de respaldo de configuración
            currentProfile = ProfileSelector.Text;
        }
	}
}
