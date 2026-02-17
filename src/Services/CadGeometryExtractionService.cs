using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadGeometryExtractionService : ICadGeometryExtractionService
    {
        private readonly ICadSolidHatchExtractionService _cadSolidHatchExtractionService;
        private readonly ICadPolylineExtractionService _cadPolylineExtractionService;

        public CadGeometryExtractionService() : this(new CadSolidHatchExtractionService(), new CadPolylineExtractionService())
        {
        }

        public CadGeometryExtractionService(ICadSolidHatchExtractionService cadSolidHatchExtractionService, ICadPolylineExtractionService cadPolylineExtractionService)
        {
            _cadSolidHatchExtractionService = cadSolidHatchExtractionService;
            _cadPolylineExtractionService = cadPolylineExtractionService;
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
                List<Curve> lines = _cadPolylineExtractionService.Extract(poly, currentTransform);
                foreach (Curve line in lines)
                {
                    data.Curves.Add(line);
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
