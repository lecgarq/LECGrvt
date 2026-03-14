using System;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadTempDwgExtractionService : ICadTempDwgExtractionService
    {
        private readonly ICadGeometryExtractionService _geometryExtractionService;
        private readonly ITransactionService _transactionService;

        public CadTempDwgExtractionService(
            ICadGeometryExtractionService geometryExtractionService,
            ITransactionService transactionService)
        {
            _geometryExtractionService = geometryExtractionService;
            _transactionService = transactionService;
        }

        public CadData Extract(Document doc, string templatePath, string dwgPath, Action<double, string>? progress = null)
        {
            return Extract(doc, templatePath, dwgPath, new LegacyProgressReporter(progress));
        }

        public CadData Extract(Document doc, string templatePath, string dwgPath, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(templatePath);
            ArgumentNullException.ThrowIfNull(dwgPath);

            reporter.Report("Initializing temporary document...", 5);
            Document tempDoc = doc.Application.NewFamilyDocument(templatePath);
            CadData data = null!;

            _transactionService.RunRollbackOnly(tempDoc, "Temp Import", _ =>
            {
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

                reporter.Report("Importing DWG file...", 15);
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
                reporter.Report("Extracting geometry...", 30);
                data = _geometryExtractionService.ExtractGeometry(tempDoc, imp);
            });

            tempDoc.Close(false);

            return data;
        }
    }
}
