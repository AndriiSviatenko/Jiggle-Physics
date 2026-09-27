using System.Collections.Generic;
using UnityEngine;

namespace JigglePhysics
{
    public class JiggleRig : MonoBehaviour
    {
        [SerializeField] private JiggleProfile defaultProfile;
        [SerializeField] private JiggleSolverConfig solverConfig;
        [SerializeField] private JiggleChainDefinition[] chains = new JiggleChainDefinition[0];
        [SerializeField] private MonoBehaviour[] colliderBehaviours = new MonoBehaviour[0];

        [Tooltip("Teleport distance in meters, measured per frame at 60 fps.")]
        [SerializeField] private float teleportThreshold = 0.5f;

        [Tooltip("Angular discontinuity that resets simulation instead of injecting an extreme impulse.")]
        [Range(1f, 180f)]
        [SerializeField] private float teleportAngle = 100f;

        [Tooltip("Layers whose ordinary Unity colliders push the simulated points out (walls, props). Colliders on this character are ignored.")]
        [SerializeField] private LayerMask worldCollisionMask = 1;

        [SerializeField] private bool drawGizmos = true;

        private readonly List<IJiggleCollider> runtimeColliders = new List<IJiggleCollider>();
        private readonly List<JiggleChainDefinition> runtimeChainDefinitions = new List<JiggleChainDefinition>();
        private readonly List<JiggleChain> reusableChains = new List<JiggleChain>();

        private JiggleChain[] runtimeChains = new JiggleChain[0];
        private IJiggleCollider[] resolvedColliders = new IJiggleCollider[0];
        private const float SubstepRoundingSlack = 1e-3f;

        private static readonly Vector3 ProbeParkingPosition = new Vector3(0f, -100000f, 0f);

        private readonly Collider[] worldOverlapBuffer = new Collider[8];
        private SphereCollider worldProbe;
        private float maxFrequency;
        private int maxSubsteps = 1;
        private bool initialized;
        private bool hasSolverOverride;
        private JiggleSolverKind solverOverride;

        public int ChainCount
        {
            get { return runtimeChains.Length; }
        }

        public bool Initialized
        {
            get { return initialized; }
        }

        public float TeleportThreshold
        {
            get { return teleportThreshold; }
            set { teleportThreshold = Mathf.Max(0f, value); }
        }

        public JiggleProfile DefaultProfile
        {
            get { return defaultProfile; }
        }

        public JiggleChain GetChain(int index)
        {
            if (index < 0 || index >= runtimeChains.Length)
            {
                return null;
            }

            return runtimeChains[index];
        }

        public JiggleSolverKind? SolverKindOverride
        {
            get { return hasSolverOverride ? solverOverride : (JiggleSolverKind?)null; }
        }

        public void SetSolverKind(JiggleSolverKind? kind)
        {
            hasSolverOverride = kind.HasValue;
            if (kind.HasValue)
            {
                solverOverride = kind.Value;
            }

            for (int i = 0; i < runtimeChains.Length; i++)
            {
                runtimeChains[i].SetSolverOverride(kind);
            }
        }

        private void OnEnable()
        {
            initialized = false;
            JiggleSimulation.Register(this);
        }

        private void OnDisable()
        {
            JiggleSimulation.Unregister(this);
            RestoreRestPose();
        }

        private void OnDestroy()
        {
            if (worldProbe != null)
            {
                Destroy(worldProbe.gameObject);
            }
        }

        public void RestoreRestPose()
        {
            for (int i = 0; i < runtimeChains.Length; i++)
            {
                runtimeChains[i].RestoreRestPose();
            }

        }

        public float LastStepMilliseconds { get; private set; }

        public void SetProfile(JiggleProfile profile)
        {
            defaultProfile = profile;
            Rebuild();
        }

        public void AddChain(Transform rootBone, Transform endBone, JiggleProfile profileOverride = null)
        {
            if (rootBone == null)
            {
                Debug.LogWarning("[JigglePhysics] AddChain called with a null root bone.", this);
                return;
            }

            if (ContainsChainDefinition(rootBone, endBone))
            {
                Debug.LogWarning($"[JigglePhysics] Chain on '{rootBone.name}' is already registered on rig '{name}'.", this);
                return;
            }

            JiggleChainDefinition definition = new JiggleChainDefinition
            {
                RootBone = rootBone,
                EndBone = endBone,
                ProfileOverride = profileOverride
            };

            runtimeChainDefinitions.Add(definition);
            Rebuild();
        }

        public void AddCollider(IJiggleCollider collider)
        {
            if (collider == null || runtimeColliders.Contains(collider))
            {
                return;
            }

            runtimeColliders.Add(collider);
            Rebuild();
        }

        public void RemoveCollider(IJiggleCollider collider)
        {
            if (runtimeColliders.Remove(collider))
            {
                Rebuild();
            }
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            Rebuild();
        }

        public LayerMask WorldCollisionMask
        {
            get { return worldCollisionMask; }
            set { worldCollisionMask = value; }
        }

        public void Rebuild()
        {
            EnsureWorldProbe();
            if (solverConfig != null)
            {
                JiggleSolverConfig.Active = solverConfig;
            }

            reusableChains.Clear();
            reusableChains.AddRange(runtimeChains);

            runtimeChains = BuildChains();
            resolvedColliders = BuildColliders();
            CacheTimingParameters();
            initialized = true;
        }

        public void ResetToAnimatedPose()
        {
            for (int i = 0; i < runtimeChains.Length; i++)
            {
                runtimeChains[i].SnapToAnimatedPose();
            }

        }

        public void ResetSimulation()
        {
            ResetToAnimatedPose();
        }

        public float GetDeltaTime()
        {
            if (UsesUnscaledTime())
            {
                return Time.unscaledDeltaTime;
            }

            return Time.deltaTime;
        }

        private bool UsesUnscaledTime()
        {
            for (int i = 0; i < runtimeChains.Length; i++)
            {
                JiggleProfile profile = runtimeChains[i].Profile;
                if (profile != null && profile.TimeMode == JiggleTimeMode.Unscaled)
                {
                    return true;
                }
            }

            return defaultProfile != null && defaultProfile.TimeMode == JiggleTimeMode.Unscaled;
        }

        public void Step(float frameDeltaTime)
        {
            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            StepInternal(frameDeltaTime);
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
            LastStepMilliseconds = (float)(elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
        }

        private void StepInternal(float frameDeltaTime)
        {
            if (!initialized)
            {
                Initialize();
            }

            if (runtimeChains.Length == 0)
            {
                return;
            }

            for (int i = 0; i < runtimeChains.Length; i++)
            {
                runtimeChains[i].PrepareFrame(frameDeltaTime, teleportThreshold, teleportAngle);
            }

            if (frameDeltaTime <= JiggleMath.Epsilon)
            {
                for (int i = 0; i < runtimeChains.Length; i++)
                {
                    runtimeChains[i].WriteBack();
                }
                return;
            }

            float fixedDeltaTime = JiggleSolverConfig.Active.FixedDeltaTime;
            float budget = Mathf.Min(frameDeltaTime, JiggleSolverConfig.Active.MaxFrameDeltaTime);
            int substeps = Mathf.Clamp(Mathf.CeilToInt(budget / fixedDeltaTime - SubstepRoundingSlack), 1, maxSubsteps);
            float stepTime = budget / substeps;

            for (int step = 0; step < substeps; step++)
            {
                for (int i = 0; i < runtimeChains.Length; i++)
                {
                    runtimeChains[i].Simulate(stepTime);
                    runtimeChains[i].ApplyCollisions(resolvedColliders);
                    runtimeChains[i].ApplyWorldCollisions(worldCollisionMask, worldProbe, worldOverlapBuffer, transform);
                }
            }

            for (int i = 0; i < runtimeChains.Length; i++)
            {
                runtimeChains[i].WriteBack();
            }
        }

        private void EnsureWorldProbe()
        {
            if (worldProbe != null || !Application.isPlaying)
            {
                return;
            }

            GameObject probeObject = new GameObject("Jiggle World Probe");
            probeObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            probeObject.layer = 2;
            probeObject.transform.position = ProbeParkingPosition;
            worldProbe = probeObject.AddComponent<SphereCollider>();
            worldProbe.isTrigger = true;
        }

        private void OnValidate()
        {
            teleportThreshold = Mathf.Max(0f, teleportThreshold);
            teleportAngle = Mathf.Clamp(teleportAngle, 1f, 180f);
        }

        private void CacheTimingParameters()
        {
            maxFrequency = JiggleProfile.MinFrequency;
            maxSubsteps = 1;

            for (int i = 0; i < runtimeChains.Length; i++)
            {
                JiggleProfile profile = runtimeChains[i].Profile;
                if (profile == null)
                {
                    continue;
                }

                maxFrequency = Mathf.Max(maxFrequency, profile.Frequency);
                maxSubsteps = Mathf.Max(maxSubsteps, profile.MaxSubsteps);
            }
        }

        private bool ContainsChainDefinition(Transform rootBone, Transform endBone)
        {
            for (int i = 0; i < chains.Length; i++)
            {
                if (chains[i] != null && chains[i].RootBone == rootBone && chains[i].EndBone == endBone)
                {
                    return true;
                }
            }

            for (int i = 0; i < runtimeChainDefinitions.Count; i++)
            {
                JiggleChainDefinition definition = runtimeChainDefinitions[i];
                if (definition.RootBone == rootBone && definition.EndBone == endBone)
                {
                    return true;
                }
            }

            return false;
        }

        private JiggleChain[] BuildChains()
        {
            List<JiggleChain> result = new List<JiggleChain>();

            for (int i = 0; i < chains.Length; i++)
            {
                AddChainFromDefinition(chains[i], result);
            }

            for (int i = 0; i < runtimeChainDefinitions.Count; i++)
            {
                AddChainFromDefinition(runtimeChainDefinitions[i], result);
            }

            return result.ToArray();
        }

        private void AddChainFromDefinition(JiggleChainDefinition definition, List<JiggleChain> target)
        {
            if (definition == null || definition.RootBone == null)
            {
                return;
            }

            JiggleProfile profile = definition.ProfileOverride != null ? definition.ProfileOverride : defaultProfile;
            if (profile == null)
            {
                Debug.LogWarning($"[JigglePhysics] Rig '{name}' has no profile for bone '{definition.RootBone.name}'.", this);
                return;
            }

            JiggleChain existing = FindReusableChain(definition);
            if (existing != null)
            {
                existing.SetSolverOverride(SolverKindOverride);
                target.Add(existing);
                return;
            }

            JiggleChain chain = new JiggleChain(definition.RootBone, definition.EndBone, profile);
            if (chain.IsValid)
            {
                chain.SetSolverOverride(SolverKindOverride);
                target.Add(chain);
            }
        }

        private JiggleChain FindReusableChain(JiggleChainDefinition definition)
        {
            for (int i = 0; i < reusableChains.Count; i++)
            {
                JiggleChain candidate = reusableChains[i];
                if (candidate == null || !candidate.IsValid)
                {
                    continue;
                }

                if (candidate.GetBone(0) == definition.RootBone && candidate.Profile == ResolveProfileFor(definition))
                {
                    reusableChains.RemoveAt(i);
                    return candidate;
                }
            }

            return null;
        }

        private JiggleProfile ResolveProfileFor(JiggleChainDefinition definition)
        {
            return definition.ProfileOverride != null ? definition.ProfileOverride : defaultProfile;
        }

        private IJiggleCollider[] BuildColliders()
        {
            List<IJiggleCollider> result = new List<IJiggleCollider>();

            for (int i = 0; i < colliderBehaviours.Length; i++)
            {
                MonoBehaviour behaviour = colliderBehaviours[i];
                if (behaviour == null || !JiggleMath.IsAlive(behaviour))
                {
                    continue;
                }

                if (behaviour is IJiggleCollider serializedCollider && !result.Contains(serializedCollider))
                {
                    result.Add(serializedCollider);
                }
            }

            for (int i = runtimeColliders.Count - 1; i >= 0; i--)
            {
                IJiggleCollider collider = runtimeColliders[i];
                if (collider == null || !JiggleMath.IsAlive(collider))
                {
                    runtimeColliders.RemoveAt(i);
                    continue;
                }

                if (!result.Contains(collider))
                {
                    result.Add(collider);
                }
            }

            return result.ToArray();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
            {
                return;
            }

            for (int i = 0; i < runtimeChains.Length; i++)
            {
                runtimeChains[i].DrawGizmos();
            }
        }
    }
}
