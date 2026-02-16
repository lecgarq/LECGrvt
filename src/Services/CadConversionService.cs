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
        private readonly ICadLineStyleService _lineStyleService;
        private readonly ICadLineMergeService _lineMergeService;
        private readonly ICadCurveFlattenService _curveFlattenService;
        private readonly ICadFilledRegionTypeService _filledRegionTypeService;
        private readonly ICadFamilyLoadPlacementService _familyLoadPlacementService;
        private readonly ICadGeometryExtractionService _geometryExtractionService;
        private readonly ICadGeometryOptimizationService _geometryOptimizationService;
        private readonly ICadDrawingViewService _drawingViewService;
        private readonly ICadFamilySaveService _familySaveService;

        public CadConversionService(
            ICadPlacementViewService placementViewService,
            ICadFamilySymbolService familySymbolService,
            ICadLineStyleService lineStyleService,
            ICadLineMergeService lineMergeService,
            ICadCurveFlattenService curveFlattenService,
            ICadFilledRegionTypeService filledRegionTypeService,
            ICadFamilyLoadPlacementService familyLoadPlacementService,
            ICadGeometryExtractionService geometryExtractionService,
            ICadGeometryOptimizationService geometryOptimizationService,
            ICadDrawingViewService drawingViewService,
            ICadFamilySaveService familySaveService)
        {
            _placementViewService = placementViewService;
            _familySymbolService = familySymbolService;
            _lineStyleService = lineStyleService;
            _lineMergeService = lineMergeService;
            _curveFlattenService = curveFlattenService;
            _filledRegionTypeService = filledRegionTypeService;
            _familyLoadPlacementService = familyLoadPlacementService;
            _geometryExtractionService = geometryExtractionService;
            _geometryOptimizationService = geometryOptimizationService;
            _drawingViewService = drawingViewService;
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
            GraphicsStyle lineStyle = _lineStyleService.CreateOrUpdateDetailLineStyle(familyDoc, styleName, color, weight);

            View planView = _drawingViewService.ResolveFamilyDrawingView(familyDoc);

            Transform toOrigin = Transform.CreateTranslation(-offset);
            
            int total = data.Curves.Count + data.Hatches.Count;
            int current = 0;

            foreach (Curve c in data.Curves)
            {
                current++;
                if (total > 0 && (current % Math.Max(1, total / 20) == 0 || current == total)) progress?.Invoke(startPct + (endPct - startPct) * current / total, $"Drawing curves... ({current}/{data.Curves.Count})");
                try
                {
                    Curve transCurve = c.CreateTransformed(toOrigin);
                    IEnumerable<Curve>? flats = _curveFlattenService.FlattenCurve(transCurve);
                    
                    if (flats != null)
                    {
                        foreach(var flat in flats)
                        {
                             DetailCurve dc = familyDoc.FamilyCreate.NewDetailCurve(planView, flat);
                             if (dc != null) dc.LineStyle = lineStyle;
                        }
                    }
                }
                catch { }
            }

            foreach (var hatch in data.Hatches)
            {
                current++;
                if (total > 0 && (current % Math.Max(1, total / 20) == 0 || current == total)) progress?.Invoke(startPct + (endPct - startPct) * current / total, $"Drawing hatches... ({current - data.Curves.Count}/{data.Hatches.Count})");
                try
                {
                    FilledRegionType? frType = _filledRegionTypeService.GetOrCreateFilledRegionType(familyDoc, hatch.Color);
                    if (frType == null) continue;
                    List<CurveLoop> validLoops = new List<CurveLoop>();
                    
                    foreach (var loop in hatch.Loops)
                    {
                        CurveLoop newLoop = new CurveLoop();
                        foreach (Curve c in loop)
                        {
                            Curve transCurve = c.CreateTransformed(toOrigin);
                            IEnumerable<Curve>? flats = _curveFlattenService.FlattenCurve(transCurve);
                            if (flats != null) 
                            {
                                foreach(var flat in flats) newLoop.Append(flat);
                            }
                        }
                        
                        if (!newLoop.IsOpen() && newLoop.GetExactLength() > 0.001)
                            validLoops.Add(newLoop);
                    }

                    if (validLoops.Any())
                        FilledRegion.Create(familyDoc, frType.Id, planView.Id, validLoops);
                }
                catch { }
            }
        }

    }
}
