using System.Collections.Generic;

namespace LECG.Services.Interfaces
{
    public interface IPurgePassSequenceService
    {
        IEnumerable<int> GetPasses();
    }
}
