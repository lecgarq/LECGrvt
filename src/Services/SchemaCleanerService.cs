using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    /// <summary>
    /// Service for cleaning third-party extensible storage schemas from Revit documents.
    /// </summary>
    public class SchemaCleanerService : ISchemaCleanerService
    {
        private readonly ISchemaVendorFilterService _schemaVendorFilterService;
        private readonly ISchemaDataStorageScanService _schemaDataStorageScanService;
        private readonly ISchemaDataStorageDeleteService _schemaDataStorageDeleteService;

        public SchemaCleanerService() : this(
            new SchemaVendorFilterService(),
            new SchemaDataStorageScanService(new SchemaVendorFilterService()),
            new SchemaDataStorageDeleteService())
        {
        }

        public SchemaCleanerService(
            ISchemaVendorFilterService schemaVendorFilterService,
            ISchemaDataStorageScanService schemaDataStorageScanService,
            ISchemaDataStorageDeleteService schemaDataStorageDeleteService)
        {
            _schemaVendorFilterService = schemaVendorFilterService;
            _schemaDataStorageScanService = schemaDataStorageScanService;
            _schemaDataStorageDeleteService = schemaDataStorageDeleteService;
        }

        /// <summary>
        /// Scan all elements in the project and collect third-party schemas.
        /// </summary>
        public HashSet<Guid> ScanForThirdPartySchemas(Document doc, Action<string>? logCallback = null)
        {
            HashSet<Guid> schemas = new HashSet<Guid>();
            int elementsWithSchemas = 0;

            // Scan all elements
            FilteredElementCollector allElements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType();

            foreach (Element elem in allElements)
            {
                try
                {
                    IList<Guid> schemaGuids = elem.GetEntitySchemaGuids();
                    if (schemaGuids.Count > 0)
                    {
                        bool hasThirdParty = false;
                        foreach (Guid guid in schemaGuids)
                        {
                            if (_schemaVendorFilterService.IsThirdPartySchema(guid))
                            {
                                schemas.Add(guid);
                                hasThirdParty = true;
                            }
                        }
                        if (hasThirdParty) elementsWithSchemas++;
                    }
                }
                catch { }
            }

            logCallback?.Invoke($"  Found {elementsWithSchemas} elements with third-party schemas.");
            return schemas;
        }

        /// <summary>
        /// Collect schemas from DataStorage elements and return their IDs for deletion.
        /// </summary>
        public (HashSet<Guid> schemas, List<ElementId> dataStorageIds) ScanDataStorageElements(Document doc, Action<string>? logCallback = null)
        {
            return _schemaDataStorageScanService.ScanDataStorageElements(doc, logCallback);
        }

        /// <summary>
        /// Delete DataStorage elements.
        /// </summary>
        public int DeleteDataStorageElements(Document doc, IEnumerable<ElementId> ids, Action<string>? logCallback = null)
        {
            return _schemaDataStorageDeleteService.DeleteDataStorageElements(doc, ids, logCallback);
        }

        /// <summary>
        /// Erase schemas and all their entities from the document.
        /// </summary>
        public int EraseSchemas(Document doc, IEnumerable<Guid> guids, Action<string>? logCallback = null)
        {
            int erased = 0;
            foreach (Guid guid in guids)
            {
                Schema? schema = Schema.Lookup(guid);
                if (schema != null)
                {
                    try
                    {
                        doc.EraseSchemaAndAllEntities(schema);
                        erased++;
                        logCallback?.Invoke($"  Erased: {schema.SchemaName}");
                    }
                    catch { }
                }
            }
            return erased;
        }

    }
}
