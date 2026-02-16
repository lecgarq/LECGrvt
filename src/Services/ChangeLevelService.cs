using Autodesk.Revit.DB;
using LECG.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class ChangeLevelService : IChangeLevelService
    {
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
            if (newLevel == null) return;

            using (Transaction t = new Transaction(doc, "Change Element Level"))
            {
                t.Start();
                
                foreach (var elem in elements)
                {
                    Parameter levelParam = elem.get_Parameter(BuiltInParameter.LEVEL_PARAM);
                    if (levelParam == null || levelParam.IsReadOnly) continue;

                    // Try to handle Toposolid or Floor specific offset params
                    Parameter offsetParam = elem.get_Parameter(BuiltInParameter.TOPOSOLID_HEIGHTABOVELEVEL_PARAM);
                    if (offsetParam == null)
                    {
                        // Fallback to generic Level Offset (e.g. Floors)
                        offsetParam = elem.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                    }
                    
                    if (offsetParam == null || offsetParam.IsReadOnly)
                    {
                        // Some elements might just be level-hosted without offset, or different param
                        // Use simple level change if offset not found/writable
                        levelParam.Set(newLevel.Id);
                        continue;
                    }

                    // Get current state to maintain absolute elevation
                    ElementId oldLevelId = levelParam.AsElementId();
                    Level? oldLevel = doc.GetElement(oldLevelId) as Level;
                    
                    // If no valid old level, treat as just setting new level
                    if (oldLevel == null)
                    {
                        levelParam.Set(newLevel.Id);
                        continue;
                    }

                    double currentOffset = offsetParam.AsDouble();
                    double oldLevelElev = oldLevel.Elevation;
                    double absoluteElev = oldLevelElev + currentOffset;

                    // Calculate new offset
                    // ABS = newLevelElev + newOffset  =>  newOffset = ABS - newLevelElev
                    double newLevelElev = newLevel.Elevation;
                    double newOffset = absoluteElev - newLevelElev;

                    // Set Level
                    levelParam.Set(newLevel.Id);
                    
                    // Set Offset
                    offsetParam.Set(newOffset);
                }

                t.Commit();
            }
        }
    }
}
