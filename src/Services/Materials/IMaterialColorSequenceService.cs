using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IMaterialColorSequenceService
    {
        Color GetNextColor();
    }
}
