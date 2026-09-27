using UnityEngine;

namespace JigglePhysics
{
    public enum JiggleTimeMode
    {
        Scaled = 0,
        Unscaled = 1
    }

    [CreateAssetMenu(fileName = "JiggleProfile", menuName = "Jiggle Physics/Jiggle Profile", order = 100)]
    public class JiggleProfile : ScriptableObject
    {
        [Header("Spring")]
        [Tooltip("Natural frequency in Hz. Breasts: 2-5 Hz. Higher = stiffer.")]
        [SerializeField] private float frequency = 3f;

        [Tooltip("Damping ratio. 0 = endless oscillation, 0.2 = jiggly, 0.5 = soft, 1 = no overshoot.")]
        [Range(0f, 1.5f)]
        [SerializeField] private float dampingRatio = 0.35f;

        [Tooltip("Gravity multiplier applied to simulated points. 1 = full gravity.")]
        [Range(-2f, 2f)]
        [SerializeField] private float gravityMultiplier = 1f;

        [Header("Inertia")]
        [Tooltip("0 follows root translation completely; 1 keeps full world-space inertia.")]
        [Range(0f, 1f)]
        [SerializeField] private float worldInertia = 0.8f;

        [Tooltip("0 follows root rotation completely; 1 keeps full rotational inertia.")]
        [Range(0f, 1f)]
        [SerializeField] private float localInertia = 1f;

        [Tooltip("Root translation above this speed is absorbed instead of becoming a huge impulse.")]
        [Min(0.01f)]
        [SerializeField] private float anchorSpeedLimit = 6f;

        [Tooltip("Root rotation above this speed is absorbed instead of becoming a huge impulse.")]
        [Min(1f)]
        [SerializeField] private float anchorAngularSpeedLimit = 720f;

        [Tooltip("Last-resort particle velocity clamp in meters per second.")]
        [Min(0.01f)]
        [SerializeField] private float maxParticleSpeed = 5f;

        [Tooltip("Spring response per anchor-local axis. Y > 1 gives a crisp anime-style vertical response.")]
        [SerializeField] private Vector3 axisWeights = new Vector3(1f, 1.35f, 0.85f);

        [Header("Limits")]
        [Tooltip("Maximum angle in degrees a bone may deviate from its animated direction.")]
        [Range(0f, 180f)]
        [SerializeField] private float maxAngle = 60f;

        [Tooltip("Maximum world-space offset of a simulated tip from its animated position, in meters.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float maxOffset = 0.15f;

        [Header("Collision")]
        [Tooltip("Radius of the simulated tip point used in collision tests, in meters.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float pointRadius = 0.03f;

        [Header("Time")]
        [SerializeField] private JiggleTimeMode timeMode = JiggleTimeMode.Scaled;

        [Tooltip("Maximum simulation substeps per rendered frame.")]
        [Range(1, 8)]
        [SerializeField] private int maxSubsteps = 4;

        [Header("Volume")]
        [Tooltip("Volume-preserving squash and stretch driven by vertical tip deviation. 0 disables it.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float squashStretch = 0f;

        [Header("Blending")]
        [Tooltip("1 = full physics, 0 = fully follow animation.")]
        [Range(0f, 1f)]
        [SerializeField] private float blend = 1f;

        [Header("Solver")]
        [Tooltip("Physics implementation used for this profile: spring-damper, minimal spring or stable verlet.")]
        [SerializeField] private JiggleSolverKind solverKind = JiggleSolverKind.SpringDamper;

        [Tooltip("Stable verlet pull toward the animated tip per 1/60 s. 0.08 gives roughly 3 Hz.")]
        [Range(0.01f, 0.9f)]
        [SerializeField] private float verletStiffness = 0.08f;

        [Tooltip("Stable verlet velocity loss per 1/60 s.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float verletDrag = 0.15f;

        public JiggleSolverKind SolverKind
        {
            get { return solverKind; }
            set { solverKind = value; }
        }

        public float VerletStiffness
        {
            get { return verletStiffness; }
            set { verletStiffness = Mathf.Clamp(value, 0.01f, 0.9f); }
        }

        public float VerletDrag
        {
            get { return verletDrag; }
            set { verletDrag = Mathf.Clamp(value, 0f, 0.9f); }
        }

        public float Frequency
        {
            get { return frequency; }
            set { frequency = Mathf.Clamp(value, MinFrequency, MaxFrequency); }
        }

        public float DampingRatio
        {
            get { return dampingRatio; }
            set { dampingRatio = Mathf.Clamp(value, MinDampingRatio, MaxDampingRatio); }
        }

        public float GravityMultiplier
        {
            get { return gravityMultiplier; }
            set { gravityMultiplier = value; }
        }

        public float WorldInertia
        {
            get { return worldInertia; }
            set { worldInertia = Mathf.Clamp01(value); }
        }

        public float LocalInertia
        {
            get { return localInertia; }
            set { localInertia = Mathf.Clamp01(value); }
        }

        public float AnchorSpeedLimit
        {
            get { return anchorSpeedLimit; }
            set { anchorSpeedLimit = Mathf.Max(0.01f, value); }
        }

        public float AnchorAngularSpeedLimit
        {
            get { return anchorAngularSpeedLimit; }
            set { anchorAngularSpeedLimit = Mathf.Max(1f, value); }
        }

        public float MaxParticleSpeed
        {
            get { return maxParticleSpeed; }
            set { maxParticleSpeed = Mathf.Max(0.01f, value); }
        }

        public Vector3 AxisWeights
        {
            get { return axisWeights; }
            set
            {
                axisWeights = new Vector3(
                    Mathf.Max(0f, value.x),
                    Mathf.Max(0f, value.y),
                    Mathf.Max(0f, value.z));
            }
        }

        public float MaxAngle
        {
            get { return maxAngle; }
            set { maxAngle = Mathf.Clamp(value, 0f, 180f); }
        }

        public float MaxOffset
        {
            get { return maxOffset; }
            set { maxOffset = Mathf.Clamp(value, MinMaxOffset, MaxMaxOffset); }
        }

        public float PointRadius
        {
            get { return pointRadius; }
            set { pointRadius = Mathf.Clamp(value, 0f, MaxPointRadius); }
        }

        public JiggleTimeMode TimeMode
        {
            get { return timeMode; }
            set { timeMode = value; }
        }

        public int MaxSubsteps
        {
            get { return maxSubsteps; }
            set { maxSubsteps = Mathf.Clamp(value, 1, MaxSubstepLimit); }
        }

        public float SquashStretch
        {
            get { return squashStretch; }
            set { squashStretch = Mathf.Clamp(value, 0f, MaxSquashStretch); }
        }

        public static float MaxSquashStretch
        {
            get { return 0.5f; }
        }

        public float Blend
        {
            get { return blend; }
            set { blend = Mathf.Clamp01(value); }
        }

        public static float MinFrequency
        {
            get { return 0.1f; }
        }

        public static float MaxFrequency
        {
            get { return JiggleMath.MaxOmega / (2f * Mathf.PI); }
        }

        public static float MinDampingRatio
        {
            get { return 0f; }
        }

        public static float MaxDampingRatio
        {
            get { return 1.5f; }
        }

        public static float MinMaxOffset
        {
            get { return 0.01f; }
        }

        public static float MaxMaxOffset
        {
            get { return 1f; }
        }

        public static float MaxPointRadius
        {
            get { return 0.2f; }
        }

        public static int MaxSubstepLimit
        {
            get { return 8; }
        }

        public float GetDeltaTime()
        {
            if (timeMode == JiggleTimeMode.Unscaled)
            {
                return Time.unscaledDeltaTime;
            }
            return Time.deltaTime;
        }

        private void OnValidate()
        {
            frequency = Mathf.Clamp(frequency, MinFrequency, MaxFrequency);
            dampingRatio = Mathf.Clamp(dampingRatio, MinDampingRatio, MaxDampingRatio);
            worldInertia = Mathf.Clamp01(worldInertia);
            localInertia = Mathf.Clamp01(localInertia);
            anchorSpeedLimit = Mathf.Max(0.01f, anchorSpeedLimit);
            anchorAngularSpeedLimit = Mathf.Max(1f, anchorAngularSpeedLimit);
            maxParticleSpeed = Mathf.Max(0.01f, maxParticleSpeed);
            axisWeights.x = Mathf.Max(0f, axisWeights.x);
            axisWeights.y = Mathf.Max(0f, axisWeights.y);
            axisWeights.z = Mathf.Max(0f, axisWeights.z);
            maxAngle = Mathf.Clamp(maxAngle, 0f, 180f);
            maxOffset = Mathf.Clamp(maxOffset, MinMaxOffset, MaxMaxOffset);
            pointRadius = Mathf.Clamp(pointRadius, 0f, MaxPointRadius);
            maxSubsteps = Mathf.Clamp(maxSubsteps, 1, MaxSubstepLimit);
            blend = Mathf.Clamp01(blend);
            squashStretch = Mathf.Clamp(squashStretch, 0f, MaxSquashStretch);
            verletStiffness = Mathf.Clamp(verletStiffness, 0.01f, 0.9f);
            verletDrag = Mathf.Clamp(verletDrag, 0f, 0.9f);
        }
    }
}
