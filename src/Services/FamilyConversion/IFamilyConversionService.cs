using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface IFamilyConversionService
    {
        void ConvertFamilyBatch(Document doc, IEnumerable<FamilyInstance> instances, string customName, string templatePath, bool isTemporary, bool replaceInPlace, IProgressReporter? reporter = null);
    }
}
