using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Configuration;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeFillPatternService : IPurgeFillPatternService
    {
        private readonly IPurgeDeleteElementService _purgeDeleteElementService;

        public PurgeFillPatternService(IPurgeReferenceScannerService referenceScanner, IPurgeDeleteElementService purgeDeleteElementService)
        {
            _purgeDeleteElementService = purgeDeleteElementService;
        }

        public int PurgeUnusedFillPatterns(Document doc, Action<string>? logCallback = null)
        {
            return PurgeUnusedFillPatterns(doc, PurgeContext.Create(doc), logCallback);
        }

        public int PurgeUnusedFillPatterns(Document doc, PurgeContext context, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(context);

            logCallback?.Invoke("Scanning for unused fill patterns...");

            var allPatterns = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .Where(p => !RevitConstants.IsBuiltInFillPattern(p.Name))
                .ToDictionary(p => p.Id, p => p.Name);

            var usedIds = context.UsedFillPatternIds;

            int deleted = 0;
            foreach (var kvp in allPatterns)
            {
                if (!usedIds.Contains(kvp.Key))
                {
                    if (_purgeDeleteElementService.DeleteElement(doc, kvp.Key, kvp.Value, logCallback)) deleted++;
                }
            }

            logCallback?.Invoke($"  Deleted {deleted} fill patterns.");
            return deleted;
        }
    }
}
