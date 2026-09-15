using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Clipper2Lib;
using LECG.Services.Interfaces;
using LECG.Utilities;
using System.Collections.Generic;

namespace LECG.Services
{
    public class AlignEdgesToposolidProcessingService
    {
        // Minimum boundary curve spacing for point sampling (~20mm)
        private const double MinBoundarySpacing = 0.0656;
        // Maximum boundary curve spacing for point sampling (~0.5m)
        private const double MaxBoundarySpacing = 1.64;

        private readonly AlignEdgesBoundaryCollectionService _boundaryCollectionService;
        private readonly AlignEdgesPointInsertionService _pointInsertionService;
        private readonly SlabService _slabService;
        private readonly AlignEdgesVertexAlignmentService _vertexAlignmentService;

        public AlignEdgesToposolidProcessingService(AlignEdgesBoundaryCollectionService boundaryCollectionService, AlignEdgesPointInsertionService pointInsertionService, SlabService slabService, AlignEdgesVertexAlignmentService vertexAlignmentService)
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

            bool alignAllInteriorVertices = referenceIds == null;
            int boundaryHitCount = 0;
            int insertedPointCount = 0;
            if (!alignAllInteriorVertices)
            {
                List<XYZ> newPoints = _boundaryCollectionService.Collect(doc, elem, intersector,
                    MinBoundarySpacing, MaxBoundarySpacing);
                boundaryHitCount = newPoints.Count;
                insertedPointCount = _pointInsertionService.AddPoints(elem, editor, newPoints);
            }

            // Automatic discovery aligns every existing boundary and interior control.
            // Manual-reference mode also inserts adaptive boundary support points.
            var (movedCount, skippedCount, missCount) =
                _vertexAlignmentService.AlignVertices(elem, editor, intersector, overlapRegion,
                    alignAllInteriorVertices);

            return AlignEdgesSourceResult.FromCounts(elem.Id, sourceName, boundaryHitCount,
                insertedPointCount, movedCount, skippedCount, missCount);
        }
    }
}
