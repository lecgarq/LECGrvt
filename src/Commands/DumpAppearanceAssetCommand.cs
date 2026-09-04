using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LECG.Core;

namespace LECG.Commands
{
    /// <summary>
    /// Diagnostic: logs the appearance-asset property tree of the material on a picked face,
    /// plus the names of all library appearance assets. Used to confirm schema/property names.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class DumpAppearanceAssetCommand : RevitCommand
    {
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            ShowLogWindow("Appearance Asset Dump");

            Log("== Library appearance assets ==");
            IList<Asset> library = doc.Application.GetAssets(AssetType.Appearance);
            foreach (var group in library.GroupBy(a => a.Name).OrderBy(g => g.Key))
            {
                string schema = (group.First().FindByName("BaseSchema") as AssetPropertyString)?.Value ?? "?";
                Log($"  {group.Key}  x{group.Count()}  BaseSchema={schema}");
            }

            Reference? picked;
            try
            {
                picked = uiDoc.Selection.PickObject(ObjectType.Face, "Pick a face to dump its material appearance");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                Log("Pick cancelled.");
                return;
            }

            Element? element = doc.GetElement(picked);
            if (element?.GetGeometryObjectFromReference(picked) is not Face face)
            {
                Log("Not a face.");
                return;
            }

            Material? material = doc.GetElement(face.MaterialElementId) as Material;
            if (material == null)
            {
                Log("Face has no material.");
                return;
            }

            Log($"== Material '{material.Name}'  class='{material.MaterialClass}' category='{material.MaterialCategory}' ==");
            if (doc.GetElement(material.AppearanceAssetId) is not AppearanceAssetElement aae)
            {
                Log("No appearance asset.");
                return;
            }

            Asset asset = aae.GetRenderingAsset();
            Log($"Appearance asset '{aae.Name}'  Asset.Name='{asset.Name}'  BaseSchema='{(asset.FindByName("BaseSchema") as AssetPropertyString)?.Value}'");
            Dump(asset, 1);
        }

        private void Dump(Asset asset, int depth)
        {
            string pad = new string(' ', depth * 2);
            for (int i = 0; i < asset.Size; i++)
            {
                AssetProperty p = asset[i];
                string value = Describe(p);
                Log($"{pad}{p.Name}  [{p.Type}]  {value}");

                Asset? connected = p.GetSingleConnectedAsset();
                if (connected != null)
                {
                    Log($"{pad}  -> connected asset '{connected.Name}'  BaseSchema='{(connected.FindByName("BaseSchema") as AssetPropertyString)?.Value}'");
                    Dump(connected, depth + 2);
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
                AssetPropertyDistance dist => $"{dist.Value:0.####} ft",
                AssetPropertyDoubleArray4d c => string.Join(",", c.GetValueAsDoubles().Select(v => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))),
                _ => string.Empty,
            };
        }
    }
}
