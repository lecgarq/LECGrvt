using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Clipper2Lib;
using LECG.Services.Interfaces;
using LECG.Utils;
using System.Collections.Generic;

namespace LECG.Services
{
    public class AlignEdgesToposolidProcessingService : IAlignEdgesToposolidProcessingService
    {
        // Minimum boundary curve spacing for point sampling (~20mm)
        private const double MinBoundarySpacing = 0.0656;
        // Maximum boundary curve spacing for point sampling (~0.5m)
        private const double MaxBoundarySpacing = 1.64;

        private readonly IAlignEdgesBoundaryCollectionService _boundaryCollectionService;
        private readonly IAlignEdgesPointInsertionService _pointInsertionService;
        private readonly ISlabService _slabService;
        private readonly IAlignEdgesVertexAlignmentService _vertexAlignmentService;

        public AlignEdgesToposolidProcessingService(IAlignEdgesBoundaryCollectionService boundaryCollectionService, IAlignEdgesPointInsertionService pointInsertionService, ISlabService slabService, IAlignEdgesVertexAlignmentService vertexAlignmentService)
        {
            _boundaryCollectionService = boundaryCollectionService;
            _pointInsertionService = pointInsertionService;
            _slabService = slabService;
            _vertexAlignmentService = vertexAlignmentService;
        }

        public AlignEdgesSourceResult Process(Document doc, Reference source, ReferenceIntersector intersector, IList<ElementId>? referenceIds = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(intersector);

            Element elem = doc.GetElement(source);
            if (elem == null)
            {
                return AlignEdgesSourceResult.Failed(ElementId.InvalidElementId, "Unknown source", "Could not resolve the selected source element.");
            }

            string sourceName = elem.Name;
            if (elem is not Floor && elem is not Toposolid)
            {
                return AlignEdgesSourceResult.Failed(elem.Id, sourceName, "Source element is not an editable slab.");
            }

            var editor = _slabService.GetEditor(elem);
            if (editor == null)
            {
                return AlignEdgesSourceResult.Failed(elem.Id, sourceName, "Could not acquire a SlabShapeEditor.");
            }

            if (!editor.IsEnabled)
            {
                editor.Enable();
            }

            // Compute 2D overlap region between source and reference footprints
            PathsD? overlapRegion = ClipperUtils.ComputeOverlapRegion(doc, elem, referenceIds);

            // Vertex-first: resolve existing boundary control before deciding whether
            // the curve still needs extra interior points.
            var (initialMovedCount, initialSkippedCount, initialMissCount) = _vertexAlignmentService.AlignVertices(editor, intersector, overlapRegion);
            if (initialMovedCount > 0)
            {
                doc.Regenerate();
            }

            List<XYZ> newPoints = _boundaryCollectionService.Collect(doc, elem, intersector, MinBoundarySpacing, MaxBoundarySpacing);
            int boundaryHitCount = newPoints.Count;
            int insertedPointCount = _pointInsertionService.AddPoints(elem, editor, newPoints);

            int totalMovedCount = initialMovedCount;
            int totalSkippedCount = initialSkippedCount;
            int finalMissCount = initialMissCount;

            if (insertedPointCount > 0)
            {
                doc.Regenerate();
                var (settleMovedCount, settleSkippedCount, settleMissCount) = _vertexAlignmentService.AlignVertices(editor, intersector, overlapRegion);
                totalMovedCount += settleMovedCount;
                totalSkippedCount = Math.Max(totalSkippedCount, settleSkippedCount);
                finalMissCount = settleMissCount;
            }

            return AlignEdgesSourceResult.FromCounts(elem.Id, sourceName, boundaryHitCount, insertedPointCount, totalMovedCount, totalSkippedCount, finalMissCount);
        }
    }
}
