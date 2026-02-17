using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadPolylineExtractionService
    {
        List<Curve> Extract(PolyLine poly, Transform currentTransform);
    }
}
