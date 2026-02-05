using UnityEngine;

namespace Game.Effects
{
    public class ScreenShakeController : MonoBehaviour
    {
        [Header("Default Settings")]
        [SerializeField] private float defaultIntensity = 0.5f;
        [SerializeField] private float defaultDuration = 0.3f;
        [SerializeField] private float defaultFrequency = 25f;
        
        [Header("Decay")]
        [SerializeField] private AnimationCurve decayCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        
        [Header("Constraints")]
        [SerializeField] private bool shakeX = true;
        [SerializeField] private bool shakeY = true;
        [SerializeField] private bool shakeZ = true;
        [SerializeField] private float maxOffset = 2f;

        private Transform _targetTransform;
        private Vector3 _originalLocalPosition;
        private float _currentDuration;
        private float _currentIntensity;
        private float _currentFrequency;
        private float _elapsed;
        private bool _isShaking;
        private int _seed;

        public bool IsShaking => _isShaking;

        private void Awake()
        {
            _targetTransform = transform;
            _originalLocalPosition = _targetTransform.localPosition;
            _seed = Random.Range(0, 1000);
        }

        public void SetTarget(Transform target)
        {
            _targetTransform = target;
            _originalLocalPosition = _targetTransform.localPosition;
        }

        public void Shake()
        {
            Shake(defaultIntensity, defaultDuration, defaultFrequency);
        }

        public void Shake(float intensity)
        {
            Shake(intensity, defaultDuration, defaultFrequency);
        }

        public void Shake(float intensity, float duration)
        {
            Shake(intensity, duration, defaultFrequency);
        }

        public void Shake(float intensity, float duration, float frequency)
        {
            if (intensity > _currentIntensity || !_isShaking)
            {
                _currentIntensity = intensity;
                _currentDuration = duration;
                _currentFrequency = frequency;
                _elapsed = 0f;
                _isShaking = true;
                _seed = Random.Range(0, 1000);
            }
        }

        public void ShakeOnce(Vector3 direction, float intensity)
        {
            if (_targetTransform == null) return;

            Vector3 offset = direction.normalized * intensity;
            offset = ClampOffset(offset);
            
            _targetTransform.localPosition = _originalLocalPosition + offset;
            
            if (!_isShaking)
            {
                _currentIntensity = intensity * 0.5f;
                _currentDuration = 0.15f;
                _elapsed = 0f;
                _isShaking = true;
            }
        }

        public void Stop()
        {
            _isShaking = false;
            _elapsed = 0f;
            
            if (_targetTransform != null)
            {
                _targetTransform.localPosition = _originalLocalPosition;
            }
        }

        private void LateUpdate()
        {
            if (!_isShaking || _targetTransform == null) return;

            _elapsed += Time.deltaTime;
            
            if (_elapsed >= _currentDuration)
            {
                Stop();
                return;
            }

            float progress = _elapsed / _currentDuration;
            float decayMultiplier = decayCurve.Evaluate(progress);
            float currentIntensity = _currentIntensity * decayMultiplier;

            float time = _elapsed * _currentFrequency;
            
            Vector3 offset = new Vector3(
                shakeX ? PerlinNoise(time, 0f) * currentIntensity : 0f,
                shakeY ? PerlinNoise(time, 100f) * currentIntensity * 0.5f : 0f,
                shakeZ ? PerlinNoise(time, 200f) * currentIntensity : 0f
            );

            offset = ClampOffset(offset);
            _targetTransform.localPosition = _originalLocalPosition + offset;
        }

        private float PerlinNoise(float x, float offset)
        {
            return (Mathf.PerlinNoise(_seed + x + offset, _seed + offset) - 0.5f) * 2f;
        }

        private Vector3 ClampOffset(Vector3 offset)
        {
            return new Vector3(
                Mathf.Clamp(offset.x, -maxOffset, maxOffset),
                Mathf.Clamp(offset.y, -maxOffset, maxOffset),
                Mathf.Clamp(offset.z, -maxOffset, maxOffset)
            );
        }

        public void UpdateOriginalPosition(Vector3 position)
        {
            _originalLocalPosition = position;
        }

        private void OnDisable()
        {
            if (_targetTransform != null && _isShaking)
            {
                _targetTransform.localPosition = _originalLocalPosition;
            }
            _isShaking = false;
        }
    }
}