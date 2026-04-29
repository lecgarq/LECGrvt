using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class AlignElementsService : IAlignElementsService
    {
        private readonly IAlignElementsTranslationService _translationService;
        private readonly IAlignElementsDistributionItemService _distributionItemService;
        private readonly IAlignElementsDistributionMoveService _distributionMoveService;
        private readonly ITransactionService _transactionService;

        public AlignElementsService(IAlignElementsTranslationService translationService, IAlignElementsDistributionItemService distributionItemService, IAlignElementsDistributionMoveService distributionMoveService, ITransactionService transactionService)
        {
            _translationService = translationService;
            _distributionItemService = distributionItemService;
            _distributionMoveService = distributionMoveService;
            _transactionService = transactionService;
        }

        public void Align(Document doc, Element reference, List<Element> targets, AlignMode mode)
        {
            ArgumentNullException.ThrowIfNull(doc);

            if (reference == null || targets == null || !targets.Any()) return;

            _transactionService.Run(doc, $"Align {mode}", currentDoc =>
            {
                // Get Reference BoundingBox
                BoundingBoxXYZ refBox = reference.get_BoundingBox(currentDoc.ActiveView);
                if (refBox == null) return;

                foreach (Element target in targets)
                {
                    BoundingBoxXYZ targetBox = target.get_BoundingBox(currentDoc.ActiveView);
                    if (targetBox == null) continue;

                    XYZ translation = _translationService.Calculate(refBox, targetBox, mode);

                    if (!translation.IsZeroLength())
                    {
                        ElementTransformUtils.MoveElement(currentDoc, target.Id, translation);
                    }
                }
            });
        }

        public void Distribute(Document doc, List<Element> elements, AlignMode mode)
        {
            ArgumentNullException.ThrowIfNull(doc);

            if (elements == null || elements.Count < 3) return; // Need at least 3 items to distribute meaningfully

            _transactionService.Run(doc, $"Distribute {mode}", currentDoc =>
            {
                List<(Element Element, BoundingBoxXYZ Box, double Position)> sortedItems = _distributionItemService.BuildAndSort(currentDoc, elements, mode);

                if (sortedItems.Count < 3) return;

                _distributionMoveService.MoveIntermediateElements(currentDoc, sortedItems, mode);
            });
        }
    }
}
