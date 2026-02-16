using LECG.Core;

namespace LECG.Services.Interfaces
{
    public interface ICadGeometryOptimizationService
    {
        CadData Optimize(CadData input);
    }
}
