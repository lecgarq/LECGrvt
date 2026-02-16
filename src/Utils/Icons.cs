using System.Windows.Media;

namespace LECG.Utils
{
    /// <summary>
    /// Centralized icon geometries for the LECG WPF application.
    /// Uses Lucide icon paths (24x24 viewbox, stroke-based).
    /// </summary>
    public static class Icons
    {
        // Navigation & Window Controls
        public static Geometry Home { get; } = Geometry.Parse("M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z M9 22V12h6v10");
        public static Geometry X { get; } = Geometry.Parse("M18 6 6 18 M6 6l12 12");
        public static Geometry Minimize { get; } = Geometry.Parse("M5 12h14");

        // Toposolid & Slab Actions
        public static Geometry ResetSlabs { get; } = Geometry.Parse("M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8 M3 3v5h5 M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16 M16 16h5v5");
        public static Geometry ArrowUpDown { get; } = Geometry.Parse("m21 16-4 4-4-4 M17 20V4 M3 8l4-4 4 4 M7 4v16");

        // CRUD & Editing
        public static Geometry Trash { get; } = Geometry.Parse("M3 6h18 M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6 M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2 M10 11v6 M14 11v6");
        public static Geometry Check { get; } = Geometry.Parse("M20 6 9 17l-5-5");
        public static Geometry Copy { get; } = Geometry.Parse("M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2 M8 8h10c1.1 0 2 .9 2 2v10c0 1.1-.9 2-2 2H8c-1.1 0-2-.9-2-2V10c0-1.1.9-2 2-2z");
        public static Geometry Plus { get; } = Geometry.Parse("M5 12h14 M12 5v14");
        public static Geometry Minus { get; } = Geometry.Parse("M5 12h14");
        public static Geometry Eraser { get; } = Geometry.Parse("m7 21-4.3-4.3c-1-1-1-2.5 0-3.4l9.6-9.6c1-1 2.5-1 3.4 0l5.6 5.6c1 1 1 2.5 0 3.4L13 21 M22 21H7 M5 11l9 9");
        public static Geometry Hammer { get; } = Geometry.Parse("m15 12-8.5 8.5c-.83.83-2.17.83-3 0 0 0 0 0 0 0a2.12 2.12 0 0 1 0-3L12 9 M17.64 6.36 21 3l-3.36-3.36a1.5 1.5 0 0 0-2.12 0l-1.03 1.03a1.5 1.5 0 0 0 0 2.12l3.36 3.36a1.5 1.5 0 0 0 2.12 0 1.5 1.5 0 0 0 0-2.12ZM14 14l3-3");

        // Decorative & Visual
        public static Geometry Sparkles { get; } = Geometry.Parse("m12 3-1.912 5.813a2 2 0 0 1-1.275 1.275L3 12l5.813 1.912a2 2 0 0 1 1.275 1.275L12 21l1.912-5.813a2 2 0 0 1 1.275-1.275L12 3Z M5 3v4 M19 17v4 M3 5h4 M17 19h4");
        public static Geometry Sun { get; } = Geometry.Parse("M12 16a4 4 0 1 0 0-8 4 4 0 0 0 0 8z M12 2v2 M12 20v2 M4.93 4.93l1.41 1.41 M17.66 17.66l1.41 1.41 M2 12h2 M20 12h2 M6.34 17.66l-1.41 1.41 M19.07 4.93l-1.41 1.41");
        public static Geometry SearchReplace { get; } = Geometry.Parse("M14 14l5 5 M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16z M16 9l-3 3 3 3 M9 9h7");
        public static Geometry Palette { get; } = Geometry.Parse("M12 22C6.5 22 2 17.5 2 12S6.5 2 12 2s10 4.5 10 10c0 .9-.73 1.64-1.64 1.64H18.5c-.45 0-.83.38-.83.83 0 .22.09.41.24.56.14.15.24.43.24.72C18.15 20.37 15.52 22 12 22ZM12 4C7.58 4 4 7.58 4 12s3.58 8 8 8c.29 0 .5-.21.5-.5 0-.14-.05-.26-.14-.35-.12-.13-.36-.35-.36-.65 0-.45.38-.83.83-.83h1.53c3.05 0 5.52-2.47 5.52-5.52 0-1.46-.6-2.82-1.68-3.89C17 7.16 14.64 4 12 4Z M6.5 13a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3z M9 8.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3z M15 8.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3z M17.5 13a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3z");
        public static Geometry Layers { get; } = Geometry.Parse("m12 2 9 4.9V17L12 22 3 17V6.9l9-4.9z M3 6.9l9 4.9 9-4.9 M12 22V11.8");
        
        // Align & Distribute
        public static Geometry AlignLeft { get; } = Geometry.Parse("M21 6H3 M17 12H3 M13 18H3"); // Just generic align lines
        public static Geometry AlignCenter { get; } = Geometry.Parse("M3 6h18 M7 12h10 M10 18h4");
        public static Geometry AlignRight { get; } = Geometry.Parse("M21 6H3 M21 12H7 M21 18H11");
        
        // Command Specifics
        public static Geometry Simplify { get; } = Geometry.Parse("M12 20a8 8 0 1 0 0-16 8 8 0 0 0 0 16z M12 10v4"); // Simple circle
        public static Geometry Contours { get; } = Geometry.Parse("M3 12h18 M3 6h18 M3 18h18"); // Lines
        public static Geometry Level { get; } = Geometry.Parse("M12 2v20 M2 12h20"); // Crosshair
        public static Geometry Material { get; } = Geometry.Parse("M2 22 22 2"); // Diagonal
        public static Geometry CircleCheck { get; } = Geometry.Parse("M22 11.08V12a10 10 0 1 1-5.93-9.14 M22 4L12 14.01l-3-3");
        public static Geometry Circle { get; } = Geometry.Parse("M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20z");

        static Icons()
        {
            // Freeze all geometries for thread safety and performance
            Home.Freeze();
            X.Freeze();
            Minimize.Freeze();
            ResetSlabs.Freeze();
            ArrowUpDown.Freeze();
            Trash.Freeze();
            Check.Freeze();
            Copy.Freeze();
            Plus.Freeze();
            Minus.Freeze();
            Eraser.Freeze();
            Hammer.Freeze();
            Sparkles.Freeze();
            Sun.Freeze();
            SearchReplace.Freeze();
            Palette.Freeze();
            Layers.Freeze();
            AlignLeft.Freeze();
            AlignCenter.Freeze();
            AlignRight.Freeze();
            Simplify.Freeze();
            Contours.Freeze();
            Level.Freeze();
            Material.Freeze();
            CircleCheck.Freeze();
            Circle.Freeze();
        }
    }
}
