using UnityEngine;

namespace Game.Effects
{
    public static class InterpolationHelper
    {
        public static Vector3 SmoothDamp(
            Vector3 current,
            Vector3 target,
            ref Vector3 velocity,
            float smoothTime,
            float maxSpeed = Mathf.Infinity)
        {
            return Vector3.SmoothDamp(current, target, ref velocity, smoothTime, maxSpeed);
        }

        public static Vector3 Lerp(Vector3 from, Vector3 to, float t)
        {
            return Vector3.Lerp(from, to, Mathf.Clamp01(t));
        }

        public static Vector3 LerpUnclamped(Vector3 from, Vector3 to, float t)
        {
            return Vector3.LerpUnclamped(from, to, t);
        }

        public static Vector3 EasedLerp(Vector3 from, Vector3 to, float t, System.Func<float, float> easingFunc)
        {
            float easedT = easingFunc(Mathf.Clamp01(t));
            return Vector3.Lerp(from, to, easedT);
        }

        public static Quaternion SlerpUnclamped(Quaternion from, Quaternion to, float t)
        {
            return Quaternion.SlerpUnclamped(from, to, t);
        }

        public static float SmoothStep(float from, float to, float t)
        {
            t = Mathf.Clamp01(t);
            t = t * t * (3f - 2f * t);
            return from + (to - from) * t;
        }

        public static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 point = uuu * p0;
            point += 3f * uu * t * p1;
            point += 3f * u * tt * p2;
            point += ttt * p3;

            return point;
        }

        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        public static Vector3 SpringDamp(
            Vector3 current,
            Vector3 target,
            ref Vector3 velocity,
            float stiffness,
            float damping,
            float deltaTime)
        {
            Vector3 displacement = current - target;
            Vector3 springForce = -stiffness * displacement;
            Vector3 dampingForce = -damping * velocity;
            Vector3 acceleration = springForce + dampingForce;

            velocity += acceleration * deltaTime;
            return current + velocity * deltaTime;
        }

        public static float ExponentialDecay(float current, float target, float decay, float deltaTime)
        {
            return Mathf.Lerp(target, current, Mathf.Exp(-decay * deltaTime));
        }

        public static Vector3 ExponentialDecay(Vector3 current, Vector3 target, float decay, float deltaTime)
        {
            float t = Mathf.Exp(-decay * deltaTime);
            return Vector3.Lerp(target, current, t);
        }

        public static Vector3 PredictPosition(Vector3 current, Vector3 velocity, float time)
        {
            return current + velocity * time;
        }

        public static Vector3 InterpolateWithPrediction(
            Vector3 previousPosition,
            Vector3 currentPosition,
            float tickProgress,
            float predictionAmount = 0.1f)
        {
            Vector3 velocity = currentPosition - previousPosition;
            Vector3 baseInterpolation = Vector3.Lerp(previousPosition, currentPosition, tickProgress);
            
            if (tickProgress > 1f - predictionAmount)
            {
                float extrapolationT = (tickProgress - (1f - predictionAmount)) / predictionAmount;
                return Vector3.Lerp(baseInterpolation, currentPosition + velocity * predictionAmount, extrapolationT);
            }
            
            return baseInterpolation;
        }
    }
}