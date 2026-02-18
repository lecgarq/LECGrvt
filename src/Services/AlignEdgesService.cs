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

        public AlignEdgesService() : this(new AlignEdgesIntersectorService(), new AlignEdgesToposolidProcessingService(new AlignEdgesBoundaryCollectionService(new AlignEdgesBoundaryPointService()), new AlignEdgesPointInsertionService(), new AlignEdgesVertexAlignmentService()))
        {
        }

        public AlignEdgesService(IAlignEdgesIntersectorService intersectorService, IAlignEdgesToposolidProcessingService toposolidProcessingService)
        {
            _intersectorService = intersectorService;
            _toposolidProcessingService = toposolidProcessingService;
        }

        public void AlignEdges(Document doc, IList<Reference> targets, IList<Reference> references)
        {
            if (targets == null || targets.Count == 0 || references == null || references.Count == 0) return;

            ReferenceIntersector intersector = _intersectorService.Create(doc, references);

            using (Transaction t = new Transaction(doc, "Align Edges"))
            {
                t.Start();
                
                foreach (Reference r in targets)
                {
                    _toposolidProcessingService.Process(doc, r, intersector);
                }
                
                t.Commit();
            }
        }

    }
}
