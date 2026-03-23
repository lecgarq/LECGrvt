using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class GeometryBoundaryService : IGeometryBoundaryService
    {
        private readonly ISlabService _slabService;
        private readonly IAppMemoryCache _appMemoryCache;

        public GeometryBoundaryService(ISlabService slabService, IAppMemoryCache appMemoryCache)
        {
            _slabService = slabService;
            _appMemoryCache = appMemoryCache;
        }

        public IList<CurveLoop> ExtractLoops(Element element)
        {
            if (element == null || !element.IsValidObject)
                throw new ArgumentException("The provided element is null or not valid.");

            Document doc = element.Document;
            ElementId sketchId = GetSketchId(element);

            if (sketchId == ElementId.InvalidElementId)
            {
                throw new InvalidOperationException($"Element {element.Id} has no associated Sketch.");
            }

            Sketch? sketch = doc.GetElement(sketchId) as Sketch;
            if (sketch == null)
            {
                throw new InvalidOperationException($"Could not fetch Sketch {sketchId} for Element {element.Id}.");
            }

            string cacheKey = BuildExtractLoopsCacheKey(doc, element.Id, sketchId);

            // The key includes document identity, element id, and sketch id. We still keep the
            // expiration short because Revit can mutate sketch geometry without changing ids.
            return _appMemoryCache.GetOrCreate(
                cacheKey,
                () => ExtractNormalizedLoops(sketch),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(10));
        }

        private IList<CurveLoop> ExtractNormalizedLoops(Sketch sketch)
        {
            var loops = new List<CurveLoop>();
            CurveArrArray profile = sketch.Profile;

            foreach (CurveArray curveArray in profile)
            {
                var loopCurves = new List<Curve>();
                foreach (Curve curve in curveArray)
                {
                    loopCurves.Add(curve);
                }

                try
                {
                    CurveLoop loop = CurveLoop.Create(loopCurves);
                    loops.Add(loop);
                }
                catch
                {
                    // Catch cases where loops are improperly formed within Revit's raw Sketch element
                }
            }

            IList<CurveLoop> planeAligned = AlignLoopsToCommonPlane(loops);
            return NormalizeWindingOrder(planeAligned);
        }

        private static string BuildExtractLoopsCacheKey(Document doc, ElementId elementId, ElementId sketchId)
        {
            string documentKey = string.IsNullOrWhiteSpace(doc.PathName) ? doc.Title : doc.PathName;
            return $"GeometryBoundaryService:ExtractLoops:{documentKey}:{elementId.Value}:{sketchId.Value}";
        }

        private ElementId GetSketchId(Element element)
        {
            if (element is Floor floor)
            {
                return floor.SketchId;
            }
            if (element is Ceiling ceiling)
            {
                return ceiling.SketchId;
            }
            if (element is Toposolid toposolid)
            {
                return toposolid.SketchId;
            }

            return ElementId.InvalidElementId;
        }

        public IList<CurveLoop> NormalizeWindingOrder(IList<CurveLoop> loops)
        {
            if (loops == null || loops.Count == 0) return new List<CurveLoop>();

            // Raw Sketch.Profile extraction natively maintains the correct
            // CCW/CW distinction established by Revit parameters and geometry.
            return loops;
        }

        public IList<CurveLoop> AlignLoopsToCommonPlane(IList<CurveLoop> loops)
        {
            ArgumentNullException.ThrowIfNull(loops);

            // Floor geometries need strictly planar XY boundaries for creation.
            // This forces all Sketch curves onto Z=0 to prevent creation drift.
            var alignedLoops = new List<CurveLoop>();

            foreach (CurveLoop loop in loops)
            {
                var alignedCurves = new List<Curve>();
                foreach (Curve curve in loop)
                {
                    alignedCurves.Add(FlattenCurve(curve));
                }

                try
                {
                    alignedLoops.Add(CurveLoop.Create(alignedCurves));
                }
                catch
                {
                    // If creation somehow fails, preserve original loop as fallback
                    alignedLoops.Add(loop);
                }
            }

            return alignedLoops;
        }

        private Curve FlattenCurve(Curve curve)
        {
            if (curve is Line line)
            {
                XYZ p0 = FlattenXYZ(line.GetEndPoint(0));
                XYZ p1 = FlattenXYZ(line.GetEndPoint(1));
                if (p0.IsAlmostEqualTo(p1)) return line;
                return Line.CreateBound(p0, p1);
            }
            else if (curve is Arc arc)
            {
                XYZ p0 = FlattenXYZ(arc.GetEndPoint(0));
                XYZ p1 = FlattenXYZ(arc.GetEndPoint(1));
                XYZ pm = FlattenXYZ(arc.Evaluate(0.5, true));

                if (arc.IsClosed)
                {
                    Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, p0);
                    return Arc.Create(plane, arc.Radius, 0, 2 * Math.PI);
                }

                try
                {
                    return Arc.Create(p0, p1, pm);
                }
                catch
                {
                    return arc;
                }
            }

            // For B-Splines, flattening is more complex, fallback to original
            return curve;
        }

        private XYZ FlattenXYZ(XYZ pt) => new XYZ(pt.X, pt.Y, 0);
    }
}
