using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services
{
    public interface ISchemaCleanerService
    {
        HashSet<Guid> ScanForThirdPartySchemas(Document doc, Action<string>? logCallback = null);
        (HashSet<Guid> schemas, List<ElementId> dataStorageIds) ScanDataStorageElements(Document doc, Action<string>? logCallback = null);
        int DeleteDataStorageElements(Document doc, IEnumerable<ElementId> ids, Action<string>? logCallback = null);
        int EraseSchemas(Document doc, IEnumerable<Guid> guids, Action<string>? logCallback = null);
    }
}
