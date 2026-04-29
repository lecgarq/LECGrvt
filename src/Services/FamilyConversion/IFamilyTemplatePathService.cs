using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IFamilyTemplatePathService
    {
        string GetTargetTemplatePath(Application app, Category category);
    }
}
