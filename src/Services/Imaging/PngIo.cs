using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LECG.Services.Imaging
{
    /// <summary>PNG decode/encode helpers. targetSize 0 keeps the source size.</summary>
    public static class PngIo
    {
        public static (byte[] pixels, int width, int height) LoadBgra32(string path, int targetSize)
            => Load(path, targetSize, PixelFormats.Bgra32, 4);

        public static (byte[] pixels, int width, int height) LoadGray8(string path, int targetSize)
            => Load(path, targetSize, PixelFormats.Gray8, 1);

        public static void SaveBgra32(string path, byte[] pixels, int width, int height)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentNullException.ThrowIfNull(pixels);
            Save(path, BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4));
        }

        public static void SaveBgr24FromBgra(string path, byte[] bgra, int width, int height)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentNullException.ThrowIfNull(bgra);
            var bgr = new byte[width * height * 3];
            for (int p = 0, s = 0, d = 0; p < width * height; p++, s += 4, d += 3)
            {
                bgr[d] = bgra[s]; bgr[d + 1] = bgra[s + 1]; bgr[d + 2] = bgra[s + 2];
            }
            Save(path, BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, bgr, width * 3));
        }

        public static void SaveGray8(string path, byte[] pixels, int width, int height)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentNullException.ThrowIfNull(pixels);
            Save(path, BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixels, width));
        }

        private static (byte[] pixels, int width, int height) Load(string path, int targetSize, PixelFormat format, int bytesPerPixel)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            using FileStream stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource source = decoder.Frames[0];

            if (targetSize > 0 && (source.PixelWidth != targetSize || source.PixelHeight != targetSize))
            {
                double sx = (double)targetSize / source.PixelWidth;
                double sy = (double)targetSize / source.PixelHeight;
                source = new TransformedBitmap(source, new ScaleTransform(sx, sy));
            }

            if (source.Format != format)
            {
                source = new FormatConvertedBitmap(source, format, null, 0);
            }

            int stride = source.PixelWidth * bytesPerPixel;
            var pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            return (pixels, source.PixelWidth, source.PixelHeight);
        }

        private static void Save(string path, BitmapSource source)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using FileStream fs = File.Create(path);
            encoder.Save(fs);
        }
    }
}
