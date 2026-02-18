using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class AlignEdgesHitPointProjectionService : IAlignEdgesHitPointProjectionService
    {
        private readonly IReferenceRaycastService _referenceRaycastService;

        public AlignEdgesHitPointProjectionService(IReferenceRaycastService referenceRaycastService)
        {
            _referenceRaycastService = referenceRaycastService;
        }

        public XYZ? ResolveHitPoint(ReferenceIntersector intersector, XYZ sketchPt, XYZ curveMid)
        {
            XYZ? hitPt = _referenceRaycastService.GetHitPoint(intersector, sketchPt);
            if (hitPt != null)
            {
                return hitPt;
            }

            XYZ dir = (curveMid - sketchPt).Normalize();
            for (double offset = 0.5; offset <= 1.5 && hitPt == null; offset += 0.5)
            {
                XYZ testPt = sketchPt.Add(dir.Multiply(offset));
                hitPt = _referenceRaycastService.GetHitPoint(intersector, testPt);
                if (hitPt != null)
                {
                    hitPt = new XYZ(sketchPt.X, sketchPt.Y, hitPt.Z);
                }
            }

            return hitPt;
        }
    }
}
