using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IImageColorExtractionService
    {
        Color GetAverageColor(string imagePath);
    }
}
