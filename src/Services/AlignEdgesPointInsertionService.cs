using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using LECG.Services.Interfaces;
using System.Collections.Generic;

namespace LECG.Services
{
    public class AlignEdgesPointInsertionService : IAlignEdgesPointInsertionService
    {
        public void AddPoints(SlabShapeEditor editor, IEnumerable<XYZ> points)
        {
            ArgumentNullException.ThrowIfNull(editor);
            ArgumentNullException.ThrowIfNull(points);

            foreach (XYZ point in points)
            {
                try
                {
                    editor.AddPoint(point);
                }
                catch
                {
                }
            }
        }
    }
}
