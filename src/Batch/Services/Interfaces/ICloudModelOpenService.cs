using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Batch.Models;

namespace LECG.Batch.Services.Interfaces
{
    public interface ICloudModelOpenService
    {
        Document? Open(UIApplication app, BatchJob job);
    }
}
