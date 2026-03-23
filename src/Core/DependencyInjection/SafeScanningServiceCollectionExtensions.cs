using System.Reflection;
using FluentValidation;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class SafeScanningServiceCollectionExtensions
    {
        public static IServiceCollection AddValidatorsFromAssemblyContaining<TMarker>(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            Assembly assembly = typeof(TMarker).Assembly;
            Type openValidatorType = typeof(IValidator<>);

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(type => type != null).Cast<Type>().ToArray();
            }

            foreach (Type implementationType in types)
            {
                if (!implementationType.IsClass || implementationType.IsAbstract)
                {
                    continue;
                }

                Type[] serviceTypes = implementationType
                    .GetInterfaces()
                    .Where(serviceType => serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == openValidatorType)
                    .ToArray();

                if (serviceTypes.Length == 0)
                {
                    continue;
                }

                foreach (Type serviceType in serviceTypes)
                {
                    bool alreadyRegistered = services.Any(descriptor =>
                        descriptor.ServiceType == serviceType &&
                        descriptor.ImplementationType == implementationType);

                    if (!alreadyRegistered)
                    {
                        services.Add(new ServiceDescriptor(serviceType, implementationType, ServiceLifetime.Singleton));
                    }
                }
            }

            return services;
        }
    }
}
