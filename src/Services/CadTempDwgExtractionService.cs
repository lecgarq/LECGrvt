using System;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadTempDwgExtractionService : ICadTempDwgExtractionService
    {
        private readonly ICadGeometryExtractionService _geometryExtractionService;

        public CadTempDwgExtractionService(ICadGeometryExtractionService geometryExtractionService)
        {
            _geometryExtractionService = geometryExtractionService;
        }

        public CadData Extract(Document doc, string templatePath, string dwgPath, Action<double, string>? progress = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(templatePath);
            ArgumentNullException.ThrowIfNull(dwgPath);

            progress?.Invoke(5, "Initializing temporary document...");
            Document tempDoc = doc.Application.NewFamilyDocument(templatePath);
            CadData data;

            using (Transaction t = new Transaction(tempDoc, "Temp Import"))
            {
                t.Start();
                DWGImportOptions opt = new DWGImportOptions
                {
                    Placement = ImportPlacement.Centered,
                    ColorMode = ImportColorMode.Preserved,
                    Unit = ImportUnit.Default
                };
                View? importView = new FilteredElementCollector(tempDoc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate);
                if (importView == null)
                {
                    throw new Exception("No valid import view found.");
                }

                progress?.Invoke(15, "Importing DWG file...");
                ElementId impId;
                bool success = tempDoc.Import(dwgPath, opt, importView, out impId);
                if (!success || impId == ElementId.InvalidElementId)
                {
                    throw new Exception("DWG Import failed.");
                }

                ImportInstance? imp = tempDoc.GetElement(impId) as ImportInstance;
                if (imp == null)
                {
                    throw new Exception("Imported DWG instance could not be resolved.");
                }
                progress?.Invoke(30, "Extracting geometry...");
                data = _geometryExtractionService.ExtractGeometry(tempDoc, imp);
                t.RollBack();
            }

            tempDoc.Close(false);

            return data;
        }
    }
}
