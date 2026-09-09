using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class CadSplineFlattenService
    {
        private readonly CadCurveTessellationService _cadCurveTessellationService;
        private readonly CadPointFlattenService _cadPointFlattenService;
        private readonly CadDoubleArrayConversionService _cadDoubleArrayConversionService;

        public CadSplineFlattenService(
            CadCurveTessellationService cadCurveTessellationService,
            CadPointFlattenService cadPointFlattenService,
            CadDoubleArrayConversionService cadDoubleArrayConversionService)
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
                if (points.Count == 0)
                {
                    return _cadCurveTessellationService.Tessellate(curve);
                }

                var cleanPoints = new List<XYZ> { points[0] };

                for (int i = 1; i < points.Count; i++)
                {
                    if (points[i].DistanceTo(cleanPoints.Last()) >= 0.005)
                    {
                        cleanPoints.Add(points[i]);
                    }
                }

                if (cleanPoints.Count >= 2)
                {
                    var newSpline = HermiteSpline.Create(cleanPoints, hermiteSpline.IsPeriodic);
                    if (newSpline.Length >= 0.005) return new List<Curve> { newSpline };
                }

                return _cadCurveTessellationService.Tessellate(curve);
            }

            if (curve is NurbSpline nurbSpline)
            {
                IList<XYZ> controlPoints = nurbSpline.CtrlPoints.Select(p => _cadPointFlattenService.Flatten(p)).ToList();

                try
                {
                    var newSpline = NurbSpline.CreateCurve(
                            nurbSpline.Degree,
                            _cadDoubleArrayConversionService.ToList(nurbSpline.Knots),
                            controlPoints,
                            _cadDoubleArrayConversionService.ToList(nurbSpline.Weights));

                    if (newSpline.Length < 0.005) return _cadCurveTessellationService.Tessellate(curve);

                    return new List<Curve> { newSpline };
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
