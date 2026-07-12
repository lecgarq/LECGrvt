using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IReferenceRaycastService
    {
        AlignEdgesHitInfo? GetHitInfo(ReferenceIntersector intersector, XYZ pt);
        AlignEdgesHitInfo? GetNearestHitInfo(ReferenceIntersector intersector, XYZ pt, double maxRadius);
    }
}
