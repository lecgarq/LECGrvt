using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class FamilySaveLoadService
    {
        private readonly FamilySaveService _familySaveService;
        private readonly FamilyProjectLoadService _familyProjectLoadService;

        public FamilySaveLoadService(FamilySaveService familySaveService, FamilyProjectLoadService familyProjectLoadService)
        {
            _familySaveService = familySaveService;
            _familyProjectLoadService = familyProjectLoadService;
        }

        public string SaveAndLoad(Document projectDoc, Document familyDoc, string targetFamilyName)
        {
            string tempFamilyPath = _familySaveService.SaveTemp(familyDoc, targetFamilyName);
            _familyProjectLoadService.Load(projectDoc, tempFamilyPath);
            return tempFamilyPath;
        }
    }
}
