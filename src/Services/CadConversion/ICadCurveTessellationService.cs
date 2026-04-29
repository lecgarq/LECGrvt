using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadCurveTessellationService
    {
        IEnumerable<Curve>? Tessellate(Curve c);
    }
}
