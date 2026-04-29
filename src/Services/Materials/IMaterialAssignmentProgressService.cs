namespace LECG.Services.Interfaces
{
    public interface IMaterialAssignmentProgressService
    {
        double ToProgressPercent(int processedTypes, int totalTypes);
    }
}
