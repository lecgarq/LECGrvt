using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IAlignEdgesHitPointProjectionService
    {
        XYZ? ResolveHitPoint(ReferenceIntersector intersector, XYZ sketchPt, XYZ curveMid);
    }
}
