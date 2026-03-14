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
            idsToCopy.AddRange(new FilteredElementCollector(sourceFamilyDoc).OfClass(typeof(ReferencePlane)).ToElementIds());

            // Collect dimensions (parametric constraints linking geometry to reference planes)
            idsToCopy.AddRange(new FilteredElementCollector(sourceFamilyDoc).OfClass(typeof(Dimension)).ToElementIds());

            // Safely collect curves, explicitly avoiding internal SketchLines which crash geometry transfer
            var curves = new FilteredElementCollector(sourceFamilyDoc).OfClass(typeof(CurveElement)).ToElements();
            foreach (var element in curves)
            {
                if (element.Category != null && element.Category.Id.Value == (long)BuiltInCategory.OST_SketchLines)
                {
                    continue; // Skip sketch lines belonging to extrusions/sweeps
                }
                idsToCopy.Add(element.Id);
            }

            return idsToCopy;
        }
    }
}
