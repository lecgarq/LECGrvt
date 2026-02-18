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

        public CadData Prepare(Document doc, ImportInstance cadInstance, Action<double, string>? progress = null)
        {
            progress?.Invoke(10, "Extracting geometry from CAD...");
            CadData data = _geometryExtractionService.ExtractGeometry(doc, cadInstance);

            progress?.Invoke(30, "Optimizing geometry...");
            CadData optimizedData = _geometryOptimizationService.Optimize(data);

            _cadDataValidationService.EnsureHasGeometry(optimizedData, "No suitable geometry found in the selected CAD.");
            return optimizedData;
        }
    }
}
