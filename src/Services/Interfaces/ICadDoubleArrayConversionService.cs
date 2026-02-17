using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface ICadDoubleArrayConversionService
    {
        IList<double> ToList(DoubleArray values);
    }
}
