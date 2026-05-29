using System;
using System.Collections.Generic;
using g3;

namespace LECG.Utilities
{
    /// <summary>
    /// Spatial index for aligned vertex positions using geometry3Sharp's PointHashGrid2d.
    /// Provides efficient K-nearest neighbor queries in 2D (XY plane).
    /// </summary>
    public class VertexSpatialIndex
    {
        private readonly PointHashGrid2d<int> _grid;
        private readonly List<(double x, double y, double z)> _points;

        /// <summary>
        /// Builds a 2D spatial hash grid from aligned vertex positions.
        /// </summary>
        /// <param name="cellSize">Hash grid cell size in feet. Smaller = faster queries but more memory.</param>
        public VertexSpatialIndex(double cellSize = 1.0)
        {
            _grid = new PointHashGrid2d<int>(cellSize, -1);
            _points = new List<(double x, double y, double z)>();
        }

        public int Count => _points.Count;

        public void AddPoint(double x, double y, double z)
        {
            int idx = _points.Count;
            _points.Add((x, y, z));
            _grid.InsertPointUnsafe(idx, new Vector2d(x, y));
        }

        /// <summary>
        /// Finds up to K nearest neighbors to a query point in XY, within maxRadius.
        /// Returns neighbors sorted by ascending distance squared.
        /// </summary>
        public List<(double distSq, double dx, double dy, double z)> FindKNearest(
            double queryX, double queryY, int k, double maxRadius)
        {
            if (_points.Count == 0 || k <= 0) return new List<(double, double, double, double)>();

            var queryPt = new Vector2d(queryX, queryY);
            var found = new HashSet<int>();
            var results = new List<(double distSq, double dx, double dy, double z)>(k);

            for (int i = 0; i < k; i++)
            {
                KeyValuePair<int, double> nearest = _grid.FindNearestInRadius(
                    queryPt,
                    maxRadius,
                    idx =>
                    {
                        var (px, py, _) = _points[idx];
                        double ddx = px - queryX;
                        double ddy = py - queryY;
                        return Math.Sqrt(ddx * ddx + ddy * ddy);
                    },
                    idx => found.Contains(idx));

                if (nearest.Key == -1) break;

                found.Add(nearest.Key);
                var (x, y, z) = _points[nearest.Key];
                double dx = x - queryX;
                double dy = y - queryY;
                double distSq = dx * dx + dy * dy;

                if (distSq < 1e-9)
                {
                    // Exact match — return immediately with just this point
                    return new List<(double, double, double, double)> { (distSq, dx, dy, z) };
                }

                results.Add((distSq, dx, dy, z));
            }

            return results;
        }
    }
}
