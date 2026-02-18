using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using LECG.Services.Interfaces;
using System.Collections.Generic;

namespace LECG.Services
{
    public class AlignEdgesToposolidProcessingService : IAlignEdgesToposolidProcessingService
    {
        private readonly IAlignEdgesBoundaryCollectionService _boundaryCollectionService;
        private readonly IAlignEdgesPointInsertionService _pointInsertionService;
        private readonly IAlignEdgesVertexAlignmentService _vertexAlignmentService;

        public AlignEdgesToposolidProcessingService(IAlignEdgesBoundaryCollectionService boundaryCollectionService, IAlignEdgesPointInsertionService pointInsertionService, IAlignEdgesVertexAlignmentService vertexAlignmentService)
        {
            _boundaryCollectionService = boundaryCollectionService;
            _pointInsertionService = pointInsertionService;
            _vertexAlignmentService = vertexAlignmentService;
        }

        public void Process(Document doc, Reference target, ReferenceIntersector intersector)
        {
            Element elem = doc.GetElement(target);
            if (elem is not Toposolid toposolid) return;

            var editor = toposolid.GetSlabShapeEditor();
            if (editor == null) return;

            if (!editor.IsEnabled)
            {
                editor.Enable();
            }

            const double minSpacing = 0.0656;
            const double maxSpacing = 3.28;

            List<XYZ> newPoints = _boundaryCollectionService.Collect(doc, toposolid, intersector, minSpacing, maxSpacing);
            _pointInsertionService.AddPoints(editor, newPoints);
            _vertexAlignmentService.AlignVertices(editor, intersector);
        }
    }
}
