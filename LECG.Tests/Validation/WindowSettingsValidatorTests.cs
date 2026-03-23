using FluentAssertions;
using FluentValidation.Results;
using LECG.Models;
using LECG.Validation.Validators;
using System.Windows;

namespace LECG.Tests.Validation;

public class WindowSettingsValidatorTests
{
    [Fact]
    public void Validate_rejects_non_positive_initialized_window_size()
    {
        var settings = new WindowSettings
        {
            IsInitialized = true,
            Width = 0,
            Height = 100,
            State = WindowState.Normal
        };

        ValidationResult result = new WindowSettingsValidator().Validate(settings);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(error => error.ErrorMessage).Should().Contain("Window width must be a positive number.");
    }
}
