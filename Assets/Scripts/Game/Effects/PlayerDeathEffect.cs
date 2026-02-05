using UnityEngine;

namespace Game.Effects
{
    public class PlayerDeathEffect : MonoBehaviour
    {
        [Header("Particle Settings")]
        [SerializeField] private int burstCount = 20;
        [SerializeField] private float particleSpeed = 5f;
        [SerializeField] private float particleLifetime = 0.8f;
        [SerializeField] private float particleSize = 0.15f;
        [SerializeField] private Gradient colorOverLifetime;
        
        [Header("Screen Shake")]
        [SerializeField] private float shakeIntensity = 0.3f;
        [SerializeField] private float shakeDuration = 0.2f;
        
        [Header("Scale Animation")]
        [SerializeField] private float scaleDuration = 0.3f;
        [SerializeField] private float maxScale = 1.5f;

        private ParticleSystem _particleSystem;
        private ParticleSystemRenderer _particleRenderer;
        private Material _particleMaterial;

        private void OnEnable()
        {
            CreateParticleSystem();
        }

        private void CreateParticleSystem()
        {
            var go = new GameObject("DeathParticles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            _particleSystem = go.AddComponent<ParticleSystem>();
            _particleRenderer = go.GetComponent<ParticleSystemRenderer>();

            var main = _particleSystem.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = particleLifetime;
            main.startSpeed = particleSpeed;
            main.startSize = particleSize;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.maxParticles = 100;

            var emission = _particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burstCount) });

            var shape = _particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var velocityOverLifetime = _particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var sizeOverLifetime = _particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            if (colorOverLifetime != null && colorOverLifetime.colorKeys.Length > 0)
            {
                var col = _particleSystem.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(colorOverLifetime);
            }

            _particleMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
            _particleMaterial.SetFloat("_Mode", 2);
            _particleRenderer.material = _particleMaterial;
            _particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private void Play(Vector3 position, Color playerColor)
        {
            transform.position = position;
            
            if (_particleMaterial != null)
            {
                _particleMaterial.SetColor("_Color", playerColor);
                _particleMaterial.SetColor("_EmissionColor", playerColor * 2f);
            }

            var main = _particleSystem.main;
            main.startColor = playerColor;

            _particleSystem.Clear();
            _particleSystem.Play();
        }

        public void PlayWithTarget(Vector3 position, Color playerColor, Transform target)
        {
            Play(position, playerColor);
            
            if (target != null)
            {
                StartCoroutine(ShakeTarget(target));
            }
        }

        private System.Collections.IEnumerator ShakeTarget(Transform target)
        {
            if (target == null) yield break;

            Vector3 originalPosition = target.localPosition;
            float elapsed = 0f;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / shakeDuration;
                float intensity = shakeIntensity * (1f - progress);

                Vector3 offset = new Vector3(
                    Random.Range(-1f, 1f) * intensity,
                    Random.Range(-1f, 1f) * intensity * 0.5f,
                    Random.Range(-1f, 1f) * intensity
                );

                target.localPosition = originalPosition + offset;
                yield return null;
            }

            target.localPosition = originalPosition;
        }

        public DeathAnimationData GetAnimationData()
        {
            return new DeathAnimationData
            {
                Duration = scaleDuration,
                MaxScale = maxScale,
                ShakeIntensity = shakeIntensity,
                ShakeDuration = shakeDuration
            };
        }

        public bool IsPlaying => _particleSystem != null && _particleSystem.isPlaying;

        private void OnDisable()
        {
            if (_particleMaterial != null)
            {
                Destroy(_particleMaterial);
            }
        }
    }

    public struct DeathAnimationData
    {
        public float Duration;
        public float MaxScale;
        public float ShakeIntensity;
        public float ShakeDuration;
    }
}