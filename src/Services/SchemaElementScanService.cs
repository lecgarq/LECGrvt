using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class SchemaElementScanService : ISchemaElementScanService
    {
        private readonly ISchemaVendorFilterService _schemaVendorFilterService;

        public SchemaElementScanService(ISchemaVendorFilterService schemaVendorFilterService)
        {
            _schemaVendorFilterService = schemaVendorFilterService;
        }

        public HashSet<Guid> ScanForThirdPartySchemas(Document doc, Action<string>? logCallback = null)
        {
            HashSet<Guid> schemas = new HashSet<Guid>();
            int elementsWithSchemas = 0;

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

                        if (hasThirdParty)
                        {
                            elementsWithSchemas++;
                        }
                    }
                }
                catch
                {
                }
            }

            logCallback?.Invoke($"  Found {elementsWithSchemas} elements with third-party schemas.");
            return schemas;
        }
    }
}
