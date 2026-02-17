using System.Collections.Generic;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class PurgePassSequenceService : IPurgePassSequenceService
    {
        public IEnumerable<int> GetPasses()
        {
            for (int i = 1; i <= 3; i++)
            {
                yield return i;
            }
        }
    }
}
