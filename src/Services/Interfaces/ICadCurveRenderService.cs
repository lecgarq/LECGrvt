using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadCurveRenderService
    {
        int DrawCurves(
            Document familyDoc,
            IList<Curve> curves,
            Transform toOrigin,
            View planView,
            GraphicsStyle lineStyle,
            Action<double, string>? progress,
            double startPct,
            double endPct,
            int total,
            int current);
    }
}
