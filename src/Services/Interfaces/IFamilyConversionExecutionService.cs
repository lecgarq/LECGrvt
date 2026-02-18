using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IFamilyConversionExecutionService
    {
        (Document? targetFamilyDoc, string tempFamilyPath) Execute(Document doc, Document sourceFamilyDoc, string templatePath, string targetFamilyName);
    }
}
