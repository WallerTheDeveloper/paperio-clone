using System.Collections.Generic;
using UnityEngine;

namespace MonoSingleton
{
    public static class MonoSingletonRegistry
    {
        private static readonly HashSet<MonoBehaviour> Instances = new();
    
        public static IReadOnlyCollection<MonoBehaviour> All => Instances;
        public static void Register(MonoBehaviour singleton) => Instances.Add(singleton);
        public static void Unregister(MonoBehaviour singleton) => Instances.Remove(singleton);

        public static List<MonoBehaviour> GetAll() => new(Instances);

        public static IEnumerable<T> GetAll<T>() where T : MonoBehaviour
        {
            foreach (var instance in Instances)
            {
                if (instance is T typed)
                    yield return typed;
            }
        }

        public static T Get<T>() where T : MonoBehaviour
        {
            foreach (var instance in Instances)
            {
                if (instance is T typed)
                    return typed;
            }
            return null;
        }

        public static void InitializeSingletonsOnScene()
        {
            var singletons = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
    
            foreach (var mono in singletons)
            {
                if (mono is IMonoSingleton)
                {
                    Register(mono);
                }
            }
        }
    }
}