using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using LECG.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class AlignEdgesService : IAlignEdgesService
    {
        private readonly IAlignEdgesIntersectorService _intersectorService;
        private readonly IAlignEdgesBoundaryCollectionService _boundaryCollectionService;
        private readonly IAlignEdgesVertexAlignmentService _vertexAlignmentService;
        private readonly IAlignEdgesPointInsertionService _pointInsertionService;

        public AlignEdgesService() : this(new AlignEdgesIntersectorService(), new AlignEdgesBoundaryCollectionService(new AlignEdgesBoundaryPointService()), new AlignEdgesVertexAlignmentService(), new AlignEdgesPointInsertionService())
        {
        }

        public AlignEdgesService(IAlignEdgesIntersectorService intersectorService, IAlignEdgesBoundaryCollectionService boundaryCollectionService, IAlignEdgesVertexAlignmentService vertexAlignmentService, IAlignEdgesPointInsertionService pointInsertionService)
        {
            _intersectorService = intersectorService;
            _boundaryCollectionService = boundaryCollectionService;
            _vertexAlignmentService = vertexAlignmentService;
            _pointInsertionService = pointInsertionService;
        }

        public void AlignEdges(Document doc, IList<Reference> targets, IList<Reference> references)
        {
            if (targets == null || targets.Count == 0 || references == null || references.Count == 0) return;

            ReferenceIntersector intersector = _intersectorService.Create(doc, references);

            // Spacing constants (in feet)
            const double MIN_SPACING = 0.0656; // 2cm
            const double MAX_SPACING = 3.28;   // 1m

            using (Transaction t = new Transaction(doc, "Align Edges"))
            {
                t.Start();
                
                foreach (Reference r in targets)
                {
                    Element elem = doc.GetElement(r);
                    if (elem is Toposolid toposolid)
                    {
                        var editor = toposolid.GetSlabShapeEditor();
                        if (editor != null)
                        {
                            if (!editor.IsEnabled) editor.Enable();
                            
                            // STEP 1: Add intermediate points along boundary curves that hit reference
                            List<XYZ> newPoints = _boundaryCollectionService.Collect(
                                doc,
                                toposolid,
                                intersector,
                                MIN_SPACING,
                                MAX_SPACING);
                            
                            // Add the new points
                            _pointInsertionService.AddPoints(editor, newPoints);
                            
                            // STEP 2: Align ALL points (existing + new) to reference
                            // Get the BASE elevation: level + height offset from level
                            _vertexAlignmentService.AlignVertices(editor, intersector);
                        }
                    }
                }
                
                t.Commit();
            }
        }

    }
}
