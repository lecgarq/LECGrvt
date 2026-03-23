namespace LECG.Validation
{
    public interface IValidationService
    {
        bool TryValidate(object instance, out string message);
    }
}
