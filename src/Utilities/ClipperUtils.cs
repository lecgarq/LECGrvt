using System.Collections.Generic;
using Autodesk.Revit.DB;
using Clipper2Lib;

namespace LECG.Utilities
{
    public static class ClipperUtils
    {
        /// <summary>
        /// Converts a Revit CurveLoop to a Clipper2 PathD by tessellating all curves
        /// and projecting onto the XY plane.
        /// </summary>
        public static PathD CurveLoopToPathD(CurveLoop loop)
        {
            ArgumentNullException.ThrowIfNull(loop);

            var path = new PathD();
            foreach (Curve curve in loop)
            {
                IList<XYZ> tessellated = curve.Tessellate();
                for (int i = 0; i < tessellated.Count - 1; i++)
                {
                    path.Add(new PointD(tessellated[i].X, tessellated[i].Y));
                }
            }
            return path;
        }

        /// <summary>
        /// Extracts the 2D footprint (outer boundary loops) of a Floor or Toposolid
        /// from its Sketch profile.
        /// </summary>
        public static PathsD GetElementFootprint(Document doc, Element element)
        {
            ArgumentNullException.ThrowIfNull(doc);

            ElementId sketchId = GetSketchId(element);
            if (sketchId == ElementId.InvalidElementId) return new PathsD();

            Sketch? sketch = doc.GetElement(sketchId) as Sketch;
            if (sketch == null) return new PathsD();

            var paths = new PathsD();
            foreach (CurveArray curveArray in sketch.Profile)
            {
                var curves = new List<Curve>();
                foreach (Curve curve in curveArray)
                {
                    curves.Add(curve);
                }

                try
                {
                    CurveLoop loop = CurveLoop.Create(curves);
                    PathD path = CurveLoopToPathD(loop);
                    if (path.Count >= 3) paths.Add(path);
                }
                catch
                {
                    // Skip malformed loops
                }
            }

            return paths;
        }

        /// <summary>
        /// Computes the 2D overlap region between a target element and a set of
        /// reference elements using Clipper2 boolean intersection.
        /// Returns null if footprints cannot be extracted or no overlap exists.
        /// </summary>
        public static PathsD? ComputeOverlapRegion(Document doc, Element target, IList<ElementId>? referenceIds)
        {
            ArgumentNullException.ThrowIfNull(doc);

            if (referenceIds == null || referenceIds.Count == 0) return null;

            PathsD targetFootprint = GetElementFootprint(doc, target);
            if (targetFootprint.Count == 0) return null;

            // Collect all reference footprints
            var allRefPaths = new PathsD();
            foreach (ElementId refId in referenceIds)
            {
                Element? refElem = doc.GetElement(refId);
                if (refElem == null) continue;

                PathsD refFootprint = GetElementFootprint(doc, refElem);
                foreach (PathD p in refFootprint)
                {
                    allRefPaths.Add(p);
                }
            }

            if (allRefPaths.Count == 0) return null;

            // Union all reference footprints into a single region
            PathsD refUnion = Clipper.Union(allRefPaths, FillRule.NonZero);
            if (refUnion.Count == 0) return null;

            // Intersect target footprint with reference union
            PathsD overlap = Clipper.Intersect(targetFootprint, refUnion, FillRule.NonZero);
            return overlap.Count > 0 ? overlap : null;
        }

        /// <summary>
        /// Checks if a 2D point (XY) lies inside a PathsD region, correctly
        /// accounting for holes. Outer boundaries (CCW/positive) add depth;
        /// holes (CW/negative) subtract it.
        /// </summary>
        public static bool IsPointInsideRegion(double x, double y, PathsD region)
        {
            ArgumentNullException.ThrowIfNull(region);

            var pt = new PointD(x, y);
            int depth = 0;
            foreach (PathD path in region)
            {
                PointInPolygonResult result = Clipper.PointInPolygon(pt, path);
                if (result == PointInPolygonResult.IsOn) return true;
                if (result == PointInPolygonResult.IsInside)
                {
                    depth += Clipper.IsPositive(path) ? 1 : -1;
                }
            }
            return depth > 0;
        }

        private static ElementId GetSketchId(Element element)
        {
            if (element is Floor floor) return floor.SketchId;
            if (element is Toposolid toposolid) return toposolid.SketchId;
            return ElementId.InvalidElementId;
        }
    }
}
