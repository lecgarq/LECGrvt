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
        private readonly ICadHatchProgressService _cadHatchProgressService;
        private readonly ICadHatchLoopPreparationService _cadHatchLoopPreparationService;

        public CadHatchRenderService(ICadFilledRegionTypeService filledRegionTypeService, ICadHatchProgressService cadHatchProgressService, ICadHatchLoopPreparationService cadHatchLoopPreparationService)
        {
            _filledRegionTypeService = filledRegionTypeService;
            _cadHatchProgressService = cadHatchProgressService;
            _cadHatchLoopPreparationService = cadHatchLoopPreparationService;
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
            ArgumentNullException.ThrowIfNull(familyDoc);
            ArgumentNullException.ThrowIfNull(hatches);
            ArgumentNullException.ThrowIfNull(toOrigin);
            ArgumentNullException.ThrowIfNull(planView);

            foreach (HatchData hatch in hatches)
            {
                current++;
                if (_cadHatchProgressService.ShouldReport(total, current))
                {
                    progress?.Invoke(_cadHatchProgressService.ToPercent(startPct, endPct, current, total), $"Drawing hatches... ({current - curveCount}/{hatches.Count})");
                }

                try
                {
                    FilledRegionType? frType = _filledRegionTypeService.GetOrCreateFilledRegionType(familyDoc, hatch.Color);
                    if (frType == null) continue;
                    List<CurveLoop> validLoops = _cadHatchLoopPreparationService.PrepareLoops(hatch, toOrigin);

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
