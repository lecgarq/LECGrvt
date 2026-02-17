using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadFamilyBuildService
    {
        string BuildAndSave(
            Document projectDoc,
            string templatePath,
            CadData data,
            XYZ offset,
            string lineStyleName,
            Color lineColor,
            int lineWeight,
            string transactionName,
            string familyName,
            Action<double, string>? progress,
            double progressStart,
            double progressEnd);
    }
}
