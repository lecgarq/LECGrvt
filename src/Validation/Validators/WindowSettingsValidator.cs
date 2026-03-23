using FluentValidation;
using LECG.Models;

namespace LECG.Validation.Validators
{
    public sealed class WindowSettingsValidator : AbstractValidator<WindowSettings>
    {
        public WindowSettingsValidator()
        {
            When(settings => settings.IsInitialized, () =>
            {
                RuleFor(settings => settings.Width)
                    .Must(value => double.IsFinite(value) && value > 0)
                    .WithMessage("Window width must be a positive number.");

                RuleFor(settings => settings.Height)
                    .Must(value => double.IsFinite(value) && value > 0)
                    .WithMessage("Window height must be a positive number.");
            });
        }
    }
}
