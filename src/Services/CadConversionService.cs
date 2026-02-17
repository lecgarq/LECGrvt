#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using LECG.Core;
using LECG.Services.Interfaces;
using System;
using System.Linq;

namespace LECG.Services
{
    public class CadConversionService : ICadConversionService
    {
        private readonly ICadFamilyLoadPlacementService _familyLoadPlacementService;
        private readonly ICadGeometryExtractionService _geometryExtractionService;
        private readonly ICadGeometryOptimizationService _geometryOptimizationService;
        private readonly ICadDataDrawService _cadDataDrawService;
        private readonly ICadFamilySaveService _familySaveService;
        private readonly ICadTempDwgExtractionService _cadTempDwgExtractionService;

        public CadConversionService(
            ICadFamilyLoadPlacementService familyLoadPlacementService,
            ICadGeometryExtractionService geometryExtractionService,
            ICadGeometryOptimizationService geometryOptimizationService,
            ICadDataDrawService cadDataDrawService,
            ICadFamilySaveService familySaveService,
            ICadTempDwgExtractionService cadTempDwgExtractionService)
        {
            _familyLoadPlacementService = familyLoadPlacementService;
            _geometryExtractionService = geometryExtractionService;
            _geometryOptimizationService = geometryOptimizationService;
            _cadDataDrawService = cadDataDrawService;
            _familySaveService = familySaveService;
            _cadTempDwgExtractionService = cadTempDwgExtractionService;
        }

        public string GetDefaultTemplatePath()
        {
            return @"C:\ProgramData\Autodesk\RVT 2026\Family Templates\English\LECG\-\LECG_046_DETAIL-ITEM.rft";
        }

        public ElementId ConvertCadToFamily(Document doc, ImportInstance cadInstance, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, Action<double, string>? progress = null)
        {
            if (cadInstance == null) return ElementId.InvalidElementId;

            progress?.Invoke(10, "Extracting geometry from CAD...");
            CadData data = _geometryExtractionService.ExtractGeometry(doc, cadInstance);
            
            progress?.Invoke(30, "Optimizing geometry...");
            CadData optimizedData = _geometryOptimizationService.Optimize(data);

            if (!optimizedData.Curves.Any() && !optimizedData.Hatches.Any())
                throw new Exception("No suitable geometry found in the selected CAD.");

            BoundingBoxXYZ box = cadInstance.get_BoundingBox(null);
            XYZ center = (box.Min + box.Max) * 0.5;

            progress?.Invoke(50, "Creating family document...");
            Document familyDoc = doc.Application.NewFamilyDocument(templatePath);
            using (Transaction t = new Transaction(familyDoc, "Create Detail Item Content"))
            {
                t.Start();
                _cadDataDrawService.Draw(familyDoc, optimizedData, center, lineStyleName, lineColor, lineWeight, progress, 50, 80);
                t.Commit();
            }

            progress?.Invoke(90, "Saving and loading family...");
            string path = _familySaveService.Save(familyDoc, familyName);
            return _familyLoadPlacementService.LoadAndPlace(doc, path, center, cadInstance.Id);
        }

        public ElementId ConvertDwgToFamily(Document doc, string dwgPath, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, Action<double, string>? progress = null)
        {
            CadData data = _cadTempDwgExtractionService.Extract(doc, templatePath, dwgPath, progress);

            if (data == null || (!data.Curves.Any() && !data.Hatches.Any()))
                 throw new Exception("No geometry found in DWG.");

            progress?.Invoke(50, "Creating final family...");
            Document familyDoc = doc.Application.NewFamilyDocument(templatePath);
            using (Transaction t = new Transaction(familyDoc, "Create Detail Item"))
            {
                t.Start();
                _cadDataDrawService.Draw(familyDoc, data, XYZ.Zero, lineStyleName, lineColor, lineWeight, progress, 50, 90);
                t.Commit();
            }

            progress?.Invoke(95, "Loading into project...");
            string path = _familySaveService.Save(familyDoc, familyName);
            return _familyLoadPlacementService.LoadOnly(doc, path);
        }
        
    }
}
