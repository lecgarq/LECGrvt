using System.Diagnostics;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using LECG.Core;
using LECG.Core.Substance;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.SubstanceSmoke;

// Install the test manifest only for a deliberate smoke run. Creates its own project, never edits an open one.
public sealed class Smoke : IExternalApplication
{
    public Result OnStartup(UIControlledApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Idling += RunOnce;
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;

    private void RunOnce(object? sender, IdlingEventArgs args)
    {
        var app = (UIApplication)sender!;
        app.Idling -= RunOnce;
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LECG", "SubstanceSmoke", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(root);
        using var writer = new StreamWriter(Path.Combine(root, "result.log")) { AutoFlush = true };
        void Log(string text) => writer.WriteLine(text);
        try { Run(app, root, Log); Log("PASS: all runtime assertions"); }
        catch (Exception ex) { Log("FAIL: " + ex); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Run(UIApplication app, string root, Action<string> log)
    {
        log($"Revit {app.Application.VersionNumber} / {app.Application.VersionBuild}");
        foreach (var group in app.Application.GetAssets(AssetType.Appearance).GroupBy(a => a.Name).OrderBy(g => g.Key))
            log($"LIBRARY {group.Key} x{group.Count()} BaseSchema={(group.First().FindByName("BaseSchema") as AssetPropertyString)?.Value}");

        var viewModel = ServiceLocator.GetRequiredService<SubstanceBatchViewModel>();
        var view = ServiceLocator.CreateWith<SubstanceBatchView>(viewModel);
        Check(ReferenceEquals(view.DataContext, viewModel), "Substance window must resolve with its view model");
        view.Close();
        log($"WINDOW resolved / {viewModel.Rows.Count} material rows / default size {viewModel.SizeMillimeters} mm");

        using Document doc = app.Application.NewProjectDocument(UnitSystem.Metric);
        var service = ServiceLocator.GetRequiredService<ISubstanceMaterialCreateService>();
        var tx = ServiceLocator.GetRequiredService<ITransactionService>();
        var scan = SubstanceLibraryScanner.Scan(@"C:\LECG\SubstanceBakes");
        log($"SCAN {scan.Entries.Count} materials / {scan.Entries.Select(e => e.Category).Distinct().Count()} categories / {scan.Warnings.Count} warnings");
        Check(scan.Warnings.Count == 0, string.Join("; ", scan.Warnings));
        var options = new SubstanceBatchOptions(new BakeOptions(Path.Combine(root, "textures"), 2048, false), TextureTransform.Uniform(2500), false);
        var ceilings = scan.Entries.Where(e => e.Category == "Ceiling").ToList();
        Check(ceilings.Count == 2, "Expected two Ceiling materials");
        foreach (var entry in ceilings)
        {
            var report = service.Create(doc, entry, options, log);
            Check(report.Outcome == SubstanceMaterialOutcome.Created, report.Error ?? report.ToString());
            VerifyMaterial(doc, entry, log);
            VerifyBakeContract(entry, options.Bake, log);
            Check(service.Create(doc, entry, options, log).Outcome == SubstanceMaterialOutcome.Skipped, "Second run must skip");
            Check(service.Create(doc, entry, options with { OverwriteExisting = true }, log).Outcome == SubstanceMaterialOutcome.Updated, "Overwrite must update");
        }

        string movedTextureRoot = Path.Combine(root, "moved-textures");
        CopyDirectory(options.Bake.OutputRoot, movedTextureRoot);
        SubstanceTextureRepathResult repath = service.RepathExisting(doc, ceilings, movedTextureRoot, log);
        Check(repath.Selected == ceilings.Count && repath.Repathed == ceilings.Count && repath.Failed == 0,
            "Repath must update every selected material without failures: " + repath.Summary);
        Check(repath.UpdatedBitmapPaths >= ceilings.Count * 3,
            "Repath must update at least albedo, roughness, and normal for each material");
        foreach (var entry in ceilings) VerifyTextureRoot(doc, entry, movedTextureRoot);
        log("REPATH " + repath.Summary);

        var metals = scan.Entries.Where(e => e.Category == "Metal").ToList();
        Check(metals.Count == 28, $"Expected 28 Metal materials, got {metals.Count}");
        var initialNames = service.ExistingMaterialNames(doc);
        foreach (var entry in metals.Where(e => initialNames.Contains(e.DisplayName)))
        {
            Check(service.Create(doc, entry, options, log).Outcome == SubstanceMaterialOutcome.Skipped, "Built-in name collision must skip");
            tx.Run(doc, "Isolate smoke materials", d => FindMaterial(d, entry.DisplayName).Name = "Smoke original " + entry.DisplayName);
        }
        var timer = Stopwatch.StartNew();
        foreach (var entry in metals)
        {
            var report = service.Create(doc, entry, options, log);
            Check(report.Outcome == SubstanceMaterialOutcome.Created, report.Error ?? report.ToString());
            VerifyMaterial(doc, entry, log);
        }
        log($"METAL {metals.Count} created, 0 failed, {timer.Elapsed.TotalSeconds:F1} seconds; F0 files={Directory.GetFiles(options.Bake.OutputRoot, "*_f0.png", SearchOption.AllDirectories).Length}");

        // The Ceiling source manifests have no opacity map; use a real cutout material for this check.
        var cutout = scan.Entries.First(e => e.Slug == "construction_rebar_grid");
        Check(service.Create(doc, cutout, options, log).Outcome == SubstanceMaterialOutcome.Created, "Cutout material must be created");
        VerifyMaterial(doc, cutout, log);

        // A painted wall exercises the diagnostic's face resolution and leaves a visual check scene.
        Wall? wall = null;
        tx.Run(doc, "Smoke scene", d =>
        {
            var level = new FilteredElementCollector(d).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault() ?? Level.Create(d, 0);
            var wallType = new FilteredElementCollector(d).OfClass(typeof(WallType)).Cast<WallType>().First(t => t.Kind == WallKind.Basic);
            wall = Wall.Create(d, Line.CreateBound(XYZ.Zero, new XYZ(16, 0, 0)), wallType.Id, level.Id, 10, 0, false, false);
            d.Regenerate();
            var face = wall.GetGeometryObjectFromReference(HostObjectUtils.GetSideFaces(wall, ShellLayerType.Exterior).First()) as Face;
            d.Paint(wall.Id, face!, FindMaterial(d, ceilings[0].DisplayName).Id);
            var cutoutWall = Wall.Create(d, Line.CreateBound(new XYZ(20, 0, 0), new XYZ(36, 0, 0)), wallType.Id, level.Id, 10, 0, false, false);
            d.Regenerate();
            var cutoutFace = (Face)cutoutWall.GetGeometryObjectFromReference(HostObjectUtils.GetSideFaces(cutoutWall, ShellLayerType.Exterior).First());
            d.Paint(cutoutWall.Id, cutoutFace, FindMaterial(d, cutout.DisplayName).Id);
            var floorType = new FilteredElementCollector(d).OfClass(typeof(FloorType)).Cast<FloorType>().First(t => !t.IsFoundationSlab);
            var points = new[] { new XYZ(0, -12, 0), new XYZ(16, -12, 0), new XYZ(16, 0, 0), XYZ.Zero };
            var loop = new CurveLoop();
            for (int i = 0; i < points.Length; i++) loop.Append(Line.CreateBound(points[i], points[(i + 1) % points.Length]));
            var floor = Floor.Create(d, new List<CurveLoop> { loop }, floorType.Id, level.Id);
            d.Regenerate();
            var top = (Face)floor.GetGeometryObjectFromReference(HostObjectUtils.GetTopFaces(floor).First());
            d.Paint(floor.Id, top, FindMaterial(d, ceilings[1].DisplayName).Id);
            var viewType = new FilteredElementCollector(d).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(v => v.ViewFamily == ViewFamily.ThreeDimensional);
            var view = View3D.CreateIsometric(d, viewType.Id);
            view.Name = "Substance verification";
            view.DisplayStyle = DisplayStyle.Realistic;
        });
        var lines = new List<string>();
        AppearanceAssetDumpService.DumpFace(doc, HostObjectUtils.GetSideFaces(wall!, ShellLayerType.Exterior).First(), lines.Add);
        foreach (string line in lines) log(line);
        Check(lines.Any(l => l.Contains($"Material '{ceilings[0].DisplayName}'")), "Diagnostic must dump the painted material");
        Check(lines.Any(l => l.Contains("bumpmap_Type")), "Diagnostic must traverse the normal asset");
        string model = Path.Combine(root, "Substance-verification.rvt");
        doc.SaveAs(model);
        doc.Close(false);
        log("MODEL " + model);
    }

    private static Material FindMaterial(Document doc, string name) =>
        new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>().Single(m => m.Name == name);

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(source, destination, StringComparison.OrdinalIgnoreCase));
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = file.Replace(source, destination, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static void VerifyTextureRoot(Document doc, SubstanceMaterialEntry entry, string expectedRoot)
    {
        var material = FindMaterial(doc, entry.DisplayName);
        var asset = ((AppearanceAssetElement)doc.GetElement(material.AppearanceAssetId)).GetRenderingAsset();
        foreach (string name in new[] { "opaque_albedo", "surface_roughness", "surface_normal" })
        {
            Asset? connected = asset.FindByName(name)?.GetSingleConnectedAsset();
            Check(connected != null, $"Missing connected asset after repath: {entry.DisplayName} / {name}");
            string bitmapProperty = name == "surface_normal" ? BumpMap.BumpmapBitmap : UnifiedBitmap.UnifiedbitmapBitmap;
            string? path = (connected!.FindByName(bitmapProperty) as AssetPropertyString)?.Value;
            Check(path != null && path.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase),
                $"Bitmap did not move to expected root: {entry.DisplayName} / {name} / {path}");
            Check(File.Exists(path), $"Repathed bitmap does not exist: {path}");
        }
    }

    private static void VerifyBakeContract(SubstanceMaterialEntry entry, BakeOptions options, Action<string> log)
    {
        BakeOutputPaths paths = BakeOutputPaths.For(entry, options.OutputRoot);
        BakeSidecar? sidecar = BakeSidecar.FromJson(File.ReadAllText(paths.Sidecar));
        Check(sidecar != null, "Bake sidecar must parse");
        Check(sidecar!.ContractVersion == BakeSidecar.CurrentContractVersion, "Bake contract version mismatch");
        Check(sidecar.Consumer == "Revit" && sidecar.DerivedOutput, "Bake must be marked as a Revit-derived output");
        Check(sidecar.SourceNormalConvention == entry.NormalFormat, "Source normal convention mismatch");
        Check(sidecar.OutputNormalConvention == "OpenGL", "Revit normal output must be OpenGL");
        Check(sidecar.AoBakedIntoBaseColor == entry.HasAo, "AO bake metadata mismatch");
        log($"BAKE contract={sidecar.ContractVersion} consumer={sidecar.Consumer} sourceNormal={sidecar.SourceNormalConvention} outputNormal={sidecar.OutputNormalConvention} aoInAlbedo={sidecar.AoBakedIntoBaseColor} derived={sidecar.DerivedOutput}");
    }

    private static void VerifyMaterial(Document doc, SubstanceMaterialEntry entry, Action<string> log)
    {
        var material = FindMaterial(doc, entry.DisplayName);
        Check(material.MaterialClass == entry.Category, "Material class mismatch");
        var asset = ((AppearanceAssetElement)doc.GetElement(material.AppearanceAssetId)).GetRenderingAsset();
        log($"ASSET {asset.Name} BaseSchema={(asset.FindByName("BaseSchema") as AssetPropertyString)?.Value}");
        foreach (string name in new[] { "opaque_albedo", "surface_roughness", "surface_normal" })
        {
            var connected = asset.FindByName(name)?.GetSingleConnectedAsset();
            Check(connected != null, $"Missing connected asset: {entry.DisplayName} / {name}");
            string bitmapProperty = name == "surface_normal" ? BumpMap.BumpmapBitmap : UnifiedBitmap.UnifiedbitmapBitmap;
            var path = (connected!.FindByName(bitmapProperty) as AssetPropertyString)?.Value;
            Check(File.Exists(path), $"Missing bitmap: {name} / {path}");
            var distance = connected.FindByName(UnifiedBitmap.TextureRealWorldScaleX) as AssetPropertyDistance;
            log($"MAP {name} schema={connected.Name} scale={distance?.Value} unit={distance?.GetUnitTypeId().TypeId} path={path}");
            Check(distance != null && Math.Abs(UnitUtils.ConvertToInternalUnits(distance.Value, distance.GetUnitTypeId()) - 2500 / 304.8) < 0.001, "Texture scale must be 2500 mm");
            if (name == "surface_normal")
            {
                var type = connected.FindByName(BumpMap.BumpmapType);
                int value = type is AssetPropertyInteger integer ? integer.Value : ((AssetPropertyEnum)type).Value;
                Check(value == 1, "Bump type must be NormalMap (1)");
            }
        }
        if (entry.OpacityPath != null)
            Check(asset.FindByName("surface_cutout")?.GetSingleConnectedAsset() != null, "Opacity map must be wired");
    }
}
