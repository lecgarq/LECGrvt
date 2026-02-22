using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
{
    public enum ServiceLifetime
    {
        Singleton,
        Scoped,
        Transient
    }

    public sealed class ServiceDescriptor
    {
        public Type ServiceType { get; }
        public Type? ImplementationType { get; }
        public Func<IServiceProvider, object>? ImplementationFactory { get; }
        public object? ImplementationInstance { get; }
        public ServiceLifetime Lifetime { get; }

        public ServiceDescriptor(Type serviceType, Type implementationType, ServiceLifetime lifetime)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Lifetime = lifetime;
        }

        public ServiceDescriptor(Type serviceType, Func<IServiceProvider, object> implementationFactory, ServiceLifetime lifetime)
        {
            ServiceType = serviceType;
            ImplementationFactory = implementationFactory;
            Lifetime = lifetime;
        }

        public ServiceDescriptor(Type serviceType, object implementationInstance)
        {
            ServiceType = serviceType;
            ImplementationInstance = implementationInstance;
            Lifetime = ServiceLifetime.Singleton;
        }
    }

    public interface IServiceCollection : IList<ServiceDescriptor>
    {
    }

    public sealed class ServiceCollection : List<ServiceDescriptor>, IServiceCollection
    {
    }

    public static class ServiceCollectionServiceExtensions
    {
        public static IServiceCollection AddSingleton<TService, TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService
        {
            services.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Singleton));
            return services;
        }

        public static IServiceCollection AddSingleton<TService>(this IServiceCollection services)
            where TService : class
        {
            services.Add(new ServiceDescriptor(typeof(TService), typeof(TService), ServiceLifetime.Singleton));
            return services;
        }

        public static IServiceCollection AddSingleton<TService>(this IServiceCollection services, Func<IServiceProvider, TService> factory)
            where TService : class
        {
            services.Add(new ServiceDescriptor(typeof(TService), sp => factory(sp), ServiceLifetime.Singleton));
            return services;
        }

        public static IServiceCollection AddTransient<TService, TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, TService
        {
            services.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Transient));
            return services;
        }

        public static IServiceCollection AddTransient<TService>(this IServiceCollection services)
            where TService : class
        {
            services.Add(new ServiceDescriptor(typeof(TService), typeof(TService), ServiceLifetime.Transient));
            return services;
        }
    }

    public static class ServiceCollectionContainerBuilderExtensions
    {
        public static IServiceProvider BuildServiceProvider(this IServiceCollection services)
        {
            return new SimpleServiceProvider(services);
        }
    }

    public static class ActivatorUtilities
    {
        public static T CreateInstance<T>(IServiceProvider provider, params object[] parameters)
        {
            if (provider is SimpleServiceProvider simpleProvider)
            {
                return (T)simpleProvider.CreateInstance(typeof(T), parameters, new HashSet<Type>());
            }

            return (T)SimpleServiceProvider.CreateInstance(typeof(T), provider, parameters, new HashSet<Type>());
        }
    }

    internal sealed class SimpleServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, List<ServiceDescriptor>> _descriptorsByServiceType;
        private readonly Dictionary<Type, object> _singletonInstances = new();
        private readonly object _syncRoot = new();

        public SimpleServiceProvider(IEnumerable<ServiceDescriptor> serviceDescriptors)
        {
            _descriptorsByServiceType = serviceDescriptors
                .GroupBy(sd => sd.ServiceType)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        public object? GetService(Type serviceType)
        {
            return ResolveService(serviceType, new HashSet<Type>());
        }

        internal object CreateInstance(Type implementationType, object[] explicitArgs, HashSet<Type> callStack)
        {
            if (!callStack.Add(implementationType))
            {
                throw new InvalidOperationException($"Circular dependency detected for type '{implementationType.FullName}'.");
            }

            try
            {
                return CreateInstance(implementationType, this, explicitArgs, callStack);
            }
            finally
            {
                callStack.Remove(implementationType);
            }
        }

        internal object? ResolveService(Type serviceType, HashSet<Type> callStack)
        {
            if (serviceType == typeof(IServiceProvider))
            {
                return this;
            }

            if (_descriptorsByServiceType.TryGetValue(serviceType, out List<ServiceDescriptor>? descriptors))
            {
                ServiceDescriptor descriptor = descriptors[descriptors.Count - 1];
                return ResolveDescriptor(descriptor, callStack);
            }

            if (!serviceType.IsAbstract && !serviceType.IsInterface)
            {
                return CreateInstance(serviceType, Array.Empty<object>(), callStack);
            }

            return null;
        }

        private object ResolveDescriptor(ServiceDescriptor descriptor, HashSet<Type> callStack)
        {
            if (descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                lock (_syncRoot)
                {
                    if (_singletonInstances.TryGetValue(descriptor.ServiceType, out object? cached))
                    {
                        return cached;
                    }

                    object created = CreateFromDescriptor(descriptor, callStack);
                    _singletonInstances[descriptor.ServiceType] = created;
                    return created;
                }
            }

            return CreateFromDescriptor(descriptor, callStack);
        }

        private object CreateFromDescriptor(ServiceDescriptor descriptor, HashSet<Type> callStack)
        {
            if (descriptor.ImplementationInstance != null)
            {
                return descriptor.ImplementationInstance;
            }

            if (descriptor.ImplementationFactory != null)
            {
                return descriptor.ImplementationFactory(this);
            }

            if (descriptor.ImplementationType == null)
            {
                throw new InvalidOperationException($"No implementation configured for service '{descriptor.ServiceType.FullName}'.");
            }

            return CreateInstance(descriptor.ImplementationType, Array.Empty<object>(), callStack);
        }

        internal static object CreateInstance(Type implementationType, IServiceProvider provider, object[] explicitArgs, HashSet<Type> callStack)
        {
            ConstructorInfo[] constructors = implementationType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                .OrderByDescending(c => c.GetParameters().Length)
                .ToArray();

            foreach (ConstructorInfo ctor in constructors)
            {
                if (TryBuildConstructorArgs(ctor, provider, explicitArgs, callStack, out object[] resolvedArgs))
                {
                    return ctor.Invoke(resolvedArgs);
                }
            }

            throw new InvalidOperationException($"Unable to create instance of type '{implementationType.FullName}'.");
        }

        private static bool TryBuildConstructorArgs(
            ConstructorInfo constructor,
            IServiceProvider provider,
            object[] explicitArgs,
            HashSet<Type> callStack,
            out object[] resolvedArgs)
        {
            ParameterInfo[] parameters = constructor.GetParameters();
            resolvedArgs = new object[parameters.Length];
            bool[] usedExplicitArgs = new bool[explicitArgs.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];

                if (TryTakeExplicitArg(parameter.ParameterType, explicitArgs, usedExplicitArgs, out object? explicitValue))
                {
                    resolvedArgs[i] = explicitValue!;
                    continue;
                }

                object? resolved = ResolveFromProvider(provider, parameter.ParameterType, callStack);
                if (resolved != null)
                {
                    resolvedArgs[i] = resolved;
                    continue;
                }

                if (parameter.HasDefaultValue)
                {
                    resolvedArgs[i] = parameter.DefaultValue!;
                    continue;
                }

                return false;
            }

            return true;
        }

        private static bool TryTakeExplicitArg(Type parameterType, object[] explicitArgs, bool[] usedExplicitArgs, out object? value)
        {
            for (int i = 0; i < explicitArgs.Length; i++)
            {
                if (usedExplicitArgs[i])
                {
                    continue;
                }

                object arg = explicitArgs[i];
                if (parameterType.IsInstanceOfType(arg))
                {
                    usedExplicitArgs[i] = true;
                    value = arg;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static object? ResolveFromProvider(IServiceProvider provider, Type type, HashSet<Type> callStack)
        {
            if (provider is SimpleServiceProvider simpleProvider)
            {
                return simpleProvider.ResolveService(type, callStack);
            }

            return provider.GetService(type);
        }
    }
}
