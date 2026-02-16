using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class FamilyGeometryCollectionService : IFamilyGeometryCollectionService
    {
        public List<ElementId> CollectGeometryElementIds(Document sourceFamilyDoc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(sourceFamilyDoc);
            List<ElementId> idsToCopy = new List<ElementId>();

            idsToCopy.AddRange(collector.OfClass(typeof(GenericForm)).ToElementIds());
            idsToCopy.AddRange(new FilteredElementCollector(sourceFamilyDoc).OfClass(typeof(FreeFormElement)).ToElementIds());
            idsToCopy.AddRange(new FilteredElementCollector(sourceFamilyDoc).OfClass(typeof(GeomCombination)).ToElementIds());
            idsToCopy.AddRange(new FilteredElementCollector(sourceFamilyDoc).OfClass(typeof(FamilyInstance)).ToElementIds());

            return idsToCopy;
        }
    }
}
