using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IReferenceRaycastService
    {
        bool CheckHitsReference(ReferenceIntersector intersector, XYZ pt);
        XYZ? GetHitPoint(ReferenceIntersector intersector, XYZ pt);
    }
}
