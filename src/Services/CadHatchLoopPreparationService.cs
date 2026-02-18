using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System.Collections.Generic;

namespace LECG.Services
{
    public class CadHatchLoopPreparationService : ICadHatchLoopPreparationService
    {
        private readonly ICadCurveFlattenService _curveFlattenService;

        public CadHatchLoopPreparationService(ICadCurveFlattenService curveFlattenService)
        {
            _curveFlattenService = curveFlattenService;
        }

        public List<CurveLoop> PrepareLoops(HatchData hatch, Transform toOrigin)
        {
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
                        foreach (Curve flat in flats)
                        {
                            newLoop.Append(flat);
                        }
                    }
                }

                if (!newLoop.IsOpen() && newLoop.GetExactLength() > 0.001)
                {
                    validLoops.Add(newLoop);
                }
            }

            return validLoops;
        }
    }
}
