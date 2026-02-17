using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadPolylineExtractionService : ICadPolylineExtractionService
    {
        public List<Curve> Extract(PolyLine poly, Transform currentTransform)
        {
            List<Curve> result = new List<Curve>();
            IList<XYZ> points = poly.GetCoordinates();
            for (int i = 0; i < points.Count - 1; i++)
            {
                XYZ p1 = currentTransform.OfPoint(points[i]);
                XYZ p2 = currentTransform.OfPoint(points[i + 1]);
                result.Add(Line.CreateBound(p1, p2));
            }

            return result;
        }
    }
}
