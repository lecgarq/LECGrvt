using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadCurveRenderService : ICadCurveRenderService
    {
        private readonly ICadCurveFlattenService _curveFlattenService;

        public CadCurveRenderService(ICadCurveFlattenService curveFlattenService)
        {
            _curveFlattenService = curveFlattenService;
        }

        public int DrawCurves(
            Document familyDoc,
            IList<Curve> curves,
            Transform toOrigin,
            View planView,
            GraphicsStyle lineStyle,
            Action<double, string>? progress,
            double startPct,
            double endPct,
            int total,
            int current)
        {
            ArgumentNullException.ThrowIfNull(familyDoc);
            ArgumentNullException.ThrowIfNull(curves);
            ArgumentNullException.ThrowIfNull(toOrigin);
            ArgumentNullException.ThrowIfNull(planView);
            ArgumentNullException.ThrowIfNull(lineStyle);

            foreach (Curve c in curves)
            {
                current++;
                if (total > 0 && (current % Math.Max(1, total / 20) == 0 || current == total))
                {
                    progress?.Invoke(startPct + (endPct - startPct) * current / total, $"Drawing curves... ({current}/{curves.Count})");
                }

                try
                {
                    Curve transCurve = c.CreateTransformed(toOrigin);
                    IEnumerable<Curve>? flats = _curveFlattenService.FlattenCurve(transCurve);

                    if (flats != null)
                    {
                        foreach (Curve flat in flats)
                        {
                            DetailCurve dc = familyDoc.FamilyCreate.NewDetailCurve(planView, flat);
                            if (dc != null) dc.LineStyle = lineStyle;
                        }
                    }
                }
                catch
                {
                }
            }

            return current;
        }
    }
}
