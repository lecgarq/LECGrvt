using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface ICadHatchLoopPreparationService
    {
        List<CurveLoop> PrepareLoops(HatchData hatch, Transform toOrigin);
    }
}
