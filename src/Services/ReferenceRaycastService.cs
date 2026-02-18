using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class ReferenceRaycastService : IReferenceRaycastService
    {
        public bool CheckHitsReference(ReferenceIntersector intersector, XYZ pt)
        {
            ArgumentNullException.ThrowIfNull(intersector);
            ArgumentNullException.ThrowIfNull(pt);

            XYZ rayStart = new XYZ(pt.X, pt.Y, 10000);
            ReferenceWithContext hit = intersector.FindNearest(rayStart, XYZ.BasisZ.Negate());
            if (hit != null) return true;

            rayStart = new XYZ(pt.X, pt.Y, -1000);
            hit = intersector.FindNearest(rayStart, XYZ.BasisZ);
            return hit != null;
        }

        public XYZ? GetHitPoint(ReferenceIntersector intersector, XYZ pt)
        {
            ArgumentNullException.ThrowIfNull(intersector);
            ArgumentNullException.ThrowIfNull(pt);

            XYZ rayStart = new XYZ(pt.X, pt.Y, 10000);
            XYZ rayDir = XYZ.BasisZ.Negate();
            ReferenceWithContext hit = intersector.FindNearest(rayStart, rayDir);

            if (hit == null)
            {
                rayStart = new XYZ(pt.X, pt.Y, -1000);
                rayDir = XYZ.BasisZ;
                hit = intersector.FindNearest(rayStart, rayDir);
            }

            if (hit != null)
            {
                double proximity = hit.Proximity;
                return rayStart.Add(rayDir.Multiply(proximity));
            }

            return null;
        }
    }
}
