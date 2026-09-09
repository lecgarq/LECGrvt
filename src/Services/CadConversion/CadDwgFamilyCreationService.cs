using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;

namespace LECG.Services
{
    public class CadDwgFamilyCreationService
    {
        private readonly CadFamilyBuildService _cadFamilyBuildService;
        private readonly CadFamilyLoadPlacementService _familyLoadPlacementService;

        public CadDwgFamilyCreationService(CadFamilyBuildService cadFamilyBuildService, CadFamilyLoadPlacementService familyLoadPlacementService)
        {
            _cadFamilyBuildService = cadFamilyBuildService;
            _familyLoadPlacementService = familyLoadPlacementService;
        }

        public ElementId CreateAndLoad(Document doc, CadData data, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(familyName);
            ArgumentNullException.ThrowIfNull(templatePath);
            ArgumentNullException.ThrowIfNull(lineStyleName);
            ArgumentNullException.ThrowIfNull(lineColor);
            ArgumentNullException.ThrowIfNull(reporter);

            reporter.Report("Creating final family...", 50);
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
                reporter,
                50,
                90);

            reporter.Report("Loading into project...", 95);
            return _familyLoadPlacementService.LoadOnly(doc, path);
        }
    }
}
