using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadCurveFlattenService : ICadCurveFlattenService
    {
        private readonly ICadCurveTessellationService _cadCurveTessellationService;
        private readonly ICadPointFlattenService _cadPointFlattenService;

        public CadCurveFlattenService() : this(new CadCurveTessellationService(), new CadPointFlattenService())
        {
        }

        public CadCurveFlattenService(ICadCurveTessellationService cadCurveTessellationService, ICadPointFlattenService cadPointFlattenService)
        {
            _cadCurveTessellationService = cadCurveTessellationService;
            _cadPointFlattenService = cadPointFlattenService;
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
                else if (c is HermiteSpline hs)
                {
                    IList<XYZ> pts = hs.ControlPoints.Select(p => _cadPointFlattenService.Flatten(p)).ToList();
                    var cleanPts = new List<XYZ> { pts[0] };
                    for (int i = 1; i < pts.Count; i++)
                    {
                        if (!pts[i].IsAlmostEqualTo(cleanPts.Last(), 0.001)) cleanPts.Add(pts[i]);
                    }
                    if (cleanPts.Count >= 2)
                    {
                        return new List<Curve> { HermiteSpline.Create(cleanPts, hs.IsPeriodic) };
                    }
                    return _cadCurveTessellationService.Tessellate(c);
                }
                else if (c is NurbSpline ns)
                {
                    IList<XYZ> ctrls = ns.CtrlPoints.Select(p => _cadPointFlattenService.Flatten(p)).ToList();
                    try
                    {
                        return new List<Curve> { NurbSpline.CreateCurve(ns.Degree, ToList(ns.Knots), ctrls, ToList(ns.Weights)) };
                    }
                    catch
                    {
                        return _cadCurveTessellationService.Tessellate(c);
                    }
                }

                return _cadCurveTessellationService.Tessellate(c);
            }
            catch
            {
                return _cadCurveTessellationService.Tessellate(c);
            }
        }

        private IList<double> ToList(DoubleArray da)
        {
            var list = new List<double>();
            for (int i = 0; i < da.Size; i++)
            {
                list.Add(da.get_Item(i));
            }
            return list;
        }

    }
}
