using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using LECG.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class AlignEdgesService : IAlignEdgesService
    {
        private readonly IAlignEdgesIntersectorService _intersectorService;
        private readonly IAlignEdgesBoundaryPointService _boundaryPointService;
        private readonly IAlignEdgesVertexAlignmentService _vertexAlignmentService;

        public AlignEdgesService() : this(new AlignEdgesIntersectorService(), new AlignEdgesBoundaryPointService(), new AlignEdgesVertexAlignmentService())
        {
        }

        public AlignEdgesService(IAlignEdgesIntersectorService intersectorService, IAlignEdgesBoundaryPointService boundaryPointService, IAlignEdgesVertexAlignmentService vertexAlignmentService)
        {
            _intersectorService = intersectorService;
            _boundaryPointService = boundaryPointService;
            _vertexAlignmentService = vertexAlignmentService;
        }

        public void AlignEdges(Document doc, IList<Reference> targets, IList<Reference> references)
        {
            if (targets == null || targets.Count == 0 || references == null || references.Count == 0) return;

            ReferenceIntersector intersector = _intersectorService.Create(doc, references);

            int addedPoints = 0;
            
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
                            List<XYZ> newPoints = new List<XYZ>();
                            
                            // Get the sketch boundary curves
                            try
                            {
                                Sketch? sketch = doc.GetElement(toposolid.SketchId) as Sketch;
                                if (sketch != null)
                                {
                                    newPoints.AddRange(_boundaryPointService.CollectBoundaryHitPoints(
                                        sketch,
                                        intersector,
                                        MIN_SPACING,
                                        MAX_SPACING,
                                        _ => { }));
                                }
                            }
                            catch (Exception)
                            {
                            }
                            
                            // Add the new points
                            foreach (XYZ pt in newPoints)
                            {
                                try
                                {
                                    editor.AddPoint(pt);
                                    addedPoints++;
                                }
                                catch { }
                            }
                            
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
