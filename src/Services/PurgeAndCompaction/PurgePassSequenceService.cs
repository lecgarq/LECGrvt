using System.Collections.Generic;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgePassSequenceService : IPurgePassSequenceService
    {
        public IEnumerable<int> GetPasses(int passCount)
        {
            return LECG.Core.Purge.PurgeSequence.GetPasses(passCount);
        }
    }
}
