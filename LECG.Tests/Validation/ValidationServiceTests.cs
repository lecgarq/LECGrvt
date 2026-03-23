using FluentValidation;
using FluentValidation.Results;
using LECG.Models;
using LECG.Validation;
using FluentAssertions;
using NSubstitute;

namespace LECG.Tests.Validation;

public class ValidationServiceTests
{
    [Fact]
    public void TryValidate_returns_messages_from_registered_validator()
    {
        var validator = Substitute.For<IValidator<PurgeDialogSettings>>();
        validator.Validate(Arg.Any<IValidationContext>())
            .Returns(new ValidationResult(new[]
            {
                new ValidationFailure(nameof(PurgeDialogSettings.IsDeepPurge), "Select at least one purge target or enable Deep Purge.")
            }));

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IValidator<PurgeDialogSettings>)).Returns(validator);

        var service = new ValidationService(serviceProvider);

        bool isValid = service.TryValidate(new PurgeDialogSettings(), out string message);

        isValid.Should().BeFalse();
        message.Should().Contain("Select at least one purge target or enable Deep Purge.");
    }
}
