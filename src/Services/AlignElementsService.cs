#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
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

        public AlignElementsService() : this(new AlignElementsTranslationService(), new AlignElementsDistributionItemService(), new AlignElementsDistributionMoveService())
        {
        }

        public AlignElementsService(IAlignElementsTranslationService translationService, IAlignElementsDistributionItemService distributionItemService, IAlignElementsDistributionMoveService distributionMoveService)
        {
            _translationService = translationService;
            _distributionItemService = distributionItemService;
            _distributionMoveService = distributionMoveService;
        }

        public void Align(Document doc, Element reference, List<Element> targets, AlignMode mode)
        {
            ArgumentNullException.ThrowIfNull(doc);

            if (reference == null || targets == null || !targets.Any()) return;

            using (Transaction t = new Transaction(doc, $"Align {mode}"))
            {
                t.Start();

                // Get Reference BoundingBox
                BoundingBoxXYZ refBox = reference.get_BoundingBox(doc.ActiveView);
                if (refBox == null) return;

                foreach (Element target in targets)
                {
                    BoundingBoxXYZ targetBox = target.get_BoundingBox(doc.ActiveView);
                    if (targetBox == null) continue;

                    XYZ translation = _translationService.Calculate(refBox, targetBox, mode);

                    if (!translation.IsZeroLength())
                    {
                        ElementTransformUtils.MoveElement(doc, target.Id, translation);
                    }
                }

                t.Commit();
            }
        }

        public void Distribute(Document doc, List<Element> elements, AlignMode mode)
        {
            ArgumentNullException.ThrowIfNull(doc);

            if (elements == null || elements.Count < 3) return; // Need at least 3 items to distribute meaningfully

            using (Transaction t = new Transaction(doc, $"Distribute {mode}"))
            {
                t.Start();

                List<(Element Element, BoundingBoxXYZ Box, double Position)> sortedItems = _distributionItemService.BuildAndSort(doc, elements, mode);

                if (sortedItems.Count < 3) return;

                _distributionMoveService.MoveIntermediateElements(doc, sortedItems, mode);

                t.Commit();
            }
        }
    }
}
