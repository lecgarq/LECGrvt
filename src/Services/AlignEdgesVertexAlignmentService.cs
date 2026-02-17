using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class AlignEdgesVertexAlignmentService : IAlignEdgesVertexAlignmentService
    {
        public (int movedCount, int skippedCount, int missCount) AlignVertices(SlabShapeEditor editor, ReferenceIntersector intersector)
        {
            int movedCount = 0;
            int skippedCount = 0;
            int missCount = 0;

            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
            {
                XYZ origin = v.Position;

                XYZ rayStart = new XYZ(origin.X, origin.Y, origin.Z + 500);
                XYZ rayDir = XYZ.BasisZ.Negate();
                ReferenceWithContext hit = intersector.FindNearest(rayStart, rayDir);

                if (hit == null)
                {
                    rayStart = new XYZ(origin.X, origin.Y, origin.Z - 500);
                    rayDir = XYZ.BasisZ;
                    hit = intersector.FindNearest(rayStart, rayDir);
                }

                if (hit != null)
                {
                    double proximity = hit.Proximity;
                    XYZ hitPoint = rayStart.Add(rayDir.Multiply(proximity));
                    double delta = hitPoint.Z - origin.Z;

                    if (Math.Abs(delta) > 0.0164)
                    {
                        try
                        {
                            editor.ModifySubElement(v, delta);
                            movedCount++;
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                else
                {
                    missCount++;
                }
            }

            return (movedCount, skippedCount, missCount);
        }
    }
}
