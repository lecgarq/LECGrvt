using LECG.Core.Geometry;

namespace LECG.Tests.Services;

public class TriangleSurfaceTests
{
    [Fact]
    public void Interpolates_the_original_sloped_triangle()
    {
        var surface = new TriangleSurface(new[]
        {
            new SurfaceTriangle(
                new SurfacePoint(0, 0, 5),
                new SurfacePoint(10, 0, 25),
                new SurfacePoint(0, 10, 35))
        });

        Assert.True(surface.TryGetElevation(2, 3, out double elevation));
        Assert.Equal(18, elevation, 8);
    }

    [Fact]
    public void Extrapolates_only_near_a_boundary()
    {
        var surface = new TriangleSurface(new[]
        {
            new SurfaceTriangle(
                new SurfacePoint(0, 0, 5),
                new SurfacePoint(10, 0, 25),
                new SurfacePoint(0, 10, 35))
        });

        Assert.True(surface.TryGetElevation(10.001, 0, out double nearElevation, 0.01));
        Assert.Equal(25.002, nearElevation, 8);
        Assert.False(surface.TryGetElevation(11, 0, out _, 0.01));
    }

    [Fact]
    public void Samples_each_side_of_a_crease_from_its_own_triangle()
    {
        var surface = new TriangleSurface(new[]
        {
            new SurfaceTriangle(
                new SurfacePoint(0, 0, 0),
                new SurfacePoint(10, 0, 0),
                new SurfacePoint(0, 10, 10)),
            new SurfaceTriangle(
                new SurfacePoint(10, 0, 0),
                new SurfacePoint(10, 10, 20),
                new SurfacePoint(0, 10, 10))
        });

        Assert.True(surface.TryGetElevation(2, 2, out double first));
        Assert.True(surface.TryGetElevation(8, 8, out double second));
        Assert.Equal(2, first, 8);
        Assert.Equal(14, second, 8);
    }
}
