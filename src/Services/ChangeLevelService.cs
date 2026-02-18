using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class ChangeLevelService : IChangeLevelService
    {
        private readonly IChangeLevelElementUpdateService _changeLevelElementUpdateService;

        public ChangeLevelService() : this(new ChangeLevelElementUpdateService())
        {
        }

        public ChangeLevelService(IChangeLevelElementUpdateService changeLevelElementUpdateService)
        {
            _changeLevelElementUpdateService = changeLevelElementUpdateService;
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

            using (Transaction t = new Transaction(doc, "Change Element Level"))
            {
                t.Start();
                
                foreach (var elem in elements)
                {
                    _changeLevelElementUpdateService.UpdateElementLevel(doc, elem, newLevel);
                }

                t.Commit();
            }
        }
    }
}
