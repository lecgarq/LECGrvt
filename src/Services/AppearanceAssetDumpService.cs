using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace LECG.Services
{
    public static class AppearanceAssetDumpService
    {
        private const int MaxDepth = 32;

        public static void Run(UIDocument uiDoc, Document doc, Action<string> log)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(log);

            log("== Library appearance assets ==");
            IList<Asset> library = doc.Application.GetAssets(AssetType.Appearance);
            foreach (var group in library.GroupBy(a => a.Name).OrderBy(g => g.Key))
            {
                string schema = (group.First().FindByName("BaseSchema") as AssetPropertyString)?.Value ?? "?";
                log($"  {group.Key}  x{group.Count()}  BaseSchema={schema}");
            }

            Reference? picked;
            try
            {
                picked = uiDoc.Selection.PickObject(ObjectType.Face, "Pick a face to dump its material appearance");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                log("Pick cancelled.");
                return;
            }

            DumpFace(doc, picked, log);
        }

        public static void DumpFace(Document doc, Reference picked, Action<string> log)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(picked);
            ArgumentNullException.ThrowIfNull(log);
            Element? element = doc.GetElement(picked);
            if (element?.GetGeometryObjectFromReference(picked) is not Face face)
            {
                log("Not a face.");
                return;
            }

            ElementId materialId = doc.IsPainted(element.Id, face)
                ? doc.GetPaintedMaterial(element.Id, face)
                : face.MaterialElementId;
            Material? material = doc.GetElement(materialId) as Material;
            if (material == null)
            {
                log("Face has no material.");
                return;
            }

            log($"== Material '{material.Name}'  class='{material.MaterialClass}' category='{material.MaterialCategory}' ==");
            if (doc.GetElement(material.AppearanceAssetId) is not AppearanceAssetElement aae)
            {
                log("No appearance asset.");
                return;
            }

            Asset asset = aae.GetRenderingAsset();
            log($"Appearance asset '{aae.Name}'  Asset.Name='{asset.Name}'  BaseSchema='{(asset.FindByName("BaseSchema") as AssetPropertyString)?.Value}'");
            Dump(asset, log, 1);
        }

        private static void Dump(Asset asset, Action<string> log, int depth)
        {
            if (depth > MaxDepth) { log("  [maximum asset depth reached]"); return; }
            string pad = new string(' ', depth * 2);
            for (int i = 0; i < asset.Size; i++)
            {
                AssetProperty p = asset[i];
                string value = Describe(p);
                log($"{pad}{p.Name}  [{p.Type}]  {value}");

                for (int j = 0; j < p.NumberOfConnectedProperties; j++)
                {
                    if (p.GetConnectedProperty(j) is not Asset connected) continue;
                    log($"{pad}  -> connected asset '{connected.Name}'  BaseSchema='{(connected.FindByName("BaseSchema") as AssetPropertyString)?.Value}'");
                    Dump(connected, log, depth + 1);
                }
            }
        }

        private static string Describe(AssetProperty p)
        {
            return p switch
            {
                AssetPropertyString s => $"\"{s.Value}\"",
                AssetPropertyBoolean b => b.Value.ToString(),
                AssetPropertyInteger n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                AssetPropertyEnum e => e.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                AssetPropertyDouble d => d.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
                AssetPropertyFloat f => f.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
                AssetPropertyDistance dist => $"{dist.Value:0.####} {dist.GetUnitTypeId().TypeId}",
                AssetPropertyDoubleArray4d c => string.Join(",", c.GetValueAsDoubles().Select(v => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))),
                _ => string.Empty,
            };
        }
    }
}
