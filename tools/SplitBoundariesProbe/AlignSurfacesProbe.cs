using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Configuration;
using LECG.Services;
using LECG.ViewModels;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using System.IO;

namespace LECG.SplitBoundariesProbe;

public sealed class AlignSurfacesProbe
{
    private UIApplication _uiApplication = null!;

    [OneTimeSetUp]
    public void Setup(UIApplication application) => _uiApplication = application;

    [Test]
    public void Find_my_edge_aligns_existing_interior_points_in_recovery_model()
    {
        string? path = Environment.GetEnvironmentVariable("LECG_ALIGN_SURFACES_MODEL_PROBE_PATH");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Assert.Ignore("Set LECG_ALIGN_SURFACES_MODEL_PROBE_PATH to a disposable model copy.");

        string progressPath = Path.ChangeExtension(path, ".align-surfaces.txt");
        File.WriteAllText(progressPath, "Opening copied model" + Environment.NewLine);
        Document document = _uiApplication.Application.OpenDocumentFile(path);
        try
        {
            Element source = document.GetElement(new ElementId(12197304))
                ?? throw new InvalidOperationException("Recovery-model source 12197304 is missing.");
            var viewModel = new AlignEdgesViewModel();
            viewModel.SetTargets(new List<Reference> { new Reference(source) }, document);
            viewModel.FindMyEdge = true;
            Assert.Multiple(() =>
            {
                Assert.That(viewModel.CanRun, Is.True);
                Assert.That(viewModel.ShowReferenceSection, Is.False);
                Assert.That(viewModel.Title, Is.EqualTo("ALIGN SURFACES"));
                Assert.That(UIConstants.ButtonAlignEdges_Text, Is.EqualTo("Align\nSurfaces"));
            });
            SlabShapeEditor editor = GetEditor(source)
                ?? throw new InvalidOperationException("Recovery-model source has no shape editor.");
            Assert.That(editor.IsEnabled, Is.True, "Recovery-model source shape editor is disabled.");

            List<(XYZ point, SlabShapeVertexType type)> verticesBefore = editor.SlabShapeVertices
                .Cast<SlabShapeVertex>().Select(vertex => (vertex.Position, vertex.VertexType)).ToList();
            List<XYZ> interiorBefore = verticesBefore
                .Where(item => item.type == SlabShapeVertexType.Interior)
                .Select(item => item.point).ToList();
            Assert.That(interiorBefore, Is.Not.Empty, "Recovery-model source has no interior points.");

            var intersectorService = new AlignEdgesIntersectorService();
            ReferenceIntersector expectedIntersector = intersectorService.CreateBroad(document,
                new HashSet<ElementId> { source.Id });
            var raycast = new ReferenceRaycastService();
            var expected = verticesBefore.Select(item =>
                    (item.point, item.type, hit: raycast.GetHitInfo(expectedIntersector, item.point)))
                .Where(item => item.hit.HasValue && Math.Abs(item.hit.Value.Point.Z - item.point.Z) > 0.0164)
                .ToList();
            List<(XYZ point, SlabShapeVertexType type, AlignEdgesHitInfo? hit)> expectedInterior =
                expected.Where(item => item.type == SlabShapeVertexType.Interior).ToList();
            Assert.That(expectedInterior, Is.Not.Empty,
                "No interior point in the recovery-model source needs alignment to a nearby surface.");
            Level? level = document.GetElement(source.LevelId) as Level;
            Parameter? height = source is Floor
                ? source.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)
                : source.get_Parameter(BuiltInParameter.TOPOSOLID_HEIGHTABOVELEVEL_PARAM);
            File.AppendAllText(progressPath,
                $"Source {source.Id}: level Z {level?.Elevation:F6}, height offset {height?.AsDouble():F6}; "
                + $"{verticesBefore.Count} shape points, {interiorBefore.Count} interior; "
                + $"{expected.Count} total and {expectedInterior.Count} interior require movement."
                + Environment.NewLine
                + string.Join(Environment.NewLine, expected.Select(item =>
                    $"({item.point.X:F4}, {item.point.Y:F4}) {item.point.Z:F6} -> "
                    + $"{item.hit!.Value.Point.Z:F6} on {item.hit.Value.ElementId}"))
                + Environment.NewLine);

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var projection = new AlignEdgesHitPointProjectionService(raycast);
            var boundaryPoints = new AlignEdgesBoundaryPointService(projection,
                new AlignEdgesCurveDivisionService());
            var boundaryCollection = new AlignEdgesBoundaryCollectionService(boundaryPoints,
                new GeometryBoundaryService(cache));
            var processing = new AlignEdgesToposolidProcessingService(boundaryCollection,
                new AlignEdgesPointInsertionService(), new SlabService(),
                new AlignEdgesVertexAlignmentService(raycast));
            var service = new AlignEdgesService(intersectorService, processing,
                new TransactionService());

            IReadOnlyList<AlignEdgesSourceResult> results = service.AlignEdgesFindMyEdge(document,
                new List<Reference> { new Reference(source) });
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Status, Is.Not.EqualTo(AlignEdgesSourceStatus.Failed),
                results[0].FailureMessage);
            Assert.That(results[0].MovedVertexCount, Is.GreaterThanOrEqualTo(expected.Count));

            List<XYZ> verticesAfter = GetEditor(source)!.SlabShapeVertices.Cast<SlabShapeVertex>()
                .Select(vertex => vertex.Position).ToList();
            foreach (var (before, _, hit) in expected)
            {
                XYZ after = verticesAfter.First(point => HorizontalDistance(point, before) <= 1e-4);
                Assert.That(after.Z, Is.EqualTo(hit!.Value.Point.Z).Within(1e-4),
                    $"Shape point at ({before.X:F4}, {before.Y:F4}) did not reach the reference surface.");
            }
            File.AppendAllText(progressPath,
                $"Aligned and verified {expected.Count} existing shape points, including "
                + $"{expectedInterior.Count} interior points." + Environment.NewLine);
        }
        catch (Exception exception)
        {
            File.AppendAllText(progressPath, exception + Environment.NewLine);
            throw;
        }
        finally
        {
            document.Close(false);
        }
    }

    private static SlabShapeEditor? GetEditor(Element element)
    {
        return element switch
        {
            Floor floor => floor.GetSlabShapeEditor(),
            Toposolid toposolid => toposolid.GetSlabShapeEditor(),
            _ => null
        };
    }

    private static double HorizontalDistance(XYZ first, XYZ second)
    {
        double dx = first.X - second.X;
        double dy = first.Y - second.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
