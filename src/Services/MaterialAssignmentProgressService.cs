using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialAssignmentProgressService : IMaterialAssignmentProgressService
    {
        public double ToProgressPercent(int processedTypes, int totalTypes)
        {
            return 20 + (processedTypes * 70.0 / totalTypes);
        }
    }
}
