using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class ChangeLevelElementUpdateService : IChangeLevelElementUpdateService
    {
        public void UpdateElementLevel(Document doc, Element elem, Level newLevel)
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

            double currentOffset = offsetParam.AsDouble();
            double oldLevelElev = oldLevel.Elevation;
            double absoluteElev = oldLevelElev + currentOffset;
            double newLevelElev = newLevel.Elevation;
            double newOffset = absoluteElev - newLevelElev;

            levelParam.Set(newLevel.Id);
            offsetParam.Set(newOffset);
        }
    }
}
