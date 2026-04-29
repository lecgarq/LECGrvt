using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class FamilyConversionFinalizeService : IFamilyConversionFinalizeService
    {
        private readonly IFamilyTempFileCleanupService _tempFileCleanupService;

        public FamilyConversionFinalizeService(IFamilyTempFileCleanupService tempFileCleanupService)
        {
            _tempFileCleanupService = tempFileCleanupService;
        }

        public void Finalize(Document sourceFamilyDoc, Document? targetFamilyDoc, string tempFamilyPath, bool isTemporary)
        {
            ArgumentNullException.ThrowIfNull(sourceFamilyDoc);
            ArgumentNullException.ThrowIfNull(tempFamilyPath);

            sourceFamilyDoc.Close(false);
            if (targetFamilyDoc != null)
            {
                targetFamilyDoc.Close(false);
            }

            _tempFileCleanupService.Cleanup(tempFamilyPath, isTemporary);
        }
    }
}
