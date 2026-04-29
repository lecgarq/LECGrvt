using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    /// <summary>
    /// Detects and repairs anomalous surface vertices on Floor/Toposolid elements.
    /// Identifies local outliers (spikes, pits, stuck points, slope breaks) via
    /// least-squares plane fitting against the Delaunay neighborhood and corrects
    /// them to the locally expected elevation.
    /// </summary>
    public class FixPointsService : IFixPointsService
    {
        private const int MaxIterations = 10;
        // Minimum scale floor for MAD-based std-dev estimate (~1.5 mm)
        private const double MinNeighborScale = 0.005;

        private readonly ITransactionService _transactionService;
        private readonly ISlabService _slabService;

        public FixPointsService(ITransactionService transactionService, ISlabService slabService)
        {
            _transactionService = transactionService;
            _slabService = slabService;
        }

        public void FixPoints(Document doc, IList<Element> elements, int sensitivity, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(elements);
            ArgumentNullException.ThrowIfNull(reporter);

            var (threshold, minAbsDev) = GetSensitivityParameters(sensitivity);
            int totalDetected = 0;
            int totalCorrected = 0;

            for (int i = 0; i < elements.Count; i++)
            {
                Element element = elements[i];
                reporter.Report($"Analyzing element {i + 1} of {elements.Count}...", (double)(i + 1) / elements.Count * 95);

                var result = ProcessElement(doc, element, threshold, minAbsDev, reporter);
                totalDetected += result.DetectedCount;
                totalCorrected += result.CorrectedCount;
            }

            reporter.Report("Done", 100);
            reporter.Log("");
            reporter.Log("=== SUMMARY ===");
            reporter.Log($"Elements analyzed: {elements.Count}");
            reporter.Log($"Anomalous vertices detected: {totalDetected}");
            reporter.Log($"Vertices corrected: {totalCorrected}");
        }

        private ProcessResult ProcessElement(
            Document doc, Element element, double threshold, double minAbsDev,
            IProgressReporter reporter)
        {
            SlabShapeEditor? editor = _slabService.GetEditor(element);
            if (editor == null)
            {
                reporter.LogWarning($"  ID {element.Id}: No SlabShapeEditor, skipping.");
                return ProcessResult.Empty;
            }

            try
            {
                return _transactionService.Run(doc, $"Fix Points - ID {element.Id}",
                    _ => DetectAndRepair(element, editor, threshold, minAbsDev, reporter));
            }
            catch (Exception ex) when (IsExpectedException(ex))
            {
                reporter.LogError($"  ID {element.Id}: {ex.Message}");
                return ProcessResult.Empty;
            }
        }

        // ── Core detection and repair ────────────────────────────────

        private ProcessResult DetectAndRepair(
            Element element, SlabShapeEditor editor,
            double threshold, double minAbsDev, IProgressReporter reporter)
        {
            if (!editor.IsEnabled) editor.Enable();

            var vertices = SnapshotVertices(editor);
            if (vertices.Count < 4)
            {
                reporter.Log($"  ID {element.Id}: Too few vertices ({vertices.Count}), skipping.");
                return ProcessResult.Empty;
            }

            var adjacency = BuildDelaunayAdjacency(vertices);
            if (adjacency == null)
            {
                reporter.LogWarning($"  ID {element.Id}: Could not triangulate surface, skipping.");
                return ProcessResult.Empty;
            }

            bool[] isModifiable = vertices.Select(v => v.VertexType == SlabShapeVertexType.Interior).ToArray();
            double[] originalZ = vertices.Select(v => v.Position.Z).ToArray();
            double[] workingZ = (double[])originalZ.Clone();
            var correctedIndices = new HashSet<int>();

            for (int iter = 0; iter < MaxIterations; iter++)
            {
                var outliers = DetectOutliers(vertices, workingZ, adjacency, isModifiable, threshold, minAbsDev);
                if (outliers.Count == 0) break;

                foreach (var (index, expectedZ) in outliers)
                {
                    workingZ[index] = expectedZ;
                    correctedIndices.Add(index);
                }

                reporter.Log($"  ID {element.Id}: Pass {iter + 1} — {outliers.Count} anomalies.");
            }

            int corrected = 0;
            foreach (int i in correctedIndices)
            {
                double delta = workingZ[i] - originalZ[i];
                if (Math.Abs(delta) < 1e-9) continue;

                try
                {
                    editor.ModifySubElement(vertices[i].Vertex, delta);
                    corrected++;
                }
                catch (Exception ex) when (IsExpectedException(ex))
                {
                }
            }

            reporter.Log($"  ID {element.Id}: {vertices.Count} vertices, {correctedIndices.Count} anomalies, {corrected} corrected.");
            return new ProcessResult(correctedIndices.Count, corrected);
        }

        // ── Outlier detection ────────────────────────────────────────

        private static List<(int index, double expectedZ)> DetectOutliers(
            List<VertexSnapshot> vertices, double[] workingZ,
            Dictionary<int, HashSet<int>> adjacency, bool[] isModifiable,
            double threshold, double minAbsDev)
        {
            var outliers = new List<(int, double)>();

            for (int i = 0; i < vertices.Count; i++)
            {
                if (!isModifiable[i]) continue;
                if (!adjacency.TryGetValue(i, out var neighbors) || neighbors.Count < 2) continue;

                var (expectedZ, localScale) = FitLocalPlane(i, vertices, workingZ, adjacency);
                double deviation = Math.Abs(workingZ[i] - expectedZ);

                if (deviation < minAbsDev) continue;

                double effectiveScale = Math.Max(localScale, MinNeighborScale);
                if (deviation / effectiveScale > threshold)
                {
                    outliers.Add((i, expectedZ));
                }
            }

            return outliers;
        }

        // ── Local plane fitting ──────────────────────────────────────

        /// <summary>
        /// Fits a least-squares plane Z = a·dx + b·dy + c through the Delaunay
        /// neighbors of the given vertex. Returns the expected Z at the vertex
        /// position (dx=0, dy=0 → Z=c) and a MAD-based robust scale estimate
        /// of neighbor residuals around the fitted plane.
        /// </summary>
        private static (double expectedZ, double localScale) FitLocalPlane(
            int vertexIndex, List<VertexSnapshot> vertices, double[] zValues,
            Dictionary<int, HashSet<int>> adjacency)
        {
            XYZ v = vertices[vertexIndex].Position;
            HashSet<int> neighbors = adjacency[vertexIndex];
            int n = neighbors.Count;

            if (n < 3)
            {
                double avgZ = 0;
                foreach (int ni in neighbors) avgZ += zValues[ni];
                avgZ /= n;

                double sumSqDiff = 0;
                foreach (int ni in neighbors)
                {
                    double diff = zValues[ni] - avgZ;
                    sumSqDiff += diff * diff;
                }

                return (avgZ, Math.Sqrt(sumSqDiff / n));
            }

            // Accumulate sums for least-squares plane fit
            double sumDx = 0, sumDy = 0, sumZ = 0;
            double sumDx2 = 0, sumDy2 = 0, sumDxDy = 0;
            double sumDxZ = 0, sumDyZ = 0;

            foreach (int ni in neighbors)
            {
                double dx = vertices[ni].Position.X - v.X;
                double dy = vertices[ni].Position.Y - v.Y;
                double z = zValues[ni];

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
            double a = 0, b = 0;
            double expectedZ;

            if (Math.Abs(det) > 1e-12)
            {
                a = (syy * sxz - sxy * syz) / det;
                b = (sxx * syz - sxy * sxz) / det;
                expectedZ = meanZ - a * meanDx - b * meanDy;
            }
            else
            {
                expectedZ = meanZ;
            }

            // MAD-based robust scale from neighbor residuals
            var absResiduals = new double[n];
            int idx = 0;
            foreach (int ni in neighbors)
            {
                double dx = vertices[ni].Position.X - v.X;
                double dy = vertices[ni].Position.Y - v.Y;
                double predicted = a * dx + b * dy + expectedZ;
                absResiduals[idx++] = Math.Abs(zValues[ni] - predicted);
            }

            Array.Sort(absResiduals);
            double mad = absResiduals[n / 2];
            double localScale = mad * 1.4826; // MAD → σ conversion

            return (expectedZ, localScale);
        }

        // ── Delaunay adjacency ───────────────────────────────────────

        private static Dictionary<int, HashSet<int>>? BuildDelaunayAdjacency(List<VertexSnapshot> vertices)
        {
            try
            {
                var triVertices = new List<Vertex>(vertices.Count);
                for (int i = 0; i < vertices.Count; i++)
                {
                    triVertices.Add(new Vertex(vertices[i].Position.X, vertices[i].Position.Y) { ID = i });
                }

                var mesh = (TriangleNet.Mesh)new GenericMesher().Triangulate(triVertices);
                if (mesh == null || mesh.Triangles.Count == 0) return null;

                var adjacency = new Dictionary<int, HashSet<int>>();
                for (int i = 0; i < vertices.Count; i++)
                    adjacency[i] = new HashSet<int>();

                foreach (var tri in mesh.Triangles)
                {
                    int i0 = tri.GetVertexID(0);
                    int i1 = tri.GetVertexID(1);
                    int i2 = tri.GetVertexID(2);

                    if (i0 < 0 || i0 >= vertices.Count ||
                        i1 < 0 || i1 >= vertices.Count ||
                        i2 < 0 || i2 >= vertices.Count)
                        continue;

                    adjacency[i0].Add(i1); adjacency[i0].Add(i2);
                    adjacency[i1].Add(i0); adjacency[i1].Add(i2);
                    adjacency[i2].Add(i0); adjacency[i2].Add(i1);
                }

                return adjacency;
            }
            catch
            {
                return null;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────

        private static List<VertexSnapshot> SnapshotVertices(SlabShapeEditor editor)
        {
            var result = new List<VertexSnapshot>();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
            {
                result.Add(new VertexSnapshot(v, v.Position, v.VertexType));
            }
            return result;
        }

        private static (double threshold, double minAbsDev) GetSensitivityParameters(int sensitivity)
        {
            return sensitivity switch
            {
                1 => (4.0, 0.15),  // Conservative: only severe anomalies (~45mm)
                2 => (3.0, 0.08),  // Moderate (~24mm)
                3 => (2.5, 0.05),  // Balanced (~15mm)
                4 => (2.0, 0.03),  // Sensitive (~9mm)
                5 => (1.5, 0.02),  // Aggressive (~6mm)
                _ => (2.5, 0.05)
            };
        }

        private static bool IsExpectedException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }

        // ── Data types ──────────────────────────────────────────────

        private sealed record VertexSnapshot(SlabShapeVertex Vertex, XYZ Position, SlabShapeVertexType VertexType);

        private readonly struct ProcessResult
        {
            public static ProcessResult Empty => new(0, 0);

            public ProcessResult(int detectedCount, int correctedCount)
            {
                DetectedCount = detectedCount;
                CorrectedCount = correctedCount;
            }

            public int DetectedCount { get; }
            public int CorrectedCount { get; }
        }
    }
}
