using Autodesk.Revit.DB;
using LECG.Core;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using System;
using System.Collections.Generic;

namespace LECG.Services
{
    public class FamilyGeometryCopyService
    {

        private readonly FamilyGeometryCollectionService _geometryCollectionService;
        private readonly FamilyParameterSetupService _familyParameterSetupService;
        private readonly ITransactionService _transactionService;
        private readonly ILogger _logger;

        public FamilyGeometryCopyService(
            FamilyGeometryCollectionService geometryCollectionService,
            FamilyParameterSetupService familyParameterSetupService,
            ITransactionService transactionService,
            ILogger logger)
        {
            _geometryCollectionService = geometryCollectionService;
            _familyParameterSetupService = familyParameterSetupService;
            _transactionService = transactionService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                        _logger.Log($"Batch copy failed for {idsToCopy.Count} elements. Falling back to individual copy...", scope: "FamilyGeometryCopy");
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
                                _logger.Log($"  Skipped: {elInfo}", scope: "FamilyGeometryCopy");
                            }
                        }
                        _logger.Log($"Individual copy: {copiedCount}/{idsToCopy.Count} elements transferred.", scope: "FamilyGeometryCopy");
                    }
                }
            }, new WarningSwallower());

            return copiedCount;
        }
    }
}
