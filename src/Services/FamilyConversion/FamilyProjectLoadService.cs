using Autodesk.Revit.DB;
using LECG.Core;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    public class FamilyProjectLoadService : IFamilyProjectLoadService
    {
        private readonly IFamilyLoadOptionsFactory _familyLoadOptionsFactory;
        private readonly ITransactionService _transactionService;

        public FamilyProjectLoadService(
            IFamilyLoadOptionsFactory familyLoadOptionsFactory,
            ITransactionService transactionService)
        {
            _familyLoadOptionsFactory = familyLoadOptionsFactory;
            _transactionService = transactionService;
        }

        public void Load(Document doc, string tempFamilyPath)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(tempFamilyPath);

            _transactionService.RunWithWarningHandler(doc, "Load Converted Family", _ =>
            {
                Family? loadedFamily = null;
                doc.LoadFamily(tempFamilyPath, _familyLoadOptionsFactory.Create(), out loadedFamily);

                if (loadedFamily != null)
                    Logger.Instance.Log($"Family loaded: {loadedFamily.Name}");
                else
                    Logger.Instance.Log("Family definition updated (already existed).");
            }, new WarningSwallower());
        }
    }
}
