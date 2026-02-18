using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class ToposolidBaseElevationService : IToposolidBaseElevationService
    {
        public (double BaseElevation, string DebugMessage) Resolve(Document doc, Toposolid toposolid)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(toposolid);

            double levelElevation = 0;
            Level? level = doc.GetElement(toposolid.LevelId) as Level;
            if (level != null) levelElevation = level.ProjectElevation;

            double heightOffset = 0;
            Parameter heightParam = toposolid.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            if (heightParam != null) heightOffset = heightParam.AsDouble();

            double baseElevation = levelElevation + heightOffset;
            string debugMessage = $"Level={levelElevation:F2} HeightOffset={heightOffset:F2} Base={baseElevation:F2}\n";
            return (baseElevation, debugMessage);
        }
    }
}
