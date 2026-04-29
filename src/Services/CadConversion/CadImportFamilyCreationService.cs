using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;

namespace LECG.Services
{
    public class CadImportFamilyCreationService : ICadImportFamilyCreationService
    {
        private readonly ICadFamilyBuildService _cadFamilyBuildService;
        private readonly ICadFamilyLoadPlacementService _familyLoadPlacementService;

        public CadImportFamilyCreationService(ICadFamilyBuildService cadFamilyBuildService, ICadFamilyLoadPlacementService familyLoadPlacementService)
        {
            _cadFamilyBuildService = cadFamilyBuildService;
            _familyLoadPlacementService = familyLoadPlacementService;
        }

        public ElementId CreateAndLoad(Document doc, CadData data, XYZ center, ElementId originalCadElementId, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, Action<double, string>? progress = null)
        {
            return CreateAndLoad(doc, data, center, originalCadElementId, familyName, templatePath, lineStyleName, lineColor, lineWeight, new LegacyProgressReporter(progress));
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
