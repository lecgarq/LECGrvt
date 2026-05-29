using System;
using Autodesk.Revit.DB;
using LECG.Core;

namespace LECG.Services.Interfaces
{
    public interface ICadDataDrawService
    {
        void Draw(Document familyDoc, CadData data, XYZ offset, string styleName, Color color, int weight, IProgressReporter reporter, double startPct = 50, double endPct = 90);
    }
}
