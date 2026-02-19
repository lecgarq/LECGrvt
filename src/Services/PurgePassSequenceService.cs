using System.Collections.Generic;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgePassSequenceService : IPurgePassSequenceService
    {
        public IEnumerable<int> GetPasses(int passCount)
        {
            for (int i = 1; i <= passCount; i++)
            {
                yield return i;
            }
        }
    }
}
