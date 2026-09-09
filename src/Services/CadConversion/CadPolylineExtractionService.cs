using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class CadPolylineExtractionService
    {
        private readonly ILogger _logger;

        public CadPolylineExtractionService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

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
                        _logger.LogWarning($"Line creation failed: {ex.Message}", scope: "CadPolylineExtraction");
                    }
                }
            }

            return result;
        }

        private static bool IsExpectedCadPolylineException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }
    }
}
