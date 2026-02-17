#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using LECG.Core;
using LECG.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class CadConversionService : ICadConversionService
    {
        private readonly ICadPlacementViewService _placementViewService;
        private readonly ICadFamilySymbolService _familySymbolService;
        private readonly ICadFamilyLoadPlacementService _familyLoadPlacementService;
        private readonly ICadGeometryExtractionService _geometryExtractionService;
        private readonly ICadGeometryOptimizationService _geometryOptimizationService;
        private readonly ICadRenderContextService _renderContextService;
        private readonly ICadCurveRenderService _curveRenderService;
        private readonly ICadHatchRenderService _hatchRenderService;
        private readonly ICadFamilySaveService _familySaveService;

        public CadConversionService(
            ICadPlacementViewService placementViewService,
            ICadFamilySymbolService familySymbolService,
            ICadFamilyLoadPlacementService familyLoadPlacementService,
            ICadGeometryExtractionService geometryExtractionService,
            ICadGeometryOptimizationService geometryOptimizationService,
            ICadRenderContextService renderContextService,
            ICadCurveRenderService curveRenderService,
            ICadHatchRenderService hatchRenderService,
            ICadFamilySaveService familySaveService)
        {
            _placementViewService = placementViewService;
            _familySymbolService = familySymbolService;
            _familyLoadPlacementService = familyLoadPlacementService;
            _geometryExtractionService = geometryExtractionService;
            _geometryOptimizationService = geometryOptimizationService;
            _renderContextService = renderContextService;
            _curveRenderService = curveRenderService;
            _hatchRenderService = hatchRenderService;
            _familySaveService = familySaveService;
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
                DrawData(familyDoc, optimizedData, center, lineStyleName, lineColor, lineWeight, progress, 50, 80);
                t.Commit();
            }

            progress?.Invoke(90, "Saving and loading family...");
            string path = _familySaveService.Save(familyDoc, familyName);
            return _familyLoadPlacementService.LoadAndPlace(doc, path, center, cadInstance.Id);
        }

        public ElementId ConvertDwgToFamily(Document doc, string dwgPath, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, Action<double, string>? progress = null)
        {
            progress?.Invoke(5, "Initializing temporary document...");
            Document tempDoc = doc.Application.NewFamilyDocument(templatePath);
            CadData data;
            
            using (Transaction t = new Transaction(tempDoc, "Temp Import"))
            {
                t.Start();
                DWGImportOptions opt = new DWGImportOptions { Placement = ImportPlacement.Centered, ColorMode = ImportColorMode.Preserved, Unit = ImportUnit.Default };
                View importView = new FilteredElementCollector(tempDoc).OfClass(typeof(View)).Cast<View>().FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate);

                progress?.Invoke(15, "Importing DWG file...");
                ElementId impId;
                bool success = tempDoc.Import(dwgPath, opt, importView, out impId);
                if (!success || impId == ElementId.InvalidElementId) throw new Exception("DWG Import failed.");

                ImportInstance imp = tempDoc.GetElement(impId) as ImportInstance;
                progress?.Invoke(30, "Extracting geometry...");
                data = _geometryExtractionService.ExtractGeometry(tempDoc, imp);
                t.RollBack(); 
            }
            tempDoc.Close(false);

            if (data == null || (!data.Curves.Any() && !data.Hatches.Any()))
                 throw new Exception("No geometry found in DWG.");

            progress?.Invoke(50, "Creating final family...");
            Document familyDoc = doc.Application.NewFamilyDocument(templatePath);
            using (Transaction t = new Transaction(familyDoc, "Create Detail Item"))
            {
                t.Start();
                DrawData(familyDoc, data, XYZ.Zero, lineStyleName, lineColor, lineWeight, progress, 50, 90);
                t.Commit();
            }

            progress?.Invoke(95, "Loading into project...");
            string path = _familySaveService.Save(familyDoc, familyName);
            return _familyLoadPlacementService.LoadOnly(doc, path);
        }
        
        private void DrawData(Document familyDoc, CadData data, XYZ offset, string styleName, Color color, int weight, Action<double, string>? progress = null, double startPct = 50, double endPct = 90)
        {
            (GraphicsStyle lineStyle, View planView, Transform toOrigin) = _renderContextService.Create(familyDoc, offset, styleName, color, weight);
            
            int total = data.Curves.Count + data.Hatches.Count;
            int current = 0;

            current = _curveRenderService.DrawCurves(familyDoc, data.Curves, toOrigin, planView, lineStyle, progress, startPct, endPct, total, current);

            current = _hatchRenderService.DrawHatches(
                familyDoc,
                data.Hatches,
                toOrigin,
                planView,
                progress,
                startPct,
                endPct,
                total,
                current,
                data.Curves.Count);
        }

    }
}
