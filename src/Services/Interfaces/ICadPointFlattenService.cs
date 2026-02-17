using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadPointFlattenService
    {
        XYZ Flatten(XYZ p);
    }
}
