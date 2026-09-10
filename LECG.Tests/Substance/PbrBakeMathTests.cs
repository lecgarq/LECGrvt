using FluentAssertions;
using LECG.Core.Substance;
using Xunit;

namespace LECG.Tests.Substance;

public class PbrBakeMathTests
{
    // one pixel, BGRA
    private static byte[] Px(byte r, byte g, byte b, byte a = 255) => new[] { b, g, r, a };

    [Fact]
    public void InvertGreen_FlipsOnlyGreen()
    {
        var px = Px(10, 200, 30, 77);
        PbrBakeMath.InvertGreen(px);
        px.Should().Equal(30, 55, 10, 77);
    }

    [Fact]
    public void MultiplyByGray_ScalesRgbKeepsAlpha()
    {
        var px = Px(200, 100, 50, 255);
        PbrBakeMath.MultiplyByGray(px, new byte[] { 128 });
        px.Should().Equal(25, 50, 100, 255);
    }

    [Fact]
    public void MultiplyByGray_LengthMismatch_Throws()
    {
        var act = () => PbrBakeMath.MultiplyByGray(new byte[8], new byte[1]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MaxGray_ReturnsMax()
    {
        PbrBakeMath.MaxGray(new byte[] { 3, 250, 7 }).Should().Be(250);
        PbrBakeMath.MaxGray(Array.Empty<byte>()).Should().Be(0);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(26, false)]
    [InlineData(27, true)]
    [InlineData(255, true)]
    public void IsMetallic_UsesTenPercentThreshold(byte max, bool expected)
    {
        PbrBakeMath.IsMetallic(max).Should().Be(expected);
    }

    [Fact]
    public void ComputeF0_Metallic0_IsDielectric()
    {
        var f0 = PbrBakeMath.ComputeF0(Px(200, 100, 50), new byte[] { 0 });
        // 0.04 linear -> sRGB ≈ 0.216 -> 55 (rounding produces 56)
        f0.Should().Equal(56, 56, 56, 255);
    }

    [Fact]
    public void ComputeF0_Metallic255_IsBaseColor()
    {
        var f0 = PbrBakeMath.ComputeF0(Px(200, 100, 50), new byte[] { 255 });
        f0.Should().Equal(50, 100, 200, 255);
    }

    [Fact]
    public void ComputeF0_Metallic255_IsIdentityForAllBytes()
    {
        // With m=1, f0Linear = DielectricF0 + (baseLinear - DielectricF0) * 1.0 = baseLinear
        // So encode(decode(x)) must equal x for every byte 0..255
        for (int b = 0; b <= 255; b++)
        {
            var px = Px((byte)b, (byte)b, (byte)b, 255);
            var f0 = PbrBakeMath.ComputeF0(px, new byte[] { 255 });
            f0[0].Should().Be((byte)b, $"byte {b} round-trip failed");
            f0[1].Should().Be((byte)b, $"byte {b} round-trip failed");
            f0[2].Should().Be((byte)b, $"byte {b} round-trip failed");
        }
    }

    [Fact]
    public void ComputeF0_HalfMetallic_LerpsInLinearSpace()
    {
        var f0 = PbrBakeMath.ComputeF0(Px(255, 255, 255), new byte[] { 128 });
        // lerp(0.04, 1.0, 128/255) = 0.5219 linear -> sRGB 0.7457 -> 190
        f0[0].Should().BeInRange(188, 192);
        f0[1].Should().Be(f0[0]);
        f0[2].Should().Be(f0[0]);
    }

    [Fact]
    public void ScaleAlbedoByInverseMetallic_ZeroesFullMetal()
    {
        var px = Px(200, 100, 50, 255);
        PbrBakeMath.ScaleAlbedoByInverseMetallic(px, new byte[] { 255 });
        px.Should().Equal(0, 0, 0, 255);
    }

    [Fact]
    public void ScaleAlbedoByInverseMetallic_NoMetalUnchanged()
    {
        var px = Px(200, 100, 50, 255);
        PbrBakeMath.ScaleAlbedoByInverseMetallic(px, new byte[] { 0 });
        px.Should().Equal(50, 100, 200, 255);
    }
}
