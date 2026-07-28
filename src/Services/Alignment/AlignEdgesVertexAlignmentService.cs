using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Clipper2Lib;
using LECG.Services.Interfaces;
using LECG.Utilities;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class AlignEdgesVertexAlignmentService
    {
        private const double MinDeltaThreshold = 0.0164;
        private const double EdgeSearchRadius = 2.0;

        // Radial search constants
        private const double SearchStep = 0.15;
        private const double RayCeiling = 10000.0;
        private const int MinHitsForPlaneFit = 3;

        private const double D45 = 0.70710678118;
        private const double D67 = 0.92387953251;
        private const double D22 = 0.38268343237;
        private static readonly XYZ[] SearchDirs =
        {
            new XYZ(1, 0, 0),
            new XYZ(D67, D22, 0),
            new XYZ(D45, D45, 0),
            new XYZ(D22, D67, 0),
            new XYZ(0, 1, 0),
            new XYZ(-D22, D67, 0),
            new XYZ(-D45, D45, 0),
            new XYZ(-D67, D22, 0),
            new XYZ(-1, 0, 0),
            new XYZ(-D67, -D22, 0),
            new XYZ(-D45, -D45, 0),
            new XYZ(-D22, -D67, 0),
            new XYZ(0, -1, 0),
            new XYZ(D22, -D67, 0),
            new XYZ(D45, -D45, 0),
            new XYZ(D67, -D22, 0)
        };

        private readonly ReferenceRaycastService _raycastService;

        public AlignEdgesVertexAlignmentService(ReferenceRaycastService raycastService)
        {
            _raycastService = raycastService;
        }

        public (int movedCount, int skippedCount, int missCount) AlignVertices(SlabShapeEditor editor, ReferenceIntersector intersector, PathsD? overlapRegion = null)
        {
            ArgumentNullException.ThrowIfNull(editor);
            ArgumentNullException.ThrowIfNull(intersector);

            PathsD? activeOverlapRegion = overlapRegion;
            bool hasOverlap = activeOverlapRegion != null && activeOverlapRegion.Count > 0;

            int movedCount = 0;
            int skippedCount = 0;

            var alignedVertices = new List<(XYZ position, double targetZ)>();
            var radialVertices = new List<(SlabShapeVertex vertex, XYZ position)>();
            var knownRefIds = new HashSet<ElementId>();

            // === Pass 1: Direct ray for vertices inside the overlap region ===
            // Process both edge AND interior vertices that fall inside the overlap
            // region. Interior vertices outside overlap are skipped — they belong to
            // the source surface's own shape, not the shared edge zone.
            // Edge vertices outside overlap go to radial search as before.
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
            {
                bool isInterior = v.VertexType == SlabShapeVertexType.Interior;
                XYZ origin = v.Position;

                bool insideOverlap = hasOverlap && activeOverlapRegion != null
                    && ClipperUtils.IsPointInsideRegion(origin.X, origin.Y, activeOverlapRegion);

                // Interior vertices are only aligned when inside the overlap region
                if (isInterior && !insideOverlap)
                {
                    continue;
                }

                if (!insideOverlap && hasOverlap)
                {
                    // Edge vertex outside overlap — route to radial search
                    radialVertices.Add((v, origin));
                    continue;
                }

                var (hitZ, hitElementId) = TryDirectRay(intersector, origin);

                if (hitZ.HasValue)
                {
                    if (hitElementId != null && hitElementId != ElementId.InvalidElementId)
                    {
                        knownRefIds.Add(hitElementId);
                    }

                    alignedVertices.Add((origin, hitZ.Value));
                    double delta = hitZ.Value - origin.Z;

                    if (Math.Abs(delta) > MinDeltaThreshold)
                    {
                        if (TryModifyVertex(editor, v, delta)) movedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                else
                {
                    // Inside overlap but ray missed (mesh gap) — try radial search
                    radialVertices.Add((v, origin));
                }
            }

            // === Pass 2: Plane-fitting radial search ===
            var stillMissed = new List<(SlabShapeVertex vertex, XYZ position)>();

            foreach (var (vertex, pos) in radialVertices)
            {
                double? z = RadialSearchPlaneFit(intersector, pos, EdgeSearchRadius, knownRefIds);

                if (z.HasValue)
                {
                    alignedVertices.Add((pos, z.Value));
                    double delta = z.Value - pos.Z;

                    if (Math.Abs(delta) > MinDeltaThreshold)
                    {
                        if (TryModifyVertex(editor, vertex, delta)) movedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                else
                {
                    stillMissed.Add((vertex, pos));
                }
            }

            // === Pass 3: Plane-fit interpolation from nearby aligned vertices ===
            // Build spatial index from all successfully aligned vertices for O(K log n) lookup
            var spatialIndex = new VertexSpatialIndex(EdgeSearchRadius);
            foreach (var (alignedPos, targetZ) in alignedVertices)
            {
                spatialIndex.AddPoint(alignedPos.X, alignedPos.Y, targetZ);
            }

            int finalMissCount = 0;

            foreach (var (vertex, pos) in stillMissed)
            {
                double? interpolatedZ = InterpolateFromNeighbors(pos, spatialIndex);

                if (interpolatedZ.HasValue)
                {
                    double delta = interpolatedZ.Value - pos.Z;
                    if (Math.Abs(delta) > MinDeltaThreshold)
                    {
                        if (TryModifyVertex(editor, vertex, delta)) movedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                else
                {
                    finalMissCount++;
                }
            }

            return (movedCount, skippedCount, finalMissCount);
        }

        /// <summary>
        /// Direct vertical ray downward from a high ceiling.
        /// Returns the hit Z and the element ID for reference tracking.
        /// </summary>
        private static (double? z, ElementId? elementId) TryDirectRay(ReferenceIntersector intersector, XYZ origin)
        {
            XYZ rayStart = new XYZ(origin.X, origin.Y, RayCeiling);
            XYZ rayDir = XYZ.BasisZ.Negate();
            ReferenceWithContext hit = intersector.FindNearest(rayStart, rayDir);

            if (hit != null)
            {
                XYZ hitPoint = rayStart.Add(rayDir.Multiply(hit.Proximity));
                ElementId elemId = hit.GetReference()?.ElementId ?? ElementId.InvalidElementId;
                return (hitPoint.Z, elemId);
            }

            return (null, null);
        }

        /// <summary>
        /// Radial search with least-squares plane fitting.
        /// Fires rays in 16 directions at expanding radii, collects hits,
        /// fits a plane Z = a*dx + b*dy + c (where dx/dy are offsets from vertex),
        /// and evaluates at the vertex center (dx=0, dy=0).
        /// This gives the exact surface Z even when hits are asymmetrically distributed (edges/corners).
        /// </summary>
        private static double? RadialSearchPlaneFit(
            ReferenceIntersector intersector, XYZ pt, double maxRadius,
            HashSet<ElementId> allowedIds)
        {
            bool filterByElement = allowedIds.Count > 0;

            for (double r = SearchStep; r <= maxRadius; r += SearchStep)
            {
                // Accumulate sums for least-squares plane fit
                // Plane model: Z = a*dx + b*dy + c  (dx, dy = offset from vertex center)
                double sumDx = 0, sumDy = 0, sumZ = 0;
                double sumDx2 = 0, sumDy2 = 0, sumDxDy = 0;
                double sumDxZ = 0, sumDyZ = 0;
                int hitCount = 0;

                foreach (XYZ dir in SearchDirs)
                {
                    double dx = dir.X * r;
                    double dy = dir.Y * r;

                    XYZ rayStart = new XYZ(pt.X + dx, pt.Y + dy, RayCeiling);
                    XYZ rayDir = XYZ.BasisZ.Negate();
                    ReferenceWithContext hit = intersector.FindNearest(rayStart, rayDir);

                    if (hit == null) continue;

                    if (filterByElement)
                    {
                        ElementId hitId = hit.GetReference()?.ElementId ?? ElementId.InvalidElementId;
                        if (!allowedIds.Contains(hitId)) continue;
                    }

                    double z = RayCeiling - hit.Proximity;

                    sumDx += dx;
                    sumDy += dy;
                    sumZ += z;
                    sumDx2 += dx * dx;
                    sumDy2 += dy * dy;
                    sumDxDy += dx * dy;
                    sumDxZ += dx * z;
                    sumDyZ += dy * z;
                    hitCount++;
                }

                if (hitCount < 2) continue;

                double meanDx = sumDx / hitCount;
                double meanDy = sumDy / hitCount;
                double meanZ = sumZ / hitCount;

                // With 3+ hits: fit plane and evaluate at vertex center
                if (hitCount >= MinHitsForPlaneFit)
                {
                    // Normal equations for Z = a*dx + b*dy + c
                    // (using centered sums for numerical stability)
                    double sxx = sumDx2 - hitCount * meanDx * meanDx;
                    double syy = sumDy2 - hitCount * meanDy * meanDy;
                    double sxy = sumDxDy - hitCount * meanDx * meanDy;
                    double sxz = sumDxZ - hitCount * meanDx * meanZ;
                    double syz = sumDyZ - hitCount * meanDy * meanZ;

                    double det = sxx * syy - sxy * sxy;
                    if (Math.Abs(det) > 1e-12)
                    {
                        double a = (syy * sxz - sxy * syz) / det;
                        double b = (sxx * syz - sxy * sxz) / det;

                        // Evaluate plane at vertex center (dx=0, dy=0):
                        // c = meanZ - a*meanDx - b*meanDy
                        // Z_vertex = a*0 + b*0 + c = c
                        return meanZ - a * meanDx - b * meanDy;
                    }
                }

                // 2 hits or degenerate: use simple average (small radius = small error)
                return meanZ;
            }

            return null;
        }

        /// <summary>
        /// Plane-fit interpolation from the nearest aligned vertices using spatial index.
        /// With 3+ neighbors: fits a plane and evaluates at vertex XY.
        /// With fewer: inverse-distance weighted average.
        /// </summary>
        private static double? InterpolateFromNeighbors(XYZ pos, VertexSpatialIndex index)
        {
            if (index.Count == 0) return null;

            const int maxNeighbors = 6;
            const double searchRadius = 20.0;

            var nearest = index.FindKNearest(pos.X, pos.Y, maxNeighbors, searchRadius);
            if (nearest.Count == 0) return null;

            // Exact match shortcut (distSq < 1e-9 handled inside FindKNearest)
            if (nearest.Count == 1 && nearest[0].distSq < 1e-9)
            {
                return nearest[0].z;
            }

            if (nearest.Count >= 3)
            {
                // Fit plane Z = a*dx + b*dy + c through neighbors (dx, dy relative to vertex)
                double sumDx = 0, sumDy = 0, sumZ = 0;
                double sumDx2 = 0, sumDy2 = 0, sumDxDy = 0;
                double sumDxZ = 0, sumDyZ = 0;
                int n = nearest.Count;

                foreach (var (_, dx, dy, z) in nearest)
                {
                    sumDx += dx; sumDy += dy; sumZ += z;
                    sumDx2 += dx * dx; sumDy2 += dy * dy; sumDxDy += dx * dy;
                    sumDxZ += dx * z; sumDyZ += dy * z;
                }

                double meanDx = sumDx / n;
                double meanDy = sumDy / n;
                double meanZ = sumZ / n;

                double sxx = sumDx2 - n * meanDx * meanDx;
                double syy = sumDy2 - n * meanDy * meanDy;
                double sxy = sumDxDy - n * meanDx * meanDy;
                double sxz = sumDxZ - n * meanDx * meanZ;
                double syz = sumDyZ - n * meanDy * meanZ;

                double det = sxx * syy - sxy * sxy;
                if (Math.Abs(det) > 1e-12)
                {
                    double a = (syy * sxz - sxy * syz) / det;
                    double b = (sxx * syz - sxy * sxz) / det;
                    return meanZ - a * meanDx - b * meanDy;
                }
            }

            // Fallback: inverse-distance weighted average
            double weightSum = 0;
            double zWeighted = 0;

            foreach (var (distSq, _, _, z) in nearest)
            {
                double w = 1.0 / distSq;
                weightSum += w;
                zWeighted += w * z;
            }

            return weightSum > 0 ? zWeighted / weightSum : null;
        }

        private static bool TryModifyVertex(SlabShapeEditor editor, SlabShapeVertex vertex, double delta)
        {
            try
            {
                editor.ModifySubElement(vertex, delta);
                return true;
            }
            catch (Exception ex) when (IsExpectedAlignEdgesException(ex))
            {
                return false;
            }
        }

        private static bool IsExpectedAlignEdgesException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }
    }
}
