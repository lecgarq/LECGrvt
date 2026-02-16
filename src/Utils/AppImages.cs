using System;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LECG.Utils
{
    /// <summary>
    /// Centralized image resources for the LECG WPF application.
    /// Loads PNG images from resources.
    /// </summary>
    public static class AppImages
    {
        // Navigation & General
        public static ImageSource Home => LoadImage("Home.png");
        
        // Toposolid & Slab Actions
        public static ImageSource ResetSlabs => LoadImage("ResetSlabs.png");
        public static ImageSource ArrowUpDown => LoadImage("OffsetElevations.png");
        public static ImageSource AssignMaterial => LoadImage("AssignMaterial.png");
        public static ImageSource SimplifyPoints => LoadImage("simplify_points_32.png");
        public static ImageSource AlignEdges => LoadImage("align_edges_32.png");
        public static ImageSource UpdateContours => LoadImage("update_contours_32.png");
        public static ImageSource ChangeLevel => LoadImage("change_level_32.png");


        // Project Health
        public static ImageSource Eraser => LoadImage("CleanSchemas.png"); // Was CleanSchemas
        public static ImageSource Trash => LoadImage("Purge.png");

        // Visualization
        public static ImageSource Sparkles => LoadImage("SexyRevit.png");
        public static ImageSource Palette => LoadImage("RenderMatch.png"); // Note: Palette used for RenderMatch now. 
        // Note: AssignMaterial has its own icon now.

        // Standards
        public static ImageSource SearchReplace => LoadImage("SearchReplace.png");
        public static ImageSource ConvertFamily => LoadImage("ConvertFamily.png");
        public static ImageSource ConvertShared => LoadImage("ConvertFamily.png"); // Reusing for now
        
        // Align Elements
        public static ImageSource AlignPlaceholder => LoadImage("AlignPlaceholder.png");
        
        // Align Icons (16 and 32)
        public static ImageSource AlignMaster32 => LoadImage("AlignMaster_32.png");
        public static ImageSource AlignMaster16 => LoadImage("AlignMaster_16.png");
        
        public static ImageSource AlignLeft32 => LoadImage("AlignLeft_32.png");
        public static ImageSource AlignLeft16 => LoadImage("AlignLeft_16.png");
        
        public static ImageSource AlignCenter32 => LoadImage("AlignCenter_32.png");
        public static ImageSource AlignCenter16 => LoadImage("AlignCenter_16.png");
        
        public static ImageSource AlignRight32 => LoadImage("AlignRight_32.png");
        public static ImageSource AlignRight16 => LoadImage("AlignRight_16.png");
        
        public static ImageSource AlignTop32 => LoadImage("AlignTop_32.png");
        public static ImageSource AlignTop16 => LoadImage("AlignTop_16.png");
        
        public static ImageSource AlignMiddle32 => LoadImage("AlignMiddle_32.png");
        public static ImageSource AlignMiddle16 => LoadImage("AlignMiddle_16.png");
        
        public static ImageSource AlignBottom32 => LoadImage("AlignBottom_32.png");
        public static ImageSource AlignBottom16 => LoadImage("AlignBottom_16.png");
        
        public static ImageSource DistributeH32 => LoadImage("DistributeH_32.png");
        public static ImageSource DistributeH16 => LoadImage("DistributeH_16.png");
        
        public static ImageSource DistributeV32 => LoadImage("DistributeV_32.png");
        public static ImageSource DistributeV16 => LoadImage("DistributeV_16.png");

        // Helpers
        private static BitmapImage LoadImage(string fileName)
        {
            try
            {
                // Format: pack://application:,,,/LECG;component/src/Resources/Images/{fileName}
                var uri = new Uri($"pack://application:,,,/LECG;component/src/Resources/Images/{fileName}", UriKind.Absolute);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad; // Important for memory handling
                image.UriSource = uri;
                image.EndInit();
                image.Freeze(); // Freeze for thread safety
                return image;
            }
            catch
            {
               // Fallback: Create a 1x1 transparent pixel to satisfy WindowIcon requirement vs returning null
               // This prevents the "Set property... threw an exception" error when WindowIcon is bound to null.
               try {
                   var fallback = new BitmapImage();
                   // Minimal base64 1x1 png
                   byte[] bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");
                   using (var stream = new System.IO.MemoryStream(bytes))
                   {
                       fallback.BeginInit();
                       fallback.CacheOption = BitmapCacheOption.OnLoad;
                       fallback.StreamSource = stream;
                       fallback.EndInit();
                       fallback.Freeze();
                       return fallback;
                   }
               } catch { return null!; }
            }
        }
    }
}
