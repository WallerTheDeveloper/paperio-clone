using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Game.Data;
using Unity.VisualScripting;
using UnityEngine;

namespace Game.Effects
{
    [Serializable]
    public struct EffectWrapper
    {
        [SerializeField] public UnityEngine.Object EffectObject;
        
        public IEffect Effect => EffectObject as IEffect;
    }
    public class EffectsManager : MonoBehaviour, IService
    {
        [SerializeField] private List<EffectWrapper> effects = new();
        [SerializeField] private int effectsPoolSize = 4;

        private readonly Queue<IEffect> _effectsPool = new();
        private readonly List<IEffect> _activeEffects = new();

        private Transform _effectsContainer;

        private IGameWorldDataProvider _gameData;
        public void Initialize(ServiceContainer services)
        {
            _gameData = services.Get<GameWorld>();
            
           _effectsContainer = new GameObject("EffectsContainer").transform;
           _effectsContainer.SetParent(transform, false);

           InitializePools();
        }

        public void Tick()
        {
            CleanupFinishedEffects();
        }

        public void Dispose()
        {
            ClearAllEffects();
        }
        
        private void InitializePools()
        {
            for (int i = 0; i < effectsPoolSize; i++)
            {
                var spawnedEffects = InstantiateEffects();
                spawnedEffects.ForEach(effect =>
                {
                    effect.Prepare(_gameData);
                    effect.GameObject.SetActive(false);
                    _effectsPool.Enqueue(effect);
                });
            }
        }

        public void PlayEffect(Effect effect, EffectData data)
        {
            var effectsFromPool = GetEffectsFromPool();
            foreach (var effectFromPool in effectsFromPool)
            {
                if (effectFromPool.Type != effect)
                {
                    continue;
                }

                effectFromPool.GameObject.SetActive(true);
                effectFromPool.Play(data);

                _activeEffects.Add(effectFromPool);
            }
        }

        private List<IEffect> InstantiateEffects()
        {
            var instantiatedEffects = new List<IEffect>();
            foreach (var effectWrapper in effects)
            {
                var instance = Instantiate(effectWrapper.EffectObject, _effectsContainer);
        
                IEffect effectInterface = (instance as GameObject)?.GetComponent<IEffect>() 
                                          ?? (instance as Component)?.GetComponent<IEffect>();

                if (effectInterface != null)
                {
                    instantiatedEffects.Add(effectInterface);
                }
                else
                {
                    Debug.LogError($"Object {instance.name} does not implement IEffect!");
                }
            }
            return instantiatedEffects;
        }
        
        private void CleanupFinishedEffects()
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                if (!_activeEffects[i].IsPlaying)
                {
                    var effect = _activeEffects[i];
                    effect.GameObject.SetActive(false);
                    _effectsPool.Enqueue(effect);
                    _activeEffects.RemoveAt(i);
                }
            }
        }

        private IEnumerable<IEffect> GetEffectsFromPool()
        {
            if (_effectsPool.Count > 0)
            {
                return new[]
                {
                    _effectsPool.Dequeue()
                };
            }

            return InstantiateEffects();
        }

        private void ClearAllEffects()
        {
            foreach (var activeEffect in _activeEffects)
            {
                activeEffect.Stop();
                activeEffect.GameObject.SetActive(false);
                _effectsPool.Enqueue(activeEffect);
            }
            _activeEffects.Clear();
        }
    }
}