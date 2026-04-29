using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IAlignEdgesHitPointProjectionService
    {
        AlignEdgesHitInfo? ResolveHit(ReferenceIntersector intersector, XYZ sketchPt, XYZ curveMid);
        XYZ? ResolveHitPoint(ReferenceIntersector intersector, XYZ sketchPt, XYZ curveMid);
    }
}
