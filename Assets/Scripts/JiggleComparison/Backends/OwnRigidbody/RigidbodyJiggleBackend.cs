using System.Diagnostics;
using JigglePhysics;
using Unity.Profiling;
using UnityEngine;

namespace JiggleComparison
{

    [DefaultExecutionOrder(10002)]
    public sealed class RigidbodyJiggleBackend : MonoBehaviour, IJiggleBackend
    {
        private sealed class Body
        {
            public Transform Root;
            public Transform Parent;
            public Quaternion RestLocalRotation;
            public Vector3 RestTipOffset;
            public Rigidbody Anchor;
            public Rigidbody Mass;
            public Vector3 TargetTip;
        }

        [SerializeField] private JiggleProfile profile;

        [Tooltip("Simulated mass of one breast in kilograms. Spring and damper are scaled by it, so it only changes how gravity reads.")]
        [Min(0.01f)]
        [SerializeField] private float massKilograms = 0.35f;

        [Tooltip("Anchor jump per frame in meters that is treated as a teleport and snaps the bodies.")]
        [Min(0.05f)]
        [SerializeField] private float teleportDistance = 1f;

        private JiggleBoneSet bones;
        private Body[] bodies = new Body[0];
        private GameObject physicsRoot;
        private ProfilerRecorder physicsRecorder;
        private bool active;
        private float scriptMilliseconds;

        public string DisplayName
        {
            get { return "Rigidbody + ConfigurableJoint"; }
        }

        public string Summary
        {
            get { return "PhysX 50 Гц, кінематичний якір + маса на пружинному джоінті, інтерполяція"; }
        }

        public bool IsAvailable
        {
            get { return bones != null && bones.Count > 0 && profile != null; }
        }

        public bool IsActive
        {
            get { return active; }
        }

        public float LastCostMilliseconds
        {
            get
            {
                if (!active)
                {
                    return 0f;
                }

                float physics = physicsRecorder.Valid ? physicsRecorder.LastValue * 1e-6f : 0f;
                return scriptMilliseconds + physics;
            }
        }

        public void Configure(JiggleBoneSet boneSet, JiggleProfile jiggleProfile)
        {
            bones = boneSet;
            profile = jiggleProfile;
        }

        public void SetActive(bool value)
        {
            if (value == active || (value && !IsAvailable))
            {
                return;
            }

            active = value;
            if (active)
            {
                EnsureBodies();
                physicsRoot.SetActive(true);
                ApplyProfile();
                ResetSimulation();
                physicsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics.Simulate");
            }
            else
            {
                if (physicsRoot != null)
                {
                    physicsRoot.SetActive(false);
                }

                bones.RestoreRestPose();
                physicsRecorder.Dispose();
            }
        }

        public void ResetSimulation()
        {
            if (!active)
            {
                return;
            }

            for (int i = 0; i < bodies.Length; i++)
            {
                Body body = bodies[i];
                body.Root.localRotation = body.RestLocalRotation;
                Snap(body, ComputeAnimatedTip(body));
            }
        }

        private void FixedUpdate()
        {
            if (!active)
            {
                return;
            }

            long start = Stopwatch.GetTimestamp();
            Vector3 gravity = Physics.gravity * profile.GravityMultiplier;
            for (int i = 0; i < bodies.Length; i++)
            {
                Body body = bodies[i];
                body.Anchor.MovePosition(body.TargetTip);
                body.Mass.AddForce(gravity, ForceMode.Acceleration);
            }

            scriptMilliseconds += ElapsedMilliseconds(start);
        }

        private void LateUpdate()
        {
            if (!active)
            {
                return;
            }

            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < bodies.Length; i++)
            {
                WriteBone(bodies[i]);
            }

            scriptMilliseconds = ElapsedMilliseconds(start);
        }

        private void WriteBone(Body body)
        {
            if (body.Root == null || body.Parent == null)
            {
                return;
            }

            Quaternion restRotation = body.Parent.rotation * body.RestLocalRotation;
            Vector3 animatedTip = body.Root.position + restRotation * body.RestTipOffset;
            if ((animatedTip - body.TargetTip).sqrMagnitude > teleportDistance * teleportDistance)
            {
                Snap(body, animatedTip);
            }

            body.TargetTip = animatedTip;

            Vector3 offset = body.Mass.transform.position - body.Anchor.transform.position;
            offset = Vector3.ClampMagnitude(offset, profile.MaxOffset);

            Vector3 restDirection = animatedTip - body.Root.position;
            Vector3 simulatedDirection = animatedTip + offset - body.Root.position;
            if (restDirection.sqrMagnitude < JiggleMath.Epsilon || simulatedDirection.sqrMagnitude < JiggleMath.Epsilon)
            {
                body.Root.rotation = restRotation;
                return;
            }

            Vector3 clamped = JiggleMath.ApplyConeConstraint(restDirection.normalized, simulatedDirection.normalized, profile.MaxAngle);
            body.Root.rotation = Quaternion.FromToRotation(restDirection.normalized, clamped) * restRotation;
        }

        private Vector3 ComputeAnimatedTip(Body body)
        {
            Quaternion restRotation = body.Parent.rotation * body.RestLocalRotation;
            return body.Root.position + restRotation * body.RestTipOffset;
        }

        private static void Snap(Body body, Vector3 position)
        {
            body.TargetTip = position;
            body.Anchor.position = position;
            body.Anchor.transform.position = position;
            body.Mass.position = position;
            body.Mass.transform.position = position;
            body.Mass.linearVelocity = Vector3.zero;
        }

        private void EnsureBodies()
        {
            if (physicsRoot != null)
            {
                return;
            }

            physicsRoot = new GameObject("Rigidbody Jiggle (" + name + ")");
            bodies = new Body[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                Transform root = bones.GetRoot(i);
                Transform tip = bones.GetTip(i);
                Body body = new Body
                {
                    Root = root,
                    Parent = root.parent,
                    RestLocalRotation = bones.GetRestLocalRotation(i),
                    RestTipOffset = Quaternion.Inverse(root.rotation) * (tip.position - root.position)
                };

                body.Anchor = CreateBody("Anchor " + root.name, true);
                body.Mass = CreateBody("Mass " + root.name, false);
                ConfigurableJoint joint = body.Mass.gameObject.AddComponent<ConfigurableJoint>();
                joint.connectedBody = body.Anchor;
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector3.zero;
                joint.connectedAnchor = Vector3.zero;
                joint.enablePreprocessing = false;
                joint.xMotion = ConfigurableJointMotion.Limited;
                joint.yMotion = ConfigurableJointMotion.Limited;
                joint.zMotion = ConfigurableJointMotion.Limited;
                joint.angularXMotion = ConfigurableJointMotion.Locked;
                joint.angularYMotion = ConfigurableJointMotion.Locked;
                joint.angularZMotion = ConfigurableJointMotion.Locked;
                bodies[i] = body;
            }
        }

        private Rigidbody CreateBody(string bodyName, bool kinematic)
        {
            GameObject bodyObject = new GameObject(bodyName);
            bodyObject.transform.SetParent(physicsRoot.transform, false);
            Rigidbody body = bodyObject.AddComponent<Rigidbody>();
            body.isKinematic = kinematic;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.mass = massKilograms;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.freezeRotation = true;
            body.automaticInertiaTensor = false;
            body.inertiaTensor = Vector3.one * 0.001f;
            return body;
        }

        private void ApplyProfile()
        {
            float omega = 2f * Mathf.PI * profile.Frequency;
            JointDrive drive = new JointDrive
            {
                positionSpring = massKilograms * omega * omega,
                positionDamper = 2f * profile.DampingRatio * massKilograms * omega,
                maximumForce = float.MaxValue
            };

            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody mass = bodies[i].Mass;
                mass.mass = massKilograms;
                ConfigurableJoint joint = mass.GetComponent<ConfigurableJoint>();
                joint.xDrive = drive;
                joint.yDrive = drive;
                joint.zDrive = drive;
                joint.linearLimit = new SoftJointLimit { limit = profile.MaxOffset };
            }
        }

        private static float ElapsedMilliseconds(long startTimestamp)
        {
            return (float)((Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency);
        }

        private void OnDestroy()
        {
            physicsRecorder.Dispose();
            if (physicsRoot != null)
            {
                Destroy(physicsRoot);
            }
        }
    }
}
