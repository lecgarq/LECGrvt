using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LECG.Utils
{
    public static class ImageUtils
    {
        public static BitmapSource CreateRibbonIcon(Geometry geometry, Brush fillBrush)
        {
            // 32x32 is standard for "LargeImage" on Ribbon
            int size = 32; 
            
            // Create a drawing visual
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // Draw transparent background (helper to ensure size)
                drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, size, size));

                // Scale geometry to fit 20x20 inside 32x32 (approx with padding)
                // Assuming standard Lucide path is usually on a 24x24 canvas
                // We'll scale it to fit nicely centrally
                
                // Save state
                drawingContext.PushTransform(new TranslateTransform(4, 4)); // Padding
                // drawingContext.PushTransform(new ScaleTransform(1.0, 1.0)); // Lucide is 24x24, fitting in 32x32 is fine with just padding

                // Draw the geometry
                // Fill if needed, or Stroke if intended as path (Lucide is usually Stroke based, but WPF Geometry.Parse fills the path if it's a shape)
                // Fill if needed, or Stroke if intended as path (Lucide is usually Stroke based, but WPF Geometry.Parse fills the path if it's a shape)
                // If they are stroke paths (lines), we need DrawGeometry(null, pen, geom).
                // Lucide SVG paths are usually strokes.
                // But Geometry.Parse creates a path. If we assume they are strokes, we need a Pen.
                // Let's assume Stroke for Lucide icons.
                
                Pen pen = new Pen(fillBrush, 2);
                pen.StartLineCap = PenLineCap.Round;
                pen.EndLineCap = PenLineCap.Round;
                pen.LineJoin = PenLineJoin.Round;

                drawingContext.DrawGeometry(null, pen, geometry);
                
                drawingContext.Pop(); // Restore Transform
            }

            // Render to Bitmap
            RenderTargetBitmap rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);
            
            // Freeze for thread safety
            if (rtb.CanFreeze) rtb.Freeze();

            return rtb;
        }
    }
}
