#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;

namespace LECG.Services
{
    public class MaterialService : IMaterialService
    {
        private int _colorIndex = 0;
        private static readonly Color[] ColorPalette = new Color[]
        {
            new Color(76, 175, 80), new Color(33, 150, 243), new Color(255, 193, 7), new Color(244, 67, 54),
            new Color(156, 39, 176), new Color(0, 188, 212), new Color(255, 87, 34), new Color(139, 195, 74),
            new Color(63, 81, 181), new Color(121, 85, 72), new Color(96, 125, 139), new Color(233, 30, 99),
        };

        public MaterialService() { }

        public Color GetNextColor()
        {
            Color color = ColorPalette[_colorIndex % ColorPalette.Length];
            _colorIndex++;
            return color;
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

        public ElementId GetOrCreateMaterial(Document doc, string name, Color color, Action<string>? logCallback = null)
        {
            Material? existing = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                .FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                logCallback?.Invoke($"  ℹ Material '{name}' already exists. Updating properties...");
                ApplyMaterialProperties(doc, existing, color, logCallback);
                return existing.Id;
            }

            logCallback?.Invoke($"  ✓ Creating material: {name}");
            ElementId newId = Material.Create(doc, name);
            Material? newMat = doc.GetElement(newId) as Material;
            if (newMat != null) ApplyMaterialProperties(doc, newMat, color, logCallback);
            return newId;
        }

        private void ApplyMaterialProperties(Document doc, Material mat, Color color, Action<string>? logCallback)
        {
            ElementId solidId = GetSolidFillPatternId(doc);
            mat.Color = color;
            mat.SurfaceForegroundPatternId = solidId; mat.SurfaceForegroundPatternColor = color;
            mat.SurfaceBackgroundPatternId = solidId; mat.SurfaceBackgroundPatternColor = color;
            mat.CutForegroundPatternId = solidId; mat.CutForegroundPatternColor = color;
            mat.CutBackgroundPatternId = solidId; mat.CutBackgroundPatternColor = color;
            logCallback?.Invoke($"    → Color: RGB({color.Red}, {color.Green}, {color.Blue})");
        }

        public bool AssignMaterialToType(Document doc, ElementType type, ElementId materialId, Action<string>? logCallback = null)
        {
            if (type is HostObjAttributes hostType)
            {
                CompoundStructure? cs = hostType.GetCompoundStructure();
                if (cs == null) { logCallback?.Invoke($"  ⚠ No compound structure on: {type.Name}"); return false; }
                IList<CompoundStructureLayer> layers = cs.GetLayers();
                for (int i = 0; i < layers.Count; i++) cs.SetMaterialId(i, materialId);
                hostType.SetCompoundStructure(cs);
                logCallback?.Invoke($"  ✓ Assigned material to layers in: {type.Name}");
                return true;
            }
            Parameter? structMatParam = type.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            if (structMatParam != null && !structMatParam.IsReadOnly)
            {
                structMatParam.Set(materialId);
                logCallback?.Invoke($"  ✓ Assigned to: {type.Name} (Structural Material Param)");
                return true;
            }
            logCallback?.Invoke($"  ⚠ Could not assign material to: {type.Name}");
            return false;
        }

        public void SyncWithRenderAppearance(Document doc, Material mat, Action<string>? logCallback = null)
        {
            if (mat == null) return;
            try { mat.UseRenderAppearanceForShading = true; } catch { }
            doc.Regenerate();
            Color renderColor = mat.Color;
            ApplyMaterialProperties(doc, mat, renderColor, null);
            logCallback?.Invoke($"  ✓ Synced graphics for: {mat.Name}");
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

            // Single Transaction for the entire operation
            using (Transaction t = new Transaction(doc, "Sync Render Appearance"))
            {
                t.Start();

                // Phase 1: Force Update Render Appearance Logic
                // To ensure the color is updated from the Appearance Asset, we must toggle the property:
                // 1. Set to FALSE (if true)
                // 2. Regenerate
                // 3. Set to TRUE
                // 4. Regenerate

                List<Material> materialsToToggle = new List<Material>();
                
                foreach (var mat in matsList)
                {
                    // If it's already true, we need to toggle it off then on to force update
                    if (mat.UseRenderAppearanceForShading)
                    {
                        mat.UseRenderAppearanceForShading = false;
                        materialsToToggle.Add(mat);
                    }
                    else
                    {
                        // If it's false, we just need to turn it on (added to list to turn on later)
                        materialsToToggle.Add(mat);
                    }
                }

                if (materialsToToggle.Count > 0)
                {
                    // Step 1: Ensure all are OFF (for those that were on)
                    // (They are already set to false in the loop above if they were true)
                    doc.Regenerate(); 

                    // Step 2: Set all to ON
                    logCallback?.Invoke($"Forcing Render Appearance update on {materialsToToggle.Count} materials...");
                    foreach(var mat in materialsToToggle)
                    {
                        mat.UseRenderAppearanceForShading = true;
                    }

                    // Step 3: Regenerate to calculate new colors
                    doc.Regenerate();
                }

                // Phase 2: Sync Graphics
                // Get or create Solid Fill (Safe inside transaction)
                ElementId solidId = GetSolidFillPatternId(doc);

                foreach (Material mat in matsList)
                {
                    processed++;
                    if (processed % 10 == 0) 
                        progressCallback?.Invoke((double)processed / total * 100, $"Processing: {mat.Name}");

                    Color renderColor = mat.Color;

                    if (IsMaterialSynced(mat, renderColor, solidId))
                    {
                        skipped++;
                        continue;
                    }
                    
                    mat.SurfaceForegroundPatternId = solidId; mat.SurfaceForegroundPatternColor = renderColor;
                    mat.SurfaceBackgroundPatternId = solidId; mat.SurfaceBackgroundPatternColor = renderColor;
                    mat.CutForegroundPatternId = solidId; mat.CutForegroundPatternColor = renderColor;
                    mat.CutBackgroundPatternId = solidId; mat.CutBackgroundPatternColor = renderColor;
                    
                    updated++;
                }

                t.Commit();
            }
            
            logCallback?.Invoke($"Sync Complete: {updated} updated, {skipped} skipped.");
            progressCallback?.Invoke(100, "Done");
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

        public ElementId CreatePBRMaterial(Document doc, string name, string folderPath, Action<string>? logCallback = null)
        {
            ElementId matId = ElementId.InvalidElementId;
            using (Transaction t = new Transaction(doc, "Create Material"))
            {
                t.Start();
                matId = GetOrCreateMaterial(doc, name, GetNextColor(), logCallback);
                t.Commit();
            }
            Material? mat = doc.GetElement(matId) as Material;
            if (mat == null) return matId;

            string? diffusePath = FindTextureFile(folderPath, "_Color");
            string? normalPath = FindTextureFile(folderPath, "Normal GL");
            string? roughPath = FindTextureFile(folderPath, "roughness");
            
            if (!string.IsNullOrEmpty(diffusePath)) logCallback?.Invoke($"  ✓ Found diffuse: {System.IO.Path.GetFileName(diffusePath)}");
            else logCallback?.Invoke($"  ⚠ No diffuse texture found in {folderPath}");

            using (AppearanceAssetEditScope editScope = new AppearanceAssetEditScope(doc))
            {
                ElementId assetId = mat.AppearanceAssetId;
                if (assetId == ElementId.InvalidElementId)
                {
                    try 
                    {
                         var assetLib = doc.Application.GetAssets(AssetType.Appearance);
                         var template = assetLib.FirstOrDefault(a => a.Name == "Generic") ?? assetLib.FirstOrDefault();
                         if (template != null) 
                         {
                             // Fix: Access .Id property as Create returns the Element
                             assetId = AppearanceAssetElement.Create(doc, name, template).Id;
                         }
                    }
                    catch { logCallback?.Invoke("  Failed to create base appearance asset."); return matId; }
                }

                if (assetId != ElementId.InvalidElementId)
                {
                    Asset editableAsset = editScope.Start(assetId);
                    
                    if (!string.IsNullOrEmpty(diffusePath)) SetupBitmapProperty(editableAsset.FindByName("generic_diffuse"), diffusePath!);
                    if (!string.IsNullOrEmpty(normalPath)) SetupBitmapProperty(editableAsset.FindByName("generic_bump_map"), normalPath!);
                    if (!string.IsNullOrEmpty(roughPath)) SetupBitmapProperty(editableAsset.FindByName("generic_reflectivity_at_0deg"), roughPath!);
                    
                    editScope.Commit(true);

                    if (mat.AppearanceAssetId == ElementId.InvalidElementId)
                    {
                        using (Transaction t = new Transaction(doc, "Assign Appearance"))
                        {
                            t.Start();
                            mat.AppearanceAssetId = assetId;
                            t.Commit();
                        }
                    }
                }
            }
            return matId;
        }

        private string? FindTextureFile(string folder, string partialName)
        {
            if (!System.IO.Directory.Exists(folder)) return null;
            return System.IO.Directory.GetFiles(folder).FirstOrDefault(f => System.IO.Path.GetFileName(f).IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void SetupBitmapProperty(AssetProperty? prop, string path)
        {
             if (prop == null) return;
            Asset? connectedAsset = null;

            if (prop.GetSingleConnectedAsset() != null)
            {
                connectedAsset = prop.GetSingleConnectedAsset();
            }
            else
            {
                try 
                {
                    System.Reflection.MethodInfo? minfo = prop.GetType().GetMethod("AddConnectedAsset", new Type[] { typeof(string) });
                    if (minfo != null)
                    {
                        connectedAsset = minfo.Invoke(prop, new object[] { "UnifiedBitmapSchema" }) as Asset;
                    }
                }
                catch { }
            }

            if (connectedAsset != null)
            {
                AssetPropertyString? pathProp = connectedAsset.FindByName("unifiedbitmap_Bitmap") as AssetPropertyString;
                if (pathProp != null) pathProp.Value = path;
                double scale = 304.8;
                SetAssetDouble(connectedAsset, "texture_RealWorldScaleX", scale);
                SetAssetDouble(connectedAsset, "texture_RealWorldScaleY", scale);
            }
        }

        private void SetAssetDouble(Asset asset, string propName, double value)
        {
            AssetPropertyDouble? prop = asset.FindByName(propName) as AssetPropertyDouble;
            if (prop != null) prop.Value = value;
        }
        public void AssignMaterialsToElements(Document doc, IList<Element> elements, Action<string>? logCallback, Action<double, string>? progressCallback)
        {
            if (elements == null || !elements.Any()) return;

            logCallback?.Invoke("ANALYZING SELECTION");
            progressCallback?.Invoke(10, "Grouping by type...");

            Dictionary<ElementId, List<Element>> elementsByType = new Dictionary<ElementId, List<Element>>();

            foreach (Element el in elements)
            {
                ElementId typeId = el.GetTypeId();
                if (typeId == ElementId.InvalidElementId) continue;

                if (!elementsByType.ContainsKey(typeId))
                {
                    elementsByType[typeId] = new List<Element>();
                }
                elementsByType[typeId].Add(el);
            }

            logCallback?.Invoke($"  Found {elementsByType.Count} unique types from {elements.Count} elements.");

            // Process each Type
            logCallback?.Invoke("");
            logCallback?.Invoke("CREATING/UPDATING MATERIALS");

            int processedTypes = 0;
            int totalTypes = elementsByType.Count;

            using (Transaction t = new Transaction(doc, "Assign Material by Type"))
            {
                t.Start();

                foreach (var kvp in elementsByType)
                {
                    processedTypes++;
                    double pct = 20 + (processedTypes * 70.0 / totalTypes);
                    
                    ElementType? elemType = doc.GetElement(kvp.Key) as ElementType;
                    if (elemType == null) continue;

                    string typeName = elemType.Name;
                    
                    // CHECK FOR MULTIPLE LAYERS
                    if (elemType is HostObjAttributes hostType)
                    {
                        var cs = hostType.GetCompoundStructure();
                        if (cs != null && cs.GetLayers().Count > 1)
                        {
                            logCallback?.Invoke($"  SKIP: {typeName} has {cs.GetLayers().Count} layers. Cannot assign single material.");
                            continue;
                        }
                    }

                    progressCallback?.Invoke(pct, $"Processing: {typeName}");
                    logCallback?.Invoke($"");
                    logCallback?.Invoke($"TYPE: {typeName} ({kvp.Value.Count} elements)");

                    // Get a color for this type
                    Color color = GetNextColor();

                    // Get or create material
                    ElementId materialId = GetOrCreateMaterial(doc, typeName, color, logCallback);

                    // Assign to type
                    AssignMaterialToType(doc, elemType, materialId, logCallback);
                }

                t.Commit();
            }

            logCallback?.Invoke("");
            logCallback?.Invoke("COMPLETE");
            logCallback?.Invoke($"Processed {processedTypes} types.");
            progressCallback?.Invoke(100, "Done");
        }
    }
}
