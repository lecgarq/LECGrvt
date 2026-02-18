using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IToposolidService
    {
        void UpdateContours(Document doc, ElementId toposolidTypeId, bool enablePrimary, double primaryInterval, bool enableSecondary, double secondaryInterval, bool isApplyMode);
    }
}
