using System.Diagnostics;
using System.Reflection;
using GatorDragonGames.JigglePhysics;
using UnityEngine;
using FrameworkRig = GatorDragonGames.JigglePhysics.JiggleRig;
using FrameworkSolver = GatorDragonGames.JigglePhysics.JigglePhysics;

namespace JiggleComparison
{

    [DefaultExecutionOrder(10001)]
    public sealed class FrameworkJiggleBackend : MonoBehaviour, IJiggleBackend
    {
        private static readonly FieldInfo RigDataField = typeof(FrameworkRig).GetField("jiggleRigData", BindingFlags.Instance | BindingFlags.NonPublic);

        [Header("JiggleBreasts preset")]
        [Range(0f, 1f)] [SerializeField] private float stiffness = 0.75f;
        [Range(0f, 1f)] [SerializeField] private float soften = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float angleLimit = 0.65f;
        [Range(0f, 1f)] [SerializeField] private float angleLimitSoften = 1f;
        [Range(0f, 1f)] [SerializeField] private float ignoreRootMotion = 0.462f;
        [Range(0f, 1f)] [SerializeField] private float stretch = 0.55f;
        [Range(0f, 1f)] [SerializeField] private float drag = 0.033f;
        [SerializeField] private float gravity = 1f;

        private JiggleBoneSet bones;
        private FrameworkRig[] rigs = new FrameworkRig[0];
        private bool active;
        private float lastCost;
        private const float TeleportDistance = 1f;

        private static bool quitting;
        private Vector3 lastAnchor;
        private bool hasLastAnchor;

        public string DisplayName
        {
            get { return "naelstrof JigglePhysics"; }
        }

        public string Summary
        {
            get { return "Фреймворк v16: Burst-джоби, верлет-точки, пресет JiggleBreasts. Телепорти ловить адаптер — у пакеті їх немає"; }
        }

        public bool IsAvailable
        {
            get { return bones != null && bones.Count > 0 && RigDataField != null; }
        }

        public bool IsActive
        {
            get { return active; }
        }

        public float LastCostMilliseconds
        {
            get { return active ? lastCost : 0f; }
        }

        public void Configure(JiggleBoneSet boneSet)
        {
            bones = boneSet;
        }

        public void SetActive(bool value)
        {
            if (value == active || (value && !IsAvailable))
            {
                return;
            }

            active = value;
            hasLastAnchor = false;
            if (active)
            {
                bones.RestoreRestPose();
                EnsureRigs();
            }

            for (int i = 0; i < rigs.Length; i++)
            {
                if (!active)
                {
                    rigs[i].SnapToRestPose();
                }

                rigs[i].gameObject.SetActive(active);
            }

            if (!active)
            {
                bones.RestoreRestPose();
            }
        }

        public void ResetSimulation()
        {
            if (!active)
            {
                return;
            }

            for (int i = 0; i < rigs.Length; i++)
            {
                rigs[i].gameObject.SetActive(false);
            }

            bones.RestoreRestPose();
            for (int i = 0; i < rigs.Length; i++)
            {
                rigs[i].gameObject.SetActive(true);
            }
        }

        private void LateUpdate()
        {
            if (!active)
            {
                return;
            }

            Vector3 anchor = bones.GetRoot(0).parent.position;
            if (hasLastAnchor && (anchor - lastAnchor).sqrMagnitude > TeleportDistance * TeleportDistance)
            {
                ResetSimulation();
            }

            lastAnchor = anchor;
            hasLastAnchor = true;

            long start = Stopwatch.GetTimestamp();
            double time = Time.timeAsDouble;
            FrameworkSolver.ScheduleSimulate(time, Time.fixedDeltaTime);
            FrameworkSolver.SchedulePose(time);
            FrameworkSolver.CompletePose();
            lastCost = (float)((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }

        private void EnsureRigs()
        {
            if (rigs.Length > 0)
            {
                return;
            }

            rigs = new FrameworkRig[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                Transform root = bones.GetRoot(i);
                GameObject host = new GameObject("Framework Jiggle " + root.name);
                host.SetActive(false);
                host.transform.SetParent(transform, false);

                FrameworkRig rig = host.AddComponent<FrameworkRig>();
                JiggleRigData data = JiggleRigData.Default();
                data.rootBone = root;
                data.jiggleTreeInputParameters = CreateParameters();
                data.OnValidate();
                RigDataField.SetValue(rig, data);
                rigs[i] = rig;
            }
        }

        private JiggleTreeInputParameters CreateParameters()
        {
            JiggleTreeInputParameters parameters = JiggleTreeInputParameters.Default();
            parameters.advancedToggle = true;
            parameters.angleLimitToggle = true;
            parameters.collisionToggle = false;
            parameters.stiffness = new JiggleTreeCurvedFloat(stiffness);
            parameters.soften = soften;
            parameters.angleLimit = new JiggleTreeCurvedFloat(angleLimit);
            parameters.angleLimitSoften = angleLimitSoften;
            parameters.ignoreRootMotion = ignoreRootMotion;
            parameters.stretch = new JiggleTreeCurvedFloat(stretch);
            parameters.drag = new JiggleTreeCurvedFloat(drag);
            parameters.airDrag = new JiggleTreeCurvedFloat(0f);
            parameters.gravity = new JiggleTreeCurvedFloat(gravity);
            parameters.blend = 1f;
            return parameters;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            quitting = false;
        }

        private void OnApplicationQuit()
        {
            quitting = true;
        }

        private void OnDestroy()
        {
            if (quitting)
            {
                FrameworkSolver.Dispose();
            }
        }
    }
}
