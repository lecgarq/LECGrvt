using System;
using Microsoft.Extensions.DependencyInjection;

namespace LECG.Core
{
    /// <summary>
    /// Provides static access to the Service Provider.
    /// This is necessary because Revit instantiates Commands via reflection using a parameterless constructor.
    /// </summary>
    public static class ServiceLocator
    {
        public static IServiceProvider? ServiceProvider { get; private set; }

        public static void Initialize(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public static T? GetService<T>()
        {
            return (T?)ServiceProvider?.GetService(typeof(T));
        }

        public static T GetRequiredService<T>() where T : notnull
        {
            if (ServiceProvider == null)
            {
                throw new InvalidOperationException("ServiceLocator has not been initialized.");
            }
            var service = ServiceProvider.GetService(typeof(T));
            if (service == null)
            {
                throw new InvalidOperationException($"Service of type {typeof(T).Name} could not be resolved.");
            }
            return (T)service;
        }

        public static T CreateWith<T>(params object[] args) where T : notnull
        {
            if (ServiceProvider == null)
            {
                throw new InvalidOperationException("ServiceLocator has not been initialized.");
            }

            return ActivatorUtilities.CreateInstance<T>(ServiceProvider, args);
        }
    }
}
