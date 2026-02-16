using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using LECG.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class AlignEdgesService : IAlignEdgesService
    {
        private string debugInfo = "";

        public void AlignEdges(Document doc, IList<Reference> targets, IList<Reference> references)
        {
            if (targets == null || targets.Count == 0 || references == null || references.Count == 0) return;

            // Collect reference element IDs
            ICollection<ElementId> refElementIds = references
                .Select(r => doc.GetElement(r)?.Id)
                .Where(id => id != null)
                .ToList()!;
            
            if (refElementIds.Count == 0) return;

            // Find a 3D view for ReferenceIntersector
            View3D? view3D = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate && v.IsSectionBoxActive == false); 
            
            if (view3D == null)
            {
                if (doc.ActiveView is View3D v) view3D = v;
                else 
                {
                    // In a service, we might prefer throwing an exception or returning a status object
                    // For now, adhering to the logic extraction
                    throw new InvalidOperationException("No suitable 3D view found for ray tracing.");
                }
            }

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
                
                // Create intersector with ALL reference element IDs
                ReferenceIntersector intersector = new ReferenceIntersector(
                    refElementIds, 
                    FindReferenceTarget.Element, 
                    view3D);
                
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
                                    foreach (CurveArray curveArr in sketch.Profile)
                                    {
                                        foreach (Curve curve in curveArr)
                                        {
                                            double length = curve.Length;
                                            
                                            // Test if any part of curve overlaps reference
                                            // Sample multiple points (8) to handle arcs and circles
                                            bool curveHitsReference = false;
                                            for (int sample = 0; sample <= 8; sample++)
                                            {
                                                double sampleT = sample / 8.0;
                                                XYZ samplePt = curve.Evaluate(sampleT, true);
                                                if (CheckHitsReference(intersector, samplePt))
                                                {
                                                    curveHitsReference = true;
                                                    break;
                                                }
                                            }
                                            
                                            // If any part of curve hits reference, densify it
                                            if (curveHitsReference)
                                            {
                                                debugInfo += $"Curve len={length:F1}ft (arc/line)\n";
                                                
                                                if (length > MIN_SPACING)
                                                {
                                                    int divisions = (int)Math.Ceiling(length / MAX_SPACING);
                                                    divisions = Math.Max(divisions, 2);
                                                    
                                                    // Get curve midpoint for offset direction
                                                    XYZ curveMid = curve.Evaluate(0.5, true);
                                                    
                                                    for (int j = 1; j < divisions; j++)
                                                    {
                                                        double param = (double)j / divisions;
                                                        XYZ sketchPt = curve.Evaluate(param, true);
                                                        
                                                        // Get hit point with CORRECT Z from reference surface
                                                        XYZ? hitPt = GetHitPoint(intersector, sketchPt);
                                                        
                                                        // If no hit, try slightly inward toward curve center (tolerance for edge cases)
                                                        if (hitPt == null)
                                                        {
                                                            // Try offsets of 0.5ft, 1ft inward
                                                            XYZ dir = (curveMid - sketchPt).Normalize();
                                                            for (double offset = 0.5; offset <= 1.5 && hitPt == null; offset += 0.5)
                                                            {
                                                                XYZ testPt = sketchPt.Add(dir.Multiply(offset));
                                                                hitPt = GetHitPoint(intersector, testPt);
                                                                if (hitPt != null)
                                                                {
                                                                    // Use original XY but hit Z
                                                                    hitPt = new XYZ(sketchPt.X, sketchPt.Y, hitPt.Z);
                                                                }
                                                            }
                                                        }
                                                        
                                                        if (hitPt != null)
                                                        {
                                                            newPoints.Add(hitPt);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
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
                            double levelElevation = 0;
                            Level? level = doc.GetElement(toposolid.LevelId) as Level;
                            if (level != null) levelElevation = level.ProjectElevation;
                            
                            // Get height offset from level
                            double heightOffset = 0;
                            Parameter heightParam = toposolid.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                            if (heightParam != null) heightOffset = heightParam.AsDouble();
                            
                            double baseElevation = levelElevation + heightOffset;
                            debugInfo += $"Level={levelElevation:F2} HeightOffset={heightOffset:F2} Base={baseElevation:F2}\n";
                            
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

        private bool CheckHitsReference(ReferenceIntersector intersector, XYZ pt)
        {
            // Start from very high Z to find surfaces at any elevation
            XYZ rayStart = new XYZ(pt.X, pt.Y, 10000);
            ReferenceWithContext hit = intersector.FindNearest(rayStart, XYZ.BasisZ.Negate());
            if (hit != null) return true;
            
            // Try from below
            rayStart = new XYZ(pt.X, pt.Y, -1000);
            hit = intersector.FindNearest(rayStart, XYZ.BasisZ);
            return hit != null;
        }
        
        // Gets the hit point with correct Z from reference surface
        private XYZ? GetHitPoint(ReferenceIntersector intersector, XYZ pt)
        {
            // Start ray from very high to ensure we hit surfaces at any elevation
            XYZ rayStart = new XYZ(pt.X, pt.Y, 10000);
            XYZ rayDir = XYZ.BasisZ.Negate();
            ReferenceWithContext hit = intersector.FindNearest(rayStart, rayDir);
            
            if (hit == null)
            {
                // Try from below
                rayStart = new XYZ(pt.X, pt.Y, -1000);
                rayDir = XYZ.BasisZ;
                hit = intersector.FindNearest(rayStart, rayDir);
            }
            
            if (hit != null)
            {
                // Calculate actual hit point from proximity
                double proximity = hit.Proximity;
                return rayStart.Add(rayDir.Multiply(proximity));
            }
            
            return null;
        }
    }
}
