using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class FixPointsService : IFixPointsService
    {
        private readonly ITransactionService _transactionService;
        private readonly ISlabService _slabService;

        public FixPointsService(ITransactionService transactionService, ISlabService slabService)
        {
            _transactionService = transactionService;
            _slabService = slabService;
        }

        public void FixPoints(Document doc, IList<Element> elements, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(elements);
            ArgumentNullException.ThrowIfNull(reporter);

            int totalCorrected = 0;
            int totalExamined = 0;
            int totalAfter = 0;
            int totalFlagged = 0;
            int elementsProcessed = 0;

            for (int i = 0; i < elements.Count; i++)
            {
                Element element = elements[i];
                reporter.Report($"Processing element {i + 1} of {elements.Count}...", (double)(i + 1) / elements.Count * 95);

                var result = ProcessElement(doc, element, reporter);
                totalCorrected += result.Corrected;
                totalExamined += result.BeforeCount;
                totalAfter += result.AfterCount;
                totalFlagged += result.Flagged;

                if (result.BeforeCount > 0)
                {
                    elementsProcessed++;
                }
            }

            reporter.Log("");
            reporter.Log("=== SUMMARY ===");
            reporter.Log($"Elements processed: {elementsProcessed}");
            reporter.Log($"Total vertices before: {totalExamined}");
            reporter.Log($"Total vertices after: {totalAfter}");
            reporter.Log($"Total vertices flagged: {totalFlagged}");
            reporter.Log($"Total vertices corrected: {totalCorrected}");
        }

        private ProcessResult ProcessElement(Document doc, Element element, IProgressReporter reporter)
        {
            SlabShapeEditor? editor = _slabService.GetEditor(element);
            if (editor == null)
            {
                reporter.LogWarning($"  ID {element.Id}: No SlabShapeEditor available, skipping.");
                return ProcessResult.Empty;
            }

            try
            {
                return _transactionService.Run(doc, $"Fix Points - ID {element.Id}", currentDoc =>
                {
                    if (!editor.IsEnabled)
                    {
                        editor.Enable();
                    }

                    // Snapshot all vertices with positions and types before modification
                    var snapshot = SnapshotVertices(editor);
                    if (snapshot.Count == 0)
                    {
                        reporter.Log($"  ID {element.Id}: No vertices found.");
                        return ProcessResult.Empty;
                    }

                    // Compute the average spacing between vertices to derive the search radius and tolerance
                    double averageSpacing = ComputeAverageSpacing(snapshot);
                    double searchRadius = averageSpacing * 2.5;
                    double tolerance = averageSpacing * 0.5;

                    int corrected = 0;
                    int flagged = 0;

                    foreach (var entry in snapshot)
                    {
                        if (entry.VertexType != SlabShapeVertexType.Edge
                            && entry.VertexType != SlabShapeVertexType.Interior)
                        {
                            continue;
                        }

                        // Find neighbors within the search radius (excluding self)
                        var neighbors = FindNeighbors(snapshot, entry.Position, searchRadius, entry.Vertex);
                        if (neighbors.Count < 2)
                        {
                            // Not enough neighbors to compute a meaningful interpolation
                            continue;
                        }

                        double interpolatedZ = ComputePlanarZ(neighbors, entry.Position);
                        double deviation = Math.Abs(entry.Position.Z - interpolatedZ);

                        if (deviation > tolerance)
                        {
                            flagged++;

                            if (TryRepairVertex(editor, element.Id, entry, interpolatedZ, deviation, reporter, out string actionMessage))
                            {
                                corrected++;
                                reporter.Log(actionMessage);
                            }
                        }
                    }

                    int afterCount = SnapshotVertices(editor).Count;
                    reporter.Log($"  ID {element.Id}: before {snapshot.Count}, after {afterCount}, flagged {flagged}, corrected {corrected}");
                    return new ProcessResult(snapshot.Count, afterCount, flagged, corrected);
                });
            }
            catch (Exception ex) when (IsExpectedFixPointsException(ex))
            {
                reporter.LogError($"  ID {element.Id}: Error processing element: {ex.Message}");
                return ProcessResult.Empty;
            }
        }

        private static bool TryRepairVertex(
            SlabShapeEditor editor,
            ElementId elementId,
            VertexSnapshot entry,
            double interpolatedZ,
            double deviation,
            IProgressReporter reporter,
            out string actionMessage)
        {
            if (entry.VertexType == SlabShapeVertexType.Interior
                && TryDeleteInteriorVertex(editor, elementId, entry, interpolatedZ, deviation, reporter, out actionMessage))
            {
                return true;
            }

            return TryModifyVertex(editor, elementId, entry, interpolatedZ, deviation, reporter, out actionMessage);
        }

        private static bool TryDeleteInteriorVertex(
            SlabShapeEditor editor,
            ElementId elementId,
            VertexSnapshot entry,
            double interpolatedZ,
            double deviation,
            IProgressReporter reporter,
            out string actionMessage)
        {
            actionMessage = string.Empty;

            try
            {
                if (editor.DeletePoint(entry.Vertex))
                {
                    actionMessage = $"    ID {elementId}: deleted interior point at {FormatPoint(entry.Position)} | deviation {deviation:F3} | target Z {interpolatedZ:F3}";
                    return true;
                }
            }
            catch (Exception ex) when (IsExpectedFixPointsException(ex))
            {
                reporter.LogWarning($"    ID {elementId}: interior delete failed at {FormatPoint(entry.Position)}: {ex.Message}");
            }

            return false;
        }

        private static bool TryModifyVertex(
            SlabShapeEditor editor,
            ElementId elementId,
            VertexSnapshot entry,
            double interpolatedZ,
            double deviation,
            IProgressReporter reporter,
            out string actionMessage)
        {
            actionMessage = string.Empty;
            double deltaZ = interpolatedZ - entry.Position.Z;

            try
            {
                editor.ModifySubElement(entry.Vertex, deltaZ);

                string method = entry.VertexType == SlabShapeVertexType.Edge
                    ? "modified edge point"
                    : "modified interior fallback";

                actionMessage = $"    ID {elementId}: {method} at {FormatPoint(entry.Position)} | delta {deltaZ:F3} | deviation {deviation:F3} | target Z {interpolatedZ:F3}";
                return true;
            }
            catch (Exception ex) when (IsExpectedFixPointsException(ex))
            {
                reporter.LogWarning($"    ID {elementId}: failed to correct {entry.VertexType.ToString().ToLowerInvariant()} point at {FormatPoint(entry.Position)}: {ex.Message}");
                return false;
            }
        }

        private static List<VertexSnapshot> SnapshotVertices(SlabShapeEditor editor)
        {
            var result = new List<VertexSnapshot>();

            foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
            {
                result.Add(new VertexSnapshot
                {
                    Vertex = vertex,
                    Position = vertex.Position,
                    VertexType = vertex.VertexType
                });
            }

            return result;
        }

        private static double ComputeAverageSpacing(List<VertexSnapshot> snapshot)
        {
            if (snapshot.Count < 2)
            {
                return 1.0; // Fallback to 1 foot if fewer than 2 vertices
            }

            double totalMinDist = 0;
            int count = 0;

            for (int i = 0; i < snapshot.Count; i++)
            {
                double minDist = double.MaxValue;

                for (int j = 0; j < snapshot.Count; j++)
                {
                    if (i == j) continue;

                    double dist = HorizontalDistance(snapshot[i].Position, snapshot[j].Position);
                    if (dist < minDist && dist > 1e-9)
                    {
                        minDist = dist;
                    }
                }

                if (minDist < double.MaxValue)
                {
                    totalMinDist += minDist;
                    count++;
                }
            }

            return count > 0 ? totalMinDist / count : 1.0;
        }

        private static List<VertexSnapshot> FindNeighbors(List<VertexSnapshot> snapshot, XYZ position, double searchRadius, SlabShapeVertex self)
        {
            var neighbors = new List<VertexSnapshot>();

            foreach (var entry in snapshot)
            {
                if (ReferenceEquals(entry.Vertex, self))
                {
                    continue;
                }

                double dist = HorizontalDistance(position, entry.Position);
                if (dist <= searchRadius && dist > 1e-9)
                {
                    neighbors.Add(entry);
                }
            }

            return neighbors;
        }

        private static double ComputePlanarZ(List<VertexSnapshot> neighbors, XYZ position)
        {
            if (neighbors.Count < 3)
            {
                // Not enough unique neighbors to form a plane; fallback to averaging
                return neighbors.Any() ? neighbors.Average(n => n.Position.Z) : position.Z;
            }

            // Take the 3 nearest neighbors to form the local plane (triangle)
            var sorted = neighbors.OrderBy(n => HorizontalDistance(position, n.Position)).ToList();
            XYZ p1 = sorted[0].Position;
            XYZ p2 = sorted[1].Position;
            XYZ p3 = sorted[2].Position;

            // Compute normal: n = (p2 - p1) x (p3 - p1)
            XYZ v1 = p2 - p1;
            XYZ v2 = p3 - p1;
            XYZ normal = v1.CrossProduct(v2);

            // Planar equation: normal.X*(x - p1.X) + normal.Y*(y - p1.Y) + normal.Z*(z - p1.Z) = 0
            // If normal.Z is near zero, the triangle is vertical (invalid for slab surface)
            if (Math.Abs(normal.Z) < 1e-9)
            {
                return neighbors.Average(n => n.Position.Z);
            }

            // z = p1.Z - (normal.X*(position.X - p1.X) + normal.Y*(position.Y - p1.Y)) / normal.Z
            double z = p1.Z - (normal.X * (position.X - p1.X) + normal.Y * (position.Y - p1.Y)) / normal.Z;
            return z;
        }

        private static double HorizontalDistance(XYZ a, XYZ b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static string FormatPoint(XYZ point)
        {
            return $"({point.X:F3}, {point.Y:F3}, {point.Z:F3})";
        }

        private static bool IsExpectedFixPointsException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.InvalidOperationException;
        }

        private struct VertexSnapshot
        {
            public SlabShapeVertex Vertex;
            public XYZ Position;
            public SlabShapeVertexType VertexType;
        }

        private readonly struct ProcessResult
        {
            public static ProcessResult Empty => new ProcessResult(0, 0, 0, 0);

            public ProcessResult(int beforeCount, int afterCount, int flagged, int corrected)
            {
                BeforeCount = beforeCount;
                AfterCount = afterCount;
                Flagged = flagged;
                Corrected = corrected;
            }

            public int BeforeCount { get; }
            public int AfterCount { get; }
            public int Flagged { get; }
            public int Corrected { get; }
        }
    }
}
