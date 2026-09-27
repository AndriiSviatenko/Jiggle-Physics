using NUnit.Framework;
using UnityEngine;

namespace JigglePhysics.Tests
{
    public class JiggleChainTests
    {
        private GameObject parentObject;
        private GameObject rootObject;
        private GameObject childObject;
        private JiggleProfile profile;

        [SetUp]
        public void SetUp()
        {
            parentObject = new GameObject("ParentBone");
            rootObject = new GameObject("RootBone");
            childObject = new GameObject("EndBone");

            rootObject.transform.SetParent(parentObject.transform);
            childObject.transform.SetParent(rootObject.transform);
            childObject.transform.localPosition = new Vector3(0f, 0f, 0.1f);

            profile = ScriptableObject.CreateInstance<JiggleProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(parentObject);
        }

        private JiggleChain CreateChain()
        {
            return new JiggleChain(rootObject.transform, childObject.transform, profile);
        }

        [Test]
        public void Chain_Constructs_WithSingleParticle()
        {
            JiggleChain chain = CreateChain();
            Assert.IsTrue(chain.IsValid);
            Assert.AreEqual(1, chain.ParticleCount);
        }

        [Test]
        public void Chain_NullRoot_IsInvalid()
        {
            JiggleChain chain = new JiggleChain(null, null, profile);
            Assert.IsFalse(chain.IsValid);
            Assert.AreEqual(0, chain.ParticleCount);
        }

        [Test]
        public void Chain_MultiBone_SimulatesIntermediateBonesOnly()
        {
            GameObject midObject = new GameObject("MidBone");
            midObject.transform.SetParent(rootObject.transform);
            midObject.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            childObject.transform.SetParent(midObject.transform);
            childObject.transform.localPosition = new Vector3(0f, 0.1f, 0f);

            JiggleChain chain = CreateChain();

            Assert.AreEqual(2, chain.ParticleCount);

            Object.DestroyImmediate(midObject);
        }

        [Test]
        public void Chain_EndBoneIsNull_FollowsSingleChildChain()
        {
            GameObject midObject = new GameObject("MidBone");
            midObject.transform.SetParent(rootObject.transform);
            midObject.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            childObject.transform.SetParent(midObject.transform);
            childObject.transform.localPosition = new Vector3(0f, 0.1f, 0f);

            JiggleChain chain = new JiggleChain(rootObject.transform, null, profile);

            Assert.AreEqual(3, chain.ParticleCount);

            Object.DestroyImmediate(midObject);
        }

        [Test]
        public void Chain_WithGravity_SagsBelowAnimatedTip()
        {
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 600; i++)
            {
                chain.Simulate(1f / 240f);
            }

            Vector3 animated = chain.GetAnimatedTip(0);
            Vector3 simulated = chain.GetSimulatedTip(0);
            Assert.Less(simulated.y, animated.y);
        }

        [Test]
        public void Chain_PreservesBoneLength()
        {
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 300; i++)
            {
                chain.Simulate(1f / 240f);
            }

            float length = chain.GetBoneLength(0);
            float actual = Vector3.Distance(chain.GetAnchor(0), chain.GetSimulatedTip(0));
            Assert.AreEqual(length, actual, 1e-4f);
        }

        [Test]
        public void Chain_RespectsMaxAngle()
        {
            profile.MaxAngle = 20f;
            profile.DampingRatio = 0.05f;
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 600; i++)
            {
                chain.Simulate(1f / 240f);
            }

            Vector3 animatedDirection = (chain.GetAnimatedTip(0) - chain.GetAnchor(0)).normalized;
            Vector3 simulatedDirection = (chain.GetSimulatedTip(0) - chain.GetAnchor(0)).normalized;
            Assert.LessOrEqual(Vector3.Angle(animatedDirection, simulatedDirection), 20.5f);
        }

        [Test]
        public void Chain_RespectsMaxOffset()
        {
            profile.MaxOffset = 0.05f;
            profile.DampingRatio = 0.05f;
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 600; i++)
            {
                chain.Simulate(1f / 240f);
            }

            float offset = Vector3.Distance(chain.GetAnimatedTip(0), chain.GetSimulatedTip(0));
            Assert.LessOrEqual(offset, 0.05f + 1e-4f);
        }

        [Test]
        public void Chain_ReadsAnimatedPose_NotConstructionPose()
        {
            JiggleChain chain = CreateChain();
            Vector3 tipBefore = chain.GetAnimatedTip(0);

            parentObject.transform.position = new Vector3(2f, 3f, 4f);
            parentObject.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            chain.Simulate(1f / 240f);

            Vector3 tipAfter = chain.GetAnimatedTip(0);
            Assert.Greater(Vector3.Distance(tipBefore, tipAfter), 1f);
        }

        [Test]
        public void Chain_StaticParent_DoesNotDriftOverManyFrames()
        {
            JiggleChain chain = CreateChain();

            for (int frame = 0; frame < 900; frame++)
            {
                chain.Simulate(1f / 60f);
                chain.ApplyCollisions(null);
                chain.WriteBack();
            }

            Vector3 animated = chain.GetAnimatedTip(0);
            Vector3 simulated = chain.GetSimulatedTip(0);

            Assert.IsTrue(JiggleMath.IsFinite(simulated));
            Assert.LessOrEqual(Vector3.Distance(animated, simulated), profile.MaxOffset + 1e-4f);
        }

        [Test]
        public void Chain_WriteBack_DoesNotAccumulateRotation()
        {
            JiggleChain chain = CreateChain();

            for (int frame = 0; frame < 600; frame++)
            {
                chain.Simulate(1f / 60f);
                chain.WriteBack();
            }

            Quaternion settled = rootObject.transform.rotation;

            for (int frame = 0; frame < 600; frame++)
            {
                chain.Simulate(1f / 60f);
                chain.WriteBack();
            }

            Assert.Less(Quaternion.Angle(settled, rootObject.transform.rotation), 1f);
        }

        [Test]
        public void Chain_BlendZero_RestoresAnimatedPose()
        {
            profile.Blend = 0f;
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 300; i++)
            {
                chain.Simulate(1f / 60f);
            }
            chain.WriteBack();

            Vector3 animatedTip = chain.GetAnimatedTip(0);
            Assert.Less(Vector3.Distance(childObject.transform.position, animatedTip), 1e-4f);
        }

        [Test]
        public void Chain_BlendZero_ProducesNoDeviation()
        {
            profile.Blend = 0f;
            JiggleChain chain = CreateChain();
            Quaternion restRotation = rootObject.transform.rotation;

            for (int i = 0; i < 300; i++)
            {
                chain.Simulate(1f / 60f);
            }
            chain.WriteBack();

            Assert.Less(Quaternion.Angle(restRotation, rootObject.transform.rotation), 0.01f);
        }

        [Test]
        public void Chain_Teleport_SnapsToAnimatedPose()
        {
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 120; i++)
            {
                chain.Simulate(1f / 60f);
            }

            parentObject.transform.position = new Vector3(1000f, 0f, 0f);
            chain.TeleportIfNeeded(0.5f, 1f / 60f);

            Vector3 animated = chain.GetAnimatedTip(0);
            Vector3 simulated = chain.GetSimulatedTip(0);
            Assert.Less(Vector3.Distance(animated, simulated), 1e-4f);
        }

        [Test]
        public void Chain_ZeroWorldInertia_FollowsAnchorTranslation()
        {
            profile.WorldInertia = 0f;
            JiggleChain chain = CreateChain();
            Vector3 before = chain.GetSimulatedTip(0);

            parentObject.transform.position += Vector3.right * 0.1f;
            chain.PrepareFrame(1f / 60f, 0.5f, 100f);

            Assert.Less(Vector3.Distance(before + Vector3.right * 0.1f, chain.GetSimulatedTip(0)), 1e-4f);
        }

        [Test]
        public void Chain_FullWorldInertia_KeepsParticleInWorldOnSmallAnchorMove()
        {
            profile.WorldInertia = 1f;
            JiggleChain chain = CreateChain();
            Vector3 before = chain.GetSimulatedTip(0);

            parentObject.transform.position += Vector3.right * 0.1f;

            for (int frame = 0; frame < 30; frame++)
            {
                chain.PrepareFrame(1f / 60f, 0.5f, 100f);
            }

            Assert.Less(Vector3.Distance(before, chain.GetSimulatedTip(0)), 1e-3f);
        }

        [Test]
        public void Chain_SmallMovement_DoesNotSnap()
        {
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 120; i++)
            {
                chain.Simulate(1f / 60f);
            }

            Vector3 simulatedBefore = chain.GetSimulatedTip(0);
            parentObject.transform.position = new Vector3(0.01f, 0f, 0f);
            chain.TeleportIfNeeded(0.5f, 1f / 60f);
            chain.Simulate(1f / 60f);

            Assert.Greater(Vector3.Distance(simulatedBefore, chain.GetSimulatedTip(0)), 0f);
        }

        [Test]
        public void Chain_Collision_PushesTipOutOfSphere()
        {
            JiggleChain chain = CreateChain();

            GameObject colliderObject = new GameObject("Collider");
            colliderObject.transform.position = new Vector3(0f, -0.225f, 0.1f);
            JiggleSphereCollider sphere = colliderObject.AddComponent<JiggleSphereCollider>();
            sphere.Radius = 0.19f;

            IJiggleCollider[] colliders = new IJiggleCollider[] { sphere };
            float required = sphere.Radius + profile.PointRadius;

            for (int i = 0; i < 240; i++)
            {
                chain.Simulate(1f / 120f);
                chain.ApplyCollisions(colliders);
            }

            float distance = Vector3.Distance(chain.GetSimulatedTip(0), colliderObject.transform.position);
            Assert.GreaterOrEqual(distance, required - 1e-4f);

            Object.DestroyImmediate(colliderObject);
        }

        [Test]
        public void Chain_Collision_DoesNotPushRestPoseThatAlreadyOverlaps()
        {
            profile.GravityMultiplier = 0f;
            JiggleChain chain = CreateChain();
            Vector3 restTip = chain.GetAnimatedTip(0);

            GameObject colliderObject = new GameObject("Collider");
            colliderObject.transform.position = restTip + Vector3.down * (0.18f + profile.PointRadius - 0.01f);
            JiggleSphereCollider sphere = colliderObject.AddComponent<JiggleSphereCollider>();
            sphere.Radius = 0.18f;
            IJiggleCollider[] colliders = new IJiggleCollider[] { sphere };

            for (int i = 0; i < 120; i++)
            {
                chain.Simulate(1f / 120f);
                chain.ApplyCollisions(colliders);
            }

            Assert.Less(Vector3.Distance(chain.GetSimulatedTip(0), restTip), 1e-3f);
            Object.DestroyImmediate(colliderObject);
        }

        [Test]
        public void Chain_WriteBack_PreservesLocalBoneLength()
        {
            JiggleChain chain = CreateChain();
            chain.Simulate(1f / 240f);

            GameObject colliderObject = new GameObject("Collider");
            colliderObject.transform.position = chain.GetSimulatedTip(0);
            JiggleSphereCollider sphere = colliderObject.AddComponent<JiggleSphereCollider>();
            sphere.Radius = 0.2f;

            chain.ApplyCollisions(new IJiggleCollider[] { sphere });
            chain.WriteBack();

            Assert.AreEqual(0.1f, childObject.transform.localPosition.magnitude, 1e-5f);
            Assert.IsTrue(JiggleMath.IsFinite(childObject.transform.position));

            Object.DestroyImmediate(colliderObject);
        }

        [Test]
        public void Chain_ZeroDeltaTime_DoesNotMove()
        {
            JiggleChain chain = CreateChain();
            Vector3 before = chain.GetSimulatedTip(0);

            chain.Simulate(0f);

            Assert.AreEqual(before, chain.GetSimulatedTip(0));
        }

        [Test]
        public void Chain_DifferentFps_ConvergesToSameRestPosition()
        {
            JiggleChain chain30 = CreateChain();
            for (int i = 0; i < 60; i++)
            {
                chain30.Simulate(1f / 30f);
            }
            Vector3 position30 = chain30.GetSimulatedTip(0);

            Object.DestroyImmediate(rootObject);
            rootObject = new GameObject("RootBone");
            rootObject.transform.SetParent(parentObject.transform);
            childObject = new GameObject("EndBone");
            childObject.transform.SetParent(rootObject.transform);
            childObject.transform.localPosition = new Vector3(0f, 0f, 0.1f);

            JiggleChain chain120 = CreateChain();
            for (int i = 0; i < 240; i++)
            {
                chain120.Simulate(1f / 120f);
            }
            Vector3 position120 = chain120.GetSimulatedTip(0);

            Assert.Less(Vector3.Distance(position30, position120), 0.01f);
        }

        [Test]
        public void Chain_ManySteps_RemainsFinite()
        {
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 20000; i++)
            {
                chain.Simulate(1f / 240f);
            }

            Assert.IsTrue(JiggleMath.IsFinite(chain.GetSimulatedTip(0)));
        }

        [Test]
        public void CapsuleCollider_Resolve_PushesPointToSurface()
        {
            GameObject colliderObject = new GameObject("Capsule");
            colliderObject.transform.position = Vector3.zero;
            colliderObject.transform.rotation = Quaternion.identity;
            JiggleCapsuleCollider capsule = colliderObject.AddComponent<JiggleCapsuleCollider>();
            capsule.Radius = 0.1f;
            capsule.Height = 0.4f;

            Vector3 resolved = capsule.Resolve(new Vector3(0.02f, 0f, 0f), 0f);
            Assert.AreEqual(0.1f, new Vector3(resolved.x, 0f, resolved.z).magnitude, 1e-4f);

            Vector3 outside = new Vector3(5f, 0f, 0f);
            Assert.AreEqual(outside, capsule.Resolve(outside, 0f));

            Object.DestroyImmediate(colliderObject);
        }

        [Test]
        public void CapsuleCollider_RespectsPointRadius()
        {
            GameObject colliderObject = new GameObject("Capsule");
            colliderObject.transform.position = Vector3.zero;
            JiggleCapsuleCollider capsule = colliderObject.AddComponent<JiggleCapsuleCollider>();
            capsule.Radius = 0.1f;
            capsule.Height = 0.4f;

            Vector3 resolved = capsule.Resolve(new Vector3(0.02f, 0f, 0f), 0.05f);
            Assert.AreEqual(0.15f, new Vector3(resolved.x, 0f, resolved.z).magnitude, 1e-4f);

            Object.DestroyImmediate(colliderObject);
        }

        [Test]
        public void CapsuleCollider_RotatedAxis_PushesAlongRotatedNormal()
        {
            GameObject colliderObject = new GameObject("Capsule");
            colliderObject.transform.position = Vector3.zero;
            colliderObject.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            JiggleCapsuleCollider capsule = colliderObject.AddComponent<JiggleCapsuleCollider>();
            capsule.Radius = 0.1f;
            capsule.Height = 0.4f;

            Vector3 resolved = capsule.Resolve(new Vector3(0f, 0.02f, 0f), 0f);
            Assert.AreEqual(0.1f, new Vector3(0f, resolved.y, resolved.z).magnitude, 1e-4f);

            Object.DestroyImmediate(colliderObject);
        }

        [Test]
        public void Chain_BlendZero_MultiBone_RestoresAuthoredPose()
        {
            GameObject midObject = InsertMidBone();
            profile.Blend = 0f;

            JiggleChain chain = CreateChain();
            Quaternion restRoot = rootObject.transform.rotation;
            Quaternion restMid = midObject.transform.rotation;

            for (int i = 0; i < 300; i++)
            {
                chain.Simulate(1f / 60f);
            }
            chain.WriteBack();

            Assert.Less(Quaternion.Angle(restRoot, rootObject.transform.rotation), 0.01f);
            Assert.Less(Quaternion.Angle(restMid, midObject.transform.rotation), 0.01f);
        }

        [Test]
        public void Chain_MultiBone_WriteBackKeepsHierarchyConsistentWithSimulatedTip()
        {
            GameObject midObject = InsertMidBone();
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 120; i++)
            {
                chain.Simulate(1f / 60f);
            }
            chain.WriteBack();

            Vector3 expectedChildPosition = chain.GetSimulatedTip(0);
            Assert.Less(Vector3.Distance(midObject.transform.position, expectedChildPosition), 1e-4f);
        }

        [Test]
        public void Chain_MultiBone_BlendZeroKeepsEveryBoneOnAuthoredPose()
        {
            GameObject midObject = InsertMidBone();
            profile.Blend = 0f;
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 300; i++)
            {
                chain.Simulate(1f / 60f);
            }
            chain.WriteBack();

            Assert.Less(Vector3.Distance(midObject.transform.position, chain.GetPureTip(0)), 1e-4f);
            Assert.Less(Vector3.Distance(childObject.transform.position, chain.GetPureTip(1)), 1e-4f);
        }

        [Test]
        public void Chain_MaxOffsetHoldsUnderStrongGravity()
        {
            profile.MaxOffset = 0.02f;
            profile.MaxAngle = 180f;
            profile.Frequency = 0.5f;
            profile.DampingRatio = 0.4f;

            JiggleChain chain = CreateChain();
            for (int i = 0; i < 1200; i++)
            {
                chain.Simulate(1f / 240f);
            }

            float offset = Vector3.Distance(chain.GetAnimatedTip(0), chain.GetSimulatedTip(0));
            Assert.LessOrEqual(offset, 0.02f + 1e-4f);
        }

        [Test]
        public void Chain_AngleLimitRespectedAfterCollision()
        {
            profile.MaxAngle = 25f;
            profile.DampingRatio = 0.05f;
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 240; i++)
            {
                chain.Simulate(1f / 240f);
            }

            GameObject colliderObject = new GameObject("BigCollider");
            colliderObject.transform.position = chain.GetAnchor(0) + new Vector3(0f, 0.05f, 0f);
            JiggleSphereCollider sphere = colliderObject.AddComponent<JiggleSphereCollider>();
            sphere.Radius = 0.3f;

            chain.ApplyCollisions(new IJiggleCollider[] { sphere });

            Vector3 animatedDirection = (chain.GetAnimatedTip(0) - chain.GetAnchor(0)).normalized;
            Vector3 simulatedDirection = (chain.GetSimulatedTip(0) - chain.GetAnchor(0)).normalized;
            Assert.LessOrEqual(Vector3.Angle(animatedDirection, simulatedDirection), 25.5f);

            Object.DestroyImmediate(colliderObject);
        }

        [Test]
        public void Chain_SustainedContact_StaysBoundedAndFinite()
        {
            JiggleChain chain = CreateChain();

            GameObject colliderObject = new GameObject("ContactCollider");
            colliderObject.transform.position = chain.GetAnimatedTip(0);
            JiggleSphereCollider sphere = colliderObject.AddComponent<JiggleSphereCollider>();
            sphere.Radius = 0.12f;
            IJiggleCollider[] colliders = new IJiggleCollider[] { sphere };

            for (int i = 0; i < 3000; i++)
            {
                chain.Simulate(1f / 120f);
                chain.ApplyCollisions(colliders);
            }

            Vector3 simulated = chain.GetSimulatedTip(0);
            Assert.IsTrue(JiggleMath.IsFinite(simulated));
            Assert.LessOrEqual(Vector3.Distance(chain.GetAnimatedTip(0), simulated), profile.MaxOffset + 1e-4f);

            Object.DestroyImmediate(colliderObject);
        }

        [Test]
        public void Chain_ScaledHierarchy_UsesWorldBoneLength()
        {
            parentObject.transform.localScale = Vector3.one * 2f;
            JiggleChain chain = CreateChain();

            Assert.AreEqual(0.2f, chain.GetBoneLength(0), 1e-4f);
        }

        [Test]
        public void Chain_MirroredScale_KeepsFiniteMotion()
        {
            parentObject.transform.localScale = new Vector3(-1f, 1f, 1f);
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 600; i++)
            {
                chain.Simulate(1f / 120f);
            }
            chain.WriteBack();

            Assert.IsTrue(JiggleMath.IsFinite(chain.GetSimulatedTip(0)));
            Assert.IsTrue(JiggleMath.IsFinite(rootObject.transform.position));
        }

        [Test]
        public void Chain_NullProfile_IsInvalid()
        {
            JiggleChain chain = new JiggleChain(rootObject.transform, childObject.transform, null);
            Assert.IsFalse(chain.IsValid);
            Assert.DoesNotThrow(() => chain.Simulate(1f / 60f));
        }

        [Test]
        public void Chain_EndBoneNotDescendant_FallsBackToChildChain()
        {
            GameObject outside = new GameObject("Outside");
            JiggleChain chain = new JiggleChain(rootObject.transform, outside.transform, profile);

            Assert.IsTrue(chain.IsValid);
            Assert.AreEqual(2, chain.ParticleCount);

            Object.DestroyImmediate(outside);
        }

        private GameObject InsertMidBone()
        {
            GameObject midObject = new GameObject("MidBone");
            midObject.transform.SetParent(rootObject.transform);
            midObject.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            childObject.transform.SetParent(midObject.transform);
            childObject.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            return midObject;
        }
    }
}
