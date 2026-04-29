using System;
using Autodesk.Revit.DB.ExtensibleStorage;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class SchemaVendorFilterService : ISchemaVendorFilterService
    {
        public bool IsThirdPartySchema(Guid guid)
        {
            Schema? schema = Schema.Lookup(guid);
            if (schema == null) return false;

            string vendorId = schema.VendorId ?? "";

            // Protect Enscape
            if (schema.SchemaName.IndexOf("Enscape", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (vendorId.IndexOf("Enscape", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            return string.IsNullOrEmpty(vendorId) || vendorId.ToLower() != "adsk";
        }
    }
}
