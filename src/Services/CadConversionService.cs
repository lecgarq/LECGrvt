#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using LECG.Services.Interfaces;
using System;

namespace LECG.Services
{
    public class CadConversionService : ICadConversionService
    {
        private readonly ICadFamilyLoadPlacementService _familyLoadPlacementService;
        private readonly ICadImportDataPreparationService _cadImportDataPreparationService;
        private readonly ICadTempDwgExtractionService _cadTempDwgExtractionService;
        private readonly ICadFamilyBuildService _cadFamilyBuildService;
        private readonly ICadDataValidationService _cadDataValidationService;
        private readonly ICadImportInstanceCenterService _cadImportInstanceCenterService;

        public CadConversionService(
            ICadFamilyLoadPlacementService familyLoadPlacementService,
            ICadImportDataPreparationService cadImportDataPreparationService,
            ICadTempDwgExtractionService cadTempDwgExtractionService,
            ICadFamilyBuildService cadFamilyBuildService,
            ICadDataValidationService cadDataValidationService,
            ICadImportInstanceCenterService cadImportInstanceCenterService)
        {
            _familyLoadPlacementService = familyLoadPlacementService;
            _cadImportDataPreparationService = cadImportDataPreparationService;
            _cadTempDwgExtractionService = cadTempDwgExtractionService;
            _cadFamilyBuildService = cadFamilyBuildService;
            _cadDataValidationService = cadDataValidationService;
            _cadImportInstanceCenterService = cadImportInstanceCenterService;
        }

        public string GetDefaultTemplatePath()
        {
            return @"C:\ProgramData\Autodesk\RVT 2026\Family Templates\English\LECG\-\LECG_046_DETAIL-ITEM.rft";
        }

        public ElementId ConvertCadToFamily(Document doc, ImportInstance cadInstance, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, Action<double, string>? progress = null)
        {
            if (cadInstance == null) return ElementId.InvalidElementId;

            CadData optimizedData = _cadImportDataPreparationService.Prepare(doc, cadInstance, progress);

            XYZ center = _cadImportInstanceCenterService.GetCenter(cadInstance);

            progress?.Invoke(50, "Creating family document...");
            string path = _cadFamilyBuildService.BuildAndSave(
                doc,
                templatePath,
                optimizedData,
                center,
                lineStyleName,
                lineColor,
                lineWeight,
                "Create Detail Item Content",
                familyName,
                progress,
                50,
                80);

            progress?.Invoke(90, "Saving and loading family...");
            return _familyLoadPlacementService.LoadAndPlace(doc, path, center, cadInstance.Id);
        }

        public ElementId ConvertDwgToFamily(Document doc, string dwgPath, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, Action<double, string>? progress = null)
        {
            CadData data = _cadTempDwgExtractionService.Extract(doc, templatePath, dwgPath, progress);

            _cadDataValidationService.EnsureHasGeometry(data, "No geometry found in DWG.");

            progress?.Invoke(50, "Creating final family...");
            string path = _cadFamilyBuildService.BuildAndSave(
                doc,
                templatePath,
                data,
                XYZ.Zero,
                lineStyleName,
                lineColor,
                lineWeight,
                "Create Detail Item",
                familyName,
                progress,
                50,
                90);

            progress?.Invoke(95, "Loading into project...");
            return _familyLoadPlacementService.LoadOnly(doc, path);
        }
        
    }
}
