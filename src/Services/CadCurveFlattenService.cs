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

        public CadCurveFlattenService() : this(new CadCurveTessellationService(), new CadPointFlattenService(), new CadDoubleArrayConversionService(), new CadSplineFlattenService(new CadCurveTessellationService(), new CadPointFlattenService(), new CadDoubleArrayConversionService()))
        {
        }

        public CadCurveFlattenService(ICadCurveTessellationService cadCurveTessellationService, ICadPointFlattenService cadPointFlattenService, ICadDoubleArrayConversionService cadDoubleArrayConversionService, ICadSplineFlattenService cadSplineFlattenService)
        {
            _cadCurveTessellationService = cadCurveTessellationService;
            _cadPointFlattenService = cadPointFlattenService;
            _cadDoubleArrayConversionService = cadDoubleArrayConversionService;
            _cadSplineFlattenService = cadSplineFlattenService;
        }

        public IEnumerable<Curve>? FlattenCurve(Curve c)
        {
            try
            {
                if (c is Line l)
                {
                    XYZ p0 = _cadPointFlattenService.Flatten(l.GetEndPoint(0));
                    XYZ p1 = _cadPointFlattenService.Flatten(l.GetEndPoint(1));
                    if (p0.IsAlmostEqualTo(p1)) return null;
                    return new List<Curve> { Line.CreateBound(p0, p1) };
                }
                else if (c is Arc a)
                {
                    XYZ p0 = _cadPointFlattenService.Flatten(a.GetEndPoint(0));
                    XYZ p1 = _cadPointFlattenService.Flatten(a.GetEndPoint(1));
                    XYZ mid = _cadPointFlattenService.Flatten(a.Evaluate(0.5, true));

                    if (p0.IsAlmostEqualTo(p1))
                    {
                        XYZ center = _cadPointFlattenService.Flatten(a.Center);
                        if (a.Radius < 0.001) return null;
                        return new List<Curve> { Arc.Create(center, a.Radius, 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY) };
                    }

                    return new List<Curve> { Arc.Create(p0, p1, mid) };
                }
                else if (c is Ellipse e)
                {
                    return new List<Curve> { Ellipse.CreateCurve(_cadPointFlattenService.Flatten(e.Center), e.RadiusX, e.RadiusY, XYZ.BasisX, XYZ.BasisY, e.GetEndParameter(0), e.GetEndParameter(1)) };
                }
                else if (c is HermiteSpline || c is NurbSpline)
                {
                    return _cadSplineFlattenService.Flatten(c);
                }

                return _cadCurveTessellationService.Tessellate(c);
            }
            catch
            {
                return _cadCurveTessellationService.Tessellate(c);
            }
        }

    }
}
