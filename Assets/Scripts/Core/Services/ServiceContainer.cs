using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Services
{
    public class ServiceContainer
    {
        private readonly Dictionary<Type, IService> _services = new();
        private readonly List<IService> _tickableServices = new();
        private bool _initialized;

        public ServiceContainer Register<T>(T service) where T : class, IService
        {
            var type = typeof(T);
            
            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[Services] Overwriting: {type.Name}");
            }
            
            _services[type] = service;
            return this; // Fluent API for chaining
        }

        public ServiceContainer Register(Type asType, IService service)
        {
            if (_services.ContainsKey(asType))
            {
                Debug.LogWarning($"[Services] Overwriting: {asType.Name}");
            }
            
            _services[asType] = service;
            return this;
        }

        public T Get<T>() where T : class, IService
        {
            var type = typeof(T);
            
            if (_services.TryGetValue(type, out var service))
            {
                return (T)service;
            }
            
            throw new InvalidOperationException(
                $"[Services] Not found: {type.Name}. Register it in Bootstrap.");
        }

        public bool TryGet<T>(out T service) where T : class, IService
        {
            var type = typeof(T);
            
            if (_services.TryGetValue(type, out var found))
            {
                service = (T)found;
                return true;
            }
            
            service = null;
            return false;
        }

        public void InitializeAll()
        {
            if (_initialized)
            {
                Debug.LogWarning("[Services] Already initialized");
                return;
            }

            foreach (var kvp in _services)
            {
                try
                {
                    kvp.Value.Initialize(this);
                    _tickableServices.Add(kvp.Value);
                    Debug.Log($"[Services] Initialized: {kvp.Key.Name}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Services] Failed to initialize {kvp.Key.Name}: {e}");
                }
            }

            _initialized = true;
        }

        public void TickAll()
        {
            foreach (var service in _tickableServices)
            {
                service.Tick();
            }
        }

        public void DisposeAll()
        {
            foreach (var kvp in _services)
            {
                try
                {
                    kvp.Value.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Services] Failed to dispose {kvp.Key.Name}: {e}");
                }
            }
            
            _services.Clear();
            _tickableServices.Clear();
            _initialized = false;
        }

    }
}
