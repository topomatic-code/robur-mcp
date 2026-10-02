using System;
using System.Collections.Generic;
using System.Reflection;

namespace Topomatic.ToolBridge.DependencyInjection
{
    public sealed class Container : IContainer
    {
        private readonly Dictionary<Type, Registration> m_RegisteredTypes;
        private readonly Dictionary<Type, Registration> m_Singletons;
        private readonly object m_SyncRoot;
        // Accessed only under m_SyncRoot. Reentrant factory calls share the current path.
        private readonly List<ResolutionStep> m_ResolutionPath;

        public Container()
        {
            m_RegisteredTypes = new Dictionary<Type, Registration>();
            m_Singletons = new Dictionary<Type, Registration>();
            m_SyncRoot = new object();
            m_ResolutionPath = new List<ResolutionStep>();
        }

        public T CreateInstance<T>() where T : class
        {
            lock (m_SyncRoot)
            {
                return (T)Resolve(typeof(T), false);
            }
        }

        public T GetSingleton<T>() where T : class
        {
            lock (m_SyncRoot)
            {
                return (T)Resolve(typeof(T), true);
            }
        }

        public void RegisterType<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            ValidateImplementation(typeof(TImplementation));
            Register(typeof(TService), new Registration(typeof(TImplementation)), false);
        }

        public void RegisterSingleton<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            ValidateImplementation(typeof(TImplementation));
            Register(typeof(TService), new Registration(typeof(TImplementation)), true);
        }

        /// <summary>Registers a factory invoked on every request for a new instance.</summary>
        public void RegisterType<TService>(Func<IContainer, TService> factory) where TService : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            Register(typeof(TService), new Registration(container => factory(container)), false);
        }

        public void RegisterType<TService>(Func<TService> factory) where TService : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            RegisterType<TService>(container => factory());
        }

        /// <summary>Registers a factory whose first successful result is cached.</summary>
        public void RegisterSingleton<TService>(Func<IContainer, TService> factory) where TService : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            Register(typeof(TService), new Registration(container => factory(container)), true);
        }

        public void RegisterSingleton<TService>(Func<TService> factory) where TService : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            RegisterSingleton<TService>(container => factory());
        }

        private void Register(Type serviceType, Registration registration, bool singleton)
        {
            lock (m_SyncRoot)
            {
                var registrations = singleton ? m_Singletons : m_RegisteredTypes;

                if (registrations.ContainsKey(serviceType))
                    throw new ContainerException($"{GetLifetime(singleton)} registration for service type '{serviceType}' already exists.");

                registrations.Add(serviceType, registration);
            }
        }

        private object Resolve(Type serviceType, bool singleton, string parameter = null)
        {
            var step = new ResolutionStep(serviceType, singleton, parameter);
            var circular = m_ResolutionPath.Exists(item => item.ServiceType == serviceType && item.Singleton == singleton);
            m_ResolutionPath.Add(step);
            try
            {
                var registrations = singleton ? m_Singletons : m_RegisteredTypes;
                Registration registration;
                if (!registrations.TryGetValue(serviceType, out registration))
                    throw CreateResolutionException($"{GetLifetime(singleton)} registration for service type '{serviceType}' is missing.");

                step.ImplementationType = registration.ImplementationType;

                if (singleton && registration.Instance != null)
                    return registration.Instance;

                if (circular)
                    throw CreateResolutionException($"Circular dependency detected for '{serviceType}'.");

                var instance = registration.Factory != null
                    ? InvokeFactory(registration.Factory)
                    : CreateInstance(registration.ImplementationType);

                if (instance == null)
                    throw CreateResolutionException($"Factory for '{serviceType}' returned null.");

                if (singleton)
                    registration.Instance = instance;

                return instance;
            }
            catch (ContainerException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw CreateResolutionException($"Failed to resolve '{serviceType}': {exception.Message}", exception);
            }
            finally
            {
                m_ResolutionPath.RemoveAt(m_ResolutionPath.Count - 1);
            }
        }

        private object InvokeFactory(Func<IContainer, object> factory)
        {
            try
            {
                return factory(this);
            }
            catch (Exception exception)
            {
                throw CreateResolutionException($"Factory failed: {exception.Message}", exception);
            }
        }

        private object CreateInstance(Type implementationType)
        {
            var constructor = GetConstructor(implementationType);
            var parameters = constructor.GetParameters();
            var arguments = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                arguments[i] = Resolve(parameter.ParameterType,
                    parameter.IsDefined(typeof(SingletonAttribute), false), parameter.Name);
            }
            try
            {
                return constructor.Invoke(arguments);
            }
            catch (TargetInvocationException exception)
            {
                var cause = exception.InnerException ?? exception;
                throw CreateResolutionException($"Constructor of '{implementationType}' failed: {cause.Message}", cause);
            }
        }

        private ContainerException CreateResolutionException(string message, Exception innerException = null)
        {
            var path = m_ResolutionPath.ConvertAll(step =>
                $"{step.ServiceType} [{GetLifetime(step.Singleton)}]" +
                (step.ImplementationType == null ? "" : $" (implementation: {step.ImplementationType})") +
                (step.Parameter == null ? "" : $" (parameter: {step.Parameter})"));
            return new ContainerException(message + " Resolution path: " + string.Join(" -> ", path) + ".", innerException);
        }

        private static string GetLifetime(bool singleton) => singleton ? "Singleton" : "Transient";

        // Choose the unique public constructor with the most parameters.
        // Every parameter must be registered, including optional parameters.
        private ConstructorInfo GetConstructor(Type implementationType)
        {
            ConstructorInfo selected = null;
            var parameterCount = -1;
            var ambiguous = false;
            foreach (var constructor in implementationType.GetConstructors())
            {
                var count = constructor.GetParameters().Length;
                if (count > parameterCount)
                {
                    selected = constructor;
                    parameterCount = count;
                    ambiguous = false;
                }
                else if (count == parameterCount)
                {
                    ambiguous = true;
                }
            }

            if (selected == null)
                throw CreateResolutionException($"Type '{implementationType}' has no public constructor.");

            if (ambiguous)
                throw CreateResolutionException($"Type '{implementationType}' has multiple public constructors with {parameterCount} parameters.");

            return selected;
        }

        private static void ValidateImplementation(Type implementationType)
        {
            if (!implementationType.IsClass || implementationType.IsAbstract || implementationType.ContainsGenericParameters)
                throw new ArgumentException($"Type '{implementationType}' must be a concrete class.");
        }

        private sealed class Registration
        {
            public readonly Type ImplementationType;
            public readonly Func<IContainer, object> Factory;
            public object Instance;

            public Registration(Type implementationType)
            {
                ImplementationType = implementationType;
            }

            public Registration(Func<IContainer, object> factory)
            {
                Factory = factory;
            }
        }

        private sealed class ResolutionStep
        {
            public readonly Type ServiceType;
            public readonly bool Singleton;
            public readonly string Parameter;
            public Type ImplementationType;

            public ResolutionStep(Type serviceType, bool singleton, string parameter)
            {
                ServiceType = serviceType;
                Singleton = singleton;
                Parameter = parameter;
            }
        }
    }
}
