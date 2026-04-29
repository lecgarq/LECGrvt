using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface IAlignEdgesPointInsertionService
    {
        int AddPoints(Element slab, SlabShapeEditor editor, IEnumerable<XYZ> points);
    }
}
