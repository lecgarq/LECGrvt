using Autodesk.Revit.DB;
using System;

namespace LECG.Services.Interfaces
{
    public interface ICadImportFamilyCreationService
    {
        ElementId CreateAndLoad(Document doc, CadData data, XYZ center, ElementId originalCadElementId, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, Action<double, string>? progress = null);
    }
}
