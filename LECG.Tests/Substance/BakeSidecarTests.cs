using FluentAssertions;
using LECG.Core.Substance;
using Xunit;

namespace LECG.Tests.Substance;

public class BakeSidecarTests
{
    private static readonly (string, long)[] Sources = { (@"C:\a.png", 100), (@"C:\b.png", 200) };

    [Fact]
    public void IsFresh_NullSidecar_False()
    {
        BakeSidecar.IsFresh(null, 2048, Sources).Should().BeFalse();
    }

    [Fact]
    public void IsFresh_SameSizeSameTicks_True()
    {
        var s = BakeSidecar.Build(2048, Sources, f0Written: false, opacityWritten: false);
        BakeSidecar.IsFresh(s, 2048, Sources).Should().BeTrue();
    }

    [Fact]
    public void IsFresh_DifferentSize_False()
    {
        var s = BakeSidecar.Build(2048, Sources, false, false);
        BakeSidecar.IsFresh(s, 4096, Sources).Should().BeFalse();
    }

    [Fact]
    public void IsFresh_SourceNewer_False()
    {
        var s = BakeSidecar.Build(2048, Sources, false, false);
        BakeSidecar.IsFresh(s, 2048, new[] { (@"C:\a.png", 101L), (@"C:\b.png", 200L) }).Should().BeFalse();
    }

    [Fact]
    public void IsFresh_ExtraSource_False()
    {
        var s = BakeSidecar.Build(2048, Sources, false, false);
        BakeSidecar.IsFresh(s, 2048, Sources.Append((@"C:\c.png", 5L))).Should().BeFalse();
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var s = BakeSidecar.Build(4096, Sources, f0Written: true, opacityWritten: false);
        var back = BakeSidecar.FromJson(s.ToJson());
        back.Should().NotBeNull();
        back!.TargetSize.Should().Be(4096);
        back.F0Written.Should().BeTrue();
        back.OpacityWritten.Should().BeFalse();
        back.SourceTicks.Should().Equal(s.SourceTicks);
    }

    [Fact]
    public void FromJson_Garbage_ReturnsNull()
    {
        BakeSidecar.FromJson("nope").Should().BeNull();
    }

    [Fact]
    public void IsFresh_AfterJsonRoundTrip_IgnoresPathCase()
    {
        var s = BakeSidecar.Build(2048, new[] { (@"C:\Lib\A.png", 100L) }, false, false);
        var back = BakeSidecar.FromJson(s.ToJson())!;
        BakeSidecar.IsFresh(back, 2048, new[] { (@"c:\lib\a.png", 100L) }).Should().BeTrue();
    }
}
