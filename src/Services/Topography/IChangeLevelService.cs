using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface IChangeLevelService
    {
        void ChangeLevel(Document doc, IEnumerable<Element> elements, Level newLevel);
        List<Level> GetLevels(Document doc);
    }
}
