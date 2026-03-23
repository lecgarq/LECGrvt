using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IReferenceRaycastService
    {
        bool CheckHitsReference(ReferenceIntersector intersector, XYZ pt);
        AlignEdgesHitInfo? GetHitInfo(ReferenceIntersector intersector, XYZ pt);
        XYZ? GetHitPoint(ReferenceIntersector intersector, XYZ pt);
        AlignEdgesHitInfo? GetNearestHitInfo(ReferenceIntersector intersector, XYZ pt, double maxRadius);
        XYZ? GetNearestHitPoint(ReferenceIntersector intersector, XYZ pt, double maxRadius);
    }
}
