using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;

namespace LECG.Services
{
    public class CadImportFamilyCreationService
    {
        private readonly CadFamilyBuildService _cadFamilyBuildService;
        private readonly CadFamilyLoadPlacementService _familyLoadPlacementService;

        public CadImportFamilyCreationService(CadFamilyBuildService cadFamilyBuildService, CadFamilyLoadPlacementService familyLoadPlacementService)
        {
            _cadFamilyBuildService = cadFamilyBuildService;
            _familyLoadPlacementService = familyLoadPlacementService;
        }

        public ElementId CreateAndLoad(Document doc, CadData data, XYZ center, ElementId originalCadElementId, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(center);
            ArgumentNullException.ThrowIfNull(familyName);
            ArgumentNullException.ThrowIfNull(templatePath);
            ArgumentNullException.ThrowIfNull(lineStyleName);
            ArgumentNullException.ThrowIfNull(lineColor);
            ArgumentNullException.ThrowIfNull(reporter);

            reporter.Report("Creating family document...", 50);
            string path = _cadFamilyBuildService.BuildAndSave(
                doc,
                templatePath,
                data,
                center,
                lineStyleName,
                lineColor,
                lineWeight,
                "Create Detail Item Content",
                familyName,
                reporter,
                50,
                80);

            reporter.Report("Saving and loading family...", 90);
            return _familyLoadPlacementService.LoadAndPlace(doc, path, center, originalCadElementId);
        }
    }
}
