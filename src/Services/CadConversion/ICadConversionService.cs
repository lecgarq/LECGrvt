using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface ICadConversionService
    {
        ElementId ConvertCadToFamily(
            Document doc,
            ImportInstance cadInstance,
            string familyName,
            string templatePath,
            string lineStyleName,
            Color lineColor,
            int lineWeight,
            IProgressReporter reporter);

        ElementId ConvertDwgToFamily(
            Document doc,
            string dwgPath,
            string familyName,
            string templatePath,
            string lineStyleName,
            Color lineColor,
            int lineWeight,
            IProgressReporter reporter);

        string GetDefaultTemplatePath();
    }
}
