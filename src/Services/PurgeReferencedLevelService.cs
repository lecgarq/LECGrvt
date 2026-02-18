using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System.Collections.Generic;

namespace LECG.Services
{
    public class PurgeReferencedLevelService : IPurgeReferencedLevelService
    {
        private readonly IPurgeReferenceScannerService _referenceScanner;

        public PurgeReferencedLevelService(IPurgeReferenceScannerService referenceScanner)
        {
            _referenceScanner = referenceScanner;
        }

        public HashSet<ElementId> CollectReferencedLevelIds(Document doc, HashSet<ElementId> validLevelIds)
        {
            var referencedLevelIds = new HashSet<ElementId>();

            foreach (Element inst in new FilteredElementCollector(doc).WhereElementIsNotElementType())
            {
                _referenceScanner.CollectUsedIds(inst, validLevelIds, referencedLevelIds);
            }

            return referencedLevelIds;
        }
    }
}
