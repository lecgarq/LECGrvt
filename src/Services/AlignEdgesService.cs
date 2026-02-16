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
        private readonly IReferenceRaycastService _referenceRaycastService;
        private readonly IAlignEdgesIntersectorService _intersectorService;
        private readonly IAlignEdgesBoundaryPointService _boundaryPointService;
        private readonly IToposolidBaseElevationService _baseElevationService;
        private string debugInfo = "";

        public AlignEdgesService() : this(new ReferenceRaycastService(), new AlignEdgesIntersectorService(), new AlignEdgesBoundaryPointService(), new ToposolidBaseElevationService())
        {
        }

        public AlignEdgesService(IReferenceRaycastService referenceRaycastService, IAlignEdgesIntersectorService intersectorService, IAlignEdgesBoundaryPointService boundaryPointService, IToposolidBaseElevationService baseElevationService)
        {
            _referenceRaycastService = referenceRaycastService;
            _intersectorService = intersectorService;
            _boundaryPointService = boundaryPointService;
            _baseElevationService = baseElevationService;
        }

        public void AlignEdges(Document doc, IList<Reference> targets, IList<Reference> references)
        {
            if (targets == null || targets.Count == 0 || references == null || references.Count == 0) return;

            ReferenceIntersector intersector = _intersectorService.Create(doc, references);

            int pointCount = 0;
            int addedPoints = 0;
            int missCount = 0;
            int skippedCount = 0;
            
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
                                        msg => debugInfo += msg));
                                }
                                else
                                {
                                    debugInfo += "No sketch found\n";
                                }
                            }
                            catch (Exception ex)
                            {
                                debugInfo += $"Sketch error: {ex.Message}\n";
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
                            (double baseElevation, string debugMessage) = _baseElevationService.Resolve(doc, toposolid);
                            debugInfo += debugMessage;
                            
                            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                            {
                                XYZ origin = v.Position;
                                
                                // v.Position is already in ABSOLUTE model coordinates
                                XYZ rayStart = new XYZ(origin.X, origin.Y, origin.Z + 500);
                                XYZ rayDir = XYZ.BasisZ.Negate();
                                ReferenceWithContext hit = intersector.FindNearest(rayStart, rayDir);
                                
                                if (hit == null)
                                {
                                    rayStart = new XYZ(origin.X, origin.Y, origin.Z - 500);
                                    rayDir = XYZ.BasisZ;
                                    hit = intersector.FindNearest(rayStart, rayDir);
                                }

                                if (hit != null)
                                {
                                    double proximity = hit.Proximity;
                                    XYZ hitPoint = rayStart.Add(rayDir.Multiply(proximity));
                                    
                                    // Both origin and hitPoint are in same absolute coords
                                    // Delta is how much to move this vertex
                                    double delta = hitPoint.Z - origin.Z;
                                    
                                    // 5mm tolerance
                                    if (Math.Abs(delta) > 0.0164)
                                    {
                                        try
                                        {
                                            editor.ModifySubElement(v, delta); 
                                            pointCount++;
                                        }
                                        catch { }
                                    }
                                    else
                                    {
                                        skippedCount++;
                                    }
                                }
                                else
                                {
                                    missCount++;
                                }
                            }
                        }
                    }
                }
                
                t.Commit();
            }
            
            // Logging can be injected, but for now we maintain the info string logic or return a result object.
            // In a pure service, we should probably return a result object.
            // But to keep constraints simple for this refactor, we will rely on exception message or separate logging mechanism later if needed.
            // For now, we assume the command handles UI feedback, so we won't show TaskDialog here.
        }

    }
}
