using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IChangeLevelElementUpdateService
    {
        void UpdateElementLevel(Document doc, Element elem, Level newLevel);
    }
}
