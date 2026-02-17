using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IAlignEdgesCurveHitService
    {
        bool CurveHitsReference(ReferenceIntersector intersector, Curve curve);
    }
}
