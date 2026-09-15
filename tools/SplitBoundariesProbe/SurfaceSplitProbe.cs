using System.IO;
using System.Reflection;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core.Geometry;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace LECG.SplitBoundariesProbe;

public sealed class SurfaceSplitProbe
{
    private UIApplication _uiApplication = null!;

    [OneTimeSetUp]
    public void Setup(UIApplication application) => _uiApplication = application;

    [Test]
    public void Inspect_recovery_model_boundary_controls()
    {
        string? path = Environment.GetEnvironmentVariable("LECG_SPLIT_MODEL_PROBE_PATH");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Assert.Ignore("Set LECG_SPLIT_MODEL_PROBE_PATH to a disposable model copy.");
        Document document = _uiApplication.Application.OpenDocumentFile(path!);
        try
        {
            var toposolid = document.GetElement(new ElementId(12197304)) as Toposolid
                ?? throw new InvalidOperationException("Source Toposolid is missing.");
            var sample = new XYZ(-25.160357, 110.911938, 0);
            var report = new StringBuilder();
            SlabShapeEditor editor = toposolid.GetSlabShapeEditor();
            report.AppendLine($"Shape enabled: {editor.IsEnabled}; shape vertices: {editor.SlabShapeVertices.Size}");
            var creases = editor.SlabShapeCreases.Cast<SlabShapeCrease>().ToList();
            report.AppendLine($"Editable creases: {creases.Count}");
            foreach (var group in creases.GroupBy(crease => crease.CreaseType))
                report.AppendLine($"Crease type {group.Key}: {group.Count()}");
            foreach (SlabShapeCrease crease in creases.Take(12))
                report.AppendLine($"Crease {crease.CreaseType}: "
                    + string.Join(" to ", crease.EndPoints.Cast<SlabShapeVertex>().Select(vertex => vertex.Position)));
            List<Curve> curves = new();
            Sketch sketch = (Sketch)document.GetElement(toposolid.SketchId);
            int loopIndex = 0;
            foreach (CurveArray curveArray in sketch.Profile)
            {
                var loopCurves = curveArray.Cast<Curve>().ToList();
                curves.AddRange(loopCurves);
                var endpoints = loopCurves.SelectMany(curve => new[]
                    { curve.GetEndPoint(0), curve.GetEndPoint(1) }).ToList();
                report.AppendLine($"Loop {loopIndex++}: {loopCurves.Count} curves, "
                    + $"X [{endpoints.Min(p => p.X):F3}, {endpoints.Max(p => p.X):F3}], "
                    + $"Y [{endpoints.Min(p => p.Y):F3}, {endpoints.Max(p => p.Y):F3}]");
            }
            report.AppendLine($"Sketch curves: {curves.Count}");
            foreach (var item in curves.Select(curve =>
            {
                XYZ flattened = new XYZ(sample.X, sample.Y, curve.GetEndPoint(0).Z);
                IntersectionResult? projection = curve.Project(flattened);
                double distance = projection == null ? double.PositiveInfinity
                    : Math.Sqrt(Math.Pow(projection.XYZPoint.X - sample.X, 2)
                        + Math.Pow(projection.XYZPoint.Y - sample.Y, 2));
                return (curve, distance);
            }).OrderBy(item => item.distance).Take(6))
                report.AppendLine($"Curve {item.curve.GetType().Name}: distance {item.distance:F8}; "
                    + $"start {item.curve.GetEndPoint(0)}, end {item.curve.GetEndPoint(1)}");
            foreach (SlabShapeVertex vertex in editor.SlabShapeVertices.Cast<SlabShapeVertex>()
                .OrderBy(vertex => vertex.Position.DistanceTo(sample)).Take(10))
            {
                XYZ p = vertex.Position;
                double xy = Math.Sqrt(Math.Pow(p.X - sample.X, 2) + Math.Pow(p.Y - sample.Y, 2));
                report.AppendLine($"Shape vertex {vertex.VertexType}: XY distance {xy:F6}; {p}");
            }
            TriangleSurface surface = ReadTopSurface(toposolid);
            bool exact = surface.TryGetElevation(sample.X, sample.Y, out double sampleZ);
            bool extrapolated = surface.TryGetElevation(sample.X, sample.Y, out double nearbyZ, 0.05);
            report.AppendLine($"Top triangles: {surface.Triangles.Count}; "
                + $"exact surface: {exact}, Z {sampleZ:F6}; "
                + $"within 0.05 ft: {extrapolated}, Z {nearbyZ:F6}");
            foreach (SurfaceTriangle triangle in surface.Triangles
                .OrderBy(t => Math.Pow(t.Centroid.X - sample.X, 2) + Math.Pow(t.Centroid.Y - sample.Y, 2))
                .Take(6))
                report.AppendLine($"Triangle: {triangle.A}; {triangle.B}; {triangle.C}");
            File.WriteAllText(Path.ChangeExtension(path!, ".inspect.txt"), report.ToString());
        }
        finally
        {
            document.Close(false);
        }
    }

    [Test]
    public void Copy_and_trim_one_recovery_model_island()
    {
        string? path = Environment.GetEnvironmentVariable("LECG_SPLIT_MODEL_PROBE_PATH");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Assert.Ignore("Set LECG_SPLIT_MODEL_PROBE_PATH to a disposable model copy.");
        string progressPath = Path.ChangeExtension(path!, ".copy-trim.txt");
        File.WriteAllText(progressPath, "Opening copied model" + Environment.NewLine);
        Document document = _uiApplication.Application.OpenDocumentFile(path!);
        try
        {
            var source = document.GetElement(new ElementId(12197304)) as Toposolid
                ?? throw new InvalidOperationException("Source Toposolid is missing.");
            File.AppendAllText(progressPath, "Copying source Toposolid" + Environment.NewLine);
            ElementId copyId;
            using (var transaction = new Transaction(document, "Copy one Toposolid"))
            {
                File.AppendAllText(progressPath, "Starting copy transaction" + Environment.NewLine);
                transaction.Start();
                FailureHandlingOptions copyFailures = transaction.GetFailureHandlingOptions();
                copyFailures.SetFailuresPreprocessor(new ProbeWarningHandler());
                copyFailures.SetForcedModalHandling(false);
                transaction.SetFailureHandlingOptions(copyFailures);
                File.AppendAllText(progressPath, "Copy transaction started" + Environment.NewLine);
                copyId = ElementTransformUtils.CopyElements(document,
                    new List<ElementId> { source.Id }, new XYZ(2000, 0, 0)).Single();
                File.AppendAllText(progressPath, $"Copy call returned {copyId}" + Environment.NewLine);
                Assert.That(transaction.Commit(), Is.EqualTo(TransactionStatus.Committed));
                File.AppendAllText(progressPath, "Copy transaction committed" + Environment.NewLine);
            }
            File.AppendAllText(progressPath, $"Copy ID {copyId}; trimming to island 1 of 16." + Environment.NewLine);
            using (var removeConstraints = new Transaction(document, "Remove copied sketch constraints"))
            {
                removeConstraints.Start();
                FailureHandlingOptions constraintFailures = removeConstraints.GetFailureHandlingOptions();
                constraintFailures.SetFailuresPreprocessor(new ProbeWarningHandler());
                constraintFailures.SetForcedModalHandling(false);
                removeConstraints.SetFailureHandlingOptions(constraintFailures);
                var constraintIds = new[] { 12197935L, 12197936L, 12197937L }
                    .Select(value => new ElementId(value)).ToList();
                File.AppendAllText(progressPath, "Removing copied sketch dependents: "
                    + string.Join(", ", constraintIds.Select(id =>
                        $"{id}:{document.GetElement(id)?.GetType().Name}")) + Environment.NewLine);
                document.Delete(constraintIds);
                Assert.That(removeConstraints.Commit(), Is.EqualTo(TransactionStatus.Committed));
                File.AppendAllText(progressPath, "Copied sketch dependents removed." + Environment.NewLine);
            }
            var reporter = new ProbeReporter(progressPath);
            MethodInfo trim = typeof(SplitBoundariesService).GetMethod("TrimCopiedSketch",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("TrimCopiedSketch was not found.");
            trim.Invoke(null, new object[] { document, copyId, 0, 16, reporter });
            File.AppendAllText(progressPath, "Sketch trimmed." + Environment.NewLine);
            using (var move = new Transaction(document, "Return trimmed Toposolid"))
            {
                move.Start();
                ElementTransformUtils.MoveElement(document, copyId, new XYZ(-2000, 0, 0));
                Assert.That(move.Commit(), Is.EqualTo(TransactionStatus.Committed));
            }
            var output = (Toposolid)document.GetElement(copyId);
            Assert.That(((Sketch)document.GetElement(output.SketchId)).Profile.Size, Is.GreaterThanOrEqualTo(1));
            TriangleSurface sourceSurface = ReadTopSurface(source);
            TriangleSurface outputSurface = ReadTopSurface(output);
            int checkedSamples = 0;
            foreach (SurfaceTriangle triangle in outputSurface.Triangles.Take(2000))
            {
                SurfacePoint sample = triangle.Centroid;
                if (!sourceSurface.TryGetElevation(sample.X, sample.Y, out double sourceZ)) continue;
                Assert.That(outputSurface.TryGetElevation(sample.X, sample.Y, out double outputZ), Is.True);
                Assert.That(outputZ, Is.EqualTo(sourceZ).Within(1e-3));
                checkedSamples++;
            }
            File.AppendAllText(progressPath, $"Verified {checkedSamples} copied surface samples." + Environment.NewLine);
        }
        catch (Exception exception)
        {
            File.AppendAllText(progressPath, exception.ToString());
            throw;
        }
        finally
        {
            document.Close(false);
        }
    }

    [Test]
    public void Native_split_preserves_recovery_model_surface()
    {
        string? path = Environment.GetEnvironmentVariable("LECG_SPLIT_MODEL_PROBE_PATH");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Assert.Ignore("Set LECG_SPLIT_MODEL_PROBE_PATH to a disposable model copy.");
        string progressPath = Path.ChangeExtension(path!, ".native-split.txt");
        File.WriteAllText(progressPath, "Opening copied model" + Environment.NewLine);
        Document document = _uiApplication.Application.OpenDocumentFile(path!);
        try
        {
            var source = document.GetElement(new ElementId(12197304)) as Toposolid
                ?? throw new InvalidOperationException("Source Toposolid is missing.");
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var boundaries = new GeometryBoundaryService(cache).ExtractLoops(source);
            CurveLoop smallest = boundaries.OrderBy(loop =>
            {
                var points = loop.SelectMany(curve => curve.Tessellate()).ToList();
                return (points.Max(point => point.X) - points.Min(point => point.X))
                    * (points.Max(point => point.Y) - points.Min(point => point.Y));
            }).First();
            var islandPoints = smallest.SelectMany(curve => curve.Tessellate()).ToList();
            const double padding = 0.1;
            double left = islandPoints.Min(point => point.X) - padding;
            double right = islandPoints.Max(point => point.X) + padding;
            double bottom = islandPoints.Min(point => point.Y) - padding;
            double top = islandPoints.Max(point => point.Y) + padding;
            double z = islandPoints[0].Z;
            var corners = new[] { new XYZ(left, bottom, z), new XYZ(right, bottom, z),
                new XYZ(right, top, z), new XYZ(left, top, z) };
            CurveLoop splitLoop = CurveLoop.Create(Enumerable.Range(0, 4)
                .Select(index => (Curve)Line.CreateBound(corners[index], corners[(index + 1) % 4])).ToList());
            File.AppendAllText(progressPath, $"Trying native Split with a rectangle around one of "
                + $"{boundaries.Count} existing loops: [{left:F3},{bottom:F3}] to [{right:F3},{top:F3}].\n");
            using var transaction = new Transaction(document, "Probe native Toposolid.Split");
            transaction.Start();
            try
            {
                ICollection<ElementId> ids = source.Split(new List<CurveLoop> { splitLoop });
                File.AppendAllText(progressPath, "Native Split returned IDs: "
                    + string.Join(", ", ids.Select(id => id.Value)) + Environment.NewLine);
            }
            finally
            {
                transaction.RollBack();
            }
        }
        catch (Exception exception)
        {
            File.AppendAllText(progressPath, exception.ToString());
            throw;
        }
        finally
        {
            document.Close(false);
        }
    }

    [Test]
    public void Split_recovery_model_element_without_losing_its_source()
    {
        string? path = Environment.GetEnvironmentVariable("LECG_SPLIT_MODEL_PROBE_PATH");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Assert.Ignore("Set LECG_SPLIT_MODEL_PROBE_PATH to a disposable model copy.");

        string progressPath = Path.ChangeExtension(path!, ".progress.txt");
        File.WriteAllText(progressPath, "Opening copied model" + Environment.NewLine);
        Document document = _uiApplication.Application.OpenDocumentFile(path!);
        File.AppendAllText(progressPath, "Copied model opened" + Environment.NewLine);
        try
        {
            const long sourceId = 12197304;
            Element source = document.GetElement(new ElementId(sourceId))
                ?? throw new InvalidOperationException($"Source element {sourceId} is missing.");
            File.AppendAllText(progressPath, $"Found {source.GetType().Name} {sourceId}" + Environment.NewLine);
            TriangleSurface sourceSurface = ReadTopSurface((Toposolid)source);
            int sourceStride = Math.Max(1, sourceSurface.Triangles.Count / 2000);
            List<SurfaceTriangle> sourceSamples = sourceSurface.Triangles
                .Where((_, index) => index % sourceStride == 0).ToList();
            int sourceSelfMismatches = sourceSamples.Count(triangle =>
                !sourceSurface.TryGetElevation(triangle.Centroid.X, triangle.Centroid.Y,
                    out double elevation)
                || Math.Abs(elevation - triangle.Centroid.Z) > 1e-3);
            File.AppendAllText(progressPath,
                $"Source triangle self-mismatches: {sourceSelfMismatches} of {sourceSamples.Count}."
                + Environment.NewLine);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new SplitBoundariesService(
                new TransactionService(), new GeometryBoundaryService(cache), new SlabService());
            var reporter = new ProbeReporter(progressPath);
            File.AppendAllText(progressPath, "Splitting element" + Environment.NewLine);
            service.SplitBoundaries(document, new List<Element> { source }, reporter);
            File.AppendAllText(progressPath, "Split returned: " + string.Join("; ", reporter.Errors) + Environment.NewLine);

            Assert.That(reporter.Errors, Is.Empty, string.Join(Environment.NewLine, reporter.Errors));
            Assert.That(document.GetElement(new ElementId(sourceId)), Is.Null,
                "The split left the original in place.");
        }
        finally
        {
            document.Close(false);
        }
    }

    [Test]
    public void Split_button_follows_checked_splittable_rows()
    {
        Document document = _uiApplication.Application.NewProjectDocument(UnitSystem.Metric);
        try
        {
            Level level = new FilteredElementCollector(document).OfClass(typeof(Level))
                .Cast<Level>().First();
            ElementId floorTypeId = new FilteredElementCollector(document).OfClass(typeof(FloorType))
                .Cast<FloorType>().First().Id;
            Floor floor;
            using (var transaction = new Transaction(document, "Create split button probe"))
            {
                transaction.Start();
                floor = Floor.Create(document, new List<CurveLoop>
                {
                    Rectangle(0, 0, 10, 10, level.Elevation),
                    Rectangle(20, 0, 30, 10, level.Elevation)
                }, floorTypeId, level.Id);
                Assert.That(transaction.Commit(), Is.EqualTo(TransactionStatus.Committed));
            }

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var transactionService = new TransactionService();
            var geometryService = new GeometryBoundaryService(cache);
            var splitService = new SplitBoundariesService(
                transactionService, geometryService, new SlabService());
            var viewModel = new SplitBoundariesViewModel(splitService);
            viewModel.SetSelectedElements(new Element[] { floor });
            Assert.Multiple(() =>
            {
                Assert.That(viewModel.Selection.RowItems, Has.Count.EqualTo(1));
                Assert.That(viewModel.CanRun, Is.True);
                Assert.That(viewModel.ApplyCommand.CanExecute(null), Is.True);
                Assert.That(viewModel.GetSelectedElements(document), Has.Count.EqualTo(1));
            });

            viewModel.Selection.RowItems[0].IsChecked = false;
            Assert.Multiple(() =>
            {
                Assert.That(viewModel.CanRun, Is.False);
                Assert.That(viewModel.ApplyCommand.CanExecute(null), Is.False);
                Assert.That(viewModel.GetSelectedElements(document), Is.Empty);
            });
        }
        finally
        {
            document.Close(false);
        }
    }

    [Test]
    public void Copy_then_remove_an_island_preserves_original_toposolid_shape()
    {
        Document document = _uiApplication.Application.NewProjectDocument(UnitSystem.Metric);
        try
        {
            Level level = new FilteredElementCollector(document).OfClass(typeof(Level)).Cast<Level>().First();
            ElementId typeId = new FilteredElementCollector(document).OfClass(typeof(ToposolidType))
                .Cast<ToposolidType>().First().Id;
            Toposolid source;
            using (var create = new Transaction(document, "Create copy edit probe"))
            {
                create.Start();
                source = Toposolid.Create(document, new List<CurveLoop>
                {
                    Rectangle(0, 0, 10, 10, level.Elevation),
                    Rectangle(20, 0, 30, 10, level.Elevation)
                }, typeId, level.Id);
                document.Regenerate();
                SlabShapeEditor editor = source.GetSlabShapeEditor();
                editor.Enable();
                document.Regenerate();
                editor.AddPoint(new XYZ(5, 5, level.Elevation + 3));
                editor.AddPoint(new XYZ(25, 5, level.Elevation + 5));
                Assert.That(create.Commit(), Is.EqualTo(TransactionStatus.Committed));
            }
            double expected = ElevationAt(ReadTopSurface(source), 5, 5);
            using var group = new TransactionGroup(document, "Probe copy and sketch edit");
            group.Start();
            ElementId copyId;
            using (var copy = new Transaction(document, "Copy shaped Toposolid"))
            {
                copy.Start();
                copyId = ElementTransformUtils.CopyElement(document, source.Id, XYZ.Zero).Single();
                Assert.That(copy.Commit(), Is.EqualTo(TransactionStatus.Committed));
            }
            var output = (Toposolid)document.GetElement(copyId);
            Assert.That(output.SketchId, Is.Not.EqualTo(source.SketchId));
            using (var scope = new SketchEditScope(document, "Remove second island"))
            {
                scope.Start(output.SketchId);
                using var edit = new Transaction(document, "Delete island sketch curves");
                edit.Start();
                var sketch = (Sketch)document.GetElement(output.SketchId);
                var remove = new List<ElementId>();
                foreach (CurveArray loop in sketch.Profile)
                    foreach (Curve curve in loop)
                        if (curve.GetEndPoint(0).X > 15 && curve.GetEndPoint(1).X > 15)
                            remove.Add(curve.Reference.ElementId);
                Assert.That(remove, Has.Count.EqualTo(4));
                document.Delete(remove);
                Assert.That(edit.Commit(), Is.EqualTo(TransactionStatus.Committed));
                scope.Commit(new ProbeWarningHandler());
            }
            Assert.That(group.Assimilate(), Is.EqualTo(TransactionStatus.Committed));
            output = (Toposolid)document.GetElement(copyId);
            Assert.That(((Sketch)document.GetElement(output.SketchId)).Profile.Size, Is.EqualTo(1));
            Assert.That(ElevationAt(ReadTopSurface(output), 5, 5), Is.EqualTo(expected).Within(1e-4));
        }
        finally
        {
            document.Close(false);
        }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void Split_preserves_a_peaked_surface_on_both_islands(bool toposolid, bool curvedBoundary)
    {
        Document document = _uiApplication.Application.NewProjectDocument(UnitSystem.Metric);
        try
        {
            Level level = new FilteredElementCollector(document).OfClass(typeof(Level))
                .Cast<Level>().First();
            ElementId typeId = toposolid
                ? new FilteredElementCollector(document).OfClass(typeof(ToposolidType))
                    .Cast<ToposolidType>().First().Id
                : new FilteredElementCollector(document).OfClass(typeof(FloorType))
                    .Cast<FloorType>().First().Id;
            Element source;
            using (var transaction = new Transaction(document, "Create split surface probe"))
            {
                transaction.Start();
                FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(new ProbeWarningHandler());
                transaction.SetFailureHandlingOptions(options);
                var profiles = curvedBoundary
                    ? new List<CurveLoop>
                    {
                        CurvedRectangle(0, 0, 10, 10, level.Elevation),
                        CurvedRectangle(20, 0, 30, 10, level.Elevation)
                    }
                    : new List<CurveLoop>
                    {
                        Rectangle(0, 0, 10, 10, level.Elevation),
                        Rectangle(20, 0, 30, 10, level.Elevation)
                    };
                source = toposolid
                    ? Toposolid.Create(document, profiles, typeId, level.Id)
                    : Floor.Create(document, profiles, typeId, level.Id);
                document.Regenerate();
                SlabShapeEditor editor = toposolid
                    ? ((Toposolid)source).GetSlabShapeEditor()
                    : ((Floor)source).GetSlabShapeEditor();
                if (!editor.IsEnabled) editor.Enable();
                document.Regenerate();
                editor.AddPoint(new XYZ(5, 5, level.Elevation + 3));
                editor.AddPoint(new XYZ(25, 5, level.Elevation + 5));
                Assert.That(transaction.Commit(), Is.EqualTo(TransactionStatus.Committed));
            }

            long sourceId = source.Id.Value;
            var sampleLocations = new[]
            {
                (2.0, 2.0), (5.0, 5.0), (8.0, 8.0),
                (22.0, 2.0), (25.0, 5.0), (28.0, 8.0)
            };
            TriangleSurface originalSurface = ReadTopSurface((HostObject)source);
            double[] expected = sampleLocations.Select(point => ElevationAt(originalSurface, point.Item1, point.Item2)).ToArray();
            Assert.That(expected.Max() - expected.Min(), Is.GreaterThan(1));

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new SplitBoundariesService(
                new TransactionService(), new GeometryBoundaryService(cache), new SlabService());
            var reporter = new ProbeReporter();
            service.SplitBoundaries(document, new List<Element> { source }, reporter);

            Assert.That(document.GetElement(new ElementId(sourceId)), Is.Null,
                string.Join(Environment.NewLine, reporter.Errors));
            List<HostObject> outputs = toposolid
                ? new FilteredElementCollector(document).OfClass(typeof(Toposolid))
                    .Cast<HostObject>().ToList()
                : new FilteredElementCollector(document).OfClass(typeof(Floor))
                    .Cast<HostObject>().ToList();
            Assert.That(outputs, Has.Count.EqualTo(2), string.Join(Environment.NewLine, reporter.Errors));

            TriangleSurface[] surfaces = outputs.Select(ReadTopSurface).ToArray();
            for (int i = 0; i < sampleLocations.Length; i++)
            {
                var (x, y) = sampleLocations[i];
                double actual = surfaces
                    .Where(surface => surface.TryGetElevation(x, y, out _))
                    .Select(surface => ElevationAt(surface, x, y))
                    .Single();
                Assert.That(actual, Is.EqualTo(expected[i]).Within(1e-3),
                    $"Surface changed at ({x}, {y}). {string.Join("; ", reporter.Errors)}");
            }
        }
        finally
        {
            document.Close(false);
        }
    }

    private static CurveLoop Rectangle(double x0, double y0, double x1, double y1, double z)
    {
        XYZ a = new XYZ(x0, y0, z);
        XYZ b = new XYZ(x1, y0, z);
        XYZ c = new XYZ(x1, y1, z);
        XYZ d = new XYZ(x0, y1, z);
        return CurveLoop.Create(new List<Curve>
        {
            Line.CreateBound(a, b), Line.CreateBound(b, c),
            Line.CreateBound(c, d), Line.CreateBound(d, a)
        });
    }

    private static CurveLoop CurvedRectangle(double x0, double y0, double x1, double y1, double z)
    {
        XYZ a = new XYZ(x0, y0, z);
        XYZ b = new XYZ(x1, y0, z);
        XYZ c = new XYZ(x1, y1, z);
        XYZ d = new XYZ(x0, y1, z);
        XYZ onArc = new XYZ(x1 + 2, (y0 + y1) / 2, z);
        return CurveLoop.Create(new List<Curve>
        {
            Line.CreateBound(a, b), Arc.Create(b, c, onArc),
            Line.CreateBound(c, d), Line.CreateBound(d, a)
        });
    }

    private static TriangleSurface ReadTopSurface(HostObject slab)
    {
        var triangles = new List<SurfaceTriangle>();
        foreach (Reference reference in HostObjectUtils.GetTopFaces(slab))
        {
            Face face = (Face)slab.GetGeometryObjectFromReference(reference);
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

    private static double ElevationAt(TriangleSurface surface, double x, double y)
    {
        if (!surface.TryGetElevation(x, y, out double elevation))
            throw new InvalidOperationException($"No surface at ({x}, {y}).");
        return elevation;
    }

    private sealed class ProbeReporter : IProgressReporter
    {
        private readonly string? _progressPath;
        public ProbeReporter(string? progressPath = null) => _progressPath = progressPath;
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public void Report(string message, double percentage) { }
        public void Log(string message)
        {
            if (_progressPath != null) File.AppendAllText(_progressPath, message + Environment.NewLine);
        }
        public void LogWarning(string message)
        {
            Warnings.Add(message);
            Log("Warning: " + message);
        }
        public void LogError(string message)
        {
            Errors.Add(message);
            Log("Error: " + message);
        }
    }

    private sealed class ProbeWarningHandler : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            foreach (FailureMessageAccessor failure in accessor.GetFailureMessages())
                if (failure.GetSeverity() == FailureSeverity.Warning) accessor.DeleteWarning(failure);
            return FailureProcessingResult.Continue;
        }
    }
}
