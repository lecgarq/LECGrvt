using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadGeometryExtractionService : ICadGeometryExtractionService
    {
        private readonly ICadSolidHatchExtractionService _cadSolidHatchExtractionService;

        public CadGeometryExtractionService() : this(new CadSolidHatchExtractionService())
        {
        }

        public CadGeometryExtractionService(ICadSolidHatchExtractionService cadSolidHatchExtractionService)
        {
            _cadSolidHatchExtractionService = cadSolidHatchExtractionService;
        }

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
                List<HatchData> hatches = _cadSolidHatchExtractionService.Extract(doc, obj, solid, currentTransform);
                foreach (HatchData hatch in hatches)
                {
                    data.Hatches.Add(hatch);
                }
            }
        }
    }
}
