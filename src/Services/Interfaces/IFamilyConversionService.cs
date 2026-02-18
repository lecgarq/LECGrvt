using Autodesk.Revit.DB;
using System.Threading.Tasks;

namespace LECG.Services.Interfaces
{
    public interface IFamilyConversionService
    {
        void ConvertFamily(Document doc, FamilyInstance instance, string name, string templatePath, bool isTemporary);
        string GetTargetTemplatePath(Autodesk.Revit.ApplicationServices.Application app, Category category);
    }
}
