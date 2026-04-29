using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class ChangeLevelService : IChangeLevelService
    {
        private readonly IChangeLevelElementUpdateService _changeLevelElementUpdateService;
        private readonly ITransactionService _transactionService;

        public ChangeLevelService(IChangeLevelElementUpdateService changeLevelElementUpdateService, ITransactionService transactionService)
        {
            _changeLevelElementUpdateService = changeLevelElementUpdateService;
            _transactionService = transactionService;
        }

        public List<Level> GetLevels(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
        }

        public void ChangeLevel(Document doc, IEnumerable<Element> elements, Level newLevel)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(elements);

            if (newLevel == null) return;

            _transactionService.Run(doc, "Change Element Level", currentDoc =>
            {
                foreach (var elem in elements)
                {
                    _changeLevelElementUpdateService.UpdateElementLevel(currentDoc, elem, newLevel);
                }
            });
        }
    }
}
