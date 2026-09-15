namespace LECG.Core.Geometry;

public readonly record struct SurfacePoint(double X, double Y, double Z);

public readonly record struct SurfaceTriangle(SurfacePoint A, SurfacePoint B, SurfacePoint C)
{
    public SurfacePoint Centroid => new(
        (A.X + B.X + C.X) / 3,
        (A.Y + B.Y + C.Y) / 3,
        (A.Z + B.Z + C.Z) / 3);
}

/// <summary>Samples the actual triangulated top face in model coordinates.</summary>
public sealed class TriangleSurface
{
    private readonly IReadOnlyList<SurfaceTriangle> _triangles;

    public TriangleSurface(IReadOnlyList<SurfaceTriangle> triangles)
    {
        ArgumentNullException.ThrowIfNull(triangles);
        if (triangles.Count == 0) throw new ArgumentException("A surface needs at least one triangle.", nameof(triangles));
        _triangles = triangles;
    }

    public IReadOnlyList<SurfaceTriangle> Triangles => _triangles;

    public bool TryGetElevation(double x, double y, out double elevation, double maxExtrapolation = 0)
    {
        elevation = 0;
        double nearestDistance = double.PositiveInfinity;
        double nearestElevation = 0;
        bool foundInside = false;

        foreach (SurfaceTriangle triangle in _triangles)
        {
            if (x < Math.Min(triangle.A.X, Math.Min(triangle.B.X, triangle.C.X)) - maxExtrapolation
                || x > Math.Max(triangle.A.X, Math.Max(triangle.B.X, triangle.C.X)) + maxExtrapolation
                || y < Math.Min(triangle.A.Y, Math.Min(triangle.B.Y, triangle.C.Y)) - maxExtrapolation
                || y > Math.Max(triangle.A.Y, Math.Max(triangle.B.Y, triangle.C.Y)) + maxExtrapolation)
                continue;
            if (!TryBarycentric(triangle, x, y, out double a, out double b, out double c))
                continue;

            double z = a * triangle.A.Z + b * triangle.B.Z + c * triangle.C.Z;
            if (a >= -1e-9 && b >= -1e-9 && c >= -1e-9)
            {
                elevation = foundInside ? Math.Max(elevation, z) : z;
                foundInside = true;
                continue;
            }

            if (foundInside || maxExtrapolation <= 0) continue;
            double distance = Math.Min(
                DistanceToSegment(x, y, triangle.A, triangle.B),
                Math.Min(DistanceToSegment(x, y, triangle.B, triangle.C),
                         DistanceToSegment(x, y, triangle.C, triangle.A)));
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestElevation = z;
            }
        }

        if (foundInside) return true;
        if (nearestDistance > maxExtrapolation) return false;
        elevation = nearestElevation;
        return true;
    }

    private static bool TryBarycentric(SurfaceTriangle t, double x, double y,
        out double a, out double b, out double c)
    {
        double denominator = (t.B.Y - t.C.Y) * (t.A.X - t.C.X)
                           + (t.C.X - t.B.X) * (t.A.Y - t.C.Y);
        a = b = c = 0;
        if (Math.Abs(denominator) < 1e-12) return false;
        a = ((t.B.Y - t.C.Y) * (x - t.C.X)
           + (t.C.X - t.B.X) * (y - t.C.Y)) / denominator;
        b = ((t.C.Y - t.A.Y) * (x - t.C.X)
           + (t.A.X - t.C.X) * (y - t.C.Y)) / denominator;
        c = 1 - a - b;
        return true;
    }

    private static double DistanceToSegment(double x, double y, SurfacePoint a, SurfacePoint b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lengthSquared = dx * dx + dy * dy;
        double t = lengthSquared == 0 ? 0 : Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / lengthSquared, 0, 1);
        double gapX = x - a.X - t * dx;
        double gapY = y - a.Y - t * dy;
        return Math.Sqrt(gapX * gapX + gapY * gapY);
    }
}
