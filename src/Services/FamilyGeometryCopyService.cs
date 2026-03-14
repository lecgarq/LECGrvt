using Autodesk.Revit.DB;
using LECG.Core;
using LECG.Services.Interfaces;
using System.Collections.Generic;

namespace LECG.Services
{
    public class FamilyGeometryCopyService : IFamilyGeometryCopyService
    {

        private readonly IFamilyGeometryCollectionService _geometryCollectionService;
        private readonly IFamilyParameterSetupService _familyParameterSetupService;
        private readonly ITransactionService _transactionService;

        public FamilyGeometryCopyService(
            IFamilyGeometryCollectionService geometryCollectionService,
            IFamilyParameterSetupService familyParameterSetupService,
            ITransactionService transactionService)
        {
            _geometryCollectionService = geometryCollectionService;
            _familyParameterSetupService = familyParameterSetupService;
            _transactionService = transactionService;
        }

        public int CopyGeometry(Document sourceFamilyDoc, Document targetFamilyDoc)
        {
            List<ElementId> idsToCopy = _geometryCollectionService.CollectGeometryElementIds(sourceFamilyDoc);
            int copiedCount = 0;

            _transactionService.RunWithWarningHandler(targetFamilyDoc, "Copy Geometry", _ =>
            {
                _familyParameterSetupService.ConfigureTargetFamilyParameters(targetFamilyDoc, sourceFamilyDoc);

                if (idsToCopy.Count > 0)
                {
                    CopyPasteOptions options = new CopyPasteOptions();

                    // Try batch first, fall back to individual
                    try
                    {
                        ElementTransformUtils.CopyElements(sourceFamilyDoc, idsToCopy, targetFamilyDoc, Transform.Identity, options);
                        copiedCount = idsToCopy.Count;
                    }
                    catch
                    {
                        LECG.Services.Logging.Logger.Instance.Log($"Batch copy failed for {idsToCopy.Count} elements. Falling back to individual copy...");
                        foreach (var id in idsToCopy)
                        {
                            try
                            {
                                ElementTransformUtils.CopyElements(sourceFamilyDoc, new List<ElementId> { id }, targetFamilyDoc, Transform.Identity, options);
                                copiedCount++;
                            }
                            catch
                            {
                                var el = sourceFamilyDoc.GetElement(id);
                                string elInfo = el != null ? $"{el.GetType().Name} '{el.Name}' (Cat: {el.Category?.Name ?? "none"})" : id.Value.ToString();
                                LECG.Services.Logging.Logger.Instance.Log($"  Skipped: {elInfo}");
                            }
                        }
                        LECG.Services.Logging.Logger.Instance.Log($"Individual copy: {copiedCount}/{idsToCopy.Count} elements transferred.");
                    }
                }
            }, new WarningSwallower());

            return copiedCount;
        }
    }
}
