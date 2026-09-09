using System;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadTempDwgExtractionService
    {
        private readonly CadGeometryExtractionService _geometryExtractionService;
        private readonly ITransactionService _transactionService;

        public CadTempDwgExtractionService(
            CadGeometryExtractionService geometryExtractionService,
            ITransactionService transactionService)
        {
            _geometryExtractionService = geometryExtractionService;
            _transactionService = transactionService;
        }

        public CadData Extract(Document doc, string templatePath, string dwgPath, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(templatePath);
            ArgumentNullException.ThrowIfNull(dwgPath);
            ArgumentNullException.ThrowIfNull(reporter);

            reporter.Report("Initializing temporary document...", 5);
            Document tempDoc = doc.Application.NewFamilyDocument(templatePath);
            CadData? data = null;

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
                    throw new InvalidOperationException("No valid import view found.");
                }

                reporter.Report("Importing DWG file...", 15);
                ElementId impId;
                bool success = tempDoc.Import(dwgPath, opt, importView, out impId);
                if (!success || impId == ElementId.InvalidElementId)
                {
                    throw new InvalidOperationException("DWG Import failed.");
                }

                ImportInstance? imp = tempDoc.GetElement(impId) as ImportInstance;
                if (imp == null)
                {
                    throw new InvalidOperationException("Imported DWG instance could not be resolved.");
                }
                reporter.Report("Extracting geometry...", 30);
                data = _geometryExtractionService.ExtractGeometry(tempDoc, imp);
            });

            tempDoc.Close(false);

            return data ?? throw new InvalidOperationException("DWG extraction completed without producing CAD geometry data.");
        }
    }
}
