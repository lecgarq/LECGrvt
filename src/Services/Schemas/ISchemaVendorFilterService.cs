using System;

namespace LECG.Services.Interfaces
{
    public interface ISchemaVendorFilterService
    {
        bool IsThirdPartySchema(Guid guid);
    }
}
