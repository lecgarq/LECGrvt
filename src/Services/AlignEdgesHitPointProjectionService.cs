using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class AlignEdgesHitPointProjectionService : IAlignEdgesHitPointProjectionService
    {
        // Radial fallback radius (~30cm) — only if inward nudge fails
        private const double FallbackRadius = 1.0;

        private readonly IReferenceRaycastService _referenceRaycastService;

        public AlignEdgesHitPointProjectionService(IReferenceRaycastService referenceRaycastService)
        {
            _referenceRaycastService = referenceRaycastService;
        }

        public AlignEdgesHitInfo? ResolveHit(ReferenceIntersector intersector, XYZ sketchPt, XYZ curveMid)
        {
            ArgumentNullException.ThrowIfNull(intersector);
            ArgumentNullException.ThrowIfNull(sketchPt);
            ArgumentNullException.ThrowIfNull(curveMid);

            AlignEdgesHitInfo? hit = _referenceRaycastService.GetHitInfo(intersector, sketchPt);
            if (hit != null) return hit;

            XYZ dir = (curveMid - sketchPt).Normalize();
            AlignEdgesHitInfo? firstHit = null;
            double firstHitOffset = 0;

            for (double offset = 0.5; offset <= 1.5; offset += 0.5)
            {
                XYZ testPt = sketchPt.Add(dir.Multiply(offset));
                AlignEdgesHitInfo? offsetHit = _referenceRaycastService.GetHitInfo(intersector, testPt);
                if (offsetHit == null)
                {
                    continue;
                }

                if (firstHit == null)
                {
                    firstHit = offsetHit;
                    firstHitOffset = offset;
                    continue;
                }

                double slope = (offsetHit.Value.Point.Z - firstHit.Value.Point.Z) / (offset - firstHitOffset);
                double projectedZ = firstHit.Value.Point.Z - firstHitOffset * slope;
                return new AlignEdgesHitInfo(new XYZ(sketchPt.X, sketchPt.Y, projectedZ), firstHit.Value.ElementId);
            }

            if (firstHit != null)
            {
                return new AlignEdgesHitInfo(new XYZ(sketchPt.X, sketchPt.Y, firstHit.Value.Point.Z), firstHit.Value.ElementId);
            }

            return _referenceRaycastService.GetNearestHitInfo(intersector, sketchPt, FallbackRadius);
        }

        public XYZ? ResolveHitPoint(ReferenceIntersector intersector, XYZ sketchPt, XYZ curveMid)
        {
            return ResolveHit(intersector, sketchPt, curveMid)?.Point;
        }
    }
}
