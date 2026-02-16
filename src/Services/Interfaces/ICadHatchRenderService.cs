using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services;

namespace LECG.Services.Interfaces
{
    public interface ICadHatchRenderService
    {
        int DrawHatches(
            Document familyDoc,
            IList<HatchData> hatches,
            Transform toOrigin,
            View planView,
            Action<double, string>? progress,
            double startPct,
            double endPct,
            int total,
            int current,
            int curveCount);
    }
}
