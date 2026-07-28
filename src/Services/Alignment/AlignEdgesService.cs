using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class AlignEdgesService
    {
        private readonly AlignEdgesIntersectorService _intersectorService;
        private readonly AlignEdgesToposolidProcessingService _toposolidProcessingService;
        private readonly ITransactionService _transactionService;

        public AlignEdgesService(AlignEdgesIntersectorService intersectorService, AlignEdgesToposolidProcessingService toposolidProcessingService, ITransactionService transactionService)
        {
            _intersectorService = intersectorService;
            _toposolidProcessingService = toposolidProcessingService;
            _transactionService = transactionService;
        }

        public IReadOnlyList<AlignEdgesSourceResult> AlignEdges(Document doc, IList<Reference> targets, IList<Reference> references)
        {
            ArgumentNullException.ThrowIfNull(doc);
            if (targets == null || targets.Count == 0 || references == null || references.Count == 0)
            {
                return Array.Empty<AlignEdgesSourceResult>();
            }

            var referenceIds = new List<ElementId>();
            foreach (Reference r in references)
            {
                Element? elem = doc.GetElement(r);
                if (elem != null)
                {
                    referenceIds.Add(elem.Id);
                }
            }

            ReferenceIntersector intersector = _intersectorService.Create(doc, references);
            return RunAlignment(doc, targets, intersector, referenceIds);
        }

        public IReadOnlyList<AlignEdgesSourceResult> AlignEdgesFindMyEdge(Document doc, IList<Reference> targets)
        {
            ArgumentNullException.ThrowIfNull(doc);
            if (targets == null || targets.Count == 0)
            {
                return Array.Empty<AlignEdgesSourceResult>();
            }

            var excludeIds = new HashSet<ElementId>();
            foreach (Reference r in targets)
            {
                Element? elem = doc.GetElement(r);
                if (elem != null)
                {
                    excludeIds.Add(elem.Id);
                }
            }

            ReferenceIntersector intersector = _intersectorService.CreateBroad(doc, excludeIds);
            return RunAlignment(doc, targets, intersector, null);
        }

        private IReadOnlyList<AlignEdgesSourceResult> RunAlignment(Document doc, IList<Reference> targets, ReferenceIntersector intersector, IList<ElementId>? referenceIds)
        {
            var results = new List<AlignEdgesSourceResult>(targets.Count);

            foreach (Reference r in targets)
            {
                Element? sourceElement = doc.GetElement(r);
                ElementId sourceId = sourceElement?.Id ?? ElementId.InvalidElementId;
                string sourceName = sourceElement?.Name ?? $"Element {sourceId.Value}";

                try
                {
                    AlignEdgesSourceResult result = _transactionService.Run(
                        doc,
                        $"Align Edges - {sourceName}",
                        currentDoc => _toposolidProcessingService.Process(currentDoc, r, intersector, referenceIds));

                    results.Add(result);
                }
                catch (Exception ex) when (IsExpectedAlignEdgesException(ex))
                {
                    results.Add(AlignEdgesSourceResult.Failed(sourceId, sourceName, ex.Message));
                }
            }

            return results;
        }

        private static bool IsExpectedAlignEdgesException(Exception ex)
        {
            return ex is ArgumentException
                or InvalidOperationException
                or RevitExceptions.ArgumentException
                or RevitExceptions.InvalidOperationException;
        }
    }
}
