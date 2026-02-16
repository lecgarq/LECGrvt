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

        public SchemaCleanerService() : this(new SchemaVendorFilterService())
        {
        }

        public SchemaCleanerService(ISchemaVendorFilterService schemaVendorFilterService)
        {
            _schemaVendorFilterService = schemaVendorFilterService;
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
                catch { }
            }

            return (schemas, dataStorageIds.Distinct().ToList());
        }

        /// <summary>
        /// Delete DataStorage elements.
        /// </summary>
        public int DeleteDataStorageElements(Document doc, IEnumerable<ElementId> ids, Action<string>? logCallback = null)
        {
            int deleted = 0;
            foreach (ElementId id in ids)
            {
                try
                {
                    doc.Delete(id);
                    deleted++;
                }
                catch { }
            }
            return deleted;
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
