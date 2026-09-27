using UnityEngine;

namespace JigglePhysics
{
    public static class JiggleMath
    {
        public const float MaxOmega = 60f;
        public const float Epsilon = 1e-8f;

        public static float ComputeOmega(float frequency)
        {
            return 2f * Mathf.PI * Mathf.Max(0.01f, frequency);
        }

        public static int ComputeSubstepCount(float frequency, float frameDeltaTime, int maxSubsteps)
        {
            float stableStep = JiggleSolverConfig.Active.StabilityFactor / ComputeOmega(frequency);
            int required = Mathf.CeilToInt(frameDeltaTime / stableStep);
            return Mathf.Clamp(required, 1, Mathf.Max(1, maxSubsteps));
        }

        public static float ComputeDampingRetention(float dampingRatio, float frequency, float deltaTime)
        {
            float lambda = 2f * dampingRatio * ComputeOmega(frequency);
            return Mathf.Exp(-lambda * deltaTime);
        }

        public static float ComputeTeleportThreshold(float threshold, float deltaTime)
        {
            float frameScale = Mathf.Max(deltaTime * JiggleSolverConfig.Active.TeleportReferenceFrameRate, 1f);
            return threshold * frameScale;
        }

        public static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        public static Vector3 Sanitize(Vector3 value, Vector3 fallback)
        {
            if (IsFinite(value))
            {
                return value;
            }
            return fallback;
        }

        public static Vector3 ClampMagnitudeSafe(Vector3 value, float maxMagnitude)
        {
            float sqrMagnitude = value.sqrMagnitude;
            if (sqrMagnitude > maxMagnitude * maxMagnitude)
            {
                return value * (maxMagnitude / Mathf.Sqrt(sqrMagnitude));
            }
            return value;
        }

        public static Vector3 ApplyConeConstraint(Vector3 animatedDirection, Vector3 simulatedDirection, float maxAngleDegrees)
        {
            float angle = Vector3.Angle(animatedDirection, simulatedDirection);
            if (angle <= maxAngleDegrees || angle < Epsilon)
            {
                return simulatedDirection;
            }

            if (angle > JiggleSolverConfig.Active.AntiParallelAngleDegrees)
            {
                return RotateAwayDeterministic(animatedDirection, maxAngleDegrees);
            }

            float t = maxAngleDegrees / angle;
            Vector3 clamped = Vector3.Slerp(animatedDirection, simulatedDirection, t);
            if (clamped.sqrMagnitude < Epsilon)
            {
                return RotateAwayDeterministic(animatedDirection, maxAngleDegrees);
            }

            return clamped.normalized;
        }

        public static Vector3 ResolveSphereCollision(Vector3 point, float pointRadius, Vector3 sphereCenter, float sphereRadius)
        {
            float combinedRadius = pointRadius + sphereRadius;
            Vector3 offset = point - sphereCenter;
            float sqrDistance = offset.sqrMagnitude;
            if (sqrDistance >= combinedRadius * combinedRadius)
            {
                return point;
            }

            if (sqrDistance < Epsilon)
            {
                return sphereCenter + Vector3.up * combinedRadius;
            }

            return sphereCenter + offset * (combinedRadius / Mathf.Sqrt(sqrDistance));
        }

        public static Vector3 ResolveCapsuleCollision(Vector3 point, float pointRadius, Vector3 capsuleStart, Vector3 capsuleEnd, float capsuleRadius)
        {
            Vector3 segment = capsuleEnd - capsuleStart;
            float segmentLengthSqr = segment.sqrMagnitude;
            float t = 0f;
            if (segmentLengthSqr > Epsilon)
            {
                t = Mathf.Clamp01(Vector3.Dot(point - capsuleStart, segment) / segmentLengthSqr);
            }

            return ResolveSphereCollision(point, pointRadius, capsuleStart + segment * t, capsuleRadius);
        }

        public static bool IsAlive(object candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            if (!(candidate is UnityEngine.Object))
            {
                return true;
            }

            UnityEngine.Object unityObject = (UnityEngine.Object)candidate;
            return unityObject != null;
        }

        public static float MaxScaleComponent(Vector3 lossyScale)
        {
            return Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
        }

        private static Vector3 RotateAwayDeterministic(Vector3 animatedDirection, float maxAngleDegrees)
        {
            Vector3 axis = Vector3.Cross(animatedDirection, Vector3.up);
            if (axis.sqrMagnitude < Epsilon)
            {
                axis = Vector3.Cross(animatedDirection, Vector3.right);
            }

            if (axis.sqrMagnitude < Epsilon)
            {
                return animatedDirection;
            }

            return Quaternion.AngleAxis(maxAngleDegrees, axis.normalized) * animatedDirection;
        }
    }
}
