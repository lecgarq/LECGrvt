using FluentAssertions;
using LECG.Models;
using LECG.Validation;

namespace LECG.Tests.Validation;

public class ValidationRulesTests
{
    [Fact]
    public void TryValidate_rejects_invalid_initialized_window_size()
    {
        var settings = new WindowSettings { IsInitialized = true, Width = 0, Height = double.NaN };

        bool valid = ValidationRules.TryValidate(settings, out string message);

        valid.Should().BeFalse();
        message.Should().Contain("Window width must be a positive number.");
        message.Should().Contain("Window height must be a positive number.");
    }

    [Fact]
    public void TryValidate_rejects_empty_purge_selection()
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

        ValidationRules.TryValidate(settings, out string message).Should().BeFalse();
        message.Should().Be("Select at least one purge target or enable Deep Purge.");
    }
}
