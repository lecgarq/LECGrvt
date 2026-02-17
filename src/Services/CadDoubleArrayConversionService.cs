using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadDoubleArrayConversionService : ICadDoubleArrayConversionService
    {
        public IList<double> ToList(DoubleArray values)
        {
            var list = new List<double>();
            for (int i = 0; i < values.Size; i++)
            {
                list.Add(values.get_Item(i));
            }

            return list;
        }
    }
}
