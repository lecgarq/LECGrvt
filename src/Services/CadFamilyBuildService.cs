using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadFamilyBuildService : ICadFamilyBuildService
    {
        private readonly ICadDataDrawService _cadDataDrawService;
        private readonly ICadFamilySaveService _familySaveService;

        public CadFamilyBuildService(ICadDataDrawService cadDataDrawService, ICadFamilySaveService familySaveService)
        {
            _cadDataDrawService = cadDataDrawService;
            _familySaveService = familySaveService;
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
            Action<double, string>? progress,
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
            using (Transaction t = new Transaction(familyDoc, transactionName))
            {
                t.Start();
                _cadDataDrawService.Draw(
                    familyDoc,
                    data,
                    offset,
                    lineStyleName,
                    lineColor,
                    lineWeight,
                    progress,
                    progressStart,
                    progressEnd);
                t.Commit();
            }

            return _familySaveService.Save(familyDoc, familyName);
        }
    }
}
