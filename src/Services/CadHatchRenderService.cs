using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadHatchRenderService : ICadHatchRenderService
    {
        private readonly ICadFilledRegionTypeService _filledRegionTypeService;
        private readonly ICadCurveFlattenService _curveFlattenService;

        public CadHatchRenderService(ICadFilledRegionTypeService filledRegionTypeService, ICadCurveFlattenService curveFlattenService)
        {
            _filledRegionTypeService = filledRegionTypeService;
            _curveFlattenService = curveFlattenService;
        }

        public int DrawHatches(
            Document familyDoc,
            IList<HatchData> hatches,
            Transform toOrigin,
            View planView,
            Action<double, string>? progress,
            double startPct,
            double endPct,
            int total,
            int current,
            int curveCount)
        {
            foreach (HatchData hatch in hatches)
            {
                current++;
                if (total > 0 && (current % Math.Max(1, total / 20) == 0 || current == total))
                {
                    progress?.Invoke(startPct + (endPct - startPct) * current / total, $"Drawing hatches... ({current - curveCount}/{hatches.Count})");
                }

                try
                {
                    FilledRegionType? frType = _filledRegionTypeService.GetOrCreateFilledRegionType(familyDoc, hatch.Color);
                    if (frType == null) continue;
                    List<CurveLoop> validLoops = new List<CurveLoop>();

                    foreach (CurveLoop loop in hatch.Loops)
                    {
                        CurveLoop newLoop = new CurveLoop();
                        foreach (Curve c in loop)
                        {
                            Curve transCurve = c.CreateTransformed(toOrigin);
                            IEnumerable<Curve>? flats = _curveFlattenService.FlattenCurve(transCurve);
                            if (flats != null)
                            {
                                foreach (Curve flat in flats) newLoop.Append(flat);
                            }
                        }

                        if (!newLoop.IsOpen() && newLoop.GetExactLength() > 0.001)
                        {
                            validLoops.Add(newLoop);
                        }
                    }

                    if (validLoops.Any())
                    {
                        FilledRegion.Create(familyDoc, frType.Id, planView.Id, validLoops);
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
