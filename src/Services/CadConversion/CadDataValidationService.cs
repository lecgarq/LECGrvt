using System;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadDataValidationService
    {
        public void EnsureHasGeometry(CadData data, string emptyDataMessage)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(emptyDataMessage);

            if (!data.Curves.Any() && !data.Hatches.Any())
            {
                throw new InvalidOperationException(emptyDataMessage);
            }
        }
    }
}
