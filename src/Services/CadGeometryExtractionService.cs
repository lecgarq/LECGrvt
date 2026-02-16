using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadGeometryExtractionService : ICadGeometryExtractionService
    {
        public CadData ExtractGeometry(Document doc, ImportInstance imp)
        {
            CadData data = new CadData();
            GeometryElement geoElem = imp.get_Geometry(new Options());

            if (geoElem != null)
            {
                foreach (GeometryObject obj in geoElem)
                {
                    ProcessGeometryObject(obj, data, doc, Transform.Identity);
                }
            }
            return data;
        }

        private void ProcessGeometryObject(GeometryObject obj, CadData data, Document doc, Transform currentTransform)
        {
            if (obj is GeometryInstance geoInst)
            {
                Transform instTransform = currentTransform.Multiply(geoInst.Transform);
                GeometryElement symbolGeo = geoInst.GetSymbolGeometry();
                foreach (GeometryObject childObj in symbolGeo)
                {
                    ProcessGeometryObject(childObj, data, doc, instTransform);
                }
            }
            else if (obj is Curve crv)
            {
                data.Curves.Add(crv.CreateTransformed(currentTransform));
            }
            else if (obj is PolyLine poly)
            {
                IList<XYZ> points = poly.GetCoordinates();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    XYZ p1 = currentTransform.OfPoint(points[i]);
                    XYZ p2 = currentTransform.OfPoint(points[i + 1]);
                    data.Curves.Add(Line.CreateBound(p1, p2));
                }
            }
            else if (obj is Solid solid && !solid.Faces.IsEmpty)
            {
                Color c = GetColor(doc, obj.GraphicsStyleId);
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
                                data.Hatches.Add(new HatchData { Color = c, Loops = transformedLoops });
                        }
                    }
                }
            }
        }

        private Color GetColor(Document doc, ElementId gsId)
        {
            if (gsId == ElementId.InvalidElementId) return new Color(0, 0, 0);
            if (doc.GetElement(gsId) is GraphicsStyle gs && gs.GraphicsStyleCategory != null) return gs.GraphicsStyleCategory.LineColor;
            return new Color(0, 0, 0);
        }
    }
}
