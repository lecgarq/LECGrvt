using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadImportInstanceCenterService : ICadImportInstanceCenterService
    {
        public XYZ GetCenter(ImportInstance importInstance)
        {
            ArgumentNullException.ThrowIfNull(importInstance);

            BoundingBoxXYZ box = importInstance.get_BoundingBox(null);
            return (box.Min + box.Max) * 0.5;
        }
    }
}
