using UnityEngine;

namespace JigglePhysics.Demo
{
    public static class JiggleLabVisualSettings
    {
        public static readonly Color AnchorColor = new Color(0.25f, 0.7f, 1f, 1f);
        public static readonly Color TargetColor = new Color(1f, 1f, 1f, 0.95f);
        public static readonly Color SimulatedColor = new Color(1f, 0.35f, 0.85f, 1f);
        public static readonly Color AnimatedBoneColor = new Color(1f, 1f, 1f, 0.42f);
        public static readonly Color SimulatedBoneColor = new Color(1f, 0.35f, 0.85f, 0.95f);
        public static readonly Color LimitColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        public static readonly Color ConeColor = new Color(1f, 0.55f, 0.15f, 0.7f);
        public static readonly Color VelocityColor = new Color(0.3f, 1f, 0.95f, 0.9f);
        public static readonly Color SpringColor = new Color(0.35f, 1f, 0.45f, 0.95f);
        public static readonly Color GravityColor = new Color(0.72f, 0.72f, 0.78f, 0.8f);
        public static readonly Color DuelLinkColor = new Color(1f, 0.95f, 0.4f, 0.9f);
        public static readonly Color LowDeviationColor = new Color(0.35f, 1f, 0.45f, 0.95f);
        public static readonly Color MediumDeviationColor = new Color(1f, 0.85f, 0.2f, 0.95f);
        public static readonly Color HighDeviationColor = new Color(1f, 0.3f, 0.25f, 0.95f);

        public static bool ShowPoints = true;
        public static bool ShowBones = true;
        public static bool ShowDeviation = true;
        public static bool ShowLimit = true;
        public static bool ShowCone;
        public static bool ShowVelocity;
        public static bool ShowForces;
        public static bool ShowTrails = true;
        public static bool ShowLabels = true;
        public static bool ShowDuelLink;

        public static void Reset()
        {
            ShowPoints = true;
            ShowBones = true;
            ShowDeviation = true;
            ShowLimit = true;
            ShowCone = false;
            ShowVelocity = false;
            ShowForces = false;
            ShowTrails = true;
            ShowLabels = true;
            ShowDuelLink = false;
        }

        public static void EnableMinimum()
        {
            ShowPoints = false;
            ShowBones = true;
            ShowDeviation = true;
            ShowLimit = false;
            ShowCone = false;
            ShowVelocity = false;
            ShowForces = false;
            ShowTrails = false;
            ShowLabels = false;
            ShowDuelLink = false;
        }

        public static void EnableAll()
        {
            ShowPoints = true;
            ShowBones = true;
            ShowDeviation = true;
            ShowLimit = true;
            ShowCone = true;
            ShowVelocity = true;
            ShowForces = true;
            ShowTrails = true;
            ShowLabels = true;
            ShowDuelLink = true;
        }

        public static Color DeviationColor(float normalized)
        {
            if (normalized >= 0.8f)
            {
                return HighDeviationColor;
            }

            return normalized >= 0.5f ? MediumDeviationColor : LowDeviationColor;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Reset();
        }
    }
}
