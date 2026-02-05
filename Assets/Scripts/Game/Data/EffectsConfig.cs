using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(fileName = "New Effects Config", menuName = "Game/Data/Effects Config")]
    public class EffectsConfig : ScriptableObject
    {
        [Header("Territory Claim Animation")]
        public float claimWaveDuration = 0.4f;
        public float claimWaveSpeed = 30f;
        public float claimBrightnessBoost = 1.5f;
        public float claimHeightPulse = 0.3f;
        public AnimationCurve claimBrightnessCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Death Effect")]
        public int deathParticleCount = 20;
        public float deathParticleSpeed = 5f;
        public float deathParticleLifetime = 0.8f;
        public float deathParticleSize = 0.15f;
        public float deathScaleDuration = 0.3f;
        public float deathMaxScale = 1.5f;

        [Header("Respawn Effect")]
        public float respawnGlowDuration = 0.6f;
        public float respawnGlowIntensity = 3f;
        public float respawnScaleDuration = 0.4f;
        public float respawnRingDuration = 0.5f;
        public float respawnRingMaxRadius = 3f;

        [Header("Screen Shake")]
        public float shakeIntensity = 0.5f;
        public float shakeDuration = 0.3f;
        public float shakeFrequency = 25f;

        [Header("Trail Glow")]
        public float trailBaseEmission = 1.5f;
        public float trailPulseSpeed = 2f;
        public float trailPulseIntensity = 0.5f;
        public float trailWidth = 0.4f;
        public float trailHeight = 0.3f;

        [Header("Position Interpolation")]
        public float interpolationSmoothTime = 0.1f;
        public float predictionAmount = 0.1f;
    }
}