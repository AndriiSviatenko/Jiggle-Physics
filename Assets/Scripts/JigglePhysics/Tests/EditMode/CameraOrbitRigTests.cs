using CharacterLogic;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

namespace JigglePhysics.Tests
{
    public class CameraOrbitRigTests
    {
        private GameObject cameraGo;
        private CinemachineCamera cmCamera;
        private CinemachineOrbitalFollow orbital;
        private CameraOrbitConfig config;
        private StubCharacterInput input;
        private CameraOrbitRig rig;

        [SetUp]
        public void SetUp()
        {
            cameraGo = new GameObject("TestCinemachineCamera");
            cmCamera = cameraGo.AddComponent<CinemachineCamera>();
            orbital = cameraGo.AddComponent<CinemachineOrbitalFollow>();

            config = ScriptableObject.CreateInstance<CameraOrbitConfig>();
            input = new StubCharacterInput();

            rig = new CameraOrbitRig(orbital, config, input);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Rig_Play_InitializesValuesFromConfig()
        {
            rig.Play();

            Assert.AreEqual(config.Radius, orbital.Radius);
            Assert.AreEqual(config.DefaultPitch, orbital.VerticalAxis.Value);
            Assert.AreEqual(config.MinPitch, orbital.VerticalAxis.Range.x);
            Assert.AreEqual(config.MaxPitch, orbital.VerticalAxis.Range.y);
            Assert.AreEqual(config.ShoulderOffset, orbital.TargetOffset);
        }

        [Test]
        public void Rig_LookRight_IncreasesYaw()
        {
            rig.Play();
            float initialYaw = orbital.HorizontalAxis.Value;

            input.Look = new Vector2(10f, 0f);
            rig.LateTick(1f / 60f);

            Assert.Greater(rig.TargetYaw, initialYaw, "TargetYaw should increase when moving mouse to the right.");
        }

        [Test]
        public void Rig_LookUp_DecreasesPitch()
        {
            rig.Play();
            float initialPitch = orbital.VerticalAxis.Value;

            input.Look = new Vector2(0f, 10f);
            rig.LateTick(1f / 60f);

            Assert.Less(rig.TargetPitch, initialPitch, "TargetPitch should decrease (tilt up toward sky) when pushing mouse up.");
        }

        [Test]
        public void Rig_InvertY_InvertsPitchDirection()
        {

            var field = typeof(CameraOrbitConfig).GetField("invertY", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(config, true);

            rig.Play();
            float initialPitch = orbital.VerticalAxis.Value;

            input.Look = new Vector2(0f, 10f);
            rig.LateTick(1f / 60f);

            Assert.Greater(rig.TargetPitch, initialPitch, "With InvertY enabled, pushing mouse up should increase pitch (tilt down).");
        }

        [Test]
        public void Rig_ZoomIn_DecreasesRadius()
        {
            rig.Play();
            float initialRadius = orbital.Radius;

            input.Zoom = 120f;
            rig.LateTick(1f / 60f);

            Assert.Less(rig.TargetRadius, initialRadius, "Scrolling up should zoom in, decreasing the target radius.");
        }

        [Test]
        public void Rig_PitchClampsWithinMinMax()
        {
            rig.Play();

            input.Look = new Vector2(0f, 10000f);
            rig.LateTick(1f / 60f);

            Assert.GreaterOrEqual(rig.TargetPitch, config.MinPitch, "Pitch must not exceed MinPitch.");

            input.Look = new Vector2(0f, -10000f);
            rig.LateTick(1f / 60f);

            Assert.LessOrEqual(rig.TargetPitch, config.MaxPitch, "Pitch must not exceed MaxPitch.");
        }
    }
}
