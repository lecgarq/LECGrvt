#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialService : IMaterialService
    {
        private readonly IRenderAppearanceService _renderAppearanceService;
        private readonly IMaterialTypeAssignmentService _materialTypeAssignmentService;
        private readonly IMaterialCreationService _materialCreationService;
        private readonly IMaterialColorSequenceService _materialColorSequenceService;

        public MaterialService() : this(new RenderAppearanceService(), new MaterialTypeAssignmentService(), new MaterialCreationService(), new MaterialColorSequenceService()) { }

        public MaterialService(IRenderAppearanceService renderAppearanceService, IMaterialTypeAssignmentService materialTypeAssignmentService, IMaterialCreationService materialCreationService, IMaterialColorSequenceService materialColorSequenceService)
        {
            _renderAppearanceService = renderAppearanceService;
            _materialTypeAssignmentService = materialTypeAssignmentService;
            _materialCreationService = materialCreationService;
            _materialColorSequenceService = materialColorSequenceService;
        }

        public Color GetNextColor()
        {
            return _materialColorSequenceService.GetNextColor();
        }

        public ElementId GetOrCreateMaterial(Document doc, string name, Color color, Action<string>? logCallback = null)
        {
            return _materialCreationService.GetOrCreateMaterial(doc, name, color, logCallback);
        }

        public bool AssignMaterialToType(Document doc, ElementType type, ElementId materialId, Action<string>? logCallback = null)
        {
            return _materialTypeAssignmentService.AssignMaterialToType(doc, type, materialId, logCallback);
        }

        public void SyncWithRenderAppearance(Document doc, Material mat, Action<string>? logCallback = null)
        {
            _renderAppearanceService.SyncWithRenderAppearance(doc, mat, logCallback);
        }

        public void BatchSyncWithRenderAppearance(Document doc, IEnumerable<Material> materials, Action<string>? logCallback = null, Action<double, string>? progressCallback = null)
        {
            _renderAppearanceService.BatchSyncWithRenderAppearance(doc, materials, logCallback, progressCallback);
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
