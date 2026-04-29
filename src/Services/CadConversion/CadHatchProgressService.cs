using LECG.Services.Interfaces;
using System;

namespace LECG.Services
{
    public class CadHatchProgressService : ICadHatchProgressService
    {
        public bool ShouldReport(int total, int current)
        {
            return total > 0 && (current % Math.Max(1, total / 20) == 0 || current == total);
        }

        public double ToPercent(double startPct, double endPct, int current, int total)
        {
            return startPct + (endPct - startPct) * current / total;
        }
    }
}
