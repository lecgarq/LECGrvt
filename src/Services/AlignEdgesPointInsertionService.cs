using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using LECG.Services.Interfaces;
using System.Collections.Generic;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class AlignEdgesPointInsertionService : IAlignEdgesPointInsertionService
    {
        public int AddPoints(Element slab, SlabShapeEditor editor, IEnumerable<XYZ> points)
        {
            ArgumentNullException.ThrowIfNull(slab);
            ArgumentNullException.ThrowIfNull(editor);
            ArgumentNullException.ThrowIfNull(points);

            double? floorReferenceElevation = TryGetFloorReferenceElevation(slab);
            int addedCount = 0;

            foreach (XYZ point in points)
            {
                try
                {
                    editor.AddPoint(ToEditorPoint(point, floorReferenceElevation));
                    addedCount++;
                }
                catch (Exception ex) when (IsExpectedInsertionException(ex))
                {
                }
            }

            return addedCount;
        }

        private static XYZ ToEditorPoint(XYZ point, double? floorReferenceElevation)
        {
            if (!floorReferenceElevation.HasValue)
            {
                return point;
            }

            return new XYZ(point.X, point.Y, point.Z - floorReferenceElevation.Value);
        }

        private static double? TryGetFloorReferenceElevation(Element slab)
        {
            if (slab is not Floor floor)
            {
                return null;
            }

            Level? level = floor.Document.GetElement(floor.LevelId) as Level;
            double levelElevation = level?.Elevation ?? 0.0;
            double heightOffset = slab.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.AsDouble() ?? 0.0;
            return levelElevation + heightOffset;
        }

        private static bool IsExpectedInsertionException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.InvalidOperationException;
        }
    }
}
