using System;
using System.IO;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class ImageColorExtractionService : IImageColorExtractionService
    {
        public Color GetAverageColor(string imagePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

            using FileStream stream = File.OpenRead(imagePath);
            BitmapDecoder decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
            {
                throw new InvalidOperationException("The image does not contain any decodable frames.");
            }

            BitmapSource source = decoder.Frames[0];
            BitmapSource sampled = Downsample(source, 64);
            int bytesPerPixel = (sampled.Format.BitsPerPixel + 7) / 8;
            int stride = sampled.PixelWidth * bytesPerPixel;
            byte[] pixels = new byte[stride * sampled.PixelHeight];
            sampled.CopyPixels(pixels, stride, 0);

            long totalR = 0;
            long totalG = 0;
            long totalB = 0;
            long sampleCount = 0;

            for (int y = 0; y < sampled.PixelHeight; y++)
            {
                int rowOffset = y * stride;
                for (int x = 0; x < sampled.PixelWidth; x++)
                {
                    int index = rowOffset + (x * bytesPerPixel);
                    if (bytesPerPixel < 3)
                    {
                        continue;
                    }

                    totalB += pixels[index];
                    totalG += pixels[index + 1];
                    totalR += pixels[index + 2];
                    sampleCount++;
                }
            }

            if (sampleCount == 0)
            {
                return new Color(128, 128, 128);
            }

            byte red = (byte)(totalR / sampleCount);
            byte green = (byte)(totalG / sampleCount);
            byte blue = (byte)(totalB / sampleCount);
            return new Color(red, green, blue);
        }

        private static BitmapSource Downsample(BitmapSource source, int maxDimension)
        {
            if (source.PixelWidth <= maxDimension && source.PixelHeight <= maxDimension)
            {
                return EnsureBgra32(source);
            }

            double scale = Math.Min(
                (double)maxDimension / source.PixelWidth,
                (double)maxDimension / source.PixelHeight);

            var transform = new TransformedBitmap(source, new System.Windows.Media.ScaleTransform(scale, scale));
            return EnsureBgra32(transform);
        }

        private static BitmapSource EnsureBgra32(BitmapSource source)
        {
            return source.Format == System.Windows.Media.PixelFormats.Bgra32
                ? source
                : new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        }
    }
}
