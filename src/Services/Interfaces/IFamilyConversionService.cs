using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface IFamilyConversionService
    {
        void ConvertFamily(Document doc, FamilyInstance instance, string name, string templatePath, bool isTemporary);
        void ConvertFamilyBatch(Document doc, IEnumerable<FamilyInstance> instances, string customName, string templatePath, bool isTemporary, bool replaceInPlace, IProgressReporter? reporter = null);
        string GetTargetTemplatePath(Autodesk.Revit.ApplicationServices.Application app, Category category);
    }
}
