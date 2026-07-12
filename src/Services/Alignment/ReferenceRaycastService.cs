using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class ReferenceRaycastService : IReferenceRaycastService
    {
        private const double RayStartCeiling = 10000.0;

        private const double SearchStep = 0.2;

        private const double D45 = 0.70710678118;
        private const double D67 = 0.92387953251;
        private const double D22 = 0.38268343237;
        private static readonly XYZ[] SearchDirections =
        {
            new XYZ(1, 0, 0),
            new XYZ(D67, D22, 0),
            new XYZ(D45, D45, 0),
            new XYZ(D22, D67, 0),
            new XYZ(0, 1, 0),
            new XYZ(-D22, D67, 0),
            new XYZ(-D45, D45, 0),
            new XYZ(-D67, D22, 0),
            new XYZ(-1, 0, 0),
            new XYZ(-D67, -D22, 0),
            new XYZ(-D45, -D45, 0),
            new XYZ(-D22, -D67, 0),
            new XYZ(0, -1, 0),
            new XYZ(D22, -D67, 0),
            new XYZ(D45, -D45, 0),
            new XYZ(D67, -D22, 0)
        };

        public AlignEdgesHitInfo? GetHitInfo(ReferenceIntersector intersector, XYZ pt)
        {
            ArgumentNullException.ThrowIfNull(intersector);
            ArgumentNullException.ThrowIfNull(pt);

            XYZ rayStart = new XYZ(pt.X, pt.Y, RayStartCeiling);
            XYZ rayDir = XYZ.BasisZ.Negate();
            ReferenceWithContext hit = intersector.FindNearest(rayStart, rayDir);
            return hit != null ? CreateHitInfo(hit, rayStart, rayDir) : null;
        }

        public AlignEdgesHitInfo? GetNearestHitInfo(ReferenceIntersector intersector, XYZ pt, double maxRadius)
        {
            ArgumentNullException.ThrowIfNull(intersector);
            ArgumentNullException.ThrowIfNull(pt);

            AlignEdgesHitInfo? directHit = GetHitInfo(intersector, pt);
            if (directHit != null) return directHit;

            // Collect hits per ring and fit a plane to compensate for slope.
            // On sloped surfaces, a single offset hit gives the wrong Z for the query point.
            for (double r = SearchStep; r <= maxRadius; r += SearchStep)
            {
                double sumDx = 0, sumDy = 0, sumZ = 0;
                double sumDx2 = 0, sumDy2 = 0, sumDxDy = 0;
                double sumDxZ = 0, sumDyZ = 0;
                int hitCount = 0;
                var hitIdCounts = new Dictionary<long, int>();

                foreach (XYZ dir in SearchDirections)
                {
                    double dx = dir.X * r;
                    double dy = dir.Y * r;

                    XYZ rayStart = new XYZ(pt.X + dx, pt.Y + dy, RayStartCeiling);
                    ReferenceWithContext hit = intersector.FindNearest(rayStart, XYZ.BasisZ.Negate());

                    if (hit == null) continue;

                    double z = RayStartCeiling - hit.Proximity;
                    ElementId hitId = hit.GetReference()?.ElementId ?? ElementId.InvalidElementId;
                    if (hitId != ElementId.InvalidElementId)
                    {
                        hitIdCounts.TryGetValue(hitId.Value, out int count);
                        hitIdCounts[hitId.Value] = count + 1;
                    }

                    sumDx += dx; sumDy += dy; sumZ += z;
                    sumDx2 += dx * dx; sumDy2 += dy * dy; sumDxDy += dx * dy;
                    sumDxZ += dx * z; sumDyZ += dy * z;
                    hitCount++;
                }

                if (hitCount < 2) continue;

                double meanDx = sumDx / hitCount;
                double meanDy = sumDy / hitCount;
                double meanZ = sumZ / hitCount;

                // 3+ hits: least-squares plane fit, evaluate at query center (dx=0, dy=0)
                if (hitCount >= 3)
                {
                    double sxx = sumDx2 - hitCount * meanDx * meanDx;
                    double syy = sumDy2 - hitCount * meanDy * meanDy;
                    double sxy = sumDxDy - hitCount * meanDx * meanDy;
                    double sxz = sumDxZ - hitCount * meanDx * meanZ;
                    double syz = sumDyZ - hitCount * meanDy * meanZ;

                    double det = sxx * syy - sxy * sxy;
                    if (Math.Abs(det) > 1e-12)
                    {
                        double a = (syy * sxz - sxy * syz) / det;
                        double b = (sxx * syz - sxy * sxz) / det;
                        double fitZ = meanZ - a * meanDx - b * meanDy;
                        return new AlignEdgesHitInfo(new XYZ(pt.X, pt.Y, fitZ), ResolveDominantElementId(hitIdCounts));
                    }
                }

                return new AlignEdgesHitInfo(new XYZ(pt.X, pt.Y, meanZ), ResolveDominantElementId(hitIdCounts));
            }

            return null;
        }

        private static AlignEdgesHitInfo CreateHitInfo(ReferenceWithContext hit, XYZ rayStart, XYZ rayDir)
        {
            XYZ point = rayStart.Add(rayDir.Multiply(hit.Proximity));
            ElementId elementId = hit.GetReference()?.ElementId ?? ElementId.InvalidElementId;
            return new AlignEdgesHitInfo(point, elementId);
        }

        private static ElementId ResolveDominantElementId(IReadOnlyDictionary<long, int> hitIdCounts)
        {
            if (hitIdCounts.Count == 0)
            {
                return ElementId.InvalidElementId;
            }

            long dominantId = hitIdCounts
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key)
                .First().Key;

            return new ElementId(dominantId);
        }
    }
}
