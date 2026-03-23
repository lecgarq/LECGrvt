using System;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;

namespace LECG.Validation
{
    public sealed class ValidationService : IValidationService
    {
        private readonly IServiceProvider _serviceProvider;

        public ValidationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public bool TryValidate(object instance, out string message)
        {
            ArgumentNullException.ThrowIfNull(instance);

            Type validatorType = typeof(IValidator<>).MakeGenericType(instance.GetType());
            if (_serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                message = string.Empty;
                return true;
            }

            ValidationResult result = validator.Validate(new ValidationContext<object>(instance));
            message = result.IsValid
                ? string.Empty
                : string.Join(Environment.NewLine, result.Errors.Select(error => error.ErrorMessage).Distinct(StringComparer.Ordinal));

            return result.IsValid;
        }
    }
}
