using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadPlacementViewService
    {
        View? ResolvePlacementView(Document doc, View? preferredView);
    }
}
