using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Configuration;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeLineStyleService : IPurgeLineStyleService
    {
        private readonly IPurgeDeleteElementService _purgeDeleteElementService;

        public PurgeLineStyleService(IPurgeDeleteElementService purgeDeleteElementService)
        {
            _purgeDeleteElementService = purgeDeleteElementService;
        }

        public int PurgeUnusedLineStyles(Document doc, Action<string>? logCallback = null)
        {
            return PurgeUnusedLineStyles(doc, PurgeContext.Create(doc), logCallback);
        }

        public int PurgeUnusedLineStyles(Document doc, PurgeContext context, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(context);

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

            var validIds = new HashSet<ElementId>(allStyles.Keys);
            var usedIds = new HashSet<ElementId>(context.UsedLineStyleIds);
            foreach (ElementId referencedId in context.ParameterReferencedIds)
            {
                if (validIds.Contains(referencedId))
                {
                    usedIds.Add(referencedId);
                }
            }

            int deleted = 0;
            foreach (var kvp in allStyles)
            {
                if (!usedIds.Contains(kvp.Key))
                {
                    if (_purgeDeleteElementService.DeleteElement(doc, kvp.Key, kvp.Value, logCallback)) deleted++;
                }
            }

            logCallback?.Invoke($"  Deleted {deleted} line styles.");
            return deleted;
        }
    }
}
