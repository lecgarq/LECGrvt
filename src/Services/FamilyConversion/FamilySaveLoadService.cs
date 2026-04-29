using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class FamilySaveLoadService : IFamilySaveLoadService
    {
        private readonly IFamilySaveService _familySaveService;
        private readonly IFamilyProjectLoadService _familyProjectLoadService;

        public FamilySaveLoadService(IFamilySaveService familySaveService, IFamilyProjectLoadService familyProjectLoadService)
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
