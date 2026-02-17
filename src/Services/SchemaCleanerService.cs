using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    /// <summary>
    /// Service for cleaning third-party extensible storage schemas from Revit documents.
    /// </summary>
    public class SchemaCleanerService : ISchemaCleanerService
    {
        private readonly ISchemaVendorFilterService _schemaVendorFilterService;
        private readonly ISchemaElementScanService _schemaElementScanService;
        private readonly ISchemaDataStorageScanService _schemaDataStorageScanService;
        private readonly ISchemaDataStorageDeleteService _schemaDataStorageDeleteService;
        private readonly ISchemaEraseService _schemaEraseService;

        public SchemaCleanerService() : this(
            new SchemaVendorFilterService(),
            new SchemaElementScanService(new SchemaVendorFilterService()),
            new SchemaDataStorageScanService(new SchemaVendorFilterService()),
            new SchemaDataStorageDeleteService(),
            new SchemaEraseService())
        {
        }

        public SchemaCleanerService(
            ISchemaVendorFilterService schemaVendorFilterService,
            ISchemaElementScanService schemaElementScanService,
            ISchemaDataStorageScanService schemaDataStorageScanService,
            ISchemaDataStorageDeleteService schemaDataStorageDeleteService,
            ISchemaEraseService schemaEraseService)
        {
            _schemaVendorFilterService = schemaVendorFilterService;
            _schemaElementScanService = schemaElementScanService;
            _schemaDataStorageScanService = schemaDataStorageScanService;
            _schemaDataStorageDeleteService = schemaDataStorageDeleteService;
            _schemaEraseService = schemaEraseService;
        }

        /// <summary>
        /// Scan all elements in the project and collect third-party schemas.
        /// </summary>
        public HashSet<Guid> ScanForThirdPartySchemas(Document doc, Action<string>? logCallback = null)
        {
            return _schemaElementScanService.ScanForThirdPartySchemas(doc, logCallback);
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
            return _schemaEraseService.EraseSchemas(doc, guids, logCallback);
        }

    }
}
