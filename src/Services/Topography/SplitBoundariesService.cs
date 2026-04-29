using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Clipper2Lib;
using LECG.Services.Interfaces;
using LECG.Utils;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class SplitBoundariesService : ISplitBoundariesService
    {
        private readonly ITransactionService _transactionService;
        private readonly IGeometryBoundaryService _geometryBoundaryService;
        private readonly ISlabService _slabService;

        public SplitBoundariesService(
            ITransactionService transactionService,
            IGeometryBoundaryService geometryBoundaryService,
            ISlabService slabService)
        {
            _transactionService = transactionService;
            _geometryBoundaryService = geometryBoundaryService;
            _slabService = slabService;
        }

        public void SplitBoundaries(Document doc, IList<Element> elements, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(elements);
            ArgumentNullException.ThrowIfNull(reporter);

            int processed = 0;

            foreach (Element element in elements)
            {
                if (element == null || !element.IsValidObject) continue;
                processed++;
                double pct = (double)processed / elements.Count * 95;
                reporter.Report($"Processing {processed} of {elements.Count}...", pct);

                ElementId originalId = element.Id;

                try
                {
                    // Document regenerates automatically after each element split commits
                    IList<CurveLoop> loops = _geometryBoundaryService.ExtractLoops(element);
                    int boundaryCount = loops.Count;

                    if (boundaryCount <= 1)
                    {
                        reporter.Log($"ID {originalId}: input boundaries {boundaryCount}, no split needed.");
                        continue;
                    }

                    if (element is Toposolid)
                    {
                        SplitToposolid(doc, element, loops, reporter);
                    }
                    else if (element is Floor)
                    {
                        SplitFloor(doc, element, loops, reporter);
                    }
                    else
                    {
                        reporter.LogWarning($"ID {originalId}: unsupported element type '{element.GetType().Name}', skipped.");
                    }
                }
                catch (Exception ex) when (IsExpectedSplitBoundariesException(ex))
                {
                    reporter.LogError($"ID {originalId}: failed - {ex.Message}");
                }
            }

            reporter.Report("Complete", 100);
        }

        private void SplitToposolid(Document doc, Element element, IList<CurveLoop> loops, IProgressReporter reporter)
        {
            var toposolid = (Toposolid)element;
            ElementId originalId = toposolid.Id;
            int loopCount = loops.Count;
            ElementId typeId = toposolid.GetTypeId();
            ElementId levelId = toposolid.LevelId;
            double heightOffset = GetHeightOffset(element);
            List<VertexSnapshot> vertexSnapshots = SnapshotVertices(element);

            var newToposolidIds = new List<ElementId>();
            var loopAssignments = new List<CurveLoop>();

            var islands = GroupLoopsByIslands(loops);

            _transactionService.Run(doc, "Split Toposolid Boundaries", currentDoc =>
            {
                foreach (List<CurveLoop> islandProfile in islands)
                {
                    Toposolid newToposolid = Toposolid.Create(currentDoc, islandProfile, typeId, levelId);
                    SetHeightOffset(newToposolid, heightOffset);
                    newToposolidIds.Add(newToposolid.Id);
                    // For Toposolids, the first loop in an island profile is the outer boundary
                    loopAssignments.Add(islandProfile[0]);
                }

                currentDoc.Delete(originalId);
            });

            if (vertexSnapshots.Count > 0 && newToposolidIds.Count > 0)
            {
                var outputSummaries = new List<string>();

                _transactionService.Run(doc, "Copy Toposolid Parameters", currentDoc =>
                {
                    for (int i = 0; i < newToposolidIds.Count; i++)
                    {
                        Element? newToposolid = currentDoc.GetElement(newToposolidIds[i]);
                        if (newToposolid == null)
                        {
                            continue;
                        }

                        SlabShapeEditor? editor = _slabService.GetEditor(newToposolid);
                        if (editor == null)
                        {
                            continue;
                        }

                        if (!editor.IsEnabled)
                        {
                            editor.Enable();
                        }

                        CurveLoop loop = loopAssignments[i];
                        int interiorPointsAdded = 0;
                        int boundaryVerticesAdjusted = 0;

                        foreach (VertexSnapshot snapshot in vertexSnapshots)
                        {
                            if (snapshot.VertexType == SlabShapeVertexType.Interior)
                            {
                                if (!IsPointInsideLoop(snapshot.Position, loop))
                                {
                                    continue;
                                }

                                try
                                {
                                    editor.AddPoint(snapshot.Position);
                                    interiorPointsAdded++;
                                }
                                catch (RevitExceptions.InvalidOperationException)
                                {
                                    // Ignore regenerated edge cases where Revit rejects a point as non-interior.
                                }
                            }
                            else if (snapshot.VertexType == SlabShapeVertexType.Edge || snapshot.VertexType == SlabShapeVertexType.Corner)
                            {
                                if (!IsPointOnLoop(snapshot.Position, loop))
                                {
                                    continue;
                                }

                                if (TryAdjustBoundaryVertex(editor, snapshot.Position))
                                {
                                    boundaryVerticesAdjusted++;
                                }
                            }
                        }

                        outputSummaries.Add(
                            $"  Output {newToposolidIds[i]}: added {interiorPointsAdded} interior points, adjusted {boundaryVerticesAdjusted} boundary vertices.");
                    }
                });

                foreach (string summary in outputSummaries)
                {
                    reporter.Log(summary);
                }
            }

            reporter.Log($"ID {originalId}: input boundaries {loopCount}, output IDs [{FormatElementIds(newToposolidIds)}].");
        }

        private void SplitFloor(Document doc, Element element, IList<CurveLoop> loops, IProgressReporter reporter)
        {
            var floor = (Floor)element;
            ElementId originalId = floor.Id;
            int loopCount = loops.Count;

            ElementId typeId = floor.GetTypeId();
            ElementId levelId = floor.LevelId;
            double heightOffset = GetHeightOffset(element);
            List<VertexSnapshot> vertexSnapshots = SnapshotVertices(element);

            var newFloorIds = new List<ElementId>();
            var loopAssignments = new List<CurveLoop>();

            var islands = GroupLoopsByIslands(loops);

            _transactionService.Run(doc, "Split Floor Boundaries", currentDoc =>
            {
                foreach (List<CurveLoop> islandProfile in islands)
                {
                    Floor newFloor = Floor.Create(currentDoc, islandProfile, typeId, levelId);
                    SetHeightOffset(newFloor, heightOffset);
                    newFloorIds.Add(newFloor.Id);
                    loopAssignments.Add(islandProfile[0]);
                }

                currentDoc.Delete(originalId);
            });

            if (vertexSnapshots.Count > 0 && newFloorIds.Count > 0)
            {
                var outputSummaries = new List<string>();

                _transactionService.Run(doc, "Copy Floor Shape Points", currentDoc =>
                {
                    // Floor's SlabShapeEditor.AddPoint() expects Z relative to the
                    // reference surface (level elevation + height offset). The snapshotted
                    // vertex positions use absolute Z, so we must subtract the reference.
                    Level? level = currentDoc.GetElement(levelId) as Level;
                    double referenceElevation = (level?.Elevation ?? 0.0) + heightOffset;

                    for (int i = 0; i < newFloorIds.Count; i++)
                    {
                        Element? newFloor = currentDoc.GetElement(newFloorIds[i]);
                        if (newFloor == null)
                        {
                            continue;
                        }

                        SlabShapeEditor? editor = _slabService.GetEditor(newFloor);
                        if (editor == null)
                        {
                            continue;
                        }

                        if (!editor.IsEnabled)
                        {
                            editor.Enable();
                        }

                        CurveLoop loop = loopAssignments[i];
                        int interiorPointsAdded = 0;
                        int boundaryVerticesAdjusted = 0;

                        foreach (VertexSnapshot snapshot in vertexSnapshots)
                        {
                            if (snapshot.VertexType == SlabShapeVertexType.Interior)
                            {
                                if (!IsPointInsideLoop(snapshot.Position, loop))
                                {
                                    continue;
                                }

                                try
                                {
                                    double relativeZ = snapshot.Position.Z - referenceElevation;
                                    editor.AddPoint(new XYZ(snapshot.Position.X, snapshot.Position.Y, relativeZ));
                                    interiorPointsAdded++;
                                }
                                catch (RevitExceptions.InvalidOperationException)
                                {
                                    // Ignore regenerated edge cases where Revit rejects a point as non-interior.
                                }
                            }
                            else if (snapshot.VertexType == SlabShapeVertexType.Edge || snapshot.VertexType == SlabShapeVertexType.Corner)
                            {
                                if (!IsPointOnLoop(snapshot.Position, loop))
                                {
                                    continue;
                                }

                                if (TryAdjustBoundaryVertex(editor, snapshot.Position))
                                {
                                    boundaryVerticesAdjusted++;
                                }
                            }
                        }

                        outputSummaries.Add(
                            $"  Output {newFloorIds[i]}: added {interiorPointsAdded} interior points, adjusted {boundaryVerticesAdjusted} boundary vertices.");
                    }
                });

                foreach (string summary in outputSummaries)
                {
                    reporter.Log(summary);
                }
            }

            reporter.Log($"ID {originalId}: input boundaries {loopCount}, output IDs [{FormatElementIds(newFloorIds)}].");
        }

        private List<VertexSnapshot> SnapshotVertices(Element element)
        {
            SlabShapeEditor? editor = _slabService.GetEditor(element);
            if (editor == null || !editor.IsEnabled)
            {
                return new List<VertexSnapshot>();
            }

            var snapshots = new List<VertexSnapshot>();

            foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
            {
                snapshots.Add(new VertexSnapshot(vertex.Position, vertex.VertexType));
            }

            return snapshots;
        }

        private static double GetHeightOffset(Element element)
        {
            Parameter? floorParam = element.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            if (floorParam != null) return floorParam.AsDouble();

            Parameter? topoParam = element.get_Parameter(BuiltInParameter.TOPOSOLID_HEIGHTABOVELEVEL_PARAM);
            return topoParam?.AsDouble() ?? 0.0;
        }

        private static void SetHeightOffset(Element element, double value)
        {
            Parameter? param = element.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)
                ?? element.get_Parameter(BuiltInParameter.TOPOSOLID_HEIGHTABOVELEVEL_PARAM);

            if (param != null && !param.IsReadOnly)
            {
                param.Set(value);
            }
        }

        private static bool IsPointInsideLoop(XYZ point, CurveLoop loop)
        {
            if (IsPointOnLoop(point, loop)) return true;

            PathD polygon = ClipperUtils.CurveLoopToPathD(loop);
            PointD testPt = new PointD(point.X, point.Y);
            PointInPolygonResult result = Clipper.PointInPolygon(testPt, polygon);
            return result != PointInPolygonResult.IsOutside;
        }

        private static List<List<CurveLoop>> GroupLoopsByIslands(IList<CurveLoop> loops)
        {
            // Sorting by area is CRITICAL to identify outer loops first.
            // Larger loops usually contain smaller ones.
            var sorted = loops
                .OrderByDescending(l => ComputeLoopArea(l))
                .ToList();

            var islands = new List<List<CurveLoop>>();
            var handled = new bool[sorted.Count];

            for (int i = 0; i < sorted.Count; i++)
            {
                if (handled[i]) continue;

                // Outer loop of a new island
                var island = new List<CurveLoop> { sorted[i] };
                handled[i] = true;

                // Find all loops contained in this outer loop
                XYZ testPoint = GetReferencePointInLoop(sorted[i]);

                for (int j = i + 1; j < sorted.Count; j++)
                {
                    if (handled[j]) continue;

                    XYZ neighborPoint = GetReferencePointInLoop(sorted[j]);
                    if (IsPointInsideLoop(neighborPoint, sorted[i]))
                    {
                        island.Add(sorted[j]);
                        handled[j] = true;
                    }
                }

                islands.Add(island);
            }

            return islands;
        }

        private static XYZ GetReferencePointInLoop(CurveLoop loop)
        {
            // A more robust way to find an internal point than BBox center (which fails for concave shapes):
            // Pick a point on the first curve and move it slightly "inside" based on the curve normal and loop orientation.
            Curve curve = loop.Cast<Curve>().First();
            XYZ p = curve.Evaluate(0.5, true);
            XYZ tangent = curve.ComputeDerivatives(0.5, true).BasisX.Normalize();
            XYZ normal = new XYZ(-tangent.Y, tangent.X, 0); // 90 deg rotation in XY plane

            // Check if loop is CCW to determine which side is "inside"
            bool isCcw = loop.IsCounterclockwise(XYZ.BasisZ);
            XYZ offsetDir = isCcw ? normal : -normal;

            XYZ testPoint = p + offsetDir * 0.1; // Offset by 0.1 feet

            // Safety check: if the offset point is still outside (shouldn't happen for simple curves),
            // fallback to tessellation average (centroid-ish)
            if (!IsPointInsideLoop(testPoint, loop))
            {
                var points = loop.Cast<Curve>().SelectMany(c => c.Tessellate()).ToList();
                return new XYZ(points.Average(pt => pt.X), points.Average(pt => pt.Y), points.Average(pt => pt.Z));
            }

            return testPoint;
        }

        private static double ComputeLoopArea(CurveLoop loop)
        {
            PathD polygon = ClipperUtils.CurveLoopToPathD(loop);
            return Math.Abs(Clipper.Area(polygon));
        }

        private static bool IsPointOnLoop(XYZ point, CurveLoop loop)
        {
            foreach (Curve curve in loop)
            {
                IList<XYZ> tessellated = curve.Tessellate();

                for (int i = 0; i < tessellated.Count - 1; i++)
                {
                    if (IsPointOnSegment(point, tessellated[i], tessellated[i + 1]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsPointOnSegment(XYZ point, XYZ start, XYZ end)
        {
            const double tolerance = 1e-4;

            double segmentLength = HorizontalDistance(start, end);
            double distance = HorizontalDistance(point, start) + HorizontalDistance(point, end);
            return Math.Abs(distance - segmentLength) <= tolerance;
        }

        private static bool TryAdjustBoundaryVertex(SlabShapeEditor editor, XYZ targetPosition)
        {
            const double tolerance = 1e-4;

            SlabShapeVertex? matchingVertex = editor.SlabShapeVertices
                .Cast<SlabShapeVertex>()
                .Where(vertex => vertex.VertexType != SlabShapeVertexType.Interior)
                .OrderBy(vertex => HorizontalDistance(vertex.Position, targetPosition))
                .FirstOrDefault();

            if (matchingVertex == null)
            {
                return false;
            }

            if (HorizontalDistance(matchingVertex.Position, targetPosition) > tolerance)
            {
                return false;
            }

            double deltaZ = targetPosition.Z - matchingVertex.Position.Z;
            if (Math.Abs(deltaZ) <= 1e-9)
            {
                return false;
            }

            try
            {
                editor.ModifySubElement(matchingVertex, deltaZ);
                return true;
            }
            catch (Exception ex) when (IsExpectedSplitBoundariesException(ex))
            {
                return false;
            }
        }

        private static double HorizontalDistance(XYZ a, XYZ b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static string FormatElementIds(IEnumerable<ElementId> elementIds)
        {
            return string.Join(", ", elementIds.Select(id => id.ToString()));
        }

        private static bool IsExpectedSplitBoundariesException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }

        private readonly struct VertexSnapshot
        {
            public VertexSnapshot(XYZ position, SlabShapeVertexType vertexType)
            {
                Position = position;
                VertexType = vertexType;
            }

            public XYZ Position { get; }
            public SlabShapeVertexType VertexType { get; }
        }
    }
}
