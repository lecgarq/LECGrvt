using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface IAlignEdgesPointInsertionService
    {
        void AddPoints(SlabShapeEditor editor, IEnumerable<XYZ> points);
    }
}
