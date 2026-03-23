using FluentValidation;
using LECG.Models;

namespace LECG.Validation.Validators
{
    public sealed class PurgeDialogSettingsValidator : AbstractValidator<PurgeDialogSettings>
    {
        public PurgeDialogSettingsValidator()
        {
            RuleFor(settings => settings)
                .Must(HasAnySelectedOperation)
                .WithMessage("Select at least one purge target or enable Deep Purge.");
        }

        private static bool HasAnySelectedOperation(PurgeDialogSettings settings)
        {
            return settings.IsDeepPurge
                || settings.PurgeLineStyles
                || settings.PurgeLinePatterns
                || settings.PurgeFillPatterns
                || settings.PurgeMaterials
                || settings.PurgeLevels
                || settings.PurgeParameters
                || settings.PurgeGroups
                || settings.PurgeGridTypes
                || settings.PurgeLevelTypes
                || settings.PurgeConstraints
                || settings.PurgeUnplacedRooms
                || settings.PurgeViewTemplates
                || settings.PurgeViewFilters;
        }
    }
}
