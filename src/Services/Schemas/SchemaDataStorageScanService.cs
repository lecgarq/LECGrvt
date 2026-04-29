using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class SchemaDataStorageScanService : ISchemaDataStorageScanService
    {
        private readonly ISchemaVendorFilterService _schemaVendorFilterService;

        public SchemaDataStorageScanService(ISchemaVendorFilterService schemaVendorFilterService)
        {
            _schemaVendorFilterService = schemaVendorFilterService;
        }

        public (HashSet<Guid> schemas, List<ElementId> dataStorageIds) ScanDataStorageElements(Document doc, Action<string>? logCallback = null)
        {
            HashSet<Guid> schemas = new HashSet<Guid>();
            List<ElementId> dataStorageIds = new List<ElementId>();

            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(DataStorage));

            foreach (DataStorage ds in collector.ToElements().Cast<DataStorage>())
            {
                try
                {
                    IList<Guid> dsSchemaGuids = ds.GetEntitySchemaGuids();
                    foreach (Guid guid in dsSchemaGuids)
                    {
                        if (_schemaVendorFilterService.IsThirdPartySchema(guid))
                        {
                            Schema? schema = Schema.Lookup(guid);
                            if (schema != null)
                            {
                                schemas.Add(guid);
                                dataStorageIds.Add(ds.Id);
                                logCallback?.Invoke($"  Found DataStorage {ds.Id}: '{schema.SchemaName}'");
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            return (schemas, dataStorageIds.Distinct().ToList());
        }
    }
}
