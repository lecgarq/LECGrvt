using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class AlignEdgesCurveHitService : IAlignEdgesCurveHitService
    {
        private readonly IReferenceRaycastService _referenceRaycastService;

        public AlignEdgesCurveHitService(IReferenceRaycastService referenceRaycastService)
        {
            _referenceRaycastService = referenceRaycastService;
        }

        public bool CurveHitsReference(ReferenceIntersector intersector, Curve curve)
        {
            ArgumentNullException.ThrowIfNull(intersector);
            ArgumentNullException.ThrowIfNull(curve);

            for (int sample = 0; sample <= 8; sample++)
            {
                double sampleT = sample / 8.0;
                XYZ samplePt = curve.Evaluate(sampleT, true);
                if (_referenceRaycastService.CheckHitsReference(intersector, samplePt))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
