using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadCurveFlattenService
    {
        IEnumerable<Curve>? FlattenCurve(Curve c);
    }
}
