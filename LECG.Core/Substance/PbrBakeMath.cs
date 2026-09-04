namespace LECG.Core.Substance;

/// <summary>
/// Pure pixel math for the PBR bake. Color buffers are interleaved 8-bit BGRA
/// (WPF Bgra32 layout). Mask buffers are 8-bit gray, one byte per pixel.
/// </summary>
public static class PbrBakeMath
{
    public const double DielectricF0 = 0.04;
    public const byte MetallicThreshold = 26; // > 10 % of 255

    private static readonly byte[] SrgbToLinearLut = BuildSrgbToLinear();
    private static readonly byte[] LinearToSrgbLut = BuildLinearToSrgb();

    public static void InvertGreen(Span<byte> bgra)
    {
        for (int i = 1; i < bgra.Length; i += 4)
        {
            bgra[i] = (byte)(255 - bgra[i]);
        }
    }

    public static void MultiplyByGray(Span<byte> bgra, ReadOnlySpan<byte> gray)
    {
        EnsureSameLength(bgra, gray);
        for (int p = 0; p < gray.Length; p++)
        {
            int o = p * 4;
            int g = gray[p];
            bgra[o] = Mul(bgra[o], g);
            bgra[o + 1] = Mul(bgra[o + 1], g);
            bgra[o + 2] = Mul(bgra[o + 2], g);
        }
    }

    public static byte MaxGray(ReadOnlySpan<byte> gray)
    {
        byte max = 0;
        foreach (byte b in gray) if (b > max) max = b;
        return max;
    }

    public static bool IsMetallic(byte maxGray) => maxGray > MetallicThreshold;

    public static byte[] ComputeF0(ReadOnlySpan<byte> baseColorBgra, ReadOnlySpan<byte> metallicGray)
    {
        EnsureSameLength(baseColorBgra, metallicGray);
        var f0 = new byte[baseColorBgra.Length];
        for (int p = 0; p < metallicGray.Length; p++)
        {
            int o = p * 4;
            double m = metallicGray[p] / 255.0;
            for (int c = 0; c < 3; c++)
            {
                double baseLinear = SrgbToLinearLut[baseColorBgra[o + c]] / 255.0;
                double f0Linear = DielectricF0 + (baseLinear - DielectricF0) * m;
                f0[o + c] = LinearToSrgbLut[(int)Math.Round(Math.Clamp(f0Linear, 0, 1) * 255)];
            }
            f0[o + 3] = 255;
        }
        return f0;
    }

    public static void ScaleAlbedoByInverseMetallic(Span<byte> bgra, ReadOnlySpan<byte> metallicGray)
    {
        EnsureSameLength(bgra, metallicGray);
        for (int p = 0; p < metallicGray.Length; p++)
        {
            int o = p * 4;
            int inv = 255 - metallicGray[p];
            bgra[o] = Mul(bgra[o], inv);
            bgra[o + 1] = Mul(bgra[o + 1], inv);
            bgra[o + 2] = Mul(bgra[o + 2], inv);
        }
    }

    private static byte Mul(byte value, int factor255) => (byte)((value * factor255 + 127) / 255);

    private static void EnsureSameLength(ReadOnlySpan<byte> bgra, ReadOnlySpan<byte> gray)
    {
        if (bgra.Length != gray.Length * 4)
        {
            throw new ArgumentException($"BGRA buffer ({bgra.Length} bytes) must be exactly 4x the gray buffer ({gray.Length} bytes).");
        }
    }

    private static byte[] BuildSrgbToLinear()
    {
        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            double c = i / 255.0;
            double lin = c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            lut[i] = (byte)Math.Round(lin * 255);
        }
        return lut;
    }

    private static byte[] BuildLinearToSrgb()
    {
        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            double lin = i / 255.0;
            double c = lin <= 0.0031308 ? lin * 12.92 : 1.055 * Math.Pow(lin, 1 / 2.4) - 0.055;
            lut[i] = (byte)Math.Round(Math.Clamp(c, 0, 1) * 255);
        }
        return lut;
    }
}
