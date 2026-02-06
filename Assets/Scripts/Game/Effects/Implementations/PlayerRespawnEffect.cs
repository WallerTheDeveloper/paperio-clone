using Game.Data;
using UnityEngine;

namespace Game.Effects.Implementations
{
    public class PlayerRespawnEffect : MonoBehaviour, IEffect
    {
        [SerializeField] private Effect type;
        
        [Header("Glow Settings")]
        [SerializeField] private float glowDuration = 0.6f;
        [SerializeField] private float glowIntensity = 3f;
        [SerializeField] private AnimationCurve glowCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        
        [Header("Scale Animation")]
        [SerializeField] private float scaleDuration = 0.4f;
        [SerializeField] private float startScale = 0f;
        [SerializeField] private AnimationCurve scaleCurve;
        
        [Header("Ring Effect")]
        [SerializeField] private bool useRingEffect = true;
        [SerializeField] private float ringExpandDuration = 0.5f;
        [SerializeField] private float ringMaxRadius = 3f;
        [SerializeField] private Material ringMaterial;

        private ParticleSystem _glowParticles;
        private GameObject _ringObject;
        private MeshRenderer _ringRenderer;
        private Material _ringMaterialInstance;
        private Coroutine _ringCoroutine;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int RingRadius = Shader.PropertyToID("_Radius");
        private static readonly int RingAlpha = Shader.PropertyToID("_Alpha");

        public Effect Type => type;
        public GameObject GameObject { get; }

        public bool IsPlaying
        {
            get
            {
                if (_glowParticles != null && _glowParticles.isPlaying) return true;
                if (_ringCoroutine != null) return true;
                return false;
            }
        }

        public void Prepare(IGameWorldDataProvider gameData)
        {
            CreateGlowParticles();
            
            if (useRingEffect)
            {
                CreateRingEffect();
            }

            if (scaleCurve == null || scaleCurve.keys.Length == 0)
            {
                scaleCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.5f, 1.2f),
                    new Keyframe(0.7f, 0.9f),
                    new Keyframe(1f, 1f)
                );
            }
        }

        public void Play(EffectData data)
        {
            var position = data.Position;
            var playerColor = data.Color;
            
            transform.position = position;

            if (_glowParticles != null)
            {
                var main = _glowParticles.main;
                Color glowColor = playerColor * glowIntensity;
                glowColor.a = 1f;
                main.startColor = glowColor;
                
                _glowParticles.Clear();
                _glowParticles.Play();
            }

            if (useRingEffect && _ringObject != null)
            {
                if (_ringCoroutine != null)
                {
                    StopCoroutine(_ringCoroutine);
                }
                _ringCoroutine = StartCoroutine(AnimateRing(playerColor));
            }
        }

        public void Stop()
        {
            if (_ringMaterialInstance != null)
            {
                Destroy(_ringMaterialInstance);
            }
        }

        private void CreateGlowParticles()
        {
            var go = new GameObject("RespawnGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            _glowParticles = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();

            var main = _glowParticles.main;
            main.duration = glowDuration;
            main.loop = false;
            main.startLifetime = glowDuration;
            main.startSpeed = 0f;
            main.startSize = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.maxParticles = 1;

            var emission = _glowParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var shape = _glowParticles.shape;
            shape.enabled = false;

            var sizeOverLifetime = _glowParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, glowCurve);

            var colorOverLifetime = _glowParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var glowMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
            glowMaterial.SetFloat("_Mode", 2);
            renderer.material = glowMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private void CreateRingEffect()
        {
            _ringObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _ringObject.name = "RespawnRing";
            _ringObject.transform.SetParent(transform, false);
            _ringObject.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            _ringObject.transform.localScale = new Vector3(0f, 0.02f, 0f);

            var collider = _ringObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            _ringRenderer = _ringObject.GetComponent<MeshRenderer>();
            
            if (ringMaterial != null)
            {
                _ringMaterialInstance = new Material(ringMaterial);
            }
            else
            {
                _ringMaterialInstance = new Material(Shader.Find("Sprites/Default"));
            }
            
            _ringRenderer.material = _ringMaterialInstance;
            _ringObject.SetActive(false);
        }

        private System.Collections.IEnumerator AnimateRing(Color color)
        {
            _ringObject.SetActive(true);
            _ringMaterialInstance.color = new Color(color.r, color.g, color.b, 0.5f);

            float elapsed = 0f;
            while (elapsed < ringExpandDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / ringExpandDuration;
                float easedProgress = Easing.ExpoOut(progress);

                float radius = ringMaxRadius * easedProgress;
                float alpha = 1f - progress;

                _ringObject.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
                
                Color ringColor = _ringMaterialInstance.color;
                ringColor.a = alpha * 0.5f;
                _ringMaterialInstance.color = ringColor;

                yield return null;
            }

            _ringObject.SetActive(false);
            _ringCoroutine = null;
        }

        public RespawnAnimationData GetAnimationData()
        {
            return new RespawnAnimationData
            {
                ScaleDuration = scaleDuration,
                StartScale = startScale,
                ScaleCurve = scaleCurve,
                GlowDuration = glowDuration,
                GlowIntensity = glowIntensity
            };
        }
    }

    public struct RespawnAnimationData
    {
        public float ScaleDuration;
        public float StartScale;
        public AnimationCurve ScaleCurve;
        public float GlowDuration;
        public float GlowIntensity;
    }
}