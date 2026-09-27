using NUnit.Framework;
using UnityEngine;

namespace JigglePhysics.Tests
{
    public class JiggleRigTests
    {
        private GameObject character;
        private GameObject parentObject;
        private GameObject rootObject;
        private GameObject childObject;
        private JiggleProfile profile;
        private JiggleRig rig;

        [SetUp]
        public void SetUp()
        {
            character = new GameObject("Character");
            parentObject = new GameObject("Chest");
            rootObject = new GameObject("BreastBone");
            childObject = new GameObject("BreastEnd");

            parentObject.transform.SetParent(character.transform);
            rootObject.transform.SetParent(parentObject.transform);
            childObject.transform.SetParent(rootObject.transform);
            childObject.transform.localPosition = new Vector3(0f, 0f, 0.1f);

            profile = ScriptableObject.CreateInstance<JiggleProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            if (rig != null)
            {
                JiggleSimulation.Unregister(rig);
                rig = null;
            }

            Object.DestroyImmediate(character);
            Object.DestroyImmediate(profile);
        }

        private JiggleRig CreateRig()
        {
            rig = character.AddComponent<JiggleRig>();
            rig.SetProfile(profile);
            rig.AddChain(rootObject.transform, childObject.transform);
            JiggleSimulation.Register(rig);
            return rig;
        }

        [Test]
        public void Simulation_RegisterAddsRigOnce()
        {
            JiggleRig rig = character.AddComponent<JiggleRig>();
            int before = JiggleSimulation.RigCount;

            JiggleSimulation.Register(rig);
            JiggleSimulation.Register(rig);

            Assert.AreEqual(before + 1, JiggleSimulation.RigCount);
            Assert.IsTrue(rig.Initialized);

            JiggleSimulation.Unregister(rig);
            Assert.AreEqual(before, JiggleSimulation.RigCount);

            Object.DestroyImmediate(rig);
        }

        [Test]
        public void Simulation_UnregisterUnknownRigIsSafe()
        {
            JiggleRig rig = character.AddComponent<JiggleRig>();
            int before = JiggleSimulation.RigCount;

            Assert.DoesNotThrow(() => JiggleSimulation.Unregister(rig));
            Assert.AreEqual(before, JiggleSimulation.RigCount);

            Object.DestroyImmediate(rig);
        }

        [Test]
        public void Simulation_RegisterNullIsSafe()
        {
            int before = JiggleSimulation.RigCount;
            Assert.DoesNotThrow(() => JiggleSimulation.Register(null));
            Assert.AreEqual(before, JiggleSimulation.RigCount);
        }

        [Test]
        public void Rig_InitializeIsIdempotent()
        {
            JiggleRig rig = CreateRig();
            rig.Initialize();
            rig.Initialize();
            Assert.IsTrue(rig.Initialized);
            Assert.AreEqual(1, rig.ChainCount);
        }

        [Test]
        public void Rig_StepWithoutChains_DoesNotThrow()
        {
            JiggleRig rig = character.AddComponent<JiggleRig>();
            Assert.DoesNotThrow(() => rig.Step(1f / 60f));
            Object.DestroyImmediate(rig);
        }

        [Test]
        public void Rig_StepMovesBoneAndKeepsItFinite()
        {
            JiggleRig rig = CreateRig();
            Quaternion restRotation = rootObject.transform.rotation;

            for (int frame = 0; frame < 240; frame++)
            {
                rig.Step(1f / 60f);
            }

            Assert.IsTrue(JiggleMath.IsFinite(rootObject.transform.position));
            Assert.Greater(Quaternion.Angle(restRotation, rootObject.transform.rotation), 0.1f);
        }

        [Test]
        public void Rig_ResetToAnimatedPose_RestoresRestRotation()
        {
            JiggleRig rig = CreateRig();
            Quaternion restRotation = rootObject.transform.rotation;

            for (int frame = 0; frame < 120; frame++)
            {
                rig.Step(1f / 60f);
            }

            rig.ResetToAnimatedPose();
            Assert.Less(Quaternion.Angle(restRotation, rootObject.transform.rotation), 0.01f);
        }

        [Test]
        public void Rig_FollowsCharacterMovement()
        {
            JiggleRig rig = CreateRig();

            for (int frame = 0; frame < 60; frame++)
            {
                rig.Step(1f / 60f);
            }

            character.transform.position = new Vector3(5f, 0f, 0f);
            rig.Step(1f / 60f);

            Assert.IsTrue(JiggleMath.IsFinite(rootObject.transform.position));
            Assert.Greater(rootObject.transform.position.x, 4f);
        }

        [Test]
        public void Rig_TimeModeDefaultsToScaled()
        {
            JiggleRig rig = CreateRig();
            profile.TimeMode = JiggleTimeMode.Scaled;
            Assert.AreEqual(Time.deltaTime, rig.GetDeltaTime());
        }

        [Test]
        public void Rig_UnscaledTimeMode_UsesUnscaledDeltaTime()
        {
            JiggleRig rig = CreateRig();
            profile.TimeMode = JiggleTimeMode.Unscaled;
            Assert.AreEqual(Time.unscaledDeltaTime, rig.GetDeltaTime());
        }

        [Test]
        public void Simulation_ResetAllRestoresEveryRig()
        {
            JiggleRig rig = CreateRig();
            Quaternion restRotation = rootObject.transform.rotation;

            for (int frame = 0; frame < 120; frame++)
            {
                rig.Step(1f / 60f);
            }

            JiggleSimulation.ResetAll();
            Assert.Less(Quaternion.Angle(restRotation, rootObject.transform.rotation), 0.01f);
        }

        [Test]
        public void Rig_SphereColliderIsPickedUp()
        {
            GameObject colliderObject = new GameObject("TorsoCollider");
            colliderObject.transform.SetParent(character.transform);
            colliderObject.transform.position = new Vector3(0f, -0.225f, 0.1f);
            JiggleSphereCollider sphere = colliderObject.AddComponent<JiggleSphereCollider>();
            sphere.Radius = 0.19f;

            JiggleRig rig = CreateRig();
            rig.AddCollider(sphere);

            for (int frame = 0; frame < 120; frame++)
            {
                rig.Step(1f / 60f);
            }

            Vector3 tip = rig.GetChain(0).GetSimulatedTip(0);
            float distance = Vector3.Distance(tip, colliderObject.transform.position);
            Assert.GreaterOrEqual(distance, sphere.Radius + profile.PointRadius - 1e-4f);
        }

        [Test]
        public void Rig_ColliderPushesTipHigherThanFreeSag()
        {
            JiggleRig rig = CreateRig();

            for (int frame = 0; frame < 120; frame++)
            {
                rig.Step(1f / 60f);
            }
            float freeTipY = rig.GetChain(0).GetSimulatedTip(0).y;

            GameObject colliderObject = new GameObject("TorsoCollider");
            colliderObject.transform.SetParent(character.transform);
            colliderObject.transform.position = new Vector3(0f, -0.225f, 0.1f);
            JiggleSphereCollider sphere = colliderObject.AddComponent<JiggleSphereCollider>();
            sphere.Radius = 0.19f;

            rig.AddCollider(sphere);
            rig.ResetToAnimatedPose();

            for (int frame = 0; frame < 120; frame++)
            {
                rig.Step(1f / 60f);
            }
            float blockedTipY = rig.GetChain(0).GetSimulatedTip(0).y;

            Assert.Greater(blockedTipY, freeTipY + 0.01f);
        }

        [Test]
        public void Rig_StepAdvancesFullFrameTime()
        {
            JiggleRig rig = CreateRig();
            rig.Step(1f / 30f);

            float sag = -childObject.transform.position.y;
            Assert.Greater(sag, 0.0045f);
            Assert.Less(sag, 0.0075f);
        }

        [Test]
        public void Rig_LargeHitchIsClampedAndStaysFinite()
        {
            JiggleRig rig = CreateRig();

            Assert.DoesNotThrow(() => rig.Step(5f));

            Assert.IsTrue(JiggleMath.IsFinite(rootObject.transform.position));
            Assert.IsTrue(JiggleMath.IsFinite(childObject.transform.position));
        }

        [Test]
        public void Rig_AddColliderDuringRuntime_DoesNotBakeDeformation()
        {
            JiggleRig rig = CreateRig();
            Quaternion restRotation = rootObject.transform.rotation;

            for (int frame = 0; frame < 120; frame++)
            {
                rig.Step(1f / 60f);
            }

            GameObject colliderObject = new GameObject("LateCollider");
            colliderObject.transform.SetParent(character.transform);
            rig.AddCollider(colliderObject.AddComponent<JiggleSphereCollider>());

            rig.ResetToAnimatedPose();
            Assert.Less(Quaternion.Angle(restRotation, rootObject.transform.rotation), 0.01f);
        }

        [Test]
        public void Rig_DestroyedCollider_DoesNotThrow()
        {
            JiggleRig rig = CreateRig();

            GameObject colliderObject = new GameObject("TempCollider");
            colliderObject.transform.SetParent(character.transform);
            rig.AddCollider(colliderObject.AddComponent<JiggleSphereCollider>());

            Object.DestroyImmediate(colliderObject);

            Assert.DoesNotThrow(() => rig.Step(1f / 60f));
        }

        [Test]
        public void Rig_DuplicateChainIsRejected()
        {
            JiggleRig rig = CreateRig();
            rig.AddChain(rootObject.transform, childObject.transform);

            Assert.AreEqual(1, rig.ChainCount);
        }

        [Test]
        public void Rig_MultiBone_AllBonesAreDriven()
        {
            GameObject midObject = new GameObject("MidBone");
            midObject.transform.SetParent(rootObject.transform);
            midObject.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            childObject.transform.SetParent(midObject.transform);
            childObject.transform.localPosition = new Vector3(0f, 0f, 0.1f);

            JiggleRig rig = CreateRig();
            Assert.AreEqual(1, rig.ChainCount);
            Assert.AreEqual(2, rig.GetChain(0).ParticleCount);

            Quaternion restRoot = rootObject.transform.rotation;
            Quaternion restMid = midObject.transform.rotation;

            for (int frame = 0; frame < 120; frame++)
            {
                rig.Step(1f / 60f);
            }

            Assert.Greater(Quaternion.Angle(restRoot, rootObject.transform.rotation), 0.1f);
            Assert.Greater(Quaternion.Angle(restMid, midObject.transform.rotation), 0.1f);

            Object.DestroyImmediate(midObject);
        }
    }
}
