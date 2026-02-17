using System;
using Autodesk.Revit.DB;
using LECG.Core;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadDataDrawService : ICadDataDrawService
    {
        private readonly ICadRenderContextService _renderContextService;
        private readonly ICadCurveRenderService _curveRenderService;
        private readonly ICadHatchRenderService _hatchRenderService;

        public CadDataDrawService(
            ICadRenderContextService renderContextService,
            ICadCurveRenderService curveRenderService,
            ICadHatchRenderService hatchRenderService)
        {
            _renderContextService = renderContextService;
            _curveRenderService = curveRenderService;
            _hatchRenderService = hatchRenderService;
        }

        public void Draw(Document familyDoc, CadData data, XYZ offset, string styleName, Color color, int weight, Action<double, string>? progress = null, double startPct = 50, double endPct = 90)
        {
            (GraphicsStyle lineStyle, View planView, Transform toOrigin) = _renderContextService.Create(familyDoc, offset, styleName, color, weight);

            int total = data.Curves.Count + data.Hatches.Count;
            int current = 0;

            current = _curveRenderService.DrawCurves(familyDoc, data.Curves, toOrigin, planView, lineStyle, progress, startPct, endPct, total, current);

            _hatchRenderService.DrawHatches(
                familyDoc,
                data.Hatches,
                toOrigin,
                planView,
                progress,
                startPct,
                endPct,
                total,
                current,
                data.Curves.Count);
        }
    }
}
