using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IBaseElementCollectionService
    {
        List<ElementData> CollectBaseElements(Document doc, bool types, bool families, bool views, bool sheets);
    }
}
