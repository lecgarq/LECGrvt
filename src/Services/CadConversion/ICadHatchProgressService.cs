namespace LECG.Services.Interfaces
{
    public interface ICadHatchProgressService
    {
        bool ShouldReport(int total, int current);
        double ToPercent(double startPct, double endPct, int current, int total);
    }
}
