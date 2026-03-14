using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

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
                    try { return new List<Curve> { Line.CreateBound(p0, p1) }; } catch { return null; }
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
                        try { return new List<Curve> { Arc.Create(center, a.Radius, 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY) }; } catch { return null; }
                    }

                    if (p0.DistanceTo(mid) < minLength || p1.DistanceTo(mid) < minLength || p0.DistanceTo(p1) < minLength)
                    {
                         if (p0.DistanceTo(p1) >= minLength)
                         {
                             try { return new List<Curve> { Line.CreateBound(p0, p1) }; } catch { return null; }
                         }
                         return null;
                    }

                    // Check for collinearity after flattening
                    XYZ v1 = (mid - p0).Normalize();
                    XYZ v2 = (p1 - mid).Normalize();
                    if (v1.IsAlmostEqualTo(v2, 0.0001) || v1.IsAlmostEqualTo(-v2, 0.0001))
                    {
                        try { return new List<Curve> { Line.CreateBound(p0, p1) }; } catch { return null; }
                    }

                    try { return new List<Curve> { Arc.Create(p0, p1, mid) }; } catch { return null; }
                }
                else if (c is Ellipse e)
                {
                    if (e.RadiusX < minLength || e.RadiusY < minLength) return null;
                    try 
                    { 
                        var ellipse = Ellipse.CreateCurve(_cadPointFlattenService.Flatten(e.Center), e.RadiusX, e.RadiusY, XYZ.BasisX, XYZ.BasisY, e.GetEndParameter(0), e.GetEndParameter(1)); 
                        if (ellipse.Length < minLength) return null;
                        return new List<Curve> { ellipse };
                    } 
                    catch { return null; }
                }
                else if (c is HermiteSpline || c is NurbSpline)
                {
                    return _cadSplineFlattenService.Flatten(c);
                }

                var tessellated = _cadCurveTessellationService.Tessellate(c);
                return tessellated?.Where(tc => tc.Length >= minLength);
            }
            catch
            {
                var tessellated = _cadCurveTessellationService.Tessellate(c);
                return tessellated?.Where(tc => tc.Length >= minLength);
            }
        }

    }
}
