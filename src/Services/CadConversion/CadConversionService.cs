using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadConversionService
    {
        private readonly CadImportDataPreparationService _cadImportDataPreparationService;
        private readonly CadImportFamilyCreationService _cadImportFamilyCreationService;
        private readonly CadTempDwgExtractionService _cadTempDwgExtractionService;
        private readonly CadDwgFamilyCreationService _cadDwgFamilyCreationService;
        private readonly CadDataValidationService _cadDataValidationService;
        private readonly CadImportInstanceCenterService _cadImportInstanceCenterService;

        public CadConversionService(
            CadImportDataPreparationService cadImportDataPreparationService,
            CadImportFamilyCreationService cadImportFamilyCreationService,
            CadTempDwgExtractionService cadTempDwgExtractionService,
            CadDwgFamilyCreationService cadDwgFamilyCreationService,
            CadDataValidationService cadDataValidationService,
            CadImportInstanceCenterService cadImportInstanceCenterService)
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
            return Configuration.RevitConstants.FindTemplate(@"English\LECG\-\LECG_046_DETAIL-ITEM.rft")
                ?? string.Empty;
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
