using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;
using LECG.Core.Geometry;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public readonly record struct SplitBoundaryInspection(int BoundaryCount, int IslandCount)
    {
        public bool CanSplit => IslandCount > 1;
    }

    public class SplitBoundariesService
    {
        private static readonly ConditionalWeakTable<CurveLoop, List<XYZ>> LoopPolygonCache = new();
        private readonly ITransactionService _transactionService;
        private readonly GeometryBoundaryService _geometryBoundaryService;
        private readonly SlabService _slabService;

        public SplitBoundariesService(
            ITransactionService transactionService,
            GeometryBoundaryService geometryBoundaryService,
            SlabService slabService)
        {
            _transactionService = transactionService;
            _geometryBoundaryService = geometryBoundaryService;
            _slabService = slabService;
        }

        public SplitBoundaryInspection InspectBoundaries(Element element)
        {
            ArgumentNullException.ThrowIfNull(element);
            IList<CurveLoop> loops = _geometryBoundaryService.ExtractLoops(element);
            return new SplitBoundaryInspection(loops.Count, GroupLoopsByIslands(loops).Count);
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
            bool sourceWasPinned = toposolid.Pinned;
            List<VertexSnapshot> vertexSnapshots = SnapshotVertices(element);
            List<CreaseSnapshot> creaseSnapshots = SnapshotCreases(toposolid);
            TriangleSurface sourceSurface = SnapshotSurface(toposolid);
            var islands = GroupLoopsByIslands(loops);
            if (islands.Count <= 1)
            {
                reporter.Log($"ID {originalId}: boundaries form one toposolid island, no split needed.");
                return;
            }

            var outputIds = new List<ElementId>();
            using var group = new TransactionGroup(doc, "Split Toposolid Boundaries");
            group.Start();
            try
            {
                for (int islandIndex = 0; islandIndex < islands.Count; islandIndex++)
                {
                    reporter.Log($"ID {originalId}: preserving surface for island {islandIndex + 1} of {islands.Count}.");
                    ElementId outputId = CopyElementInTransaction(doc, originalId, XYZ.Zero, reporter);
                    RemoveCopiedSketchConstraints(doc, outputId, islandIndex, islands.Count, reporter);
                    TrimCopiedSketch(doc, outputId, islandIndex, islands.Count, reporter);
                    Toposolid output = doc.GetElement(outputId) as Toposolid
                        ?? throw new InvalidOperationException($"Copied Toposolid {outputId} was not found.");
                    RestoreCopiedCreases(doc, output, islands[islandIndex], creaseSnapshots, reporter);
                    VerifyCopiedSurface(output, islands[islandIndex], sourceSurface,
                        vertexSnapshots, creaseSnapshots);
                    if (sourceWasPinned)
                        SetElementPinnedInTransaction(doc, outputId, true, reporter);
                    outputIds.Add(outputId);
                }

                DeleteElementInTransaction(doc, originalId, reporter);
                if (group.Assimilate() != TransactionStatus.Committed)
                    throw new InvalidOperationException($"Could not commit split for Toposolid {originalId}.");
            }
            catch
            {
                if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
                outputIds.Clear();
                throw;
            }

            reporter.Log($"ID {originalId}: input boundaries {loopCount}, output IDs [{FormatElementIds(outputIds)}].");
        }

        private void SplitFloor(Document doc, Element element, IList<CurveLoop> loops, IProgressReporter reporter)
        {
            var floor = (Floor)element;
            ElementId originalId = floor.Id;
            int loopCount = loops.Count;
            bool sourceWasPinned = floor.Pinned;

            ElementId typeId = floor.GetTypeId();
            ElementId levelId = floor.LevelId;
            double heightOffset = GetHeightOffset(element);
            List<VertexSnapshot> vertexSnapshots = SnapshotVertices(element);
            List<CreaseSnapshot> creaseSnapshots = SnapshotCreases(floor);
            TriangleSurface sourceSurface = SnapshotSurface(floor);

            var newFloorIds = new List<ElementId>();

            var islands = GroupLoopsByIslands(loops)
                .Select(profile => SplitProfileAtBoundaryControls(profile, vertexSnapshots,
                    doc.Application.ShortCurveTolerance)).ToList();

            if (islands.Count <= 1)
            {
                reporter.Log($"ID {originalId}: boundaries form one floor island, no split needed.");
                return;
            }

            _transactionService.Run(doc, "Split Floor Boundaries", currentDoc =>
            {
                foreach (List<CurveLoop> islandProfile in islands)
                {
                    Floor newFloor = Floor.Create(currentDoc, islandProfile, typeId, levelId);
                    SetHeightOffset(newFloor, heightOffset);
                    newFloorIds.Add(newFloor.Id);
                }
                currentDoc.Regenerate();
                for (int i = 0; i < newFloorIds.Count; i++)
                {
                    Floor newFloor = currentDoc.GetElement(newFloorIds[i]) as Floor
                        ?? throw new InvalidOperationException($"Created floor {newFloorIds[i]} was not found.");
                    RestoreSurface(currentDoc, newFloor, islands[i], sourceSurface, vertexSnapshots,
                        reporter, creaseSnapshots, refineSurface: false);
                    newFloor.Pinned = sourceWasPinned;
                }
                if (floor.Pinned) floor.Pinned = false;
                currentDoc.Delete(originalId);
            });

            reporter.Log($"ID {originalId}: input boundaries {loopCount}, output IDs [{FormatElementIds(newFloorIds)}].");
        }

        private static IList<CurveLoop> ReadSketchLoops(Document doc, ElementId sketchId)
        {
            Sketch sketch = doc.GetElement(sketchId) as Sketch
                ?? throw new InvalidOperationException($"Sketch {sketchId} was not found.");
            var loops = new List<CurveLoop>();
            foreach (CurveArray curveArray in sketch.Profile)
                loops.Add(CurveLoop.Create(curveArray.Cast<Curve>().ToList()));
            return loops;
        }

        private static ElementId CopyElementInTransaction(Document doc, ElementId sourceId,
            XYZ offset, IProgressReporter reporter)
        {
            using var copy = new Transaction(doc, "Copy shaped slab");
            copy.Start();
            ConfigureFailures(copy, reporter);
            ElementId copyId = ElementTransformUtils.CopyElements(doc,
                new List<ElementId> { sourceId }, offset).Single();
            Element? copiedElement = doc.GetElement(copyId);
            if (copiedElement?.Pinned == true) copiedElement.Pinned = false;
            if (copy.Commit() != TransactionStatus.Committed)
                throw new InvalidOperationException($"Could not copy element {sourceId}.");
            return copyId;
        }

        private static void RemoveCopiedSketchConstraints(Document doc, ElementId copyId,
            int keepIsland, int expectedIslands, IProgressReporter reporter)
        {
            Toposolid copy = doc.GetElement(copyId) as Toposolid
                ?? throw new InvalidOperationException($"Copied Toposolid {copyId} was not found.");
            List<List<CurveLoop>> islands = GroupLoopsByIslands(ReadSketchLoops(doc, copy.SketchId));
            if (islands.Count != expectedIslands)
                throw new InvalidOperationException(
                    $"Copied Toposolid has {islands.Count} islands; expected {expectedIslands}.");
            var modelCurves = ((Sketch)doc.GetElement(copy.SketchId)).GetAllElements()
                .Select(doc.GetElement).OfType<ModelCurve>().ToList();
            var removeCurveIds = new HashSet<ElementId>();
            for (int islandIndex = 0; islandIndex < islands.Count; islandIndex++)
            {
                if (islandIndex == keepIsland) continue;
                foreach (Curve curve in islands[islandIndex].SelectMany(loop => loop.Cast<Curve>()))
                    removeCurveIds.Add(ResolveSketchCurveId(doc, curve, modelCurves));
            }

            List<ElementId> constraintIds = removeCurveIds
                .SelectMany(id => doc.GetElement(id)?.GetDependentElements(null)
                    ?? Array.Empty<ElementId>())
                .Distinct()
                .Where(id =>
                {
                    Element? dependent = doc.GetElement(id);
                    return dependent != null
                        && dependent.GetType() == typeof(Element)
                        && dependent.Category == null
                        && dependent.GetTypeId() == ElementId.InvalidElementId;
                })
                .ToList();
            if (constraintIds.Count == 0) return;

            using var remove = new Transaction(doc, "Remove copied sketch constraints");
            remove.Start();
            ConfigureFailures(remove, reporter);
            doc.Delete(constraintIds);
            if (remove.Commit() != TransactionStatus.Committed)
                throw new InvalidOperationException(
                    $"Could not remove sketch constraints from copied Toposolid {copyId}.");
            reporter.Log($"Copied Toposolid {copyId}: removed {constraintIds.Count} sketch constraints.");
        }

        private static void DeleteElementInTransaction(Document doc, ElementId elementId,
            IProgressReporter reporter)
        {
            using var delete = new Transaction(doc, "Delete original multi-boundary Toposolid");
            delete.Start();
            ConfigureFailures(delete, reporter);
            Element? element = doc.GetElement(elementId);
            if (element?.Pinned == true) element.Pinned = false;
            doc.Delete(elementId);
            if (delete.Commit() != TransactionStatus.Committed)
                throw new InvalidOperationException($"Could not delete original Toposolid {elementId}.");
        }

        private static void SetElementPinnedInTransaction(Document doc, ElementId elementId,
            bool pinned, IProgressReporter reporter)
        {
            using var transaction = new Transaction(doc, "Restore split element pin state");
            transaction.Start();
            ConfigureFailures(transaction, reporter);
            Element element = doc.GetElement(elementId)
                ?? throw new InvalidOperationException($"Split element {elementId} was not found.");
            element.Pinned = pinned;
            if (transaction.Commit() != TransactionStatus.Committed)
                throw new InvalidOperationException($"Could not restore pin state on element {elementId}.");
        }

        private static void RestoreCopiedCreases(Document doc, Toposolid output,
            List<CurveLoop> profile, IReadOnlyList<CreaseSnapshot> sourceCreases,
            IProgressReporter reporter)
        {
            List<CreaseSnapshot> retained = sourceCreases
                .Where(crease => crease.CreaseType != SlabShapeCreaseType.Boundary)
                .Where(crease => IsPointInsideProfile(crease.Start, profile)
                    || profile.Any(loop => IsPointOnLoop(crease.Start, loop)))
                .Where(crease => IsPointInsideProfile(crease.End, profile)
                    || profile.Any(loop => IsPointOnLoop(crease.End, loop)))
                .OrderBy(crease => crease.CreaseType == SlabShapeCreaseType.UserDrawn ? 0 : 1)
                .ToList();
            if (retained.Count == 0) return;

            using var restore = new Transaction(doc, "Restore Toposolid surface creases");
            restore.Start();
            ConfigureFailures(restore, reporter);
            SlabShapeEditor editor = output.GetSlabShapeEditor();
            int restored = 0;
            foreach (CreaseSnapshot crease in retained)
            {
                if (HasMatchingCrease(editor, crease)) continue;
                SlabShapeVertex start = FindShapeVertex(editor, crease.Start, output.Id);
                SlabShapeVertex end = FindShapeVertex(editor, crease.End, output.Id);
                editor.AddSplitLine(start, end);
                restored++;
            }
            if (restore.Commit() != TransactionStatus.Committed)
                throw new InvalidOperationException($"Could not restore surface creases on Toposolid {output.Id}.");
            reporter.Log($"Copied Toposolid {output.Id}: retained {retained.Count} source creases, "
                + $"has {editor.SlabShapeCreases.Size} output creases, restored {restored}.");
        }

        private static bool HasMatchingCrease(SlabShapeEditor editor, CreaseSnapshot expected)
        {
            foreach (SlabShapeCrease crease in editor.SlabShapeCreases)
            {
                List<XYZ> endpoints = crease.EndPoints.Cast<SlabShapeVertex>()
                    .Select(vertex => vertex.Position).ToList();
                if (endpoints.Count != 2) continue;
                if (SameShapePoint(endpoints[0], expected.Start)
                        && SameShapePoint(endpoints[1], expected.End)
                    || SameShapePoint(endpoints[0], expected.End)
                        && SameShapePoint(endpoints[1], expected.Start))
                    return true;
            }
            return false;
        }

        private static SlabShapeVertex FindShapeVertex(SlabShapeEditor editor, XYZ expected,
            ElementId outputId)
        {
            SlabShapeVertex? match = editor.SlabShapeVertices.Cast<SlabShapeVertex>()
                .FirstOrDefault(vertex => SameShapePoint(vertex.Position, expected));
            return match ?? throw new InvalidOperationException(
                $"Element {outputId} lost crease endpoint "
                + $"({expected.X:F6}, {expected.Y:F6}, {expected.Z:F6}).");
        }

        private static void RestoreShapeCreases(SlabShapeEditor editor,
            IReadOnlyList<CreaseSnapshot> sourceCreases, List<CurveLoop> profile, ElementId outputId)
        {
            foreach (CreaseSnapshot crease in sourceCreases
                .Where(item => item.CreaseType != SlabShapeCreaseType.Boundary)
                .Where(item => IsPointInsideProfile(item.Start, profile)
                    || profile.Any(loop => IsPointOnLoop(item.Start, loop)))
                .Where(item => IsPointInsideProfile(item.End, profile)
                    || profile.Any(loop => IsPointOnLoop(item.End, loop)))
                .OrderBy(item => item.CreaseType == SlabShapeCreaseType.UserDrawn ? 0 : 1))
            {
                if (HasMatchingCrease(editor, crease)) continue;
                SlabShapeVertex start = FindShapeVertex(editor, crease.Start, outputId);
                SlabShapeVertex end = FindShapeVertex(editor, crease.End, outputId);
                editor.AddSplitLine(start, end);
            }
        }

        private static bool SameShapePoint(XYZ first, XYZ second)
        {
            return HorizontalDistance(first, second) <= 1e-4
                && Math.Abs(first.Z - second.Z) <= 1e-4;
        }

        private static void ConfigureFailures(Transaction transaction, IProgressReporter reporter)
        {
            FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new SplitWarningsPreprocessor(reporter));
            options.SetForcedModalHandling(false);
            transaction.SetFailureHandlingOptions(options);
        }

        private static void TrimCopiedSketch(Document doc, ElementId copyId, int keepIsland,
            int expectedIslands, IProgressReporter reporter)
        {
            Toposolid copy = doc.GetElement(copyId) as Toposolid
                ?? throw new InvalidOperationException($"Copied toposolid {copyId} was not found.");
            using (var scope = new SketchEditScope(doc, "Keep one Toposolid island"))
            {
                scope.Start(copy.SketchId);
                using var edit = new Transaction(doc, "Remove other boundary loops");
                edit.Start();
                List<List<CurveLoop>> islands = GroupLoopsByIslands(ReadSketchLoops(doc, copy.SketchId));
                if (islands.Count != expectedIslands)
                    throw new InvalidOperationException(
                        $"Copied Toposolid has {islands.Count} islands; expected {expectedIslands}.");
                var modelCurves = ((Sketch)doc.GetElement(copy.SketchId)).GetAllElements()
                    .Select(doc.GetElement).OfType<ModelCurve>().ToList();
                var removeIds = new List<ElementId>();
                for (int islandIndex = 0; islandIndex < islands.Count; islandIndex++)
                {
                    if (islandIndex == keepIsland) continue;
                    foreach (CurveLoop loop in islands[islandIndex].OrderBy(ComputeLoopArea))
                    {
                        List<ElementId> loopIds = loop.Cast<Curve>()
                            .Select(curve => ResolveSketchCurveId(doc, curve, modelCurves)).ToList();
                        removeIds.AddRange(loopIds);
                    }
                }
                removeIds = removeIds.Distinct().ToList();
                if (removeIds.Count == 0)
                    throw new InvalidOperationException($"No boundary loops were removed from copied Toposolid {copyId}.");
                try
                {
                    doc.Delete(removeIds);
                }
                catch (Exception ex) when (IsExpectedSplitBoundariesException(ex))
                {
                    throw new InvalidOperationException(
                        $"Could not remove {removeIds.Count} unwanted sketch curves from copied "
                        + $"Toposolid {copyId}: {ex.Message}", ex);
                }
                if (edit.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException($"Could not trim copied Toposolid {copyId}.");
                scope.Commit(new SplitWarningsPreprocessor(reporter));
            }
            if (GroupLoopsByIslands(ReadSketchLoops(doc, copy.SketchId)).Count != 1)
                throw new InvalidOperationException($"Copied Toposolid {copyId} still has multiple islands.");
        }

        private static ElementId ResolveSketchCurveId(Document doc, Curve profileCurve,
            IReadOnlyList<ModelCurve> modelCurves)
        {
            ElementId? referenceId = profileCurve.Reference?.ElementId;
            if (referenceId != null && modelCurves.Any(modelCurve =>
                modelCurve.IsValidObject && modelCurve.Id == referenceId))
                return referenceId;
            List<ModelCurve> matches = modelCurves.Where(modelCurve =>
                modelCurve.IsValidObject && AreSameCurve(profileCurve, modelCurve.GeometryCurve)).ToList();
            if (matches.Count == 1) return matches[0].Id;
            throw new InvalidOperationException(
                $"Sketch curve {profileCurve.GetType().Name} near {profileCurve.GetEndPoint(0)} "
                + $"matched {matches.Count} model curves.");
        }

        private static bool AreSameCurve(Curve a, Curve b)
        {
            if (Math.Abs(a.Length - b.Length) > 1e-4
                || a.Evaluate(0.5, true).DistanceTo(b.Evaluate(0.5, true)) > 1e-4)
                return false;
            return a.GetEndPoint(0).DistanceTo(b.GetEndPoint(0)) <= 1e-4
                    && a.GetEndPoint(1).DistanceTo(b.GetEndPoint(1)) <= 1e-4
                || a.GetEndPoint(0).DistanceTo(b.GetEndPoint(1)) <= 1e-4
                    && a.GetEndPoint(1).DistanceTo(b.GetEndPoint(0)) <= 1e-4;
        }

        private static void VerifyCopiedSurface(Toposolid output, List<CurveLoop> profile,
            TriangleSurface sourceSurface, IReadOnlyList<VertexSnapshot> sourceVertices,
            IReadOnlyList<CreaseSnapshot> sourceCreases)
        {
            SlabShapeEditor editor = output.GetSlabShapeEditor();
            if (!editor.IsEnabled)
                throw new InvalidOperationException($"Copied Toposolid {output.Id} lost its shape editor.");
            List<XYZ> outputVertices = editor.SlabShapeVertices.Cast<SlabShapeVertex>()
                .Select(vertex => vertex.Position).ToList();
            foreach (VertexSnapshot sourceVertex in sourceVertices)
            {
                XYZ point = sourceVertex.Position;
                if (!IsPointInsideProfile(point, profile)
                    && !profile.Any(loop => IsPointOnLoop(point, loop))) continue;
                if (!outputVertices.Any(vertex => HorizontalDistance(vertex, point) <= 1e-4
                    && Math.Abs(vertex.Z - point.Z) <= 1e-4))
                    throw new InvalidOperationException($"Copied Toposolid {output.Id} lost source shape point "
                        + $"({point.X:F6}, {point.Y:F6}, {point.Z:F6}).");
            }

            TriangleSurface outputSurface = SnapshotSurface(output);
            foreach (VertexSnapshot vertex in sourceVertices)
            {
                XYZ point = vertex.Position;
                if (!IsPointInsideProfile(point, profile)
                    && !profile.Any(loop => IsPointOnLoop(point, loop))) continue;
                VerifySurfaceSample(sourceSurface, outputSurface,
                    new SurfacePoint(point.X, point.Y, point.Z), output.Id);
            }
            foreach (CreaseSnapshot crease in sourceCreases)
            {
                if (crease.CreaseType == SlabShapeCreaseType.Boundary) continue;
                if (!IsPointInsideProfile(crease.Start, profile)
                        && !profile.Any(loop => IsPointOnLoop(crease.Start, loop))
                    || !IsPointInsideProfile(crease.End, profile)
                        && !profile.Any(loop => IsPointOnLoop(crease.End, loop))) continue;
                XYZ midpoint = (crease.Start + crease.End) / 2;
                VerifySurfaceSample(sourceSurface, outputSurface,
                    new SurfacePoint(midpoint.X, midpoint.Y, midpoint.Z), output.Id);
            }
        }

        private static void VerifySurfaceSample(TriangleSurface source, TriangleSurface output,
            SurfacePoint sample, ElementId outputId)
        {
            bool sourceFound = source.TryGetElevation(sample.X, sample.Y, out double sourceZ, 0.001);
            bool outputFound = output.TryGetElevation(sample.X, sample.Y, out double outputZ, 0.001);
            if (!sourceFound || !outputFound || Math.Abs(sourceZ - outputZ) > 1e-3)
                throw new InvalidOperationException($"Copied Toposolid {outputId} differs from the source "
                    + $"at ({sample.X:F6}, {sample.Y:F6}): source Z {sourceZ:F6}, "
                    + $"output Z {outputZ:F6}, difference {Math.Abs(sourceZ - outputZ):F6}.");
        }

        private sealed class SplitWarningsPreprocessor : IFailuresPreprocessor
        {
            private readonly IProgressReporter _reporter;
            public SplitWarningsPreprocessor(IProgressReporter reporter) => _reporter = reporter;
            public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
            {
                foreach (FailureMessageAccessor failure in accessor.GetFailureMessages())
                {
                    if (failure.GetSeverity() != FailureSeverity.Warning) continue;
                    _reporter.LogWarning(failure.GetDescriptionText());
                    accessor.DeleteWarning(failure);
                }
                return FailureProcessingResult.Continue;
            }
        }

        private static void VerifyFloorSurface(Floor source, Floor output, List<CurveLoop> profile)
        {
            CurveLoop outer = profile[0];
            bool counterclockwise = outer.IsCounterclockwise(XYZ.BasisZ);
            int checkedPoints = 0;
            foreach (Curve curve in outer)
            {
                XYZ midpoint = curve.Evaluate(0.5, true);
                XYZ tangent = curve.ComputeDerivatives(0.5, true).BasisX;
                XYZ inward = new XYZ(-tangent.Y, tangent.X, 0).Normalize();
                XYZ sample = midpoint + (counterclockwise ? inward : -inward) * 0.01;
                XYZ? sourcePoint = source.GetVerticalProjectionPoint(sample, FloorFace.Top);
                if (sourcePoint == null) continue;

                XYZ? outputPoint = output.GetVerticalProjectionPoint(sample, FloorFace.Top);
                if (outputPoint == null || Math.Abs(outputPoint.Z - sourcePoint.Z) > 1e-4)
                {
                    throw new InvalidOperationException(
                        $"Floor {output.Id} surface elevation differs from source {source.Id} near ({sample.X:F4}, {sample.Y:F4}).");
                }
                checkedPoints++;
            }
            if (checkedPoints == 0)
            {
                throw new InvalidOperationException($"Floor {output.Id} surface could not be verified against source {source.Id}.");
            }
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

        private List<CreaseSnapshot> SnapshotCreases(HostObject source)
        {
            SlabShapeEditor? editor = _slabService.GetEditor(source);
            if (editor == null) return new List<CreaseSnapshot>();
            if (!editor.IsEnabled) return new List<CreaseSnapshot>();
            return editor.SlabShapeCreases.Cast<SlabShapeCrease>()
                .Select(crease =>
                {
                    List<XYZ> endpoints = crease.EndPoints.Cast<SlabShapeVertex>()
                        .Select(vertex => vertex.Position).ToList();
                    return endpoints.Count == 2
                        ? new CreaseSnapshot(endpoints[0], endpoints[1], crease.CreaseType)
                        : null;
                })
                .Where(snapshot => snapshot != null)
                .Cast<CreaseSnapshot>()
                .ToList();
        }

        private static TriangleSurface SnapshotSurface(HostObject slab)
        {
            var triangles = new List<SurfaceTriangle>();
            foreach (Reference topFace in HostObjectUtils.GetTopFaces(slab))
            {
                if (slab.GetGeometryObjectFromReference(topFace) is not Face face) continue;
                Mesh mesh = face.Triangulate();
                for (int i = 0; i < mesh.NumTriangles; i++)
                {
                    MeshTriangle triangle = mesh.get_Triangle(i);
                    XYZ a = triangle.get_Vertex(0);
                    XYZ b = triangle.get_Vertex(1);
                    XYZ c = triangle.get_Vertex(2);
                    triangles.Add(new SurfaceTriangle(
                        new SurfacePoint(a.X, a.Y, a.Z),
                        new SurfacePoint(b.X, b.Y, b.Z),
                        new SurfacePoint(c.X, c.Y, c.Z)));
                }
            }

            return new TriangleSurface(triangles);
        }

        internal SurfaceTransferSnapshot CaptureSurfaceTransfer(HostObject source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return new SurfaceTransferSnapshot(
                SnapshotSurface(source),
                SnapshotVertices(source),
                SnapshotCreases(source));
        }

        internal List<CurveLoop> PrepareSurfaceTransferProfile(
            IList<CurveLoop> profile, SurfaceTransferSnapshot snapshot, double minLength)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(snapshot);
            return SplitProfileAtBoundaryControls(profile.ToList(), snapshot.Vertices, minLength);
        }

        internal void RestoreTransferredSurface(Document doc, HostObject output,
            List<CurveLoop> profile, SurfaceTransferSnapshot snapshot, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            RestoreSurface(doc, output, profile, snapshot.Surface, snapshot.Vertices, reporter,
                snapshot.Creases, refineSurface: false);
        }

        private void RestoreSurface(Document doc, HostObject output, List<CurveLoop> profile,
            TriangleSurface sourceSurface, IReadOnlyList<VertexSnapshot> sourceVertices,
            IProgressReporter reporter, IReadOnlyList<CreaseSnapshot>? sourceCreases = null,
            bool refineSurface = true)
        {
            SlabShapeEditor editor = _slabService.GetEditor(output)
                ?? throw new InvalidOperationException($"Element {output.Id} has no slab shape editor.");
            if (!editor.IsEnabled)
            {
                editor.Enable();
                doc.Regenerate();
            }

            // The fresh boundary vertices need their old surface elevations, even where
            // the source had no explicit shape-edit point at that XY.
            reporter.Log($"Element {output.Id}: stabilizing boundary elevations.");
            StabilizeBoundaryVertices(doc, editor, sourceSurface, sourceVertices, output.Id);

            // Triangulation vertices are not necessarily editable shape points.
            // In particular, a tessellated boundary vertex cannot be added with AddPoint.
            var controls = sourceVertices.Select(vertex => vertex.Position)
                .Where(point => IsPointInsideProfile(point, profile)
                    && !profile.Any(loop => IsPointOnLoop(point, loop)))
                .DistinctBy(point => (Math.Round(point.X * 10000), Math.Round(point.Y * 10000)));
            reporter.Log($"Element {output.Id}: restoring source control points.");
            foreach (XYZ point in controls)
                SetSurfacePoint(editor, sourceSurface, sourceVertices, point,
                    allowInsert: true, output.Id, "source control");

            doc.Regenerate();
            StabilizeBoundaryVertices(doc, editor, sourceSurface, sourceVertices, output.Id);
            RestoreShapeCreases(editor, sourceCreases ?? Array.Empty<CreaseSnapshot>(),
                profile, output.Id);
            doc.Regenerate();
            StabilizeBoundaryVertices(doc, editor, sourceSurface, sourceVertices, output.Id);
            VerifyShapeControls(editor, sourceVertices, profile, output.Id);
            if (!refineSurface)
            {
                VerifyCreaseElevations(output, profile, sourceSurface,
                    sourceCreases ?? Array.Empty<CreaseSnapshot>());
                return;
            }
            List<XYZ> profilePolygon = profile.SelectMany(TessellateLoop).ToList();
            double minX = profilePolygon.Min(point => point.X);
            double maxX = profilePolygon.Max(point => point.X);
            double minY = profilePolygon.Min(point => point.Y);
            double maxY = profilePolygon.Max(point => point.Y);
            // ponytail: Three refinement rounds bound Revit regenerations; increase only
            // for a real model that still has recoverable surface mismatches after round three.
            for (int round = 0; round < 3; round++)
            {
                TriangleSurface outputSurface = SnapshotSurface(output);
                var samples = SurfaceSamples(sourceSurface)
                    .Concat(SurfaceSamples(outputSurface))
                    .Where(point => point.X >= minX - 0.05 && point.X <= maxX + 0.05
                        && point.Y >= minY - 0.05 && point.Y <= maxY + 0.05)
                    .Where(point => IsPointInsideProfile(
                        new XYZ(point.X, point.Y, point.Z), profile))
                    .Where(point => !IsPointNearProfile(
                        new XYZ(point.X, point.Y, point.Z), profile, 0.01))
                    .DistinctBy(point => (Math.Round(point.X * 10000), Math.Round(point.Y * 10000)))
                    .ToList();
                if (samples.Count == 0)
                    throw new InvalidOperationException($"Element {output.Id} has no surface samples to verify.");
                reporter.Log($"Element {output.Id}: checking {samples.Count} surface samples in round {round + 1}.");
                var mismatches = samples
                    .Where(point =>
                    {
                        if (!sourceSurface.TryGetElevation(point.X, point.Y, out double sourceZ, 0.05)
                            || !outputSurface.TryGetElevation(point.X, point.Y, out double outputZ, 0.05))
                            throw new InvalidOperationException($"Surface sampling failed for element {output.Id}.");
                        return Math.Abs(sourceZ - outputZ) > 1e-3;
                    })
                    .ToList();
                reporter.Log($"Element {output.Id}: {mismatches.Count} surface mismatches in round {round + 1}.");
                if (mismatches.Count > 0)
                {
                    SurfacePoint first = mismatches[0];
                    sourceSurface.TryGetElevation(first.X, first.Y, out double sourceZ, 0.05);
                    outputSurface.TryGetElevation(first.X, first.Y, out double outputZ, 0.05);
                    reporter.Log($"Element {output.Id}: first mismatch at ({first.X:F6}, {first.Y:F6}), "
                        + $"source Z {sourceZ:F6}, output Z {outputZ:F6}.");
                }
                // ponytail: Above 5,000 mismatches, batch refinement changed rather than
                // recovered the real model's surface; retry only with a proven Revit method.
                if (mismatches.Count > 5000)
                    throw new InvalidOperationException($"Element {output.Id} differs from the source surface "
                        + $"at {mismatches.Count} locations; no changes were committed.");
                if (mismatches.Count == 0)
                {
                    return;
                }
                if (round == 2)
                    throw new InvalidOperationException(
                        $"Element {output.Id} still differs from the source surface at {mismatches.Count} sampled locations.");

                var refinements = new List<XYZ>();
                List<SlabShapeVertex> editableVertices = editor.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
                foreach (SurfacePoint point in mismatches)
                {
                    XYZ sample = new XYZ(point.X, point.Y, point.Z);
                    if (profile.Any(loop => IsPointOnLoop(sample, loop)))
                    {
                        sourceSurface.TryGetElevation(point.X, point.Y, out double sourceZ, 0.05);
                        outputSurface.TryGetElevation(point.X, point.Y, out double outputZ, 0.05);
                        SlabShapeVertex? closestOutput = editor.SlabShapeVertices.Cast<SlabShapeVertex>()
                            .OrderBy(vertex => HorizontalDistance(vertex.Position, sample)).FirstOrDefault();
                        VertexSnapshot? closestSource = sourceVertices
                            .OrderBy(vertex => HorizontalDistance(vertex.Position, sample)).Cast<VertexSnapshot?>().FirstOrDefault();
                        throw new InvalidOperationException(
                            $"Cannot restore boundary surface at ({point.X:F6}, {point.Y:F6}): "
                            + $"source Z {sourceZ:F6}, output Z {outputZ:F6}; "
                            + $"output height offset {GetHeightOffset(output):F6}, "
                            + $"level Z {(doc.GetElement(output.LevelId) as Level)?.Elevation:F6}; "
                            + $"nearest output vertex {FormatNearest(closestOutput?.Position, sample)}, "
                            + $"nearest source vertex {FormatNearest(closestSource?.Position, sample)}; "
                            + $"nearby boundary vertices: {FormatBoundaryVertices(editor, sourceSurface, sample)}.");
                    }
                    double expected = GetSourceElevation(sourceSurface, sourceVertices, sample, out _);
                    if (editableVertices.Any(vertex =>
                        HorizontalDistance(vertex.Position, sample) <= 1e-4))
                    {
                        SetSurfacePoint(editor, sourceSurface, sourceVertices, sample,
                            allowInsert: false, output.Id, "surface refinement");
                        continue;
                    }
                    refinements.Add(new XYZ(point.X, point.Y, expected));
                }
                if (refinements.Count > 0)
                {
                    reporter.Log($"Element {output.Id}: adding {refinements.Count} surface points in one batch.");
                    try
                    {
                        editor.AddPoints(refinements);
                    }
                    catch (Exception ex) when (IsExpectedSplitBoundariesException(ex))
                    {
                        throw new InvalidOperationException(
                            $"Element {output.Id}: could not add surface refinement points: {ex.Message}", ex);
                    }
                }
                doc.Regenerate();
                StabilizeBoundaryVertices(doc, editor, sourceSurface, sourceVertices, output.Id);
            }
        }

        private static void StabilizeBoundaryVertices(Document doc, SlabShapeEditor editor,
            TriangleSurface sourceSurface, IReadOnlyList<VertexSnapshot> sourceVertices, ElementId outputId)
        {
            var recentChanges = new List<string>();
            var commandedOffsets = new Dictionary<(long X, long Y), double>();
            for (int pass = 0; pass < 6; pass++)
            {
                List<SlabShapeVertex> boundaryVertices = editor.SlabShapeVertices.Cast<SlabShapeVertex>()
                    .Where(vertex => vertex.VertexType != SlabShapeVertexType.Interior)
                    .ToList();
                int changed = 0;
                recentChanges.Clear();
                foreach (SlabShapeVertex vertex in boundaryVertices)
                {
                    XYZ point = vertex.Position;
                    double expected = GetSourceElevation(sourceSurface, sourceVertices, point,
                        out bool originalControl);
                    if (Math.Abs(point.Z - expected) <= (originalControl ? 1e-4 : 1e-3)) continue;
                    if (recentChanges.Count < 5)
                        recentChanges.Add($"({point.X:F6},{point.Y:F6}) actual {point.Z:F6}, expected {expected:F6}");
                    var key = ((long)Math.Round(point.X * 100000), (long)Math.Round(point.Y * 100000));
                    commandedOffsets.TryGetValue(key, out double previousOffset);
                    double nextOffset = previousOffset + expected - point.Z;
                    editor.ModifySubElement(vertex, nextOffset);
                    commandedOffsets[key] = nextOffset;
                    changed++;
                }
                if (changed == 0) return;
                doc.Regenerate();
            }
            throw new InvalidOperationException($"Element {outputId} boundary elevations did not stabilize after 6 passes: "
                + string.Join("; ", recentChanges));
        }

        private static void VerifyShapeControls(SlabShapeEditor editor,
            IReadOnlyList<VertexSnapshot> sourceVertices, List<CurveLoop> profile, ElementId outputId)
        {
            List<XYZ> outputVertices = editor.SlabShapeVertices.Cast<SlabShapeVertex>()
                .Select(vertex => vertex.Position).ToList();
            foreach (VertexSnapshot sourceVertex in sourceVertices)
            {
                XYZ point = sourceVertex.Position;
                if (!IsPointInsideProfile(point, profile)
                    && !profile.Any(loop => IsPointOnLoop(point, loop))) continue;
                if (!outputVertices.Any(vertex => SameShapePoint(vertex, point)))
                    throw new InvalidOperationException(
                        $"Element {outputId} did not preserve source point "
                        + $"({point.X:F4}, {point.Y:F4}, {point.Z:F4}).");
            }
        }

        private static void VerifyCreaseElevations(HostObject output, List<CurveLoop> profile,
            TriangleSurface sourceSurface, IReadOnlyList<CreaseSnapshot> sourceCreases)
        {
            SlabShapeEditor editor = output switch
            {
                Floor floor => floor.GetSlabShapeEditor(),
                Toposolid toposolid => toposolid.GetSlabShapeEditor(),
                _ => throw new InvalidOperationException($"Element {output.Id} has no slab shape editor.")
            };
            TriangleSurface outputSurface = SnapshotSurface(output);
            foreach (CreaseSnapshot crease in sourceCreases
                .Where(item => item.CreaseType != SlabShapeCreaseType.Boundary)
                .Where(item => IsPointInsideProfile(item.Start, profile)
                    || profile.Any(loop => IsPointOnLoop(item.Start, loop)))
                .Where(item => IsPointInsideProfile(item.End, profile)
                    || profile.Any(loop => IsPointOnLoop(item.End, loop))))
            {
                if (!HasMatchingCrease(editor, crease))
                    throw new InvalidOperationException($"Element {output.Id} did not preserve the "
                        + $"{crease.CreaseType} source crease {crease.Start} -> {crease.End}.");

                XYZ midpoint = (crease.Start + crease.End) / 2;
                bool sourceFound = sourceSurface.TryGetElevation(
                    midpoint.X, midpoint.Y, out double sourceZ, 0.001);
                // A Revit auto crease can bridge a concavity or void, so its midpoint may
                // intentionally have no source top face. Its exact 3D endpoints and matching
                // output crease are the complete editable shape definition in that case.
                if (!sourceFound) continue;
                bool outputFound = outputSurface.TryGetElevation(
                    midpoint.X, midpoint.Y, out double outputZ, 0.001);
                if (!outputFound || Math.Abs(sourceZ - outputZ) > 0.001)
                    throw new InvalidOperationException($"Element {output.Id} changed the surface along "
                        + $"a {crease.CreaseType} source crease at ({midpoint.X:F4}, {midpoint.Y:F4}): "
                        + $"source Z {sourceZ:F6}; output found {outputFound}, "
                        + $"Z {outputZ:F6}; difference {Math.Abs(sourceZ - outputZ):F6}; "
                        + $"endpoints {crease.Start} -> {crease.End}.");
            }
        }

        private static bool IsPointInsideProfile(XYZ point, List<CurveLoop> profile)
        {
            return profile.Count(loop => IsPointInsideLoop(point, loop)) % 2 == 1;
        }

        private static bool IsPointNearProfile(XYZ point, List<CurveLoop> profile, double tolerance)
        {
            foreach (Curve curve in profile.SelectMany(loop => loop.Cast<Curve>()))
            {
                XYZ flattened = new XYZ(point.X, point.Y, curve.GetEndPoint(0).Z);
                IntersectionResult? projection = curve.Project(flattened);
                if (projection != null
                    && HorizontalDistance(flattened, projection.XYZPoint) <= tolerance)
                    return true;
            }
            return false;
        }

        private static List<CurveLoop> SplitProfileAtBoundaryControls(
            List<CurveLoop> profile, IReadOnlyList<VertexSnapshot> sourceVertices, double minLength)
        {
            var result = new List<CurveLoop>(profile.Count);
            foreach (CurveLoop loop in profile)
            {
                var segments = new List<Curve>();
                foreach (Curve curve in loop)
                {
                    var fractions = new List<double> { 0 };
                    foreach (VertexSnapshot vertex in sourceVertices)
                    {
                        if (!TryProjectToCurveXY(vertex.Position, curve, out double fraction)) continue;
                        if (fraction <= 0 || fraction >= 1) continue;
                        fractions.Add(fraction);
                    }
                    fractions.Sort();
                    fractions.Add(1);

                    var accepted = new List<double> { 0 };
                    for (int i = 1; i < fractions.Count - 1; i++)
                    {
                        XYZ previous = curve.Evaluate(accepted[^1], true);
                        XYZ candidate = curve.Evaluate(fractions[i], true);
                        XYZ end = curve.GetEndPoint(1);
                        if (HorizontalDistance(previous, candidate) > minLength
                            && HorizontalDistance(candidate, end) > minLength)
                            accepted.Add(fractions[i]);
                    }
                    accepted.Add(1);
                    for (int i = 0; i < accepted.Count - 1; i++)
                    {
                        Curve segment = curve.Clone();
                        segment.MakeBound(curve.ComputeRawParameter(accepted[i]),
                            curve.ComputeRawParameter(accepted[i + 1]));
                        segments.Add(segment);
                    }
                }
                result.Add(CurveLoop.Create(segments));
            }
            return result;
        }

        private static bool TryProjectToCurveXY(XYZ point, Curve curve, out double fraction)
        {
            fraction = 0;
            XYZ flattened = new XYZ(point.X, point.Y, curve.GetEndPoint(0).Z);
            IntersectionResult? projected = curve.Project(flattened);
            if (projected == null || HorizontalDistance(projected.XYZPoint, flattened) > 1e-4)
                return false;
            fraction = curve.ComputeNormalizedParameter(projected.Parameter);
            return fraction >= -1e-8 && fraction <= 1 + 1e-8;
        }

        private static IEnumerable<SurfacePoint> SurfaceSamples(TriangleSurface surface)
        {
            foreach (SurfaceTriangle triangle in surface.Triangles)
            {
                yield return triangle.Centroid;
                yield return Midpoint(triangle.A, triangle.B);
                yield return Midpoint(triangle.B, triangle.C);
                yield return Midpoint(triangle.C, triangle.A);
            }
        }

        private static SurfacePoint Midpoint(SurfacePoint a, SurfacePoint b)
        {
            return new SurfacePoint((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);
        }

        private static double GetSourceElevation(TriangleSurface surface,
            IReadOnlyList<VertexSnapshot> sourceVertices, XYZ point, out bool originalControl)
        {
            foreach (VertexSnapshot vertex in sourceVertices)
            {
                if (HorizontalDistance(vertex.Position, point) > 1e-4) continue;
                originalControl = true;
                return vertex.Position.Z;
            }
            originalControl = false;
            if (surface.TryGetElevation(point.X, point.Y, out double elevation, 0.05))
                return elevation;
            throw new InvalidOperationException(
                $"Source surface has no elevation at ({point.X:F6}, {point.Y:F6}).");
        }

        private static void SetSurfacePoint(SlabShapeEditor editor, TriangleSurface surface,
            IReadOnlyList<VertexSnapshot> sourceVertices, XYZ point,
            bool allowInsert, ElementId outputId, string pointRole)
        {
            double elevation = GetSourceElevation(surface, sourceVertices, point, out _);

            SlabShapeVertex? existing = editor.SlabShapeVertices.Cast<SlabShapeVertex>()
                .FirstOrDefault(vertex => HorizontalDistance(vertex.Position, point) <= 1e-4);
            if (existing != null)
            {
                double delta = elevation - existing.Position.Z;
                if (Math.Abs(delta) > 1e-6) editor.ModifySubElement(existing, delta);
            }
            else if (allowInsert)
            {
                try
                {
                    editor.AddPoint(new XYZ(point.X, point.Y, elevation));
                }
                catch (Exception ex) when (IsExpectedSplitBoundariesException(ex))
                {
                    throw new InvalidOperationException(
                        $"Element {outputId}: could not add {pointRole} point "
                        + $"({point.X:F6}, {point.Y:F6}, {elevation:F6}): {ex.Message}", ex);
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"Element boundary has no vertex at ({point.X:F4}, {point.Y:F4}).");
            }
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
            List<XYZ> polygon = TessellateLoop(loop);
            bool inside = false;
            bool nearBoundary = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                XYZ a = polygon[i];
                XYZ b = polygon[j];
                if (!nearBoundary && IsPointOnSegment(point, a, b, 2e-4))
                    nearBoundary = true;
                if ((a.Y > point.Y) != (b.Y > point.Y)
                    && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
            return nearBoundary && IsPointOnLoop(point, loop) || inside;
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
            List<XYZ> polygon = TessellateLoop(loop);
            double twiceArea = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                XYZ a = polygon[i];
                XYZ b = polygon[(i + 1) % polygon.Count];
                twiceArea += a.X * b.Y - b.X * a.Y;
            }
            return Math.Abs(twiceArea) / 2;
        }

        private static List<XYZ> TessellateLoop(CurveLoop loop)
        {
            return LoopPolygonCache.GetValue(loop, key =>
            {
                var polygon = new List<XYZ>();
                foreach (Curve curve in key)
                {
                    if (curve is Arc arc)
                    {
                        double angle = Math.Abs(curve.GetEndParameter(1) - curve.GetEndParameter(0));
                        double maxAngle = Math.Sqrt(8e-5 / Math.Max(arc.Radius, 1e-6));
                        int segments = Math.Clamp((int)Math.Ceiling(angle / maxAngle), 8, 4096);
                        for (int index = 0; index < segments; index++)
                            polygon.Add(curve.Evaluate((double)index / segments, true));
                    }
                    else
                    {
                        IList<XYZ> points = curve.Tessellate();
                        for (int index = 0; index < points.Count - 1; index++)
                            polygon.Add(points[index]);
                    }
                }
                return polygon;
            });
        }

        private static bool IsPointOnLoop(XYZ point, CurveLoop loop)
        {
            // Revit's projection is exact but very expensive for thousands of mesh samples.
            // The fine polygon only rejects distant points; projection decides near-edge points.
            List<XYZ> polygon = TessellateLoop(loop);
            bool nearBoundary = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if (!IsPointOnSegment(point, polygon[j], polygon[i], 2e-4)) continue;
                nearBoundary = true;
                break;
            }
            if (!nearBoundary) return false;
            foreach (Curve curve in loop)
            {
                if (TryProjectToCurveXY(point, curve, out _)) return true;
            }

            return false;
        }

        private static bool IsPointOnSegment(XYZ point, XYZ start, XYZ end, double tolerance = 1e-4)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double lengthSquared = dx * dx + dy * dy;
            if (lengthSquared <= tolerance * tolerance)
                return HorizontalDistance(point, start) <= tolerance;
            double fraction = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared;
            if (fraction < 0 || fraction > 1) return false;
            double nearestX = start.X + fraction * dx;
            double nearestY = start.Y + fraction * dy;
            double gapX = point.X - nearestX;
            double gapY = point.Y - nearestY;
            return gapX * gapX + gapY * gapY <= tolerance * tolerance;
        }

        private static double HorizontalDistance(XYZ a, XYZ b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static string FormatNearest(XYZ? vertex, XYZ sample)
        {
            return vertex == null ? "none" : $"({vertex.X:F6}, {vertex.Y:F6}, {vertex.Z:F6}) "
                + $"at XY distance {HorizontalDistance(vertex, sample):F6}";
        }

        private static string FormatBoundaryVertices(SlabShapeEditor editor, TriangleSurface sourceSurface, XYZ sample)
        {
            return string.Join("; ", editor.SlabShapeVertices.Cast<SlabShapeVertex>()
                .Where(vertex => vertex.VertexType != SlabShapeVertexType.Interior)
                .OrderBy(vertex => HorizontalDistance(vertex.Position, sample))
                .Take(4)
                .Select(vertex =>
                {
                    XYZ actual = vertex.Position;
                    bool found = sourceSurface.TryGetElevation(actual.X, actual.Y, out double expected, 0.05);
                    return $"({actual.X:F4},{actual.Y:F4}) actual {actual.Z:F4}, "
                        + (found ? $"expected {expected:F4}" : "source unavailable");
                }));
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

        internal sealed class SurfaceTransferSnapshot
        {
            public SurfaceTransferSnapshot(TriangleSurface surface, List<VertexSnapshot> vertices,
                List<CreaseSnapshot> creases)
            {
                Surface = surface;
                Vertices = vertices;
                Creases = creases;
            }

            public TriangleSurface Surface { get; }
            public List<VertexSnapshot> Vertices { get; }
            public List<CreaseSnapshot> Creases { get; }
        }

        internal readonly struct VertexSnapshot
        {
            public VertexSnapshot(XYZ position, SlabShapeVertexType vertexType)
            {
                Position = position;
                VertexType = vertexType;
            }

            public XYZ Position { get; }
            public SlabShapeVertexType VertexType { get; }
        }

        internal sealed class CreaseSnapshot
        {
            public CreaseSnapshot(XYZ start, XYZ end, SlabShapeCreaseType creaseType)
            {
                Start = start;
                End = end;
                CreaseType = creaseType;
            }

            public XYZ Start { get; }
            public XYZ End { get; }
            public SlabShapeCreaseType CreaseType { get; }
        }

    }
}
