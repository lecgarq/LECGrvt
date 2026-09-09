using System;
using Autodesk.Revit.DB;
using LECG.Core;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    public class FamilyProjectLoadService
    {
        private readonly FamilyLoadOptionsFactory _familyLoadOptionsFactory;
        private readonly ITransactionService _transactionService;
        private readonly ILogger _logger;

        public FamilyProjectLoadService(
            FamilyLoadOptionsFactory familyLoadOptionsFactory,
            ITransactionService transactionService,
            ILogger logger)
        {
            _familyLoadOptionsFactory = familyLoadOptionsFactory;
            _transactionService = transactionService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                    _logger.Log($"Family loaded: {loadedFamily.Name}", scope: "FamilyProjectLoad");
                else
                    _logger.Log("Family definition updated (already existed).", scope: "FamilyProjectLoad");
            }, new WarningSwallower());
        }
    }
}
