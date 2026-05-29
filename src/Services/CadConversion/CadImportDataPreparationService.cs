using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using LECG.Services.Interfaces;
using System;

namespace LECG.Services
{
    public class CadImportDataPreparationService : ICadImportDataPreparationService
    {
        private readonly ICadGeometryExtractionService _geometryExtractionService;
        private readonly ICadGeometryOptimizationService _geometryOptimizationService;
        private readonly ICadDataValidationService _cadDataValidationService;

        public CadImportDataPreparationService(ICadGeometryExtractionService geometryExtractionService, ICadGeometryOptimizationService geometryOptimizationService, ICadDataValidationService cadDataValidationService)
        {
            _geometryExtractionService = geometryExtractionService;
            _geometryOptimizationService = geometryOptimizationService;
            _cadDataValidationService = cadDataValidationService;
        }

        public CadData Prepare(Document doc, ImportInstance cadInstance, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(cadInstance);
            ArgumentNullException.ThrowIfNull(reporter);

            reporter.Report("Extracting geometry from CAD...", 10);
            CadData data = _geometryExtractionService.ExtractGeometry(doc, cadInstance);

            reporter.Report("Optimizing geometry...", 30);
            CadData optimizedData = _geometryOptimizationService.Optimize(data);

            _cadDataValidationService.EnsureHasGeometry(optimizedData, "No suitable geometry found in the selected CAD.");
            return optimizedData;
        }
    }
}
