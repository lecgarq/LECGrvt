using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadDrawingViewService
    {
        View ResolveFamilyDrawingView(Document familyDoc);
    }
}
