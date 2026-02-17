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
        private readonly IPurgeReferenceScannerService _referenceScanner;
        private readonly IPurgeDeleteElementService _purgeDeleteElementService;

        public PurgeFillPatternService() : this(new PurgeReferenceScannerService(), new PurgeDeleteElementService())
        {
        }

        public PurgeFillPatternService(IPurgeReferenceScannerService referenceScanner, IPurgeDeleteElementService purgeDeleteElementService)
        {
            _referenceScanner = referenceScanner;
            _purgeDeleteElementService = purgeDeleteElementService;
        }

        public int PurgeUnusedFillPatterns(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning for unused fill patterns...");

            var allPatterns = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .Where(p => !RevitConstants.IsBuiltInFillPattern(p.Name))
                .ToDictionary(p => p.Id, p => p.Name);

            var usedIds = new HashSet<ElementId>();
            foreach (Material mat in new FilteredElementCollector(doc).OfClass(typeof(Material)))
            {
                _referenceScanner.AddIfValid(usedIds, mat.SurfaceForegroundPatternId);
                _referenceScanner.AddIfValid(usedIds, mat.SurfaceBackgroundPatternId);
                _referenceScanner.AddIfValid(usedIds, mat.CutForegroundPatternId);
                _referenceScanner.AddIfValid(usedIds, mat.CutBackgroundPatternId);
            }

            foreach (FilledRegionType frt in new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)))
            {
                _referenceScanner.AddIfValid(usedIds, frt.ForegroundPatternId);
                _referenceScanner.AddIfValid(usedIds, frt.BackgroundPatternId);
            }

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
