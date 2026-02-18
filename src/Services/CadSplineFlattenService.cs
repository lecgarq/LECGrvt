using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class CadSplineFlattenService : ICadSplineFlattenService
    {
        private readonly ICadCurveTessellationService _cadCurveTessellationService;
        private readonly ICadPointFlattenService _cadPointFlattenService;
        private readonly ICadDoubleArrayConversionService _cadDoubleArrayConversionService;

        public CadSplineFlattenService(
            ICadCurveTessellationService cadCurveTessellationService,
            ICadPointFlattenService cadPointFlattenService,
            ICadDoubleArrayConversionService cadDoubleArrayConversionService)
        {
            _cadCurveTessellationService = cadCurveTessellationService;
            _cadPointFlattenService = cadPointFlattenService;
            _cadDoubleArrayConversionService = cadDoubleArrayConversionService;
        }

        public IEnumerable<Curve>? Flatten(Curve curve)
        {
            if (curve is HermiteSpline hermiteSpline)
            {
                IList<XYZ> points = hermiteSpline.ControlPoints.Select(p => _cadPointFlattenService.Flatten(p)).ToList();
                var cleanPoints = new List<XYZ> { points[0] };

                for (int i = 1; i < points.Count; i++)
                {
                    if (!points[i].IsAlmostEqualTo(cleanPoints.Last(), 0.001))
                    {
                        cleanPoints.Add(points[i]);
                    }
                }

                if (cleanPoints.Count >= 2)
                {
                    return new List<Curve> { HermiteSpline.Create(cleanPoints, hermiteSpline.IsPeriodic) };
                }

                return _cadCurveTessellationService.Tessellate(curve);
            }

            if (curve is NurbSpline nurbSpline)
            {
                IList<XYZ> controlPoints = nurbSpline.CtrlPoints.Select(p => _cadPointFlattenService.Flatten(p)).ToList();

                try
                {
                    return new List<Curve>
                    {
                        NurbSpline.CreateCurve(
                            nurbSpline.Degree,
                            _cadDoubleArrayConversionService.ToList(nurbSpline.Knots),
                            controlPoints,
                            _cadDoubleArrayConversionService.ToList(nurbSpline.Weights))
                    };
                }
                catch
                {
                    return _cadCurveTessellationService.Tessellate(curve);
                }
            }

            return null;
        }
    }
}
