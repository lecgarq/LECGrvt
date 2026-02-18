using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadImportInstanceCenterService
    {
        XYZ GetCenter(ImportInstance importInstance);
    }
}
