using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgeMaterialUsageCollectorService : IPurgeMaterialUsageCollectorService
    {
        private readonly IPurgeReferenceScannerService _referenceScanner;

        public PurgeMaterialUsageCollectorService(IPurgeReferenceScannerService referenceScanner)
        {
            _referenceScanner = referenceScanner;
        }

        public HashSet<ElementId> CollectUsedMaterialIds(Document doc, HashSet<ElementId> validMaterialIds)
        {
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

            return usedIds;
        }
    }
}
