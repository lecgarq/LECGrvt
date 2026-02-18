using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;
using System.Collections.Generic;

namespace LECG.Services
{
    public class AlignEdgesBoundaryCollectionService : IAlignEdgesBoundaryCollectionService
    {
        private readonly IAlignEdgesBoundaryPointService _boundaryPointService;

        public AlignEdgesBoundaryCollectionService(IAlignEdgesBoundaryPointService boundaryPointService)
        {
            _boundaryPointService = boundaryPointService;
        }

        public List<XYZ> Collect(Document doc, Toposolid toposolid, ReferenceIntersector intersector, double minSpacing, double maxSpacing)
        {
            List<XYZ> points = new List<XYZ>();

            try
            {
                Sketch? sketch = doc.GetElement(toposolid.SketchId) as Sketch;
                if (sketch != null)
                {
                    points.AddRange(_boundaryPointService.CollectBoundaryHitPoints(
                        sketch,
                        intersector,
                        minSpacing,
                        maxSpacing,
                        _ => { }));
                }
            }
            catch (Exception)
            {
            }

            return points;
        }
    }
}
