using CharacterLogic;
using NUnit.Framework;
using UnityEngine;

namespace JigglePhysics.Tests
{
    public class CharacterMoverTests
    {
        private const float DeltaTime = 1f / 50f;

        private GameObject character;
        private CharacterController controller;
        private CharacterControllerConfig config;
        private StubCharacterInput input;
        private StubGround ground;
        private CharacterMover mover;

        [SetUp]
        public void SetUp()
        {
            character = new GameObject("TestCharacter");
            controller = character.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.height = 1.8f;
            controller.radius = 0.3f;

            config = ScriptableObject.CreateInstance<CharacterControllerConfig>();
            config.AnimationDrivenLocomotion = false;
            input = new StubCharacterInput();
            ground = new StubGround();

            mover = new CharacterMover(controller, character.transform, config, input, ground);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(config);
        }

        private void Step(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                mover.Tick(DeltaTime);
            }
        }

        [Test]
        public void Mover_AcceleratesOnMoveInput()
        {
            ground.IsGrounded = true;
            input.Move = new Vector2(0f, 1f);
            Step(30);

            Assert.Greater(mover.HorizontalVelocity.magnitude, 0.5f);
            Assert.Greater(mover.NormalizedSpeed, 0.1f);
        }

        [Test]
        public void Mover_DeceleratesWhenInputReleased()
        {
            ground.IsGrounded = true;
            input.Move = new Vector2(0f, 1f);
            Step(40);
            float moving = mover.HorizontalVelocity.magnitude;

            input.Move = Vector2.zero;
            Step(60);

            Assert.Greater(moving, mover.HorizontalVelocity.magnitude);
            Assert.Less(mover.HorizontalVelocity.magnitude, 0.1f);
        }

        [Test]
        public void Mover_RunReachesHigherSpeedThanWalk()
        {
            ground.IsGrounded = true;
            input.Move = new Vector2(0f, 1f);
            input.Running = false;
            Step(80);
            float walk = mover.HorizontalVelocity.magnitude;

            input.Running = true;
            Step(80);
            float run = mover.HorizontalVelocity.magnitude;

            Assert.Greater(run, walk);
            Assert.GreaterOrEqual(run, config.RunSpeed - 0.05f);
            Assert.LessOrEqual(run, config.RunSpeed * 1.45f + 0.05f);
            Assert.AreEqual(config.WalkSpeed, walk, 0.2f);
        }

        [Test]
        public void Mover_JumpSetsUpwardVelocityWhenGrounded()
        {
            ground.IsGrounded = true;
            Step(2);

            input.Jump = true;
            Step(1);

            Assert.Greater(mover.VerticalVelocity, config.JumpVelocity * 0.5f);
        }

        [Test]
        public void Mover_DoesNotJumpWithoutInput()
        {
            ground.IsGrounded = true;
            Step(20);

            Assert.LessOrEqual(mover.VerticalVelocity, 0f);
        }

        [Test]
        public void Mover_GravityAccumulatesWhenAirborne()
        {
            ground.IsGrounded = false;
            Step(1);
            float first = mover.VerticalVelocity;

            Step(10);
            float later = mover.VerticalVelocity;

            Assert.Less(later, first);
            Assert.Less(later, 0f);
        }

        [Test]
        public void Mover_FallSpeedIsClamped()
        {
            ground.IsGrounded = false;
            Step(500);

            Assert.GreaterOrEqual(mover.VerticalVelocity, -config.MaxFallSpeed - 0.01f);
        }

        [Test]
        public void Mover_CoyoteAllowsJumpShortlyAfterLeavingGround()
        {
            ground.IsGrounded = true;
            Step(5);

            ground.IsGrounded = false;
            Step(2);

            input.Jump = true;
            Step(1);

            Assert.Greater(mover.VerticalVelocity, config.JumpVelocity * 0.5f);
        }

        [Test]
        public void Mover_DoubleJumpFiresOnceAfterCoyoteExpires()
        {
            ground.IsGrounded = true;
            Step(5);

            ground.IsGrounded = false;
            Step(20);

            input.Jump = true;
            Step(1);
            float doubleJump = mover.VerticalVelocity;

            input.Jump = true;
            Step(40);

            Assert.Greater(doubleJump, 0f);
            Assert.Less(mover.VerticalVelocity, 0f);
        }

        [Test]
        public void Mover_JumpBufferTriggersOnLanding()
        {
            ground.IsGrounded = false;
            Step(10);

            input.Jump = true;
            Step(1);

            ground.IsGrounded = true;
            Step(1);

            Assert.Greater(mover.VerticalVelocity, config.JumpVelocity * 0.5f);
        }

        [Test]
        public void Mover_TurnsTowardMovementDirection()
        {
            ground.IsGrounded = true;
            Quaternion start = character.transform.rotation;

            input.Move = new Vector2(1f, 0f);
            Step(40);

            Assert.Greater(Quaternion.Angle(start, character.transform.rotation), 1f);
        }

        [Test]
        public void Mover_EntersSlideOnSitInputWhileMoving()
        {
            ground.IsGrounded = true;
            input.Move = new Vector2(0f, 1f);
            input.Running = true;
            Step(30);

            input.Sit = true;
            Step(1);

            Assert.IsTrue(mover.IsSliding);
            Assert.AreEqual(ParkourState.Sliding, mover.State);
            Assert.Less(controller.height, 1.2f);
        }

        [Test]
        public void Mover_SlideJumpLaunchesAndRestoresHeight()
        {
            ground.IsGrounded = true;
            input.Move = new Vector2(0f, 1f);
            input.Running = true;
            Step(30);

            input.Sit = true;
            Step(1);
            Assert.IsTrue(mover.IsSliding);

            input.Jump = true;
            Step(1);

            Assert.AreEqual(ParkourState.Airborne, mover.State);
            Assert.AreEqual(1.8f, controller.height);
            Assert.Greater(mover.VerticalVelocity, config.JumpVelocity * 0.5f);
        }

        [Test]
        public void Mover_TriggerStumble_EntersStumblingState()
        {
            mover.TriggerStumble(Vector3.back);

            Assert.IsTrue(mover.IsStumbling);
            Assert.AreEqual(ParkourState.Stumbling, mover.State);
            Assert.AreEqual(0f, mover.NormalizedSpeed);
        }

        [Test]
        public void Mover_ApplySpeedBoost_IncreasesVelocity()
        {
            mover.ApplySpeedBoost(Vector3.forward, 15f, 2f);

            Assert.GreaterOrEqual(mover.HorizontalVelocity.magnitude, 14f);
        }

        [Test]
        public void Mover_EntersCrouchWhenStationaryOrSlow()
        {
            ground.IsGrounded = true;
            input.Move = Vector2.zero;
            input.Sit = true;
            Step(1);

            Assert.IsTrue(mover.IsCrouching);
            Assert.AreEqual(ParkourState.Crouching, mover.State);
            Assert.AreEqual(1.8f * 0.55f, controller.height, 0.01f);
        }

        [Test]
        public void Mover_CrouchExitOnSitPressRestoresHeight()
        {
            ground.IsGrounded = true;
            input.Sit = true;
            Step(1);
            Assert.IsTrue(mover.IsCrouching);

            input.Sit = true;
            Step(1);

            Assert.IsFalse(mover.IsCrouching);
            Assert.AreEqual(ParkourState.Grounded, mover.State);
            Assert.AreEqual(1.8f, controller.height, 0.01f);
        }

        [Test]
        public void Mover_SlideSeamlesslyTransitionsToCrouchWhenSitHeld()
        {
            ground.IsGrounded = true;
            input.Move = new Vector2(0f, 1f);
            input.Running = true;
            Step(30);

            input.Running = false;
            input.Sit = true;
            input.SitHeldState = true;
            Step(1);
            Assert.IsTrue(mover.IsSliding);

            // Fast forward slide to its end (0.82s = ~42 frames at 50fps)
            Step(50);

            Assert.IsTrue(mover.IsCrouching);
            Assert.AreEqual(ParkourState.Crouching, mover.State);
            Assert.AreEqual(1.8f * 0.55f, controller.height, 0.01f);
        }

        [Test]
        public void Mover_CrouchWalkLimitsSpeedToCrouchSpeed()
        {
            ground.IsGrounded = true;
            input.Sit = true;
            Step(1);
            Assert.IsTrue(mover.IsCrouching);

            input.Move = new Vector2(0f, 1f);
            Step(40);

            float expectedMaxCrouchSpeed = config.WalkSpeed * 0.85f;
            Assert.LessOrEqual(mover.HorizontalVelocity.magnitude, expectedMaxCrouchSpeed + 0.1f);
        }

        [Test]
        public void Mover_RecoversGroundedStateAfterFallingOntoGround()
        {
            ground.IsGrounded = false;
            Step(10);
            Assert.AreEqual(ParkourState.Airborne, mover.State);
            Assert.IsFalse(mover.IsGrounded);

            ground.IsGrounded = true;
            Step(2);

            Assert.AreEqual(ParkourState.Grounded, mover.State);
            Assert.IsTrue(mover.IsGrounded);
        }

        [Test]
        public void Mover_RecoversGroundedStateAfterJumpLanding()
        {
            ground.IsGrounded = true;
            Step(5);

            input.Jump = true;
            Step(1);
            Assert.Greater(mover.VerticalVelocity, 0f);

            ground.IsGrounded = false;
            Step(60);
            Assert.AreEqual(ParkourState.Airborne, mover.State);
            Assert.Less(mover.VerticalVelocity, 0f);

            ground.IsGrounded = true;
            Step(2);

            Assert.AreEqual(ParkourState.Grounded, mover.State);
            Assert.IsTrue(mover.IsGrounded);
        }
    }
}
