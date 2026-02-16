using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Core;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadGeometryOptimizationService : ICadGeometryOptimizationService
    {
        private readonly ICadCurveFlattenService _curveFlattenService;
        private readonly ICadLineMergeService _lineMergeService;

        public CadGeometryOptimizationService(ICadCurveFlattenService curveFlattenService, ICadLineMergeService lineMergeService)
        {
            _curveFlattenService = curveFlattenService;
            _lineMergeService = lineMergeService;
        }

        public CadData Optimize(CadData input)
        {
            CadData output = new CadData();
            output.Hatches.AddRange(input.Hatches);

            List<Curve> flatCurves = new List<Curve>();
            foreach (Curve c in input.Curves)
            {
                IEnumerable<Curve>? flats = _curveFlattenService.FlattenCurve(c);
                if (flats != null) flatCurves.AddRange(flats);
            }

            List<Line> lines = flatCurves.OfType<Line>().ToList();
            List<Curve> others = flatCurves.Where(c => c is not Line).ToList();
            List<Line> mergedLines = _lineMergeService.MergeCollinearLines(lines);

            output.Curves.AddRange(mergedLines);
            output.Curves.AddRange(others);
            return output;
        }
    }
}
