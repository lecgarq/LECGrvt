using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
        public class AlignEdgesService : IAlignEdgesService
        {
        private readonly IAlignEdgesIntersectorService _intersectorService;
        private readonly IAlignEdgesToposolidProcessingService _toposolidProcessingService;
        private readonly ITransactionService _transactionService;

        public AlignEdgesService(IAlignEdgesIntersectorService intersectorService, IAlignEdgesToposolidProcessingService toposolidProcessingService, ITransactionService transactionService)
        {
            _intersectorService = intersectorService;
            _toposolidProcessingService = toposolidProcessingService;
            _transactionService = transactionService;
        }

        public void AlignEdges(Document doc, IList<Reference> targets, IList<Reference> references)
        {
            if (targets == null || targets.Count == 0 || references == null || references.Count == 0) return;

            ReferenceIntersector intersector = _intersectorService.Create(doc, references);

            _transactionService.Run(doc, "Align Edges", currentDoc =>
            {
                foreach (Reference r in targets)
                {
                    _toposolidProcessingService.Process(currentDoc, r, intersector);
                }
            });
        }

    }
}
