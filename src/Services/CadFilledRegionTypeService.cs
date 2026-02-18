using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadFilledRegionTypeService : ICadFilledRegionTypeService
    {
        public FilledRegionType? GetOrCreateFilledRegionType(Document doc, Color color)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(color);

            string name = $"Solid_{color.Red}_{color.Green}_{color.Blue}";
            FilledRegionType? existing = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .FirstOrDefault(x => x.Name == name);
            if (existing != null) return existing;

            FilledRegionType? newType = null;
            FilledRegionType? anyType = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .FirstElement() as FilledRegionType;
            if (anyType != null)
            {
                newType = anyType.Duplicate(name) as FilledRegionType;
                if (newType == null) return null;

                newType.ForegroundPatternColor = color;
                newType.BackgroundPatternColor = color;

                FillPatternElement? solidPattern = FillPatternElement.GetFillPatternElementByName(doc, FillPatternTarget.Drafting, "<Solid fill>");
                if (solidPattern == null)
                {
                    solidPattern = new FilteredElementCollector(doc)
                        .OfClass(typeof(FillPatternElement))
                        .Cast<FillPatternElement>()
                        .FirstOrDefault(p => p.GetFillPattern().IsSolidFill);
                }

                if (solidPattern != null)
                {
                    newType.ForegroundPatternId = solidPattern.Id;
                    newType.BackgroundPatternId = solidPattern.Id;
                }
            }

            return newType;
        }
    }
}
