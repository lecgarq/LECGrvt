using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IFamilyGeometryCollectionService
    {
        List<ElementId> CollectGeometryElementIds(Document sourceFamilyDoc);
    }
}
