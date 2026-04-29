using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class CadCurveTessellationService : ICadCurveTessellationService
    {
        private readonly ICadPointFlattenService _cadPointFlattenService;

        public CadCurveTessellationService(ICadPointFlattenService cadPointFlattenService)
        {
            _cadPointFlattenService = cadPointFlattenService;
        }

        public IEnumerable<Curve>? Tessellate(Curve c)
        {
            ArgumentNullException.ThrowIfNull(c);

            IList<XYZ> points = c.Tessellate();
            if (points.Count < 2)
            {
                return null;
            }

            List<Curve> lines = new List<Curve>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                XYZ p1 = _cadPointFlattenService.Flatten(points[i]);
                XYZ p2 = _cadPointFlattenService.Flatten(points[i + 1]);
                if (p1.DistanceTo(p2) >= 0.005)
                {
                    try
                    {
                        lines.Add(Line.CreateBound(p1, p2));
                    }
                    catch (Exception ex) when (IsExpectedCadCurveTessellationException(ex))
                    {
                        Logging.Logger.Instance.LogWarning($"Line creation failed: {ex.Message}", nameof(CadCurveTessellationService), ex);
                    }
                }
            }

            return lines;
        }

        private static bool IsExpectedCadCurveTessellationException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }
    }
}
