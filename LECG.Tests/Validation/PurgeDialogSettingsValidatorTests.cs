using FluentValidation.Results;
using LECG.Models;
using LECG.Validation.Validators;
using FluentAssertions;

namespace LECG.Tests.Validation;

public class PurgeDialogSettingsValidatorTests
{
    [Fact]
    public void Validate_rejects_empty_safe_purge_selection()
    {
        var settings = new PurgeDialogSettings
        {
            PurgeLineStyles = false,
            PurgeLinePatterns = false,
            PurgeFillPatterns = false,
            PurgeMaterials = false,
            PurgeLevels = false,
            PurgeParameters = false,
            PurgeGroups = false,
            PurgeGridTypes = false,
            PurgeLevelTypes = false,
            PurgeConstraints = false,
            PurgeUnplacedRooms = false,
            PurgeViewTemplates = false,
            PurgeViewFilters = false,
            IsDeepPurge = false
        };

        ValidationResult result = new PurgeDialogSettingsValidator().Validate(settings);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.ErrorMessage).Should().Contain("Select at least one purge target or enable Deep Purge.");
    }
}
