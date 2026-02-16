using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IFamilyLoadOptionsFactory
    {
        IFamilyLoadOptions Create();
    }
}
