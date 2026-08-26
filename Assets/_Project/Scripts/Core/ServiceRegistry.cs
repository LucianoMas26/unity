using System;
using System.Collections.Generic;

namespace Survival.Core
{
    /// <summary>
    /// Minimal typed registry, so independent assemblies can talk through interfaces defined
    /// in Core without referencing each other. Player asks for <c>ITerrainSampler</c>; it never
    /// needs to know that World is the one answering.
    /// <para>Register in OnEnable, unregister in OnDisable. Nothing survives a scene change.</para>
    /// </summary>
    public static class ServiceRegistry
    {
        static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) return;
            Services[typeof(T)] = service;
        }

        public static void Unregister<T>(T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out var existing) && ReferenceEquals(existing, service))
                Services.Remove(typeof(T));
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out var found))
            {
                service = found as T;
                return service != null;
            }

            service = null;
            return false;
        }

        public static T Get<T>() where T : class => TryGet<T>(out var service) ? service : null;

        public static void Clear() => Services.Clear();
    }
}
