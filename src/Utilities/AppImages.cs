using System;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LECG.Utilities
{
    /// <summary>
    /// Centralized image resources for the LECG WPF application.
    /// Loads PNG images from resources.
    /// </summary>
    public static class AppImages
    {
        private static readonly Lazy<BitmapImage> EmptyFallback = new Lazy<BitmapImage>(() =>
        {
            var img = new BitmapImage();
            byte[] bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");
            using (var stream = new System.IO.MemoryStream(bytes))
            {
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = stream;
                img.EndInit();
                img.Freeze();
            }
            return img;
        });

        // One named 32 px and 16 px asset per ribbon action. SVG masters live in Resources/IconSources.
        public static ImageSource Home32 => Icon("Home", 32);
        public static ImageSource Home16 => Icon("Home", 16);
        public static ImageSource CleanSchemas32 => Icon("CleanSchemas", 32);
        public static ImageSource CleanSchemas16 => Icon("CleanSchemas", 16);
        public static ImageSource CompactStyles32 => Icon("CompactStyles", 32);
        public static ImageSource CompactStyles16 => Icon("CompactStyles", 16);
        public static ImageSource PurgeUnused32 => Icon("PurgeUnused", 32);
        public static ImageSource PurgeUnused16 => Icon("PurgeUnused", 16);
        public static ImageSource FormulaGrouping32 => Icon("FormulaGrouping", 32);
        public static ImageSource FormulaGrouping16 => Icon("FormulaGrouping", 16);
        public static ImageSource Warnings32 => Icon("Warnings", 32);
        public static ImageSource Warnings16 => Icon("Warnings", 16);
        public static ImageSource CadBlocks32 => Icon("CadBlocks", 32);
        public static ImageSource CadBlocks16 => Icon("CadBlocks", 16);
        public static ImageSource BatchRename32 => Icon("BatchRename", 32);
        public static ImageSource BatchRename16 => Icon("BatchRename", 16);
        public static ImageSource ConvertFamily32 => Icon("ConvertFamily", 32);
        public static ImageSource ConvertFamily16 => Icon("ConvertFamily", 16);
        public static ImageSource ConvertShared32 => Icon("ConvertShared", 32);
        public static ImageSource ConvertShared16 => Icon("ConvertShared", 16);
        public static ImageSource CategoryChanger32 => Icon("CategoryChanger", 32);
        public static ImageSource CategoryChanger16 => Icon("CategoryChanger", 16);
        public static ImageSource SharedToFamilyParam32 => Icon("SharedToFamilyParam", 32);
        public static ImageSource SharedToFamilyParam16 => Icon("SharedToFamilyParam", 16);
        public static ImageSource FilterCopy32 => Icon("FilterCopy", 32);
        public static ImageSource FilterCopy16 => Icon("FilterCopy", 16);
        public static ImageSource AssignMaterial32 => Icon("AssignMaterial", 32);
        public static ImageSource AssignMaterial16 => Icon("AssignMaterial", 16);
        public static ImageSource OffsetElevations32 => Icon("OffsetElevations", 32);
        public static ImageSource OffsetElevations16 => Icon("OffsetElevations", 16);
        public static ImageSource ResetSlabs32 => Icon("ResetSlabs", 32);
        public static ImageSource ResetSlabs16 => Icon("ResetSlabs", 16);
        public static ImageSource SimplifyPoints32 => Icon("SimplifyPoints", 32);
        public static ImageSource SimplifyPoints16 => Icon("SimplifyPoints", 16);
        public static ImageSource AlignEdges32 => Icon("AlignEdges", 32);
        public static ImageSource AlignEdges16 => Icon("AlignEdges", 16);
        public static ImageSource UpdateContours32 => Icon("UpdateContours", 32);
        public static ImageSource UpdateContours16 => Icon("UpdateContours", 16);
        public static ImageSource ChangeLevel32 => Icon("ChangeLevel", 32);
        public static ImageSource ChangeLevel16 => Icon("ChangeLevel", 16);
        public static ImageSource FloorToToposolid32 => Icon("FloorToToposolid", 32);
        public static ImageSource FloorToToposolid16 => Icon("FloorToToposolid", 16);
        public static ImageSource ToposolidToFloor32 => Icon("ToposolidToFloor", 32);
        public static ImageSource ToposolidToFloor16 => Icon("ToposolidToFloor", 16);
        public static ImageSource FixPoints32 => Icon("FixPoints", 32);
        public static ImageSource FixPoints16 => Icon("FixPoints", 16);
        public static ImageSource SplitBoundaries32 => Icon("SplitBoundaries", 32);
        public static ImageSource SplitBoundaries16 => Icon("SplitBoundaries", 16);
        public static ImageSource DivideToposolid32 => Icon("DivideToposolid", 32);
        public static ImageSource DivideToposolid16 => Icon("DivideToposolid", 16);
        public static ImageSource AlignMaster32 => Icon("AlignMaster", 32);
        public static ImageSource AlignMaster16 => Icon("AlignMaster", 16);
        public static ImageSource AlignLeft32 => Icon("AlignLeft", 32);
        public static ImageSource AlignLeft16 => Icon("AlignLeft", 16);
        public static ImageSource AlignCenter32 => Icon("AlignCenter", 32);
        public static ImageSource AlignCenter16 => Icon("AlignCenter", 16);
        public static ImageSource AlignRight32 => Icon("AlignRight", 32);
        public static ImageSource AlignRight16 => Icon("AlignRight", 16);
        public static ImageSource AlignTop32 => Icon("AlignTop", 32);
        public static ImageSource AlignTop16 => Icon("AlignTop", 16);
        public static ImageSource AlignMiddle32 => Icon("AlignMiddle", 32);
        public static ImageSource AlignMiddle16 => Icon("AlignMiddle", 16);
        public static ImageSource AlignBottom32 => Icon("AlignBottom", 32);
        public static ImageSource AlignBottom16 => Icon("AlignBottom", 16);
        public static ImageSource DistributeH32 => Icon("DistributeH", 32);
        public static ImageSource DistributeH16 => Icon("DistributeH", 16);
        public static ImageSource DistributeV32 => Icon("DistributeV", 32);
        public static ImageSource DistributeV16 => Icon("DistributeV", 16);
        public static ImageSource TypeToLinked32 => Icon("TypeToLinked", 32);
        public static ImageSource TypeToLinked16 => Icon("TypeToLinked", 16);
        public static ImageSource RenderMatch32 => Icon("RenderMatch", 32);
        public static ImageSource RenderMatch16 => Icon("RenderMatch", 16);
        public static ImageSource PbrMaterial32 => Icon("PbrMaterial", 32);
        public static ImageSource PbrMaterial16 => Icon("PbrMaterial", 16);
        public static ImageSource SexyRevit32 => Icon("SexyRevit", 32);
        public static ImageSource SexyRevit16 => Icon("SexyRevit", 16);

        private static BitmapImage Icon(string name, int size)
        {
            return LoadImage($"Lecg{name}_{size}.png");
        }

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
                return EmptyFallback.Value;
            }
        }
    }
}
