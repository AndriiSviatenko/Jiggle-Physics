using System.Collections.Generic;
using UnityEngine;

namespace JigglePhysics
{
    public class JiggleChain
    {
        private readonly JiggleProfile profile;
        private readonly Transform rootBone;
        private readonly Transform parentOfRoot;
        private readonly string debugName;

        private JiggleParticle[] particles = new JiggleParticle[0];
        private Quaternion[] restLocalRotations = new Quaternion[0];
        private Vector3[] restTipOffsets = new Vector3[0];
        private Quaternion[] restWorldRotations = new Quaternion[0];

        private Vector3[] anchors = new Vector3[0];
        private Vector3[] animatedTips = new Vector3[0];
        private Quaternion[] animatedRotations = new Quaternion[0];
        private Quaternion[] simulatedRotations = new Quaternion[0];

        private Vector3[] pureAnchors = new Vector3[0];
        private Vector3[] pureTips = new Vector3[0];
        private Quaternion[] pureRotations = new Quaternion[0];
        private Quaternion[] lastWrittenLocalRotations = new Quaternion[0];
        private Vector3[] restLocalScales = new Vector3[0];
        private int[] stretchAxes = new int[0];
        private float[] verticalBaselines = new float[0];
        private bool hasWrittenPose;
        private bool hasWrittenScale;
        private float lastFrameDeltaTime;
        private float lastStepTime;
        private Vector3[] springAccelerations = new Vector3[0];
        private Vector3[] debugVelocities = new Vector3[0];

        private Vector3 previousAnchorPosition;
        private Quaternion previousAnchorRotation;
        private Vector3 filteredAnchorPosition;
        private Quaternion filteredAnchorRotation = Quaternion.identity;
        private bool hasAnchorPose;

        private const float SquashBaselineTime = 0.6f;
        private const float VerletReferenceRate = 60f;
        private const float MaxVerletPull = 0.9f;
        private const float AnchorFilterTime = 0.035f;
        private const float CollisionRestTolerance = 1e-5f;

        private bool hasSolverOverride;
        private JiggleSolverKind solverOverride;

        public JiggleChain(Transform rootBone, Transform endBone, JiggleProfile profile)
        {
            this.rootBone = rootBone;
            this.profile = profile;
            debugName = rootBone != null ? rootBone.name : "null";

            if (rootBone == null)
            {
                return;
            }

            parentOfRoot = rootBone.parent;
            Build(rootBone, endBone);
            SnapToAnimatedPose();
        }

        public int ParticleCount
        {
            get { return particles.Length; }
        }

        public JiggleProfile Profile
        {
            get { return profile; }
        }

        public JiggleSolverKind ActiveSolverKind
        {
            get
            {
                if (hasSolverOverride)
                {
                    return solverOverride;
                }

                return profile != null ? profile.SolverKind : JiggleSolverKind.SpringDamper;
            }
        }

        public void SetSolverOverride(JiggleSolverKind? kind)
        {
            hasSolverOverride = kind.HasValue;
            if (kind.HasValue)
            {
                solverOverride = kind.Value;
            }
        }

        public string DebugName
        {
            get { return debugName; }
        }

        public bool IsValid
        {
            get { return particles.Length > 0 && rootBone != null && profile != null; }
        }

        public int SnapCount { get; private set; }

        public Vector3 GetAnimatedTip(int index)
        {
            if (index < 0 || index >= animatedTips.Length)
            {
                return Vector3.zero;
            }
            return animatedTips[index];
        }

        public Vector3 GetPureTip(int index)
        {
            if (index < 0 || index >= pureTips.Length)
            {
                return Vector3.zero;
            }
            return pureTips[index];
        }

        public Vector3 GetAnchor(int index)
        {
            if (index < 0 || index >= anchors.Length)
            {
                return Vector3.zero;
            }
            return anchors[index];
        }

        public Vector3 GetSimulatedTip(int index)
        {
            if (index < 0 || index >= particles.Length)
            {
                return Vector3.zero;
            }
            return particles[index].Position;
        }

        public float GetBoneLength(int index)
        {
            if (index < 0 || index >= particles.Length)
            {
                return 0f;
            }
            return particles[index].Length;
        }

        public Transform GetBone(int index)
        {
            if (index < 0 || index >= particles.Length)
            {
                return null;
            }
            return particles[index].Bone;
        }

        public Vector3 GetVelocity(int index)
        {
            if (index < 0 || index >= debugVelocities.Length)
            {
                return Vector3.zero;
            }

            return debugVelocities[index];
        }

        public Vector3 GetSpringAcceleration(int index)
        {
            if (index < 0 || index >= springAccelerations.Length)
            {
                return Vector3.zero;
            }

            return springAccelerations[index];
        }

        public void SnapToAnimatedPose()
        {
            if (!IsValid)
            {
                return;
            }

            RefreshPose();

            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Snap(pureTips[i]);
                simulatedRotations[i] = pureRotations[i];

                Transform bone = particles[i].Bone;
                if (bone != null)
                {
                    bone.rotation = pureRotations[i];
                    bone.localScale = restLocalScales[i];
                }

                verticalBaselines[i] = 0f;
                springAccelerations[i] = Vector3.zero;
                debugVelocities[i] = Vector3.zero;
            }

            hasWrittenScale = false;

            CacheAnchorPose();
            SnapCount++;
        }

        public void PrepareFrame(float frameDeltaTime, float teleportThreshold, float teleportAngle)
        {
            if (!IsValid)
            {
                return;
            }

            lastFrameDeltaTime = frameDeltaTime;
            CaptureAnimatedPose();
            RefreshPose();

            Vector3 anchorPosition = GetAnchorPosition();
            Quaternion anchorRotation = GetAnchorRotation();

            if (!hasAnchorPose)
            {
                previousAnchorPosition = anchorPosition;
                previousAnchorRotation = anchorRotation;
                filteredAnchorPosition = anchorPosition;
                filteredAnchorRotation = anchorRotation;
                hasAnchorPose = true;
                return;
            }

            float allowedDistance = JiggleMath.ComputeTeleportThreshold(teleportThreshold, frameDeltaTime);
            float angleDelta = Quaternion.Angle(previousAnchorRotation, anchorRotation);
            bool teleported = Vector3.Distance(previousAnchorPosition, anchorPosition) > allowedDistance
                || angleDelta > teleportAngle;

            if (teleported)
            {
                SnapToAnimatedPose();
                return;
            }

            ApplyAnchorInertia(
                anchorPosition,
                anchorRotation,
                Mathf.Max(frameDeltaTime, JiggleMath.Epsilon));

            previousAnchorPosition = anchorPosition;
            previousAnchorRotation = anchorRotation;
        }

        public void TeleportIfNeeded(float threshold, float deltaTime)
        {
            if (!IsValid)
            {
                return;
            }

            RefreshPose();
            float allowed = JiggleMath.ComputeTeleportThreshold(threshold, deltaTime);
            if ((pureTips[0] - particles[0].Position).sqrMagnitude > allowed * allowed)
            {
                SnapToAnimatedPose();
            }
        }

        public void Simulate(float deltaTime)
        {
            if (!IsValid)
            {
                return;
            }

            RefreshPose();

            float stepRatio = lastStepTime > JiggleMath.Epsilon ? Mathf.Clamp(deltaTime / lastStepTime, 0.25f, 4f) : 1f;
            lastStepTime = deltaTime;

            JiggleSolverKind solver = ActiveSolverKind;
            Vector3 gravity = Physics.gravity * profile.GravityMultiplier;
            float retention = JiggleMath.ComputeDampingRetention(profile.DampingRatio, profile.Frequency, deltaTime);
            float omegaSqr = JiggleMath.ComputeOmega(profile.Frequency);
            omegaSqr *= omegaSqr;

            for (int i = 0; i < particles.Length; i++)
            {
                JiggleParticle particle = particles[i];
                Vector3 animatedTip = animatedTips[i];

                particle.Position = JiggleMath.Sanitize(particle.Position, animatedTip);
                particle.PreviousPosition = JiggleMath.Sanitize(particle.PreviousPosition, particle.Position);
                particle.Velocity = JiggleMath.Sanitize(particle.Velocity, Vector3.zero);

                Vector3 before = particle.Position;
                IntegrateParticle(ref particle, solver, animatedTip, gravity, retention, omegaSqr, deltaTime, stepRatio, i);
                ClampParticleStep(ref particle, before, deltaTime);
                particles[i] = particle;

                ApplyLengthConstraint(i);
                ApplyAngleConstraint(i);
                ApplyOffsetClamp(i);
                CacheSimulatedRotation(i);
            }
        }

        private void IntegrateParticle(ref JiggleParticle particle, JiggleSolverKind solver, Vector3 animatedTip, Vector3 gravity, float retention, float omegaSqr, float deltaTime, float stepRatio, int index)
        {
            if (solver == JiggleSolverKind.MinimalSpring)
            {
                float omega = JiggleMath.ComputeOmega(profile.Frequency);
                float velocityRetention = Mathf.Exp(-2f * profile.DampingRatio * omega * deltaTime);
                Vector3 spring = ApplyAxisWeights((animatedTip - particle.Position) * omegaSqr);
                springAccelerations[index] = spring;
                particle.SpringIntegrate(gravity + spring, velocityRetention, deltaTime);
                debugVelocities[index] = particle.Velocity;
                return;
            }

            if (solver == JiggleSolverKind.StableVerlet)
            {

                float referenceSteps = deltaTime * VerletReferenceRate;
                float pullFraction = Mathf.Min(profile.VerletStiffness * referenceSteps * referenceSteps, MaxVerletPull);
                float velocityRetention = Mathf.Pow(1f - profile.VerletDrag, referenceSteps);
                float stepSqr = Mathf.Max(deltaTime * deltaTime, JiggleMath.Epsilon);
                Vector3 pull = (animatedTip - particle.Position) * pullFraction;
                Vector3 verletSpringAcceleration = pull / stepSqr;
                springAccelerations[index] = verletSpringAcceleration;
                particle.VerletIntegrate(gravity + verletSpringAcceleration, velocityRetention, deltaTime, stepRatio);
                debugVelocities[index] = (particle.Position - particle.PreviousPosition) / Mathf.Max(deltaTime, JiggleMath.Epsilon);
                return;
            }

            Vector3 springAcceleration = ApplyAxisWeights((animatedTip - particle.Position) * omegaSqr);
            springAccelerations[index] = springAcceleration;
            particle.VerletIntegrate(gravity + springAcceleration, retention, deltaTime, stepRatio);
            debugVelocities[index] = (particle.Position - particle.PreviousPosition) / Mathf.Max(deltaTime, JiggleMath.Epsilon);
        }

        private void ClampParticleStep(ref JiggleParticle particle, Vector3 before, float deltaTime)
        {
            Vector3 step = particle.Position - before;
            float maxStep = profile.MaxParticleSpeed * deltaTime;
            if (step.sqrMagnitude > maxStep * maxStep)
            {
                particle.Position = before + step.normalized * maxStep;
            }
        }

        public void ApplyCollisions(IJiggleCollider[] colliders)
        {
            if (!IsValid || colliders == null || colliders.Length == 0)
            {
                return;
            }

            for (int i = 0; i < particles.Length; i++)
            {
                JiggleParticle particle = particles[i];
                Vector3 before = particle.Position;
                Vector3 resolved = before;

                for (int c = 0; c < colliders.Length; c++)
                {
                    IJiggleCollider collider = colliders[c];
                    if (collider == null || !JiggleMath.IsAlive(collider) || !collider.IsActive)
                    {
                        continue;
                    }

                    Vector3 animatedTip = animatedTips[i];
                    float allowedDepth = (collider.Resolve(animatedTip, profile.PointRadius) - animatedTip).magnitude;
                    Vector3 push = collider.Resolve(resolved, profile.PointRadius) - resolved;
                    float depth = push.magnitude;
                    if (depth > allowedDepth + CollisionRestTolerance)
                    {
                        resolved += push * ((depth - allowedDepth) / depth);
                    }
                }

                if (resolved == before)
                {
                    continue;
                }

                Vector3 delta = resolved - before;
                particle.Position = JiggleMath.Sanitize(resolved, animatedTips[i]);
                particle.PreviousPosition += delta;
                particles[i] = particle;

                ApplyAngleConstraint(i);
                CacheSimulatedRotation(i);
            }
        }

        public void ApplyWorldCollisions(LayerMask mask, SphereCollider probe, Collider[] buffer, Transform ignoreRoot)
        {
            if (!IsValid || mask.value == 0 || probe == null || buffer == null)
            {
                return;
            }

            float radius = profile.PointRadius;
            probe.radius = radius;

            for (int i = 0; i < particles.Length; i++)
            {
                JiggleParticle particle = particles[i];
                Vector3 before = particle.Position;
                Vector3 resolved = before;

                int count = Physics.OverlapSphereNonAlloc(resolved, radius, buffer, mask, QueryTriggerInteraction.Ignore);
                for (int c = 0; c < count; c++)
                {
                    Collider other = buffer[c];
                    if (other == null || other == probe || (ignoreRoot != null && other.transform.IsChildOf(ignoreRoot)))
                    {
                        continue;
                    }

                    float allowedDepth = Physics.ComputePenetration(probe, animatedTips[i], Quaternion.identity, other,
                        other.transform.position, other.transform.rotation, out _, out float animatedDepth) ? animatedDepth : 0f;

                    if (Physics.ComputePenetration(probe, resolved, Quaternion.identity, other, other.transform.position, other.transform.rotation,
                            out Vector3 direction, out float distance) && distance > allowedDepth)
                    {
                        resolved += direction * (distance - allowedDepth);
                    }
                }

                if (resolved == before)
                {
                    continue;
                }

                Vector3 delta = resolved - before;
                particle.Position = JiggleMath.Sanitize(resolved, animatedTips[i]);
                particle.PreviousPosition += delta;
                particles[i] = particle;

                ApplyAngleConstraint(i);
                CacheSimulatedRotation(i);
            }
        }

        public void WriteBack()
        {
            if (!IsValid)
            {
                return;
            }

            float blend = profile.Blend;

            for (int i = 0; i < particles.Length; i++)
            {
                Transform bone = particles[i].Bone;
                if (bone == null)
                {
                    continue;
                }

                if (blend <= 0f)
                {
                    bone.rotation = pureRotations[i];
                    continue;
                }

                bone.rotation = blend >= 1f
                    ? simulatedRotations[i]
                    : Quaternion.Slerp(pureRotations[i], simulatedRotations[i], blend);

                lastWrittenLocalRotations[i] = bone.localRotation;
            }

            WriteSquashStretch(blend);
            hasWrittenPose = true;
        }

        public void RestoreRestPose()
        {
            for (int i = 0; i < particles.Length; i++)
            {
                Transform bone = particles[i].Bone;
                if (bone == null)
                {
                    continue;
                }

                bone.localRotation = restLocalRotations[i];
                bone.localScale = restLocalScales[i];
                lastWrittenLocalRotations[i] = bone.localRotation;
                verticalBaselines[i] = 0f;
            }

            hasWrittenScale = false;
        }

        public float GetTipDeviation(int index)
        {
            if (index < 0 || index >= particles.Length)
            {
                return 0f;
            }

            return Vector3.Distance(particles[index].Position, pureTips[index]);
        }

        private void WriteSquashStretch(float blend)
        {
            float amount = profile.SquashStretch * Mathf.Clamp01(blend);
            if (amount <= 0f)
            {
                if (hasWrittenScale)
                {
                    for (int i = 0; i < particles.Length; i++)
                    {
                        if (particles[i].Bone != null)
                        {
                            particles[i].Bone.localScale = restLocalScales[i];
                        }
                    }

                    hasWrittenScale = false;
                }

                return;
            }

            Vector3 up = GetAnchorRotation() * Vector3.up;
            float range = Mathf.Max(profile.MaxOffset, JiggleMath.Epsilon);
            float baselineBlend = 1f - Mathf.Exp(-Mathf.Max(0f, lastFrameDeltaTime) / SquashBaselineTime);

            for (int i = 0; i < particles.Length; i++)
            {
                Transform bone = particles[i].Bone;
                if (bone == null)
                {
                    continue;
                }

                float vertical = Vector3.Dot(particles[i].Position - pureTips[i], up);
                verticalBaselines[i] = Mathf.Lerp(verticalBaselines[i], vertical, baselineBlend);
                float normalized = Mathf.Clamp((vertical - verticalBaselines[i]) / range, -1f, 1f);
                float axial = Mathf.Max(0.5f, 1f - amount * normalized);
                float radial = 1f / Mathf.Sqrt(axial);

                Vector3 scale = new Vector3(radial, radial, radial);
                scale[stretchAxes[i]] = axial;
                bone.localScale = Vector3.Scale(restLocalScales[i], scale);
            }

            hasWrittenScale = true;
        }

        public void DrawGizmos()
        {
            if (!IsValid)
            {
                return;
            }

            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i].Bone == null)
                {
                    continue;
                }

                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(pureAnchors[i], pureTips[i]);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(anchors[i], particles[i].Position);
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(particles[i].Position, profile.PointRadius);
            }
        }

        private void RefreshPose()
        {
            for (int i = 0; i < particles.Length; i++)
            {
                Transform bone = particles[i].Bone;

                Quaternion pureRotation;
                if (i == 0)
                {
                    pureRotation = parentOfRoot != null
                        ? parentOfRoot.rotation * restLocalRotations[0]
                        : restWorldRotations[0];
                }
                else
                {
                    pureRotation = pureRotations[i - 1] * restLocalRotations[i];
                }

                Vector3 pureAnchor = i == 0
                    ? (bone != null ? bone.position : Vector3.zero)
                    : pureTips[i - 1];

                pureRotations[i] = pureRotation;
                pureAnchors[i] = pureAnchor;
                pureTips[i] = pureAnchor + pureRotation * restTipOffsets[i];

                Quaternion animatedRotation = i == 0
                    ? pureRotation
                    : simulatedRotations[i - 1] * restLocalRotations[i];

                Vector3 anchor = i == 0
                    ? (bone != null ? bone.position : Vector3.zero)
                    : particles[i - 1].Position;

                animatedRotations[i] = animatedRotation;
                anchors[i] = anchor;
                animatedTips[i] = anchor + animatedRotation * restTipOffsets[i];
            }
        }

        private void ApplyLengthConstraint(int index)
        {
            JiggleParticle particle = particles[index];
            Vector3 offset = particle.Position - anchors[index];
            float length = particle.Length;

            if (offset.sqrMagnitude < JiggleMath.Epsilon)
            {
                particle.Position = anchors[index] + animatedRotations[index] * restTipOffsets[index].normalized * length;
            }
            else
            {
                particle.Position = anchors[index] + offset.normalized * length;
            }

            particles[index] = particle;
        }

        private void ApplyAngleConstraint(int index)
        {
            JiggleParticle particle = particles[index];
            Vector3 anchor = anchors[index];
            Vector3 animatedDirection = animatedTips[index] - anchor;
            Vector3 simulatedDirection = particle.Position - anchor;

            if (animatedDirection.sqrMagnitude < JiggleMath.Epsilon || simulatedDirection.sqrMagnitude < JiggleMath.Epsilon)
            {
                return;
            }

            Vector3 clamped = JiggleMath.ApplyConeConstraint(
                animatedDirection.normalized,
                simulatedDirection.normalized,
                profile.MaxAngle);

            particle.Position = anchor + clamped * simulatedDirection.magnitude;
            particles[index] = particle;
        }

        private void ApplyOffsetClamp(int index)
        {
            JiggleParticle particle = particles[index];
            Vector3 offset = particle.Position - animatedTips[index];
            particle.Position = animatedTips[index] + JiggleMath.ClampMagnitudeSafe(offset, profile.MaxOffset);
            particles[index] = particle;
        }

        private void CacheSimulatedRotation(int index)
        {
            Vector3 animatedDirection = animatedTips[index] - anchors[index];
            Vector3 simulatedDirection = particles[index].Position - anchors[index];

            if (animatedDirection.sqrMagnitude < JiggleMath.Epsilon || simulatedDirection.sqrMagnitude < JiggleMath.Epsilon)
            {
                simulatedRotations[index] = animatedRotations[index];
                return;
            }

            Quaternion delta = Quaternion.FromToRotation(animatedDirection.normalized, simulatedDirection.normalized);
            simulatedRotations[index] = delta * animatedRotations[index];
        }

        private void Build(Transform root, Transform endBone)
        {
            List<Transform> bones = CollectBones(root, endBone);
            if (bones.Count == 0)
            {
                particles = new JiggleParticle[0];
                return;
            }

            Transform tipBone = ResolveTipBone(bones, endBone);
            int count = bones.Count;

            particles = new JiggleParticle[count];
            restLocalRotations = new Quaternion[count];
            restTipOffsets = new Vector3[count];
            restWorldRotations = new Quaternion[count];
            anchors = new Vector3[count];
            animatedTips = new Vector3[count];
            animatedRotations = new Quaternion[count];
            simulatedRotations = new Quaternion[count];
            pureAnchors = new Vector3[count];
            pureTips = new Vector3[count];
            pureRotations = new Quaternion[count];
            lastWrittenLocalRotations = new Quaternion[count];
            restLocalScales = new Vector3[count];
            stretchAxes = new int[count];
            verticalBaselines = new float[count];
            springAccelerations = new Vector3[count];
            debugVelocities = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                Transform bone = bones[i];
                Transform next = i + 1 < count ? bones[i + 1] : tipBone;
                Vector3 worldOffset = ResolveRestOffset(bone, next);

                restLocalRotations[i] = bone.localRotation;
                restWorldRotations[i] = bone.rotation;
                restTipOffsets[i] = Quaternion.Inverse(bone.rotation) * worldOffset;
                restLocalScales[i] = bone.localScale;
                stretchAxes[i] = DominantAxis(restTipOffsets[i]);

                Vector3 tip = bone.position + worldOffset;
                particles[i] = new JiggleParticle
                {
                    Bone = bone,
                    Length = worldOffset.magnitude,
                    Position = tip,
                    PreviousPosition = tip
                };

                animatedRotations[i] = bone.rotation;
                simulatedRotations[i] = bone.rotation;
                pureRotations[i] = bone.rotation;
                anchors[i] = bone.position;
                pureAnchors[i] = bone.position;
                animatedTips[i] = tip;
                pureTips[i] = tip;
                lastWrittenLocalRotations[i] = bone.localRotation;
            }

            CacheAnchorPose();
        }

        private void CaptureAnimatedPose()
        {
            if (!hasWrittenPose)
            {
                return;
            }

            for (int i = 0; i < particles.Length; i++)
            {
                Transform bone = particles[i].Bone;
                if (bone == null)
                {
                    continue;
                }

                if (Quaternion.Angle(bone.localRotation, lastWrittenLocalRotations[i]) > 0.01f)
                {
                    restLocalRotations[i] = bone.localRotation;
                }
            }
        }

        private void ApplyAnchorInertia(Vector3 anchorPosition, Quaternion anchorRotation, float frameDeltaTime)
        {

            float filterBlend = 1f - Mathf.Exp(-frameDeltaTime / AnchorFilterTime);
            Vector3 nextFilteredPosition = Vector3.Lerp(filteredAnchorPosition, anchorPosition, filterBlend);
            Quaternion nextFilteredRotation = Quaternion.Slerp(filteredAnchorRotation, anchorRotation, filterBlend);

            Vector3 rawTranslation = anchorPosition - previousAnchorPosition;
            Vector3 smoothTranslation = nextFilteredPosition - filteredAnchorPosition;
            Vector3 kinkTranslation = rawTranslation - smoothTranslation;

            float distance = smoothTranslation.magnitude;
            float maxDistance = profile.AnchorSpeedLimit * frameDeltaTime;
            float excessDistance = Mathf.Max(0f, distance - maxDistance);
            float followDistance = distance * (1f - profile.WorldInertia) + excessDistance * profile.WorldInertia;
            Vector3 historyTranslation = kinkTranslation + (distance > JiggleMath.Epsilon
                ? smoothTranslation * (followDistance / distance)
                : Vector3.zero);

            Quaternion rawRotation = anchorRotation * Quaternion.Inverse(previousAnchorRotation);
            Quaternion smoothRotation = nextFilteredRotation * Quaternion.Inverse(filteredAnchorRotation);
            Quaternion kinkRotation = rawRotation * Quaternion.Inverse(smoothRotation);

            float angle = Quaternion.Angle(Quaternion.identity, smoothRotation);
            float maxAngle = profile.AnchorAngularSpeedLimit * frameDeltaTime;
            float excessAngle = Mathf.Max(0f, angle - maxAngle);
            float followAngle = angle * (1f - profile.LocalInertia) + excessAngle * profile.LocalInertia;
            Quaternion followRotation = angle > JiggleMath.Epsilon
                ? Quaternion.Slerp(Quaternion.identity, smoothRotation, followAngle / angle)
                : Quaternion.identity;
            Quaternion historyRotation = kinkRotation * followRotation;

            filteredAnchorPosition = nextFilteredPosition;
            filteredAnchorRotation = nextFilteredRotation;

            for (int i = 0; i < particles.Length; i++)
            {
                JiggleParticle particle = particles[i];
                particle.TranslateHistory(historyTranslation);
                particle.RotateHistory(anchorPosition, historyRotation);
                particles[i] = particle;
            }
        }

        private Vector3 ApplyAxisWeights(Vector3 worldAcceleration)
        {
            Quaternion anchorRotation = GetAnchorRotation();
            Vector3 local = Quaternion.Inverse(anchorRotation) * worldAcceleration;
            local = Vector3.Scale(local, profile.AxisWeights);
            return anchorRotation * local;
        }

        private Vector3 GetAnchorPosition()
        {
            return parentOfRoot != null ? parentOfRoot.position : rootBone.position;
        }

        private Quaternion GetAnchorRotation()
        {
            return parentOfRoot != null ? parentOfRoot.rotation : rootBone.rotation;
        }

        private void CacheAnchorPose()
        {
            if (rootBone == null)
            {
                return;
            }

            previousAnchorPosition = GetAnchorPosition();
            previousAnchorRotation = GetAnchorRotation();
            filteredAnchorPosition = previousAnchorPosition;
            filteredAnchorRotation = previousAnchorRotation;
            hasAnchorPose = true;
        }

        private static int DominantAxis(Vector3 localDirection)
        {
            Vector3 abs = new Vector3(Mathf.Abs(localDirection.x), Mathf.Abs(localDirection.y), Mathf.Abs(localDirection.z));
            if (abs.x >= abs.y && abs.x >= abs.z)
            {
                return 0;
            }

            return abs.y >= abs.z ? 1 : 2;
        }

        private static Vector3 ResolveRestOffset(Transform bone, Transform next)
        {
            if (next != null)
            {
                Vector3 offset = next.position - bone.position;
                if (offset.sqrMagnitude > JiggleMath.Epsilon)
                {
                    return offset;
                }
            }

            Vector3 direction = FallbackDirection(bone);
            float scale = JiggleMath.MaxScaleComponent(bone.lossyScale);
            if (!float.IsFinite(scale) || scale < JiggleMath.Epsilon)
            {
                scale = 1f;
            }

            return direction * (JiggleSolverConfig.Active.FallbackBoneLength * scale);
        }

        private static Vector3 FallbackDirection(Transform bone)
        {
            if (bone.childCount > 0)
            {
                Vector3 toChild = bone.GetChild(0).position - bone.position;
                if (toChild.sqrMagnitude > JiggleMath.Epsilon)
                {
                    return toChild.normalized;
                }
            }

            if (bone.parent != null)
            {
                Vector3 fromParent = bone.position - bone.parent.position;
                if (fromParent.sqrMagnitude > JiggleMath.Epsilon)
                {
                    return fromParent.normalized;
                }
            }

            return bone.up;
        }

        private static List<Transform> CollectBones(Transform root, Transform endBone)
        {
            List<Transform> bones = new List<Transform>();

            if (endBone != null && endBone != root)
            {
                if (!endBone.IsChildOf(root))
                {
                    Debug.LogWarning($"[JigglePhysics] End bone '{endBone.name}' is not a descendant of root '{root.name}'; falling back to the first-child chain.");
                }
                else
                {
                    Transform current = root;
                    while (current != null && current != endBone)
                    {
                        bones.Add(current);
                        current = FindChildLeadingTo(current, endBone);
                    }
                    return bones;
                }
            }

            bones.Add(root);
            Transform walker = root;
            while (walker.childCount == 1)
            {
                walker = walker.GetChild(0);
                bones.Add(walker);
            }

            return bones;
        }

        private static Transform FindChildLeadingTo(Transform parent, Transform target)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == target || target.IsChildOf(child))
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform ResolveTipBone(List<Transform> bones, Transform endBone)
        {
            Transform last = bones[bones.Count - 1];

            if (endBone != null && endBone.IsChildOf(last))
            {
                return endBone;
            }

            if (last.childCount == 1)
            {
                return last.GetChild(0);
            }

            return null;
        }
    }
}
