using System;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialCreationService : IMaterialCreationService
    {
        public ElementId GetOrCreateMaterial(Document doc, string name, Color color, Action<string>? logCallback = null)
        {
            Material? existing = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                .FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                logCallback?.Invoke($"  â„¹ Material '{name}' already exists. Updating properties...");
                ApplyMaterialProperties(doc, existing, color, logCallback);
                return existing.Id;
            }

            logCallback?.Invoke($"  âœ“ Creating material: {name}");
            ElementId newId = Material.Create(doc, name);
            Material? newMat = doc.GetElement(newId) as Material;
            if (newMat != null) ApplyMaterialProperties(doc, newMat, color, logCallback);
            return newId;
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
    }
}
