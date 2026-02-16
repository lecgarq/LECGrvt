using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace LECG.Services
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
            System.Action<double, string>? progress = null);
            
        ElementId ConvertDwgToFamily(
            Document doc,
            string dwgPath,
            string familyName,
            string templatePath,
            string lineStyleName,
            Color lineColor,
            int lineWeight,
            System.Action<double, string>? progress = null);

        string GetDefaultTemplatePath();
    }
}
