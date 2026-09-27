using NUnit.Framework;
using UnityEngine;

namespace JigglePhysics.Tests
{
    public class JiggleVolumeTests
    {
        private const float FrameTime = 1f / 60f;

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

        private static void RunFrames(JiggleChain chain, int frames, float stepTime)
        {
            int stepsPerFrame = Mathf.Max(1, Mathf.RoundToInt(FrameTime / stepTime));
            for (int frame = 0; frame < frames; frame++)
            {
                chain.PrepareFrame(FrameTime, 10f, 180f);
                for (int step = 0; step < stepsPerFrame; step++)
                {
                    chain.Simulate(stepTime);
                }

                chain.WriteBack();
            }
        }

        [Test]
        public void SquashStretch_Disabled_KeepsRestScale()
        {
            profile.SquashStretch = 0f;
            JiggleChain chain = CreateChain();
            parentObject.transform.position = Vector3.up * 0.2f;

            RunFrames(chain, 10, 1f / 240f);

            Assert.AreEqual(Vector3.one, rootObject.transform.localScale);
        }

        [Test]
        public void SquashStretch_WhenTipDeviates_PreservesVolume()
        {
            profile.SquashStretch = 0.3f;
            profile.WorldInertia = 1f;
            JiggleChain chain = CreateChain();

            parentObject.transform.position = Vector3.up * 0.2f;
            RunFrames(chain, 3, 1f / 240f);

            Vector3 scale = rootObject.transform.localScale;
            Assert.AreNotEqual(Vector3.one, scale);
            Assert.AreEqual(1f, scale.x * scale.y * scale.z, 1e-3f);
        }

        [Test]
        public void RestoreRestPose_ResetsRotationAndScale()
        {
            profile.SquashStretch = 0.3f;
            profile.WorldInertia = 1f;
            JiggleChain chain = CreateChain();
            Quaternion restRotation = rootObject.transform.localRotation;

            parentObject.transform.position = Vector3.up * 0.2f;
            RunFrames(chain, 3, 1f / 240f);
            chain.RestoreRestPose();

            Assert.AreEqual(Vector3.one, rootObject.transform.localScale);
            Assert.Less(Quaternion.Angle(restRotation, rootObject.transform.localRotation), 0.01f);
        }

        [Test]
        public void StableVerlet_SettledSag_DoesNotDependOnStepRate()
        {
            profile.SolverKind = JiggleSolverKind.StableVerlet;
            profile.MaxAngle = 180f;
            profile.MaxOffset = 1f;

            JiggleChain coarse = CreateChain();
            RunFrames(coarse, 240, 1f / 60f);
            Vector3 coarseTip = coarse.GetSimulatedTip(0);

            coarse.SnapToAnimatedPose();
            JiggleChain fine = CreateChain();
            RunFrames(fine, 240, 1f / 240f);
            Vector3 fineTip = fine.GetSimulatedTip(0);

            Assert.Less(fineTip.y, fine.GetAnimatedTip(0).y);
            Assert.AreEqual(coarseTip.y, fineTip.y, 0.002f);
        }
    }
}
