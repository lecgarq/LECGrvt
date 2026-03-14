using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using LECG.Services.Interfaces;
using System;

namespace LECG.Services
{
    public class CadConversionService : ICadConversionService
    {
        private readonly ICadImportDataPreparationService _cadImportDataPreparationService;
        private readonly ICadImportFamilyCreationService _cadImportFamilyCreationService;
        private readonly ICadTempDwgExtractionService _cadTempDwgExtractionService;
        private readonly ICadDwgFamilyCreationService _cadDwgFamilyCreationService;
        private readonly ICadDataValidationService _cadDataValidationService;
        private readonly ICadImportInstanceCenterService _cadImportInstanceCenterService;

        public CadConversionService(
            ICadImportDataPreparationService cadImportDataPreparationService,
            ICadImportFamilyCreationService cadImportFamilyCreationService,
            ICadTempDwgExtractionService cadTempDwgExtractionService,
            ICadDwgFamilyCreationService cadDwgFamilyCreationService,
            ICadDataValidationService cadDataValidationService,
            ICadImportInstanceCenterService cadImportInstanceCenterService)
        {
            _cadImportDataPreparationService = cadImportDataPreparationService;
            _cadImportFamilyCreationService = cadImportFamilyCreationService;
            _cadTempDwgExtractionService = cadTempDwgExtractionService;
            _cadDwgFamilyCreationService = cadDwgFamilyCreationService;
            _cadDataValidationService = cadDataValidationService;
            _cadImportInstanceCenterService = cadImportInstanceCenterService;
        }

        public string GetDefaultTemplatePath()
        {
            return @"C:\ProgramData\Autodesk\RVT 2026\Family Templates\English\LECG\-\LECG_046_DETAIL-ITEM.rft";
        }

        public ElementId ConvertCadToFamily(Document doc, ImportInstance cadInstance, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, Action<double, string>? progress = null)
        {
            return ConvertCadToFamily(doc, cadInstance, familyName, templatePath, lineStyleName, lineColor, lineWeight, new LegacyProgressReporter(progress));
        }

        public ElementId ConvertCadToFamily(Document doc, ImportInstance cadInstance, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, IProgressReporter reporter)
        {
            if (cadInstance == null) return ElementId.InvalidElementId;

            CadData optimizedData = _cadImportDataPreparationService.Prepare(doc, cadInstance, reporter);

            XYZ center = _cadImportInstanceCenterService.GetCenter(cadInstance);

            return _cadImportFamilyCreationService.CreateAndLoad(
                doc,
                optimizedData,
                center,
                cadInstance.Id,
                familyName,
                templatePath,
                lineStyleName,
                lineColor,
                lineWeight,
                reporter);
        }

        public ElementId ConvertDwgToFamily(Document doc, string dwgPath, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, Action<double, string>? progress = null)
        {
            return ConvertDwgToFamily(doc, dwgPath, familyName, templatePath, lineStyleName, lineColor, lineWeight, new LegacyProgressReporter(progress));
        }

        public ElementId ConvertDwgToFamily(Document doc, string dwgPath, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, IProgressReporter reporter)
        {
            CadData data = _cadTempDwgExtractionService.Extract(doc, templatePath, dwgPath, reporter);

            _cadDataValidationService.EnsureHasGeometry(data, "No geometry found in DWG.");

            return _cadDwgFamilyCreationService.CreateAndLoad(
                doc,
                data,
                familyName,
                templatePath,
                lineStyleName,
                lineColor,
                lineWeight,
                reporter);
        }
        
    }
}
