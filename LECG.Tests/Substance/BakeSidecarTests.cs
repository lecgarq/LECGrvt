using FluentAssertions;
using LECG.Core.Substance;
using Xunit;

namespace LECG.Tests.Substance;

public class BakeSidecarTests
{
    private static readonly (string, long)[] Sources = { (@"C:\a.png", 100), (@"C:\b.png", 200) };

    private static BakeSidecar Build(int size = 2048) =>
        BakeSidecar.Build(size, Sources, "DirectX", true, true, false, false);

    private static bool IsFresh(
        BakeSidecar? sidecar,
        int size = 2048,
        IEnumerable<(string, long)>? sources = null) =>
        BakeSidecar.IsFresh(sidecar, size, sources ?? Sources, "DirectX", true, true);

    [Fact]
    public void IsFresh_NullSidecar_False()
    {
        IsFresh(null).Should().BeFalse();
    }

    [Fact]
    public void IsFresh_SameSizeSameTicks_True()
    {
        var s = Build();
        IsFresh(s).Should().BeTrue();
    }

    [Fact]
    public void IsFresh_DifferentSize_False()
    {
        var s = Build();
        IsFresh(s, 4096).Should().BeFalse();
    }

    [Fact]
    public void IsFresh_SourceNewer_False()
    {
        var s = Build();
        IsFresh(s, sources: new[] { (@"C:\a.png", 101L), (@"C:\b.png", 200L) }).Should().BeFalse();
    }

    [Fact]
    public void IsFresh_ExtraSource_False()
    {
        var s = Build();
        IsFresh(s, sources: Sources.Append((@"C:\c.png", 5L))).Should().BeFalse();
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var s = BakeSidecar.Build(4096, Sources, "DirectX", true, true, f0Written: true, opacityWritten: false);
        var back = BakeSidecar.FromJson(s.ToJson());
        back.Should().NotBeNull();
        back!.TargetSize.Should().Be(4096);
        back.ContractVersion.Should().Be(BakeSidecar.CurrentContractVersion);
        back.Consumer.Should().Be("Revit");
        back.SourceNormalConvention.Should().Be("DirectX");
        back.SourceNormalConventionDeclared.Should().BeTrue();
        back.OutputNormalConvention.Should().Be("OpenGL");
        back.AoBakedIntoBaseColor.Should().BeTrue();
        back.DerivedOutput.Should().BeTrue();
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
        var s = BakeSidecar.Build(2048, new[] { (@"C:\Lib\A.png", 100L) }, "DirectX", true, false, false, false);
        var back = BakeSidecar.FromJson(s.ToJson())!;
        BakeSidecar.IsFresh(back, 2048, new[] { (@"c:\lib\a.png", 100L) }, "DirectX", true, false).Should().BeTrue();
    }

    [Fact]
    public void IsFresh_OldOrDifferentTransformContract_IsFalse()
    {
        var s = Build();
        s.ContractVersion = 0;
        IsFresh(s).Should().BeFalse();

        s = Build();
        BakeSidecar.IsFresh(s, 2048, Sources, "OpenGL", true, true).Should().BeFalse();
    }
}
