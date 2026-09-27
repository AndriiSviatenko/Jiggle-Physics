using UnityEngine;

namespace CharacterLogic.EditorTools
{
    public enum LinkKind
    {
        Jump,
        Step,
        Drop,
        WallRun,
        Beam,
        Vault,
        Blink,
        DoubleJump
    }

    public sealed class Deck
    {
        public string Name;
        public float X;
        public float ZStart;
        public float ZEnd;
        public float TopY;
        public float Width;

        public float Length => ZEnd - ZStart;
        public Vector3 Centre => new Vector3(X, TopY, (ZStart + ZEnd) * 0.5f);
    }

    public sealed class Link
    {
        public string From;
        public string To;
        public LinkKind Kind;
        public float Gap;
        public float Rise;
        public float Allowed;
        public float AllowedMomentum;

        public bool NeedsMomentum => Gap > Allowed + 0.001f;
        public bool Ok => Gap <= AllowedMomentum + 0.001f;
    }

    public sealed class VaultProp
    {
        public string Name;
        public Vector3 Position;
        public float Height;
    }

    public sealed class WallRunSpan
    {
        public string Name;
        public float Length;
    }

    public sealed class BeamSpan
    {
        public string Name;
        public float Width;
        public float Length;
    }

    public sealed class SlideGate
    {
        public string Name;
        public float Clearance;
    }

    public sealed class RouteReport
    {
        public int LinkChecks;
        public int FailedLinks;
        public int MomentumLinks;
        public int GeometryChecks;
        public int FailedGeometry;
        public int PropChecks;
        public int FailedProps;

        public bool Passed => FailedLinks == 0 && FailedGeometry == 0 && FailedProps == 0;
    }

    public static class RouteMetrics
    {
        public const float Gravity = 9.81f;
        public const float JumpVelocity = 6.5f;
        public const float RunSpeed = 8.5f;
        public const float MomentumSpeed = 12.0f;
        public const float Comfort = 0.85f;
        public const float MomentumComfort = 0.95f;
        public const float MaxDropGap = 9.5f;
        public const float MaxStepRise = 1.35f;
        public const float MaxVaultHeight = 1.35f;
        public const float MaxWallRunGap = 13.5f;
        public const float MinWallRunLength = 5f;
        public const float MinBeamWidth = 0.45f;
        public const float MinSlideClearance = 1f;
        public const float MaxLandingTolerance = 0.2f;
        public const float DeckThickness = 3f;
        public const float StreetY = -36f;

        public static float BallisticGap(float rise)
        {
            return BallisticGap(rise, RunSpeed, Comfort);
        }

        public static float MomentumGap(float rise)
        {
            return BallisticGap(rise, MomentumSpeed, MomentumComfort);
        }

        private static float BallisticGap(float rise, float speed, float comfort)
        {
            float discriminant = JumpVelocity * JumpVelocity - 2f * Gravity * rise;
            if (rise >= 0f)
            {
                if (discriminant <= 0f)
                {
                    return 0f;
                }

                float time = (JumpVelocity + Mathf.Sqrt(discriminant)) / Gravity;
                return comfort * speed * time;
            }

            float fallTime = (JumpVelocity + Mathf.Sqrt(JumpVelocity * JumpVelocity + 2f * Gravity * -rise)) / Gravity;
            return Mathf.Min(MaxDropGap, comfort * speed * fallTime);
        }

        public static float AllowedGap(LinkKind kind, float rise)
        {
            switch (kind)
            {
                case LinkKind.Blink:
                    return 15.0f;
                case LinkKind.DoubleJump:
                    return rise > 2.5f ? 0f : Mathf.Max(3.5f, BallisticGap(Mathf.Max(0f, rise - 1.5f)));
                case LinkKind.WallRun:
                    return MaxWallRunGap;
                case LinkKind.Beam:
                case LinkKind.Vault:
                    return 0f;
                default:
                    return BallisticGap(rise);
            }
        }

        public static float AllowedMomentumGap(LinkKind kind, float rise)
        {
            switch (kind)
            {
                case LinkKind.Blink:
                    return 18.0f;
                case LinkKind.DoubleJump:
                    return rise > 2.8f ? 0f : Mathf.Max(4.5f, MomentumGap(Mathf.Max(0f, rise - 1.5f)));
                case LinkKind.WallRun:
                    return MaxWallRunGap;
                case LinkKind.Beam:
                case LinkKind.Vault:
                    return 0f;
                default:
                    return MomentumGap(rise);
            }
        }
    }
}
