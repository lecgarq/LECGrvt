using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;
using System.Collections.Generic;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class AlignEdgesBoundaryCollectionService : IAlignEdgesBoundaryCollectionService
    {
        private readonly IAlignEdgesBoundaryPointService _boundaryPointService;
        private readonly IGeometryBoundaryService _geometryBoundaryService;

        public AlignEdgesBoundaryCollectionService(IAlignEdgesBoundaryPointService boundaryPointService, IGeometryBoundaryService geometryBoundaryService)
        {
            _boundaryPointService = boundaryPointService;
            _geometryBoundaryService = geometryBoundaryService;
        }

        public List<XYZ> Collect(Document doc, Element slab, ReferenceIntersector intersector, double minSpacing, double maxSpacing)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(slab);
            ArgumentNullException.ThrowIfNull(intersector);

            List<XYZ> points = new List<XYZ>();

            try
            {
                IList<CurveLoop> loops = _geometryBoundaryService.ExtractLoops(slab);
                if (loops.Count > 0)
                {
                    points.AddRange(_boundaryPointService.CollectBoundaryHitPoints(
                        loops,
                        intersector,
                        minSpacing,
                        maxSpacing,
                        _ => { }));
                }
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidCastException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (RevitExceptions.ArgumentException)
            {
            }
            catch (RevitExceptions.InvalidOperationException)
            {
            }

            return points;
        }
    }
}
