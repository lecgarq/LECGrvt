using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class CadPolylineExtractionService : ICadPolylineExtractionService
    {
        public List<Curve> Extract(PolyLine poly, Transform currentTransform)
        {
            ArgumentNullException.ThrowIfNull(poly);
            ArgumentNullException.ThrowIfNull(currentTransform);

            List<Curve> result = new List<Curve>();
            IList<XYZ> points = poly.GetCoordinates();
            for (int i = 0; i < points.Count - 1; i++)
            {
                XYZ p1 = currentTransform.OfPoint(points[i]);
                XYZ p2 = currentTransform.OfPoint(points[i + 1]);

                if (p1.DistanceTo(p2) >= 0.005)
                {
                    try
                    {
                        result.Add(Line.CreateBound(p1, p2));
                    }
                    catch (Exception ex) when (IsExpectedCadPolylineException(ex))
                    {
                        Logging.Logger.Instance.LogWarning($"[CadPolylineExtractionService] Line creation failed: {ex.Message}");
                    }
                }
            }

            return result;
        }

        private static bool IsExpectedCadPolylineException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.InvalidOperationException;
        }
    }
}
