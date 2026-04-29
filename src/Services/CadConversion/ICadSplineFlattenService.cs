using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface ICadSplineFlattenService
    {
        IEnumerable<Curve>? Flatten(Curve curve);
    }
}
