using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadPointFlattenService
    {
        public XYZ Flatten(XYZ p)
        {
            ArgumentNullException.ThrowIfNull(p);

            return new XYZ(p.X, p.Y, 0);
        }
    }
}
