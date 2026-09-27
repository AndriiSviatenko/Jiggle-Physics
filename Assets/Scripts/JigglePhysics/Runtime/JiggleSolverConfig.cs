using UnityEngine;

namespace JigglePhysics
{
    [CreateAssetMenu(fileName = "JiggleSolverConfig", menuName = "Jiggle Physics/Solver Config", order = 101)]
    public class JiggleSolverConfig : ScriptableObject
    {
        private static JiggleSolverConfig active;

        public static JiggleSolverConfig Active
        {
            get
            {
                if (active == null)
                {
                    active = CreateInstance<JiggleSolverConfig>();
                }

                return active;
            }
            set { active = value != null ? value : active; }
        }
        [Tooltip("Fraction of the stability limit a substep may use. Lower is more stable, costs more substeps.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float stabilityFactor = 0.25f;

        [Tooltip("Hard clamp on simulated time per rendered frame, in seconds. Protects against hitches.")]
        [Range(0.01f, 0.2f)]
        [SerializeField] private float maxFrameDeltaTime = 0.05f;

        [Tooltip("Internal fixed simulation rate. 90 Hz keeps 1.5-5 Hz soft-body motion stable at 30/60/120+ FPS.")]
        [Range(30f, 240f)]
        [SerializeField] private float simulationRate = 90f;

        [Tooltip("Frame rate the teleport threshold is expressed against, in fps.")]
        [Range(15f, 240f)]
        [SerializeField] private float teleportReferenceFrameRate = 60f;

        [Tooltip("Bone length used when a chain bone has no child to measure against, in meters.")]
        [Range(0.001f, 1f)]
        [SerializeField] private float fallbackBoneLength = 0.05f;

        [Tooltip("Angle above which the cone clamp switches to a deterministic fallback to avoid Slerp flicker.")]
        [Range(90f, 180f)]
        [SerializeField] private float antiParallelAngleDegrees = 179f;

        public float StabilityFactor
        {
            get { return stabilityFactor; }
        }

        public float MaxFrameDeltaTime
        {
            get { return maxFrameDeltaTime; }
        }

        public float FixedDeltaTime
        {
            get { return 1f / Mathf.Max(30f, simulationRate); }
        }

        public float TeleportReferenceFrameRate
        {
            get { return teleportReferenceFrameRate; }
        }

        public float FallbackBoneLength
        {
            get { return fallbackBoneLength; }
        }

        public float AntiParallelAngleDegrees
        {
            get { return antiParallelAngleDegrees; }
        }

        private void OnValidate()
        {
            stabilityFactor = Mathf.Clamp(stabilityFactor, 0.05f, 0.5f);
            maxFrameDeltaTime = Mathf.Clamp(maxFrameDeltaTime, 0.01f, 0.2f);
            simulationRate = Mathf.Clamp(simulationRate, 30f, 240f);
            teleportReferenceFrameRate = Mathf.Clamp(teleportReferenceFrameRate, 15f, 240f);
            fallbackBoneLength = Mathf.Clamp(fallbackBoneLength, 0.001f, 1f);
            antiParallelAngleDegrees = Mathf.Clamp(antiParallelAngleDegrees, 90f, 180f);
        }
    }
}
