using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface IAlignEdgesCurveDivisionService
    {
        IReadOnlyList<double> GetInteriorParameters(double length, double minSpacing, double maxSpacing);
    }
}
