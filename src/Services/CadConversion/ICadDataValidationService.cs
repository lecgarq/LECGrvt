namespace LECG.Services.Interfaces
{
    public interface ICadDataValidationService
    {
        void EnsureHasGeometry(CadData data, string emptyDataMessage);
    }
}
