using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class FamilyConversionNamingService : IFamilyConversionNamingService
    {
        public string ResolveTargetFamilyName(string sourceFamilyName, string customName)
        {
            return string.IsNullOrWhiteSpace(customName) ? $"{sourceFamilyName}_Converted" : customName;
        }
    }
}
