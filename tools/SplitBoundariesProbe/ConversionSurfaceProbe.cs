using System.IO;
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

public sealed class ConversionSurfaceProbe
{
    private UIApplication _uiApplication = null!;

    [OneTimeSetUp]
    public void Setup(UIApplication application) => _uiApplication = application;

    [Test]
    public void Both_conversions_preserve_recovery_model_surface()
    {
        string? path = Environment.GetEnvironmentVariable("LECG_CONVERSION_MODEL_PROBE_PATH");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Assert.Ignore("Set LECG_CONVERSION_MODEL_PROBE_PATH to a disposable model copy.");

        string reportPath = Path.ChangeExtension(path!, ".conversion.txt");
        var report = new StringBuilder();
        Document document = _uiApplication.Application.OpenDocumentFile(path!);
        try
        {
            var source = document.GetElement(new ElementId(12197304)) as Toposolid
                ?? document.GetElement(new ElementId(12197503)) as Toposolid
                ?? throw new InvalidOperationException("Recovery-model source Toposolid is missing.");
            List<XYZ> expectedPoints = ReadShapePoints(source);
            List<(XYZ Start, XYZ End)> expectedCreases = ReadShapeCreases(source);
            TriangleSurface expectedSurface = ReadTopSurface(source);
            FloorType floorType = new FilteredElementCollector(document)
                .OfClass(typeof(FloorType)).Cast<FloorType>()
                .First(type => !type.IsFoundationSlab);

            var transactionService = new TransactionService();
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var boundaryService = new GeometryBoundaryService(cache);
            var slabService = new SlabService();
            var transferService = new SplitBoundariesService(
                transactionService, boundaryService, slabService);
            var conversionService = new ConversionService(
                transactionService, boundaryService, transferService);
            var reporter = new ProbeReporter(report);

            HashSet<long> floorIds = CollectIds<Floor>(document);
            conversionService.ConvertToposolidToFloor(document, new Element[] { source },
                floorType.Id, source.LevelId, false, reporter);
            Floor convertedFloor = NewElement<Floor>(document, floorIds);
            VerifyShape("Toposolid to Floor", convertedFloor, expectedPoints,
                expectedCreases, expectedSurface, report);

            HashSet<long> topoIds = CollectIds<Toposolid>(document);
            conversionService.ConvertFloorToToposolid(document, new Element[] { convertedFloor },
                source.GetTypeId(), source.LevelId, false, reporter);
            Toposolid convertedToposolid = NewElement<Toposolid>(document, topoIds);
            VerifyShape("Floor to Toposolid", convertedToposolid, expectedPoints,
                expectedCreases, expectedSurface, report);
        }
        catch (Exception exception)
        {
            report.AppendLine(exception.ToString());
            throw;
        }
        finally
        {
            File.WriteAllText(reportPath, report.ToString());
            document.Close(false);
        }
    }

    [Test]
    public void Create_type_and_preserve_level_work_in_both_directions()
    {
        string? path = Environment.GetEnvironmentVariable("LECG_CONVERSION_MODEL_PROBE_PATH");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Assert.Ignore("Set LECG_CONVERSION_MODEL_PROBE_PATH to a disposable model copy.");

        string reportPath = Path.ChangeExtension(path!, ".create-type-preserve-level.txt");
        var report = new StringBuilder();
        Document document = _uiApplication.Application.OpenDocumentFile(path!);
        try
        {
            var source = document.GetElement(new ElementId(12197304)) as Toposolid
                ?? document.GetElement(new ElementId(12197503)) as Toposolid
                ?? throw new InvalidOperationException("Recovery-model source Toposolid is missing.");
            ElementId originalLevelId = source.LevelId;
            var sourceType = (ToposolidType)document.GetElement(source.GetTypeId());
            List<XYZ> expectedPoints = ReadShapePoints(source);
            List<(XYZ Start, XYZ End)> expectedCreases = ReadShapeCreases(source);
            TriangleSurface expectedSurface = ReadTopSurface(source);

            var floorTypeIds = CollectIds<FloorType>(document);
            var transactionService = new TransactionService();
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var boundaryService = new GeometryBoundaryService(cache);
            var transferService = new SplitBoundariesService(
                transactionService, boundaryService, new SlabService());
            var conversionService = new ConversionService(
                transactionService, boundaryService, transferService);
            var reporter = new ProbeReporter(report);

            HashSet<long> floorIds = CollectIds<Floor>(document);
            conversionService.ConvertToposolidToFloor(document, new Element[] { source },
                ElementId.InvalidElementId, ElementId.InvalidElementId, false, reporter,
                createTypeFromSource: true, preserveSourceLevel: true);
            Floor convertedFloor = NewElement<Floor>(document, floorIds);
            var createdFloorType = (FloorType)document.GetElement(convertedFloor.GetTypeId());
            Assert.That(floorTypeIds, Does.Not.Contain(createdFloorType.Id.Value));
            Assert.That(convertedFloor.LevelId, Is.EqualTo(originalLevelId));
            VerifyCompoundStructure(sourceType, createdFloorType);
            VerifyShape("Toposolid to created Floor type", convertedFloor, expectedPoints,
                expectedCreases, expectedSurface, report);
            report.AppendLine($"Toposolid to Floor: created type '{createdFloorType.Name}', "
                + $"copied {createdFloorType.GetCompoundStructure().GetLayers().Count} layers, "
                + $"and preserved level {convertedFloor.LevelId.Value}.");

            var toposolidTypeIds = CollectIds<ToposolidType>(document);
            HashSet<long> toposolidIds = CollectIds<Toposolid>(document);
            conversionService.ConvertFloorToToposolid(document, new Element[] { convertedFloor },
                ElementId.InvalidElementId, ElementId.InvalidElementId, false, reporter,
                createTypeFromSource: true, preserveSourceLevel: true);
            Toposolid convertedToposolid = NewElement<Toposolid>(document, toposolidIds);
            var createdToposolidType = (ToposolidType)document.GetElement(convertedToposolid.GetTypeId());
            Assert.That(toposolidTypeIds, Does.Not.Contain(createdToposolidType.Id.Value));
            Assert.That(convertedToposolid.LevelId, Is.EqualTo(convertedFloor.LevelId));
            VerifyCompoundStructure(createdFloorType, createdToposolidType);
            VerifyShape("Floor to created Toposolid type", convertedToposolid, expectedPoints,
                expectedCreases, expectedSurface, report);
            report.AppendLine($"Floor to Toposolid: created type '{createdToposolidType.Name}', "
                + $"copied {createdToposolidType.GetCompoundStructure().GetLayers().Count} layers, "
                + $"and preserved level {convertedToposolid.LevelId.Value}.");

            var floorViewModel = new ConvertFloorToToposolidViewModel(conversionService);
            floorViewModel.Initialize(document);
            var topoViewModel = new ConvertToposolidToFloorViewModel(conversionService);
            topoViewModel.Initialize(document);
            Assert.That(floorViewModel.TargetTypes.Any(option => option.Name == "Create Type"), Is.True);
            Assert.That(floorViewModel.Levels.Any(option => option.Name == "Preserve Level"), Is.True);
            Assert.That(topoViewModel.TargetTypes.Any(option => option.Name == "Create Type"), Is.True);
            Assert.That(topoViewModel.Levels.Any(option => option.Name == "Preserve Level"), Is.True);

            var multiLayerFloor = document.GetElement(new ElementId(739904)) as Floor
                ?? throw new InvalidOperationException("Recovery-model Floor 739904 is missing.");
            FloorType multiLayerType = new FilteredElementCollector(document)
                .OfClass(typeof(FloorType)).Cast<FloorType>()
                .Where(type =>
                {
                    CompoundStructure? structure = type.GetCompoundStructure();
                    return !type.IsFoundationSlab
                        && structure != null
                        && structure.GetLayers().Count > 1
                        && structure.IsValid(document, out _, out _);
                })
                .OrderByDescending(type => type.GetCompoundStructure().GetLayers().Count)
                .First();
            int sourceLayerCount = multiLayerType.GetCompoundStructure().GetLayers().Count;
            Assert.That(sourceLayerCount, Is.GreaterThan(1),
                "The recovery model needs a multi-layer Floor type for this regression.");
            transactionService.Run(document, "Use multi-layer test type", currentDoc =>
                multiLayerFloor.ChangeTypeId(multiLayerType.Id));

            ElementId multiLayerSourceLevelId = multiLayerFloor.LevelId;
            List<XYZ> multiLayerPoints = ReadShapePoints(multiLayerFloor);
            List<(XYZ Start, XYZ End)> multiLayerCreases = ReadShapeCreases(multiLayerFloor);
            TriangleSurface multiLayerSurface = ReadTopSurface(multiLayerFloor);
            HashSet<long> priorToposolidIds = CollectIds<Toposolid>(document);
            var priorToposolidTypeIds = CollectIds<ToposolidType>(document);
            conversionService.ConvertFloorToToposolid(document, new Element[] { multiLayerFloor },
                ElementId.InvalidElementId, ElementId.InvalidElementId, false, reporter,
                createTypeFromSource: true, preserveSourceLevel: true);
            Toposolid multiLayerOutput = NewElement<Toposolid>(document, priorToposolidIds);
            var multiLayerOutputType =
                (ToposolidType)document.GetElement(multiLayerOutput.GetTypeId());
            Assert.That(priorToposolidTypeIds,
                Does.Not.Contain(multiLayerOutputType.Id.Value));
            Assert.That(multiLayerOutput.LevelId, Is.EqualTo(multiLayerSourceLevelId));
            Assert.That(multiLayerOutput.Pinned, Is.EqualTo(multiLayerFloor.Pinned));
            VerifyCompoundStructure(multiLayerType, multiLayerOutputType);
            VerifyShape("Multi-layer Floor to created Toposolid type", multiLayerOutput,
                multiLayerPoints, multiLayerCreases, multiLayerSurface, report);
            report.AppendLine($"Multi-layer Floor to Toposolid: copied all "
                + $"{sourceLayerCount} layers and preserved level "
                + $"{multiLayerOutput.LevelId.Value}.");

            HashSet<long> priorFloorIds = CollectIds<Floor>(document);
            var priorFloorTypeIds = CollectIds<FloorType>(document);
            conversionService.ConvertToposolidToFloor(document, new Element[] { multiLayerOutput },
                ElementId.InvalidElementId, ElementId.InvalidElementId, false, reporter,
                createTypeFromSource: true, preserveSourceLevel: true);
            Floor multiLayerRoundTrip = NewElement<Floor>(document, priorFloorIds);
            var multiLayerRoundTripType =
                (FloorType)document.GetElement(multiLayerRoundTrip.GetTypeId());
            Assert.That(priorFloorTypeIds,
                Does.Not.Contain(multiLayerRoundTripType.Id.Value));
            Assert.That(multiLayerRoundTrip.LevelId, Is.EqualTo(multiLayerSourceLevelId));
            Assert.That(multiLayerRoundTrip.Pinned, Is.EqualTo(multiLayerOutput.Pinned));
            VerifyCompoundStructure(multiLayerOutputType, multiLayerRoundTripType);
            VerifyShape("Multi-layer Toposolid to created Floor type", multiLayerRoundTrip,
                multiLayerPoints, multiLayerCreases, multiLayerSurface, report);
            report.AppendLine($"Multi-layer Toposolid to Floor: copied all "
                + $"{sourceLayerCount} layers and preserved level "
                + $"{multiLayerRoundTrip.LevelId.Value}.");
        }
        catch (Exception exception)
        {
            report.AppendLine(exception.ToString());
            throw;
        }
        finally
        {
            File.WriteAllText(reportPath, report.ToString());
            document.Close(false);
        }
    }

    [Test]
    public void Split_preserves_recovery_floor_controls_and_creases()
    {
        string? path = Environment.GetEnvironmentVariable("LECG_CONVERSION_MODEL_PROBE_PATH");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Assert.Ignore("Set LECG_CONVERSION_MODEL_PROBE_PATH to a disposable model copy.");

        string reportPath = Path.ChangeExtension(path!, ".floor-split.txt");
        var report = new StringBuilder();
        Document document = _uiApplication.Application.OpenDocumentFile(path!);
        try
        {
            var source = document.GetElement(new ElementId(1641444)) as Floor
                ?? throw new InvalidOperationException("Source Floor 1641444 is missing.");
            List<XYZ> expectedPoints = ReadShapePoints(source);
            List<(XYZ Start, XYZ End)> expectedCreases = ReadShapeCreases(source);
            TriangleSurface expectedSurface = ReadTopSurface(source);
            HashSet<long> originalIds = CollectIds<Floor>(document);

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new SplitBoundariesService(new TransactionService(),
                new GeometryBoundaryService(cache), new SlabService());
            var reporter = new ProbeReporter(report);
            service.SplitBoundaries(document, new Element[] { source }, reporter);

            Assert.That(document.GetElement(new ElementId(1641444)), Is.Null,
                report.ToString());
            List<Floor> outputs = new FilteredElementCollector(document)
                .OfClass(typeof(Floor)).Cast<Floor>()
                .Where(floor => !originalIds.Contains(floor.Id.Value)).ToList();
            Assert.That(outputs.Count, Is.GreaterThan(1), report.ToString());
            List<XYZ> outputPoints = outputs.SelectMany(ReadShapePoints).ToList();
            foreach (XYZ expected in expectedPoints)
                Assert.That(outputPoints.Any(actual => SamePoint(actual, expected)), Is.True,
                    $"Floor split lost shape point {expected}.\n{report}");

            TriangleSurface[] outputSurfaces = outputs.Select(output => ReadTopSurface(output)).ToArray();
            int checkedCreases = 0;
            foreach ((XYZ start, XYZ end) in expectedCreases)
            {
                XYZ midpoint = (start + end) / 2;
                if (!expectedSurface.TryGetElevation(midpoint.X, midpoint.Y, out double expectedZ, 0.001))
                    continue;
                double[] matches = outputSurfaces
                    .Where(surface => surface.TryGetElevation(midpoint.X, midpoint.Y, out _, 0.001))
                    .Select(surface =>
                    {
                        surface.TryGetElevation(midpoint.X, midpoint.Y, out double elevation, 0.001);
                        return elevation;
                    }).ToArray();
                Assert.That(matches.Any(actual => Math.Abs(actual - expectedZ) <= 0.001), Is.True,
                    $"Floor split changed crease elevation at {midpoint}.\n{report}");
                checkedCreases++;
            }
            report.AppendLine($"Floor split produced {outputs.Count} outputs, preserved "
                + $"{expectedPoints.Count} controls, and verified {checkedCreases} crease samples.");
        }
        catch (Exception exception)
        {
            report.AppendLine(exception.ToString());
            throw;
        }
        finally
        {
            File.WriteAllText(reportPath, report.ToString());
            document.Close(false);
        }
    }

    [Test]
    public void Split_preserves_pin_state_for_pinned_recovery_floor()
    {
        string? path = Environment.GetEnvironmentVariable("LECG_CONVERSION_MODEL_PROBE_PATH");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Assert.Ignore("Set LECG_CONVERSION_MODEL_PROBE_PATH to a disposable model copy.");

        string reportPath = Path.ChangeExtension(path!, ".pinned-floor-split.txt");
        var report = new StringBuilder();
        Document document = _uiApplication.Application.OpenDocumentFile(path!);
        try
        {
            var source = document.GetElement(new ElementId(739904)) as Floor
                ?? throw new InvalidOperationException("Source Floor 739904 is missing.");
            Assert.That(source.Pinned, Is.True, "The recovery-model regression source must be pinned.");
            List<XYZ> expectedPoints = ReadShapePoints(source);
            List<(XYZ Start, XYZ End)> expectedCreases = ReadShapeCreases(source);
            TriangleSurface expectedSurface = ReadTopSurface(source);
            HashSet<long> originalIds = CollectIds<Floor>(document);

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new SplitBoundariesService(new TransactionService(),
                new GeometryBoundaryService(cache), new SlabService());
            var reporter = new ProbeReporter(report);
            service.SplitBoundaries(document, new Element[] { source }, reporter);

            Assert.That(document.GetElement(new ElementId(739904)), Is.Null, report.ToString());
            List<Floor> outputs = new FilteredElementCollector(document)
                .OfClass(typeof(Floor)).Cast<Floor>()
                .Where(floor => !originalIds.Contains(floor.Id.Value)).ToList();
            Assert.That(outputs.Count, Is.EqualTo(2), report.ToString());
            Assert.That(outputs.All(output => output.Pinned), Is.True,
                "Every output must inherit the source floor's pinned state.\n" + report);

            List<XYZ> outputPoints = outputs.SelectMany(ReadShapePoints).ToList();
            foreach (XYZ expected in expectedPoints)
                Assert.That(outputPoints.Any(actual => SamePoint(actual, expected)), Is.True,
                    $"Pinned floor split lost shape point {expected}.\n{report}");

            TriangleSurface[] outputSurfaces = outputs.Select(ReadTopSurface).ToArray();
            foreach ((XYZ start, XYZ end) in expectedCreases)
            {
                XYZ midpoint = (start + end) / 2;
                Assert.That(expectedSurface.TryGetElevation(
                    midpoint.X, midpoint.Y, out double expectedZ, 0.001), Is.True);
                Assert.That(outputSurfaces.Any(surface =>
                    surface.TryGetElevation(midpoint.X, midpoint.Y, out double actualZ, 0.001)
                    && Math.Abs(actualZ - expectedZ) <= 0.001), Is.True,
                    $"Pinned floor split changed crease elevation at {midpoint}.\n{report}");
            }
            report.AppendLine($"Pinned floor split produced {outputs.Count} pinned outputs, "
                + $"preserved {expectedPoints.Count} controls, and verified "
                + $"{expectedCreases.Count} crease samples.");
        }
        catch (Exception exception)
        {
            report.AppendLine(exception.ToString());
            throw;
        }
        finally
        {
            File.WriteAllText(reportPath, report.ToString());
            document.Close(false);
        }
    }

    [Test]
    public void Split_preserves_complex_recovery_floor()
    {
        string? path = Environment.GetEnvironmentVariable("LECG_CONVERSION_MODEL_PROBE_PATH");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Assert.Ignore("Set LECG_CONVERSION_MODEL_PROBE_PATH to a disposable model copy.");

        string reportPath = Path.ChangeExtension(path!, ".complex-floor-split.txt");
        var report = new StringBuilder();
        Document document = _uiApplication.Application.OpenDocumentFile(path!);
        try
        {
            var source = document.GetElement(new ElementId(2181761)) as Floor
                ?? throw new InvalidOperationException("Source Floor 2181761 is missing.");
            List<XYZ> expectedPoints = ReadShapePoints(source);
            HashSet<long> originalIds = CollectIds<Floor>(document);

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new SplitBoundariesService(new TransactionService(),
                new GeometryBoundaryService(cache), new SlabService());
            var reporter = new ProbeReporter(report);
            service.SplitBoundaries(document, new Element[] { source }, reporter);

            Assert.That(document.GetElement(new ElementId(2181761)), Is.Null, report.ToString());
            List<Floor> outputs = new FilteredElementCollector(document)
                .OfClass(typeof(Floor)).Cast<Floor>()
                .Where(floor => !originalIds.Contains(floor.Id.Value)).ToList();
            Assert.That(outputs.Count, Is.EqualTo(7), report.ToString());
            List<XYZ> outputPoints = outputs.SelectMany(ReadShapePoints).ToList();
            foreach (XYZ expected in expectedPoints)
                Assert.That(outputPoints.Any(actual => SamePoint(actual, expected)), Is.True,
                    $"Complex floor split lost shape point {expected}.\n{report}");
            report.AppendLine($"Complex floor split produced {outputs.Count} outputs and preserved "
                + $"{expectedPoints.Count} controls.");
        }
        catch (Exception exception)
        {
            report.AppendLine(exception.ToString());
            throw;
        }
        finally
        {
            File.WriteAllText(reportPath, report.ToString());
            document.Close(false);
        }
    }

    [Test]
    public void Inspect_model_finds_all_eligible_recovery_elements()
    {
        string? path = Environment.GetEnvironmentVariable("LECG_CONVERSION_MODEL_PROBE_PATH");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Assert.Ignore("Set LECG_CONVERSION_MODEL_PROBE_PATH to a disposable model copy.");

        string reportPath = Path.ChangeExtension(path!, ".inspection.txt");
        Document document = _uiApplication.Application.OpenDocumentFile(path!);
        try
        {
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new SplitBoundariesService(new TransactionService(),
                new GeometryBoundaryService(cache), new SlabService());
            var viewModel = new SplitBoundariesViewModel(service);
            viewModel.InspectDocument(document);

            long[] ids = viewModel.RowItems.Select(row => row.Id).ToArray();
            var report = new StringBuilder();
            report.AppendLine(viewModel.InspectionStatus);
            if (document.GetElement(new ElementId(12197304)) is Toposolid knownToposolid)
            {
                SplitBoundaryInspection direct = service.InspectBoundaries(knownToposolid);
                report.AppendLine($"Direct 12197304: {direct.BoundaryCount} loops / "
                    + $"{direct.IslandCount} islands / eligible {direct.CanSplit}");
            }
            else
            {
                report.AppendLine("Direct 12197304: not a Toposolid in this model.");
            }
            foreach (var row in viewModel.RowItems)
                report.AppendLine($"{row.Id} | {row.Type} | {row.Name} | {row.Status}");
            File.WriteAllText(reportPath, report.ToString());

            Assert.That(ids, Does.Contain(1641444), "The failing multi-island Floor was not found.");
            Assert.That(viewModel.RowItems.Any(row => row.Type == nameof(Toposolid)), Is.True,
                "No eligible Toposolid was found.");
            Assert.That(viewModel.RowItems.All(row => row.Status.StartsWith("Ready:")), Is.True);
            Assert.That(viewModel.RowItems.All(row => row.IsChecked), Is.True);
            Assert.That(viewModel.CanRun, Is.True);

        }
        finally
        {
            document.Close(false);
        }
    }

    [Test]
    public void Inspect_recovery_split_failure_sources()
    {
        string? path = Environment.GetEnvironmentVariable("LECG_CONVERSION_MODEL_PROBE_PATH");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            Assert.Ignore("Set LECG_CONVERSION_MODEL_PROBE_PATH to a disposable model copy.");

        string reportPath = Path.ChangeExtension(path!, ".failure-sources.txt");
        var report = new StringBuilder();
        Document document = _uiApplication.Application.OpenDocumentFile(path!);
        try
        {
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new SplitBoundariesService(new TransactionService(),
                new GeometryBoundaryService(cache), new SlabService());
            foreach (long value in new[] { 739904L, 2181761L })
            {
                var floor = document.GetElement(new ElementId(value)) as Floor
                    ?? throw new InvalidOperationException($"Floor {value} is missing.");
                SplitBoundaryInspection inspection = service.InspectBoundaries(floor);
                SlabShapeEditor editor = floor.GetSlabShapeEditor();
                report.AppendLine($"ID {value}: group {floor.GroupId.Value}, pinned {floor.Pinned}, "
                    + $"modifiable {floor.IsModifiable}, workset {floor.WorksetId.IntegerValue}, "
                    + $"loops {inspection.BoundaryCount}, islands {inspection.IslandCount}, "
                    + $"vertices {editor.SlabShapeVertices.Size}, creases {editor.SlabShapeCreases.Size}.");
                foreach (var group in editor.SlabShapeCreases.Cast<SlabShapeCrease>()
                    .GroupBy(crease => crease.CreaseType))
                    report.AppendLine($"  Creases {group.Key}: {group.Count()}");
                if (value == 2181761)
                {
                    XYZ target = new XYZ(-2.9584, 26.7085, 0);
                    foreach (SlabShapeCrease crease in editor.SlabShapeCreases.Cast<SlabShapeCrease>()
                        .OrderBy(crease =>
                        {
                            List<XYZ> points = crease.EndPoints.Cast<SlabShapeVertex>()
                                .Select(vertex => vertex.Position).ToList();
                            XYZ midpoint = points.Count == 2 ? (points[0] + points[1]) / 2 : XYZ.Zero;
                            return Math.Pow(midpoint.X - target.X, 2) + Math.Pow(midpoint.Y - target.Y, 2);
                        }).Take(4))
                        report.AppendLine($"  Nearby {crease.CreaseType}: "
                            + string.Join(" -> ", crease.EndPoints.Cast<SlabShapeVertex>()
                                .Select(vertex => vertex.Position.ToString())));
                }
            }
            File.WriteAllText(reportPath, report.ToString());
        }
        finally
        {
            document.Close(false);
        }
    }

    private static void VerifyShape(string label, HostObject output, IReadOnlyList<XYZ> expectedPoints,
        IReadOnlyList<(XYZ Start, XYZ End)> expectedCreases, TriangleSurface expectedSurface,
        StringBuilder report)
    {
        List<XYZ> actualPoints = ReadShapePoints(output);
        foreach (XYZ expected in expectedPoints)
            Assert.That(actualPoints.Any(actual => SamePoint(actual, expected)), Is.True,
                $"{label} lost shape point {expected}.");

        TriangleSurface actualSurface = ReadTopSurface(output);
        int checkedSamples = 0;
        foreach ((XYZ start, XYZ end) in expectedCreases)
        {
            XYZ midpoint = (start + end) / 2;
            var sample = new SurfacePoint(midpoint.X, midpoint.Y, midpoint.Z);
            if (!actualSurface.TryGetElevation(sample.X, sample.Y, out double actualZ, 0.001)) continue;
            Assert.That(expectedSurface.TryGetElevation(
                sample.X, sample.Y, out double expectedZ, 0.001), Is.True);
            Assert.That(actualZ, Is.EqualTo(expectedZ).Within(0.001),
                $"{label} changed surface Z at ({sample.X:F6}, {sample.Y:F6}).");
            checkedSamples++;
        }
        Assert.That(checkedSamples, Is.GreaterThan(0));
        report.AppendLine($"{label}: preserved {expectedPoints.Count} controls and verified "
            + $"{checkedSamples} surface samples on element {output.Id}.");
    }

    private static void VerifyCompoundStructure(HostObjAttributes sourceType,
        HostObjAttributes targetType)
    {
        CompoundStructure source = sourceType.GetCompoundStructure();
        CompoundStructure target = targetType.GetCompoundStructure();
        IList<CompoundStructureLayer> sourceLayers = source.GetLayers();
        IList<CompoundStructureLayer> targetLayers = target.GetLayers();
        Assert.That(targetLayers.Count, Is.EqualTo(sourceLayers.Count));
        Assert.That(target.GetWidth(), Is.EqualTo(source.GetWidth()).Within(1e-9));
        Assert.That(target.GetNumberOfShellLayers(ShellLayerType.Exterior),
            Is.EqualTo(source.GetNumberOfShellLayers(ShellLayerType.Exterior)));
        Assert.That(target.GetNumberOfShellLayers(ShellLayerType.Interior),
            Is.EqualTo(source.GetNumberOfShellLayers(ShellLayerType.Interior)));
        Assert.That(target.VariableLayerIndex, Is.EqualTo(source.VariableLayerIndex));
        Assert.That(target.StructuralMaterialIndex, Is.EqualTo(source.StructuralMaterialIndex));
        for (int index = 0; index < sourceLayers.Count; index++)
        {
            Assert.That(targetLayers[index].Width,
                Is.EqualTo(sourceLayers[index].Width).Within(1e-9));
            Assert.That(targetLayers[index].MaterialId,
                Is.EqualTo(sourceLayers[index].MaterialId));
            Assert.That(targetLayers[index].Function,
                Is.EqualTo(sourceLayers[index].Function));
        }
    }

    private static HashSet<long> CollectIds<T>(Document document) where T : Element =>
        new FilteredElementCollector(document).OfClass(typeof(T)).Cast<T>()
            .Select(element => element.Id.Value).ToHashSet();

    private static T NewElement<T>(Document document, HashSet<long> originalIds) where T : Element =>
        new FilteredElementCollector(document).OfClass(typeof(T)).Cast<T>()
            .Single(element => !originalIds.Contains(element.Id.Value));

    private static List<XYZ> ReadShapePoints(HostObject element)
    {
        SlabShapeEditor editor = element switch
        {
            Floor floor => floor.GetSlabShapeEditor(),
            Toposolid toposolid => toposolid.GetSlabShapeEditor(),
            _ => throw new InvalidOperationException()
        };
        return editor.SlabShapeVertices.Cast<SlabShapeVertex>()
            .Select(vertex => vertex.Position).ToList();
    }

    private static List<(XYZ Start, XYZ End)> ReadShapeCreases(HostObject element)
    {
        SlabShapeEditor editor = element switch
        {
            Floor floor => floor.GetSlabShapeEditor(),
            Toposolid toposolid => toposolid.GetSlabShapeEditor(),
            _ => throw new InvalidOperationException()
        };
        return editor.SlabShapeCreases.Cast<SlabShapeCrease>()
            .Where(crease => crease.CreaseType != SlabShapeCreaseType.Boundary)
            .Select(crease => crease.EndPoints.Cast<SlabShapeVertex>()
                .Select(vertex => vertex.Position).ToList())
            .Where(points => points.Count == 2)
            .Select(points => (points[0], points[1])).ToList();
    }

    private static TriangleSurface ReadTopSurface(HostObject element)
    {
        var triangles = new List<SurfaceTriangle>();
        foreach (Reference reference in HostObjectUtils.GetTopFaces(element))
        {
            if (element.GetGeometryObjectFromReference(reference) is not Face face) continue;
            Mesh mesh = face.Triangulate();
            for (int index = 0; index < mesh.NumTriangles; index++)
            {
                MeshTriangle triangle = mesh.get_Triangle(index);
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

    private static bool SamePoint(XYZ first, XYZ second) =>
        Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2)) <= 0.0001
        && Math.Abs(first.Z - second.Z) <= 0.0001;

    private sealed class ProbeReporter : IProgressReporter
    {
        private readonly StringBuilder _report;
        public ProbeReporter(StringBuilder report) => _report = report;
        public void Report(string message, double percent) => _report.AppendLine($"[{percent:F0}%] {message}");
        public void Log(string message) => _report.AppendLine(message);
        public void LogWarning(string message) => _report.AppendLine("Warning: " + message);
        public void LogError(string message) => _report.AppendLine("Error: " + message);
    }
}
