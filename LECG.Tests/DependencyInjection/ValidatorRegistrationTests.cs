using FluentValidation;
using LECG.Validation.Validators;
using FluentAssertions;

namespace LECG.Tests.DependencyInjection;

public class ValidatorRegistrationTests
{
    [Fact]
    public void AddValidatorsFromAssemblyContaining_registers_closed_generic_validators()
    {
        Type serviceCollectionType = Type.GetType("Microsoft.Extensions.DependencyInjection.ServiceCollection, LECG", throwOnError: true)!;
        object services = Activator.CreateInstance(serviceCollectionType)!;

        Type scanningExtensionsType = Type.GetType("Microsoft.Extensions.DependencyInjection.SafeScanningServiceCollectionExtensions, LECG", throwOnError: true)!;
        scanningExtensionsType
            .GetMethod("AddValidatorsFromAssemblyContaining")!
            .MakeGenericMethod(typeof(TestWidgetValidator))
            .Invoke(null, new[] { services });

        Type builderExtensionsType = Type.GetType("Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions, LECG", throwOnError: true)!;
        object provider = builderExtensionsType
            .GetMethod("BuildServiceProvider")!
            .Invoke(null, new[] { services })!;

        object? validator = provider.GetType()
            .GetMethod("GetService")!
            .Invoke(provider, new object[] { typeof(IValidator<TestWidget>) });

        validator.Should().BeOfType<TestWidgetValidator>();
    }

    private sealed record TestWidget(string Name);

    private sealed class TestWidgetValidator : AbstractValidator<TestWidget>
    {
        public TestWidgetValidator()
        {
            RuleFor(widget => widget.Name).NotEmpty();
        }
    }
}
