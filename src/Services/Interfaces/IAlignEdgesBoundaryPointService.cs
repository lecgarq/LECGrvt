using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IAlignEdgesBoundaryPointService
    {
        List<XYZ> CollectBoundaryHitPoints(
            Sketch sketch,
            ReferenceIntersector intersector,
            double minSpacing,
            double maxSpacing,
            Action<string>? debugLog = null);
    }
}
