using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialElementGroupingService : IMaterialElementGroupingService
    {
        public Dictionary<ElementId, List<Element>> GroupByType(IList<Element> elements)
        {
            ArgumentNullException.ThrowIfNull(elements);

            Dictionary<ElementId, List<Element>> elementsByType = new Dictionary<ElementId, List<Element>>();

            foreach (Element el in elements)
            {
                ElementId typeId = el.GetTypeId();
                if (typeId == ElementId.InvalidElementId) continue;

                if (!elementsByType.ContainsKey(typeId))
                {
                    elementsByType[typeId] = new List<Element>();
                }
                elementsByType[typeId].Add(el);
            }

            return elementsByType;
        }
    }
}
