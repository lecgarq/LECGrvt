using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadFamilyBuildService : ICadFamilyBuildService
    {
        private readonly ICadDataDrawService _cadDataDrawService;
        private readonly ICadFamilySaveService _familySaveService;
        private readonly ITransactionService _transactionService;

        public CadFamilyBuildService(
            ICadDataDrawService cadDataDrawService,
            ICadFamilySaveService familySaveService,
            ITransactionService transactionService)
        {
            _cadDataDrawService = cadDataDrawService;
            _familySaveService = familySaveService;
            _transactionService = transactionService;
        }

        public string BuildAndSave(
            Document projectDoc,
            string templatePath,
            CadData data,
            XYZ offset,
            string lineStyleName,
            Color lineColor,
            int lineWeight,
            string transactionName,
            string familyName,
            IProgressReporter reporter,
            double progressStart,
            double progressEnd)
        {
            ArgumentNullException.ThrowIfNull(projectDoc);
            ArgumentNullException.ThrowIfNull(templatePath);
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(offset);
            ArgumentNullException.ThrowIfNull(lineStyleName);
            ArgumentNullException.ThrowIfNull(lineColor);
            ArgumentNullException.ThrowIfNull(transactionName);
            ArgumentNullException.ThrowIfNull(familyName);

            Document familyDoc = projectDoc.Application.NewFamilyDocument(templatePath);
            _transactionService.Run(familyDoc, transactionName, _ =>
            {
                _cadDataDrawService.Draw(
                    familyDoc,
                    data,
                    offset,
                    lineStyleName,
                    lineColor,
                    lineWeight,
                    reporter,
                    progressStart,
                    progressEnd);
            });

            return _familySaveService.Save(familyDoc, familyName);
        }
    }
}
