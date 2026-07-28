using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class FamilyLoadOptionsFactory : IFamilyLoadOptionsFactory
    {
        public IFamilyLoadOptions Create(bool overwriteParameterValues = true)
        {
            return new FamilyLoadOptions(overwriteParameterValues);
        }

        private class FamilyLoadOptions : IFamilyLoadOptions
        {
            private readonly bool _overwriteParameterValues;

            public FamilyLoadOptions(bool overwriteParameterValues)
            {
                _overwriteParameterValues = overwriteParameterValues;
            }

            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = _overwriteParameterValues;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = _overwriteParameterValues;
                return true;
            }
        }
    }
}
