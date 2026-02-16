using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Configuration;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeMaterialService : IPurgeMaterialService
    {
        private readonly IPurgeReferenceScannerService _referenceScanner;

        public PurgeMaterialService() : this(new PurgeReferenceScannerService())
        {
        }

        public PurgeMaterialService(IPurgeReferenceScannerService referenceScanner)
        {
            _referenceScanner = referenceScanner;
        }

        public int PurgeUnusedMaterials(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning for unused materials...");

            var allMaterials = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .Where(m => !RevitConstants.IsBuiltInMaterial(m.Name))
                .ToDictionary(m => m.Id, m => m.Name);

            var validMaterialIds = new HashSet<ElementId>(allMaterials.Keys);
            var usedIds = new HashSet<ElementId>();

            var safeClassesForParams = new List<Type>
            {
                typeof(HostObject),
                typeof(FamilyInstance),
                typeof(FamilySymbol)
            };
            var safeClassFilter = new ElementMulticlassFilter(safeClassesForParams);

            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent();

            var typesCollector = new FilteredElementCollector(doc)
                .WhereElementIsElementType();

            void ProcessElement(Element e)
            {
                if (!e.IsValidObject) return;

                try
                {
                    var mats = e.GetMaterialIds(false);
                    foreach (var id in mats) usedIds.Add(id);
                }
                catch
                {
                }

                try
                {
                    if (safeClassFilter.PassesFilter(e))
                    {
                        _referenceScanner.CollectUsedIds(e, validMaterialIds, usedIds);
                    }
                }
                catch
                {
                }
            }

            foreach (var e in typesCollector) ProcessElement(e);
            foreach (var e in collector) ProcessElement(e);

            int deleted = 0;
            foreach (var kvp in allMaterials)
            {
                if (!usedIds.Contains(kvp.Key))
                {
                    if (DeleteElement(doc, kvp.Key, kvp.Value, logCallback)) deleted++;
                }
            }

            logCallback?.Invoke($"  Deleted {deleted} materials.");
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
