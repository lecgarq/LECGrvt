namespace LECG.Core.Naming
{
    public static class FamilySelectionPolicy
    {
        public static bool IsSafeToConvert(bool isWorkPlaneBased)
        {
            // Work-plane based families are currently blocked due to complex coordinate issues
            return !isWorkPlaneBased;
        }
    }
}
