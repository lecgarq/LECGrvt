using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RenderSolidFillPatternService : IRenderSolidFillPatternService
    {
        public ElementId GetSolidFillPatternId(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement));
            foreach (FillPatternElement fpe in collector.Cast<FillPatternElement>())
            {
                FillPattern fp = fpe.GetFillPattern();
                if (fp != null && fp.IsSolidFill) return fpe.Id;
            }

            FillPattern solidPattern = new FillPattern("Solid Fill", FillPatternTarget.Drafting, FillPatternHostOrientation.ToHost);
            return FillPatternElement.Create(doc, solidPattern).Id;
        }
    }
}
