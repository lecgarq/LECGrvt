using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class CadCurveFlattenService : ICadCurveFlattenService
    {
        private readonly ICadCurveTessellationService _cadCurveTessellationService;
        private readonly ICadPointFlattenService _cadPointFlattenService;
        private readonly ICadDoubleArrayConversionService _cadDoubleArrayConversionService;
        private readonly ICadSplineFlattenService _cadSplineFlattenService;

        public CadCurveFlattenService(ICadCurveTessellationService cadCurveTessellationService, ICadPointFlattenService cadPointFlattenService, ICadDoubleArrayConversionService cadDoubleArrayConversionService, ICadSplineFlattenService cadSplineFlattenService)
        {
            _cadCurveTessellationService = cadCurveTessellationService;
            _cadPointFlattenService = cadPointFlattenService;
            _cadDoubleArrayConversionService = cadDoubleArrayConversionService;
            _cadSplineFlattenService = cadSplineFlattenService;
        }

        public IEnumerable<Curve>? FlattenCurve(Curve c)
        {
            const double minLength = 0.005; // Slightly above standard tolerance to be safe

            try
            {
                if (c is Line l)
                {
                    XYZ p0 = _cadPointFlattenService.Flatten(l.GetEndPoint(0));
                    XYZ p1 = _cadPointFlattenService.Flatten(l.GetEndPoint(1));

                    if (p0.DistanceTo(p1) < minLength) return null;
                    return TryCreateLine(p0, p1);
                }
                else if (c is Arc a)
                {
                    XYZ p0 = _cadPointFlattenService.Flatten(a.GetEndPoint(0));
                    XYZ p1 = _cadPointFlattenService.Flatten(a.GetEndPoint(1));
                    XYZ mid = _cadPointFlattenService.Flatten(a.Evaluate(0.5, true));

                    if (p0.DistanceTo(p1) < minLength && a.Length < minLength) return null;

                    if (p0.IsAlmostEqualTo(p1))
                    {
                        XYZ center = _cadPointFlattenService.Flatten(a.Center);
                        if (a.Radius < minLength) return null;
                        return TryCreateCircle(center, a.Radius);
                    }

                    if (p0.DistanceTo(mid) < minLength || p1.DistanceTo(mid) < minLength || p0.DistanceTo(p1) < minLength)
                    {
                        if (p0.DistanceTo(p1) >= minLength)
                        {
                            return TryCreateLine(p0, p1);
                        }
                        return null;
                    }

                    // Check for collinearity after flattening
                    XYZ v1 = (mid - p0).Normalize();
                    XYZ v2 = (p1 - mid).Normalize();
                    if (v1.IsAlmostEqualTo(v2, 0.0001) || v1.IsAlmostEqualTo(-v2, 0.0001))
                    {
                        return TryCreateLine(p0, p1);
                    }

                    return TryCreateArc(p0, p1, mid);
                }
                else if (c is Ellipse e)
                {
                    if (e.RadiusX < minLength || e.RadiusY < minLength) return null;
                    return TryCreateEllipse(
                        _cadPointFlattenService.Flatten(e.Center),
                        e.RadiusX,
                        e.RadiusY,
                        e.GetEndParameter(0),
                        e.GetEndParameter(1),
                        minLength);
                }
                else if (c is HermiteSpline || c is NurbSpline)
                {
                    return _cadSplineFlattenService.Flatten(c);
                }

                var tessellated = _cadCurveTessellationService.Tessellate(c);
                return tessellated?.Where(tc => tc.Length >= minLength);
            }
            catch (Exception ex) when (IsExpectedCadCurveException(ex))
            {
                var tessellated = _cadCurveTessellationService.Tessellate(c);
                return tessellated?.Where(tc => tc.Length >= minLength);
            }
        }

        private static List<Curve>? TryCreateLine(XYZ p0, XYZ p1)
        {
            try
            {
                return new List<Curve> { Line.CreateBound(p0, p1) };
            }
            catch (Exception ex) when (IsExpectedCadCurveException(ex))
            {
                return null;
            }
        }

        private static List<Curve>? TryCreateCircle(XYZ center, double radius)
        {
            try
            {
                return new List<Curve> { Arc.Create(center, radius, 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY) };
            }
            catch (Exception ex) when (IsExpectedCadCurveException(ex))
            {
                return null;
            }
        }

        private static List<Curve>? TryCreateArc(XYZ p0, XYZ p1, XYZ mid)
        {
            try
            {
                return new List<Curve> { Arc.Create(p0, p1, mid) };
            }
            catch (Exception ex) when (IsExpectedCadCurveException(ex))
            {
                return null;
            }
        }

        private static List<Curve>? TryCreateEllipse(XYZ center, double radiusX, double radiusY, double startParameter, double endParameter, double minLength)
        {
            try
            {
                Curve ellipse = Ellipse.CreateCurve(center, radiusX, radiusY, XYZ.BasisX, XYZ.BasisY, startParameter, endParameter);
                if (ellipse.Length < minLength)
                {
                    return null;
                }

                return new List<Curve> { ellipse };
            }
            catch (Exception ex) when (IsExpectedCadCurveException(ex))
            {
                return null;
            }
        }

        private static bool IsExpectedCadCurveException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.InvalidOperationException;
        }

    }
}
