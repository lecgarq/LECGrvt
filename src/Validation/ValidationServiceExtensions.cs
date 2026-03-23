using LECG.Views.Base;

namespace LECG.Validation
{
    public static class ValidationServiceExtensions
    {
        public static bool TryValidateAndShow(this IValidationService validationService, object instance, string title)
        {
            ArgumentNullException.ThrowIfNull(validationService);
            ArgumentNullException.ThrowIfNull(instance);

            if (validationService.TryValidate(instance, out string message))
            {
                return true;
            }

            LecgDialog.Show(title, message);
            return false;
        }
    }
}
