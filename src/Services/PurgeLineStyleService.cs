using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Configuration;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeLineStyleService : IPurgeLineStyleService
    {
        public int PurgeUnusedLineStyles(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning for unused line styles...");

            Category? linesCategory = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            if (linesCategory == null) return 0;

            var allStyles = new Dictionary<ElementId, string>();
            foreach (Category subCat in linesCategory.SubCategories)
            {
                if (!RevitConstants.IsBuiltInLineStyle(subCat.Name))
                {
                    allStyles[subCat.Id] = subCat.Name;
                }
            }
            logCallback?.Invoke($"  Found {allStyles.Count} potential candidates.");

            var usedIds = new HashSet<ElementId>();
            var curves = new FilteredElementCollector(doc).OfClass(typeof(CurveElement));

            foreach (CurveElement curve in curves)
            {
                if (curve.LineStyle is GraphicsStyle gs && gs.GraphicsStyleCategory != null)
                {
                    usedIds.Add(gs.GraphicsStyleCategory.Id);
                }
            }

            int deleted = 0;
            foreach (var kvp in allStyles)
            {
                if (!usedIds.Contains(kvp.Key))
                {
                    if (DeleteElement(doc, kvp.Key, kvp.Value, logCallback)) deleted++;
                }
            }

            logCallback?.Invoke($"  Deleted {deleted} line styles.");
            return deleted;
        }

        private bool DeleteElement(Document doc, ElementId id, string name, Action<string>? logCallback)
        {
            try
            {
                doc.Delete(id);
                logCallback?.Invoke($"  Deleted: {name}");
                return true;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"  Could not delete '{name}': {ex.Message}");
                return false;
            }
        }
    }
}
