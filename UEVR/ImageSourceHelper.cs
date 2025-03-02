using System;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace UEVR
{
	internal static class ImageSourceHelper
	{
		/// <summary>
    /// Carga una imagen desde un path de Windows y la convierte en un BitmapImage.
    /// </summary>
    /// <param name="imagePath">Ruta completa del archivo de imagen.</param>
    /// <returns>Objeto BitmapImage o null si hay un error.</returns>
    public static BitmapImage LoadBitmapImage(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !System.IO.File.Exists(imagePath))
        {
            Console.WriteLine("Ruta inválida o archivo no encontrado.");
            return null;
        }

        try
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // Carga inmediata
            bitmap.EndInit();
            bitmap.Freeze(); // Congela la imagen para evitar problemas de acceso en diferentes hilos
            return bitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al cargar la imagen: {ex.Message}");
            return null;
        }
    }


		public static ImageSource? GetImage (string imagePath)
		{
			if (!File.Exists (imagePath)) { return null; };
			// Crea una nueva instancia de BitmapImage
			BitmapImage bitmapImage = new BitmapImage();
			// Asigna la URI de la imagen al BitmapImage
			bitmapImage.BeginInit ();
			bitmapImage.UriSource = new Uri (imagePath, UriKind.RelativeOrAbsolute);
			bitmapImage.EndInit ();
			return bitmapImage;
		}
	}

}