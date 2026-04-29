using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Core;

namespace LECG.Services.Interfaces
{
    public interface IDivideToposolidService
    {
        void DivideToposolids(Document doc, IList<Element> elements, IProgressReporter reporter);
    }
}
