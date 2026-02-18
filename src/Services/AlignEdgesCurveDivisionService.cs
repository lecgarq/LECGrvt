using LECG.Services.Interfaces;
using System;
using System.Collections.Generic;

namespace LECG.Services
{
    public class AlignEdgesCurveDivisionService : IAlignEdgesCurveDivisionService
    {
        public IReadOnlyList<double> GetInteriorParameters(double length, double minSpacing, double maxSpacing)
        {
            List<double> values = new List<double>();

            if (length <= minSpacing)
            {
                return values;
            }

            int divisions = (int)Math.Ceiling(length / maxSpacing);
            divisions = Math.Max(divisions, 2);

            for (int j = 1; j < divisions; j++)
            {
                values.Add((double)j / divisions);
            }

            return values;
        }
    }
}
