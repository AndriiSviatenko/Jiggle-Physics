using NUnit.Framework;
using UnityEngine;

namespace JigglePhysics.Tests
{
    public class JiggleChainDebugTests
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

        private void Simulate(JiggleChain chain, int steps, float deltaTime = 1f / 240f)
        {
            for (int i = 0; i < steps; i++)
            {
                chain.Simulate(deltaTime);
            }
        }

        [Test]
        public void Chain_FreshState_HasNoVelocityOrSpringAcceleration()
        {
            JiggleChain chain = CreateChain();

            Assert.AreEqual(Vector3.zero, chain.GetVelocity(0));
            Assert.AreEqual(Vector3.zero, chain.GetSpringAcceleration(0));
        }

        [Test]
        public void Chain_Snap_ResetsDebugState()
        {
            JiggleChain chain = CreateChain();
            Simulate(chain, 60);

            Assert.AreNotEqual(Vector3.zero, chain.GetVelocity(0));

            chain.SnapToAnimatedPose();

            Assert.AreEqual(Vector3.zero, chain.GetVelocity(0));
            Assert.AreEqual(Vector3.zero, chain.GetSpringAcceleration(0));
        }

        [Test]
        public void Chain_SpringAcceleration_PointsTowardAnimatedTarget()
        {
            JiggleChain chain = CreateChain();
            Simulate(chain, 60);

            Vector3 toTarget = chain.GetAnimatedTip(0) - chain.GetSimulatedTip(0);
            Vector3 springAcceleration = chain.GetSpringAcceleration(0);

            Assert.Greater(toTarget.magnitude, 1e-5f);
            Assert.Greater(Vector3.Dot(springAcceleration, toTarget), 0f);
            Assert.IsTrue(JiggleMath.IsFinite(springAcceleration));
        }

        [Test]
        public void Chain_Velocity_IsDownwardWhileFalling()
        {
            profile.DampingRatio = 0f;
            JiggleChain chain = CreateChain();
            Simulate(chain, 12);

            Assert.Less(chain.GetVelocity(0).y, 0f);
        }

        [Test]
        public void Chain_MinimalSpring_ReportsAnalyticVelocity()
        {
            profile.SolverKind = JiggleSolverKind.MinimalSpring;
            JiggleChain chain = CreateChain();
            Simulate(chain, 24);

            Assert.Less(chain.GetVelocity(0).y, 0f);
            Assert.Greater(chain.GetSpringAcceleration(0).magnitude, 0f);
        }

        [Test]
        public void Chain_StableVerlet_ReportsFiniteDebugState()
        {
            profile.SolverKind = JiggleSolverKind.StableVerlet;
            JiggleChain chain = CreateChain();
            Simulate(chain, 120);

            Assert.IsTrue(JiggleMath.IsFinite(chain.GetVelocity(0)));
            Assert.IsTrue(JiggleMath.IsFinite(chain.GetSpringAcceleration(0)));
            Assert.Greater(chain.GetSpringAcceleration(0).magnitude, 0f);
        }

        [Test]
        public void Chain_DebugAccessors_InvalidIndex_ReturnZero()
        {
            JiggleChain chain = CreateChain();

            Assert.AreEqual(Vector3.zero, chain.GetVelocity(-1));
            Assert.AreEqual(Vector3.zero, chain.GetVelocity(99));
            Assert.AreEqual(Vector3.zero, chain.GetSpringAcceleration(-1));
            Assert.AreEqual(Vector3.zero, chain.GetSpringAcceleration(99));
        }

        [Test]
        public void Chain_InvalidChain_DebugAccessors_ReturnZero()
        {
            JiggleChain chain = new JiggleChain(null, null, profile);

            Assert.AreEqual(Vector3.zero, chain.GetVelocity(0));
            Assert.AreEqual(Vector3.zero, chain.GetSpringAcceleration(0));
        }
    }
}
