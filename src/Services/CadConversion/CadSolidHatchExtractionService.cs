using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadSolidHatchExtractionService : ICadSolidHatchExtractionService
    {
        public List<HatchData> Extract(Document doc, GeometryObject sourceObject, Solid solid, Transform currentTransform)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(sourceObject);
            ArgumentNullException.ThrowIfNull(solid);
            ArgumentNullException.ThrowIfNull(currentTransform);

            List<HatchData> result = new List<HatchData>();
            Color c = GetColor(doc, sourceObject.GraphicsStyleId);

            foreach (Face face in solid.Faces)
            {
                if (face is PlanarFace pf)
                {
                    XYZ normal = currentTransform.OfVector(pf.FaceNormal).Normalize();
                    if (normal.IsAlmostEqualTo(XYZ.BasisZ) || normal.IsAlmostEqualTo(-XYZ.BasisZ))
                    {
                        var loops = pf.GetEdgesAsCurveLoops();
                        List<CurveLoop> transformedLoops = new List<CurveLoop>();
                        foreach (CurveLoop loop in loops)
                        {
                            CurveLoop tLoop = new CurveLoop();
                            foreach (Curve loopCrv in loop)
                            {
                                tLoop.Append(loopCrv.CreateTransformed(currentTransform));
                            }

                            transformedLoops.Add(tLoop);
                        }

                        if (transformedLoops.Count > 0)
                        {
                            result.Add(new HatchData { Color = c, Loops = transformedLoops });
                        }
                    }
                }
            }

            return result;
        }

        private Color GetColor(Document doc, ElementId gsId)
        {
            if (gsId == ElementId.InvalidElementId) return new Color(0, 0, 0);
            if (doc.GetElement(gsId) is GraphicsStyle gs && gs.GraphicsStyleCategory != null) return gs.GraphicsStyleCategory.LineColor;
            return new Color(0, 0, 0);
        }
    }
}
