using Autodesk.Revit.DB;
using System;

namespace LECG.Services.Interfaces
{
    public interface ICadDwgFamilyCreationService
    {
        ElementId CreateAndLoad(Document doc, CadData data, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, Action<double, string>? progress = null);
    }
}
