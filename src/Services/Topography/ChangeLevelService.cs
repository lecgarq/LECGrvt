using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class ChangeLevelService
    {
        private readonly ITransactionService _transactionService;

        public ChangeLevelService(ITransactionService transactionService)
        {
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
                    UpdateElementLevel(currentDoc, elem, newLevel);
                }
            });
        }

        private static void UpdateElementLevel(Document doc, Element elem, Level newLevel)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(elem);
            ArgumentNullException.ThrowIfNull(newLevel);

            Parameter levelParam = elem.get_Parameter(BuiltInParameter.LEVEL_PARAM);
            if (levelParam == null || levelParam.IsReadOnly) return;

            Parameter offsetParam = elem.get_Parameter(BuiltInParameter.TOPOSOLID_HEIGHTABOVELEVEL_PARAM);
            if (offsetParam == null)
            {
                offsetParam = elem.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            }

            if (offsetParam == null || offsetParam.IsReadOnly)
            {
                levelParam.Set(newLevel.Id);
                return;
            }

            ElementId oldLevelId = levelParam.AsElementId();
            Level? oldLevel = doc.GetElement(oldLevelId) as Level;

            if (oldLevel == null)
            {
                levelParam.Set(newLevel.Id);
                return;
            }

            double absoluteElev = oldLevel.Elevation + offsetParam.AsDouble();
            double newOffset = absoluteElev - newLevel.Elevation;

            levelParam.Set(newLevel.Id);
            offsetParam.Set(newOffset);
        }
    }
}
