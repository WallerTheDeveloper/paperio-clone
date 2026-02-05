using System.Collections.Generic;
using Core.Services;
using Game.Data;
using UnityEngine;

namespace Game.Effects
{
    public class EffectsManager : MonoBehaviour, IService
    {
        [Header("Effect Prefabs")]
        [SerializeField] private PlayerDeathEffect deathEffectPrefab;
        [SerializeField] private PlayerRespawnEffect respawnEffectPrefab;
        
        [Header("Configuration")]
        [SerializeField] private int deathEffectPoolSize = 4;
        [SerializeField] private int respawnEffectPoolSize = 4;
        

        private readonly Queue<PlayerDeathEffect> _deathPool = new();
        private readonly Queue<PlayerRespawnEffect> _respawnPool = new();
        private readonly List<PlayerDeathEffect> _activeDeathEffects = new();
        private readonly List<PlayerRespawnEffect> _activeRespawnEffects = new();

        private Transform _effectsContainer;
        private bool _isInitialized;

        private TerritoryClaimAnimator _territoryAnimator;
        public void Initialize(ServiceContainer services)
        {
           _effectsContainer = new GameObject("EffectsContainer").transform;
           _effectsContainer.SetParent(transform, false);

           InitializePools();

           _isInitialized = true;
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
            for (int i = 0; i < deathEffectPoolSize; i++)
            {
                var effect = CreateDeathEffect();
                effect.gameObject.SetActive(false);
                _deathPool.Enqueue(effect);
            }

            for (int i = 0; i < respawnEffectPoolSize; i++)
            {
                var effect = CreateRespawnEffect();
                effect.gameObject.SetActive(false);
                _respawnPool.Enqueue(effect);
            }
        }

        private PlayerDeathEffect CreateDeathEffect()
        {
            if (deathEffectPrefab != null)
            {
                return Instantiate(deathEffectPrefab, _effectsContainer);
            }

            var go = new GameObject("DeathEffect");
            go.transform.SetParent(_effectsContainer, false);
            return go.AddComponent<PlayerDeathEffect>();
        }

        private PlayerRespawnEffect CreateRespawnEffect()
        {
            if (respawnEffectPrefab != null)
            {
                return Instantiate(respawnEffectPrefab, _effectsContainer);
            }

            var go = new GameObject("RespawnEffect");
            go.transform.SetParent(_effectsContainer, false);
            return go.AddComponent<PlayerRespawnEffect>();
        }

        public void PlayDeathEffect(Vector3 position, Color playerColor, Transform cameraTransform = null)
        {
            if (!_isInitialized) return;

            var effect = GetDeathEffectFromPool();
            effect.gameObject.SetActive(true);
            effect.PlayWithTarget(position, playerColor, cameraTransform);
            _activeDeathEffects.Add(effect);
        }

        public void PlayRespawnEffect(Vector3 position, Color playerColor)
        {
            if (!_isInitialized) return;

            var effect = GetRespawnEffectFromPool();
            effect.gameObject.SetActive(true);
            effect.Play(position, playerColor);
            _activeRespawnEffects.Add(effect);
        }

        public void PlayTerritoryClaimEffect(List<TerritoryChange> changes, uint playerId, Color playerColor)
        {
            if (!_isInitialized || _territoryAnimator == null) return;

            _territoryAnimator.PlayClaimAnimation(changes, playerId, playerColor);
        }

        private void CleanupFinishedEffects()
        {
            for (int i = _activeDeathEffects.Count - 1; i >= 0; i--)
            {
                if (!_activeDeathEffects[i].IsPlaying)
                {
                    var effect = _activeDeathEffects[i];
                    effect.gameObject.SetActive(false);
                    _deathPool.Enqueue(effect);
                    _activeDeathEffects.RemoveAt(i);
                }
            }

            for (int i = _activeRespawnEffects.Count - 1; i >= 0; i--)
            {
                if (!_activeRespawnEffects[i].IsPlaying)
                {
                    var effect = _activeRespawnEffects[i];
                    effect.gameObject.SetActive(false);
                    _respawnPool.Enqueue(effect);
                    _activeRespawnEffects.RemoveAt(i);
                }
            }
        }

        private PlayerDeathEffect GetDeathEffectFromPool()
        {
            if (_deathPool.Count > 0)
            {
                return _deathPool.Dequeue();
            }
            return CreateDeathEffect();
        }

        private PlayerRespawnEffect GetRespawnEffectFromPool()
        {
            if (_respawnPool.Count > 0)
            {
                return _respawnPool.Dequeue();
            }
            return CreateRespawnEffect();
        }

        private void ClearAllEffects()
        {
            foreach (var effect in _activeDeathEffects)
            {
                effect.gameObject.SetActive(false);
                _deathPool.Enqueue(effect);
            }
            _activeDeathEffects.Clear();

            foreach (var effect in _activeRespawnEffects)
            {
                effect.gameObject.SetActive(false);
                _respawnPool.Enqueue(effect);
            }
            _activeRespawnEffects.Clear();

            if (_territoryAnimator != null)
            {
                _territoryAnimator.ClearAllAnimations();
            }
        }
    }
}