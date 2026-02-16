using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RenderAppearanceService : IRenderAppearanceService
    {
        public void SyncWithRenderAppearance(Document doc, Material mat, Action<string>? logCallback = null)
        {
            if (mat == null) return;
            try { mat.UseRenderAppearanceForShading = true; } catch { }
            doc.Regenerate();
            Color renderColor = mat.Color;
            ApplyMaterialProperties(doc, mat, renderColor, null);
            logCallback?.Invoke($"  âœ“ Synced graphics for: {mat.Name}");
        }

        public void BatchSyncWithRenderAppearance(Document doc, IEnumerable<Material> materials, Action<string>? logCallback = null, Action<double, string>? progressCallback = null)
        {
            var matsList = materials.ToList();
            if (!matsList.Any()) return;

            int total = matsList.Count;
            int processed = 0;
            int skipped = 0;
            int updated = 0;

            logCallback?.Invoke($"Analyzing {total} materials...");
            progressCallback?.Invoke(0, "Analyzing materials...");

            using (Transaction t = new Transaction(doc, "Sync Render Appearance"))
            {
                t.Start();

                List<Material> materialsToToggle = new List<Material>();

                foreach (var mat in matsList)
                {
                    if (mat.UseRenderAppearanceForShading)
                    {
                        mat.UseRenderAppearanceForShading = false;
                        materialsToToggle.Add(mat);
                    }
                    else
                    {
                        materialsToToggle.Add(mat);
                    }
                }

                if (materialsToToggle.Count > 0)
                {
                    doc.Regenerate();

                    logCallback?.Invoke($"Forcing Render Appearance update on {materialsToToggle.Count} materials...");
                    foreach (var mat in materialsToToggle)
                    {
                        mat.UseRenderAppearanceForShading = true;
                    }

                    doc.Regenerate();
                }

                ElementId solidId = GetSolidFillPatternId(doc);

                foreach (Material mat in matsList)
                {
                    processed++;
                    if (processed % 10 == 0)
                    {
                        progressCallback?.Invoke((double)processed / total * 100, $"Processing: {mat.Name}");
                    }

                    Color renderColor = mat.Color;

                    if (IsMaterialSynced(mat, renderColor, solidId))
                    {
                        skipped++;
                        continue;
                    }

                    mat.SurfaceForegroundPatternId = solidId;
                    mat.SurfaceForegroundPatternColor = renderColor;
                    mat.SurfaceBackgroundPatternId = solidId;
                    mat.SurfaceBackgroundPatternColor = renderColor;
                    mat.CutForegroundPatternId = solidId;
                    mat.CutForegroundPatternColor = renderColor;
                    mat.CutBackgroundPatternId = solidId;
                    mat.CutBackgroundPatternColor = renderColor;

                    updated++;
                }

                t.Commit();
            }

            logCallback?.Invoke($"Sync Complete: {updated} updated, {skipped} skipped.");
            progressCallback?.Invoke(100, "Done");
        }

        private ElementId GetSolidFillPatternId(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement));
            foreach (FillPatternElement fpe in collector.Cast<FillPatternElement>())
            {
                FillPattern fp = fpe.GetFillPattern();
                if (fp != null && fp.IsSolidFill) return fpe.Id;
            }
            FillPattern solidPattern = new FillPattern("Solid Fill", FillPatternTarget.Drafting, FillPatternHostOrientation.ToHost);
            return FillPatternElement.Create(doc, solidPattern).Id;
        }

        private void ApplyMaterialProperties(Document doc, Material mat, Color color, Action<string>? logCallback)
        {
            ElementId solidId = GetSolidFillPatternId(doc);
            mat.Color = color;
            mat.SurfaceForegroundPatternId = solidId; mat.SurfaceForegroundPatternColor = color;
            mat.SurfaceBackgroundPatternId = solidId; mat.SurfaceBackgroundPatternColor = color;
            mat.CutForegroundPatternId = solidId; mat.CutForegroundPatternColor = color;
            mat.CutBackgroundPatternId = solidId; mat.CutBackgroundPatternColor = color;
            logCallback?.Invoke($"    â†’ Color: RGB({color.Red}, {color.Green}, {color.Blue})");
        }

        private bool IsMaterialSynced(Material mat, Color targetColor, ElementId solidId)
        {
            if (!ColorsEqual(mat.Color, targetColor)) return false;

            if (mat.SurfaceForegroundPatternId != solidId) return false;
            if (!ColorsEqual(mat.SurfaceForegroundPatternColor, targetColor)) return false;

            if (mat.SurfaceBackgroundPatternId != solidId) return false;
            if (!ColorsEqual(mat.SurfaceBackgroundPatternColor, targetColor)) return false;

            if (mat.CutForegroundPatternId != solidId) return false;
            if (!ColorsEqual(mat.CutForegroundPatternColor, targetColor)) return false;

            if (mat.CutBackgroundPatternId != solidId) return false;
            if (!ColorsEqual(mat.CutBackgroundPatternColor, targetColor)) return false;

            return true;
        }

        private bool ColorsEqual(Color? c1, Color? c2)
        {
            if (c1 == null || c2 == null) return false;
            return c1.Red == c2.Red && c1.Green == c2.Green && c1.Blue == c2.Blue;
        }
    }
}
