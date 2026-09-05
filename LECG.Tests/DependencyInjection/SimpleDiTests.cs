using FluentAssertions;

namespace LECG.Tests.DependencyInjection;

public class SimpleDiTests
{
    [Fact]
    public void BuildServiceProvider_UsesOptionalDefault_WhenStringDependencyIsNotRegistered()
    {
        Type serviceCollectionType = Type.GetType("Microsoft.Extensions.DependencyInjection.ServiceCollection, LECG", throwOnError: true)!;
        object services = Activator.CreateInstance(serviceCollectionType)!;

        Type extensionsType = Type.GetType("Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions, LECG", throwOnError: true)!;
        extensionsType
            .GetMethods()
            .Single(method =>
                method.Name == "AddSingleton"
                && method.IsGenericMethodDefinition
                && method.GetGenericArguments().Length == 2
                && method.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(IOptionalStringDependencyService), typeof(OptionalStringDependencyService))
            .Invoke(null, new[] { services });

        Type builderExtensionsType = Type.GetType("Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions, LECG", throwOnError: true)!;
        object provider = builderExtensionsType
            .GetMethod("BuildServiceProvider")!
            .Invoke(null, new[] { services })!;

        object? resolved = provider.GetType()
            .GetMethod("GetService")!
            .Invoke(provider, new object[] { typeof(IOptionalStringDependencyService) });

        resolved.Should().BeOfType<OptionalStringDependencyService>();
        ((OptionalStringDependencyService)resolved!).SettingsPath.Should().BeNull();
    }

    private interface IOptionalStringDependencyService
    {
    }

    private sealed class OptionalStringDependencyService : IOptionalStringDependencyService
    {
        public OptionalStringDependencyService(string? settingsPath = null)
        {
            SettingsPath = settingsPath;
        }

        public string? SettingsPath { get; }
    }
}
