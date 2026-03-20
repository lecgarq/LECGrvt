using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
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
                processed++;
                double pct = (double)processed / elements.Count * 95;
                reporter.Report($"Processing {processed} of {elements.Count}...", pct);

                try
                {
                    IList<CurveLoop> loops = _geometryBoundaryService.ExtractLoops(element);
                    int boundaryCount = loops.Count;

                    if (boundaryCount <= 1)
                    {
                        reporter.Log($"ID {element.Id}: input boundaries {boundaryCount}, no split needed.");
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
                        reporter.LogWarning($"ID {element.Id}: unsupported element type '{element.GetType().Name}', skipped.");
                    }
                }
                catch (Exception ex) when (IsExpectedSplitBoundariesException(ex))
                {
                    reporter.LogError($"ID {element.Id}: failed - {ex.Message}");
                }
            }

            reporter.Report("Complete", 100);
        }

        private void SplitToposolid(Document doc, Element element, IList<CurveLoop> loops, IProgressReporter reporter)
        {
            var toposolid = (Toposolid)element;
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

                currentDoc.Delete(element.Id);
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

            reporter.Log($"ID {element.Id}: input boundaries {loopCount}, output IDs [{FormatElementIds(newToposolidIds)}].");
        }

        private void SplitFloor(Document doc, Element element, IList<CurveLoop> loops, IProgressReporter reporter)
        {
            var floor = (Floor)element;
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

                currentDoc.Delete(element.Id);
            });

            if (vertexSnapshots.Count > 0 && newFloorIds.Count > 0)
            {
                var outputSummaries = new List<string>();

                _transactionService.Run(doc, "Copy Floor Shape Points", currentDoc =>
                {
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
                            $"  Output {newFloorIds[i]}: added {interiorPointsAdded} interior points, adjusted {boundaryVerticesAdjusted} boundary vertices.");
                    }
                });

                foreach (string summary in outputSummaries)
                {
                    reporter.Log(summary);
                }
            }

            reporter.Log($"ID {element.Id}: input boundaries {loopCount}, output IDs [{FormatElementIds(newFloorIds)}].");
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
            Parameter? param = element.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            return param?.AsDouble() ?? 0.0;
        }

        private static void SetHeightOffset(Element element, double value)
        {
            Parameter? param = element.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            if (param != null && !param.IsReadOnly)
            {
                param.Set(value);
            }
        }

        private static bool IsPointInsideLoop(XYZ point, CurveLoop loop)
        {
            // First check if it's on the edge to avoid rounding issues
            if (IsPointOnLoop(point, loop)) return true;

            double testX = point.X;
            double testY = point.Y;
            int crossings = 0;

            foreach (Curve curve in loop)
            {
                IList<XYZ> tessellated = curve.Tessellate();

                for (int i = 0; i < tessellated.Count - 1; i++)
                {
                    XYZ p1 = tessellated[i];
                    XYZ p2 = tessellated[i + 1];

                    if (((p1.Y <= testY) && (p2.Y > testY)) || ((p2.Y <= testY) && (p1.Y > testY)))
                    {
                        double intersectX = (p2.X - p1.X) * (testY - p1.Y) / (p2.Y - p1.Y) + p1.X;
                        if (testX < intersectX)
                        {
                            crossings++;
                        }
                    }
                }
            }

            return (crossings % 2) == 1;
        }

        private static List<List<CurveLoop>> GroupLoopsByIslands(IList<CurveLoop> loops)
        {
            // Grouping by containment. 
            // We assume outer loops are followed by their inner loops as per IGeometryBoundaryService extract contract.
            var sorted = loops.ToList(); 
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
            // Pick a point that is likely to be inside the loop
            // We use the midpoint of a chord as a starting point.
            Curve curve = loop.Cast<Curve>().First();
            return (curve.GetEndPoint(0) + curve.GetEndPoint(1)) * 0.5;
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
