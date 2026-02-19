using System.Collections.Generic;

namespace LECG.Core.Purge
{
    public static class PurgeSequence
    {
        public static IEnumerable<int> GetPasses(int passCount)
        {
            for (int i = 1; i <= passCount; i++)
            {
                yield return i;
            }
        }
    }
}
