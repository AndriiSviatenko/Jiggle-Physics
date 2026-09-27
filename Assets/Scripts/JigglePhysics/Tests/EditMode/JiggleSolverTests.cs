using NUnit.Framework;
using UnityEngine;

namespace JigglePhysics.Tests
{
    public class JiggleSolverTests
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
        public void Profile_DefaultsToSpringDamper()
        {
            Assert.AreEqual(JiggleSolverKind.SpringDamper, profile.SolverKind);
        }

        [Test]
        public void MinimalSpring_SagsBelowAnimatedTip()
        {
            profile.SolverKind = JiggleSolverKind.MinimalSpring;
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 600; i++)
            {
                chain.Simulate(1f / 240f);
            }

            Assert.IsTrue(JiggleMath.IsFinite(chain.GetSimulatedTip(0)));
            Assert.Less(chain.GetSimulatedTip(0).y, chain.GetAnimatedTip(0).y);
        }

        [Test]
        public void MinimalSpring_PreservesBoneLength()
        {
            profile.SolverKind = JiggleSolverKind.MinimalSpring;
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
        public void MinimalSpring_ManySteps_RemainsFiniteAndBounded()
        {
            profile.SolverKind = JiggleSolverKind.MinimalSpring;
            profile.DampingRatio = 0.05f;
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 5000; i++)
            {
                chain.Simulate(1f / 240f);
            }

            Vector3 simulated = chain.GetSimulatedTip(0);
            Assert.IsTrue(JiggleMath.IsFinite(simulated));
            Assert.LessOrEqual(Vector3.Distance(chain.GetAnimatedTip(0), simulated), profile.MaxOffset + 1e-4f);
        }

        [Test]
        public void StableVerlet_SagsBelowAnimatedTip()
        {
            profile.SolverKind = JiggleSolverKind.StableVerlet;
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 600; i++)
            {
                chain.Simulate(1f / 240f);
            }

            Assert.IsTrue(JiggleMath.IsFinite(chain.GetSimulatedTip(0)));
            Assert.Less(chain.GetSimulatedTip(0).y, chain.GetAnimatedTip(0).y);
        }

        [Test]
        public void StableVerlet_PreservesBoneLength()
        {
            profile.SolverKind = JiggleSolverKind.StableVerlet;
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
        public void StableVerlet_ManySteps_RemainsFiniteAndBounded()
        {
            profile.SolverKind = JiggleSolverKind.StableVerlet;
            profile.VerletDrag = 0f;
            JiggleChain chain = CreateChain();

            for (int i = 0; i < 5000; i++)
            {
                chain.Simulate(1f / 240f);
            }

            Vector3 simulated = chain.GetSimulatedTip(0);
            Assert.IsTrue(JiggleMath.IsFinite(simulated));
            Assert.LessOrEqual(Vector3.Distance(chain.GetAnimatedTip(0), simulated), profile.MaxOffset + 1e-4f);
        }

        [Test]
        public void Rig_SolverOverride_AppliesToChains()
        {
            GameObject character = new GameObject("Character");
            parentObject.transform.SetParent(character.transform);
            JiggleRig rig = character.AddComponent<JiggleRig>();
            rig.SetProfile(profile);
            rig.AddChain(rootObject.transform, childObject.transform);

            Assert.AreEqual(JiggleSolverKind.SpringDamper, rig.GetChain(0).ActiveSolverKind);

            rig.SetSolverKind(JiggleSolverKind.MinimalSpring);
            Assert.AreEqual(JiggleSolverKind.MinimalSpring, rig.GetChain(0).ActiveSolverKind);

            rig.SetSolverKind(null);
            Assert.AreEqual(JiggleSolverKind.SpringDamper, rig.GetChain(0).ActiveSolverKind);

            Object.DestroyImmediate(character);
        }

        [Test]
        public void Rig_SolverOverride_PersistsAcrossRebuild()
        {
            GameObject character = new GameObject("Character");
            parentObject.transform.SetParent(character.transform);
            JiggleRig rig = character.AddComponent<JiggleRig>();
            rig.SetProfile(profile);
            rig.AddChain(rootObject.transform, childObject.transform);
            rig.SetSolverKind(JiggleSolverKind.StableVerlet);

            GameObject colliderObject = new GameObject("Collider");
            colliderObject.transform.SetParent(character.transform);
            rig.AddCollider(colliderObject.AddComponent<JiggleSphereCollider>());

            Assert.AreEqual(JiggleSolverKind.StableVerlet, rig.GetChain(0).ActiveSolverKind);

            Object.DestroyImmediate(character);
        }
    }
}
