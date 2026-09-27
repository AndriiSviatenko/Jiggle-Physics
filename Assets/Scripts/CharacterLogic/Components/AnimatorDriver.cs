using ComponentLogic;
using UnityEngine;

namespace CharacterLogic
{

    public class AnimatorDriver : IComponent, ITickableComponent
    {
        private const float TriggerLifetime = 0.12f;
        private const float FallDelay = 0.04f;
        private const float RollLandingMinImpact = 3.2f;
        private const float HeavyLandingImpact = 5.8f;
        private const float SoftLandingMaxSpeed = 0.35f;
        private const float SpeedResponse = 12f;
        private const float DefaultVaultClipLength = 0.67f;
        private const int ActionVault = 0;
        private const int ActionDash = 1;
        private const int ActionFlip = 2;
        private const int ActionStumble = 3;
        private const int UpperBodyLayer = 1;
        private const float UpperBodyFadeInSpeed = 20f;
        private const float UpperBodyFadeOutSpeed = 10f;
        private static readonly int UpperBodyIdleShortHash = Animator.StringToHash(ParkourAnimatorIds.StateUpperIdle);
        private static readonly int AttackShortHash = Animator.StringToHash(ParkourAnimatorIds.StateAttack);
        private static readonly int Attack2ShortHash = Animator.StringToHash(ParkourAnimatorIds.StateAttack2);

        private static readonly float[] ActionBlendDurations = { 0.06f, 0.04f, 0.06f, 0.05f };
        private static readonly int UpperBodyIdleId = Animator.StringToHash(ParkourAnimatorIds.LayerUpperBody + "." + ParkourAnimatorIds.StateUpperIdle);
        private static readonly int BaseLocomotionId = Animator.StringToHash(ParkourAnimatorIds.LayerBase + "." + ParkourAnimatorIds.StateLocomotion);
        private static readonly int BaseCrouchShortId = Animator.StringToHash(ParkourAnimatorIds.StateCrouch);

        private const float StuckStateSeconds = 1.5f;

        private static readonly int SpeedId = Animator.StringToHash(ParkourAnimatorIds.Speed);
        private static readonly int VerticalSpeedId = Animator.StringToHash(ParkourAnimatorIds.VerticalSpeed);
        private static readonly int CadenceId = Animator.StringToHash(ParkourAnimatorIds.LocomotionCadence);
        private static readonly int ActionSpeedId = Animator.StringToHash(ParkourAnimatorIds.ActionSpeed);
        private static readonly int WallSideId = Animator.StringToHash(ParkourAnimatorIds.WallSide);
        private static readonly int GroundedId = Animator.StringToHash(ParkourAnimatorIds.Grounded);
        private static readonly int FallingId = Animator.StringToHash(ParkourAnimatorIds.Falling);
        private static readonly int WallRunningId = Animator.StringToHash(ParkourAnimatorIds.WallRunning);
        private static readonly int SlidingId = Animator.StringToHash(ParkourAnimatorIds.Sliding);
        private static readonly int CrouchingId = Animator.StringToHash(ParkourAnimatorIds.Crouching);

        private readonly Animator animator;
        private readonly CharacterMover mover;
        private readonly ICharacterInput input;
        private readonly float runSpeed;
        private readonly float walkSpeed;
        private const float WalkClipSpeed = 1.16f;
        private const float RunClipSpeed = 6.2f;
        private readonly float vaultDuration;
        private readonly int[] triggerIds;
        private readonly float[] triggerTimers;
        private readonly int[] actionIds;
        private readonly float vaultClipLength;

        private float smoothedSpeed;
        private float airTime;
        private int lastBaseStateHash;
        private float stuckTimer;
        private bool reportedFailure;
        private float landingImpact;
        private bool useSecondAttack;
        private bool jumpPending;
        private bool doubleJumpPending;
        private bool wallJumpPending;
        private bool landingPending;
        private bool dashPending;
        private bool slidePending;
        private bool stumblePending;
        private ParkourState previousState;
        private float upperBodyWeight;
        private float targetUpperBodyWeight;

        public AnimatorDriver(Animator animator, CharacterMover mover, ICharacterInput input, CharacterControllerConfig config)
        {
            this.animator = animator;
            this.mover = mover;
            this.input = input;
            runSpeed = config != null ? Mathf.Max(0.1f, config.RunSpeed) : 6.5f;
            walkSpeed = config != null ? Mathf.Max(0.1f, config.WalkSpeed) : 1.7f;
            vaultDuration = config != null ? Mathf.Max(0.05f, config.VaultDuration) : 0.45f;
            vaultClipLength = ResolveClipLength(animator, ParkourAnimatorIds.VaultClipName, DefaultVaultClipLength);

            triggerIds = new int[ParkourAnimatorIds.Triggers.Length];
            triggerTimers = new float[triggerIds.Length];
            for (int i = 0; i < triggerIds.Length; i++)
            {
                triggerIds[i] = Animator.StringToHash(ParkourAnimatorIds.Triggers[i]);
            }

            actionIds = new int[ParkourAnimatorIds.ActionStatePaths.Length];
            for (int i = 0; i < actionIds.Length; i++)
            {
                actionIds[i] = Animator.StringToHash(ParkourAnimatorIds.ActionStatePaths[i]);
            }

            if (animator != null && animator.layerCount > UpperBodyLayer)
            {
                animator.SetLayerWeight(UpperBodyLayer, 0f);
                upperBodyWeight = 0f;
                targetUpperBodyWeight = 0f;
            }

            mover.Jumped += () => jumpPending = true;
            mover.DoubleJumped += () => doubleJumpPending = true;
            mover.WallJumped += () => wallJumpPending = true;
            mover.Landed += impact =>
            {
                landingPending = true;
                landingImpact = impact;
            };
            mover.Blinked += (_, _) => dashPending = true;
            mover.Slid += () => slidePending = true;
            mover.Stumbled += () => stumblePending = true;
        }

        public bool IsPresent
        {
            get { return animator != null; }
        }

        public void Tick(float deltaTime)
        {
            if (animator == null || deltaTime <= 0f)
            {
                return;
            }

            ExpireTriggers(deltaTime);
            UpdateContinuousParameters(deltaTime);

            try
            {
                FireEventTriggers();
                UpdateUpperBodyLayer(deltaTime);
                WatchdogBaseLayer(deltaTime);
            }
            catch (System.Exception exception)
            {
                if (!reportedFailure)
                {
                    reportedFailure = true;
                    Debug.LogError("[AnimatorDriver] Tick failed: " + exception);
                }
            }

            previousState = mover.State;
        }

        private void WatchdogBaseLayer(float deltaTime)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.fullPathHash != lastBaseStateHash)
            {
                lastBaseStateHash = info.fullPathHash;
                stuckTimer = 0f;
                return;
            }

            bool busy = mover.IsVaulting || mover.IsSliding || mover.IsWallRunning || mover.IsStumbling || mover.IsFlipping
                || mover.State == ParkourState.Blinking || animator.IsInTransition(0);
            if (busy || !mover.IsGrounded || info.fullPathHash == BaseLocomotionId || info.shortNameHash == BaseCrouchShortId)
            {
                stuckTimer = 0f;
                return;
            }

            stuckTimer += deltaTime;
            if (stuckTimer < StuckStateSeconds)
            {
                return;
            }

            stuckTimer = 0f;
            Debug.LogWarning("[AnimatorDriver] Base layer stuck in state " + info.fullPathHash + " while grounded; recovering to Locomotion.");
            animator.CrossFadeInFixedTime(BaseLocomotionId, 0.15f, 0);
        }

        private void UpdateContinuousParameters(float deltaTime)
        {
            float blend = 1f - Mathf.Exp(-SpeedResponse * deltaTime);
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, mover.NormalizedSpeed, blend);

            float speedT = Mathf.Clamp01(smoothedSpeed);

            float baseCadence = Mathf.Lerp(0.96f, 1.04f, speedT);
            float overspeed = Mathf.Max(0f, mover.HorizontalVelocity.magnitude / Mathf.Max(0.1f, runSpeed) - 1f);
            float cadence = Mathf.Clamp(baseCadence + overspeed * 0.12f, 0.85f, 1.15f);

            bool grounded = mover.IsGrounded || mover.State == ParkourState.Grounded || mover.IsCrouching;
            bool supported = grounded || mover.IsWallRunning || mover.IsVaulting || mover.IsSliding || mover.IsStumbling;
            airTime = supported ? 0f : airTime + deltaTime;

            animator.SetFloat(SpeedId, smoothedSpeed);
            animator.SetFloat(VerticalSpeedId, mover.VerticalVelocity);
            animator.SetFloat(CadenceId, cadence);
            animator.SetFloat(ActionSpeedId, vaultClipLength / vaultDuration);
            animator.SetFloat(WallSideId, mover.State == ParkourState.WallRunLeft ? -1f : mover.State == ParkourState.WallRunRight ? 1f : 0f);
            animator.SetBool(GroundedId, grounded);
            bool isFalling = !supported && !mover.IsFlipping && (airTime > FallDelay || mover.VerticalVelocity < -1.5f);
            animator.SetBool(FallingId, isFalling);
            animator.SetBool(WallRunningId, mover.IsWallRunning);
            animator.SetBool(SlidingId, mover.IsSliding);
            animator.SetBool(CrouchingId, mover.IsCrouching);
            animator.speed = 1f;
        }

        private void FireEventTriggers()
        {
            if (wallJumpPending)
            {
                Fire(ParkourAnimatorIds.WallJump);
            }
            else if (doubleJumpPending)
            {
                PlayAction(ActionFlip);
            }
            else if (jumpPending)
            {
                Fire(ParkourAnimatorIds.Jump);
            }

            wallJumpPending = false;
            doubleJumpPending = false;
            jumpPending = false;

            if (mover.State == ParkourState.Vaulting && previousState != ParkourState.Vaulting)
            {
                PlayAction(ActionVault);
            }

            if (slidePending)
            {
                Fire(ParkourAnimatorIds.Slide);
                slidePending = false;
            }

            if (stumblePending)
            {
                PlayAction(ActionStumble);
                stumblePending = false;
            }

            if (dashPending)
            {
                PlayAction(ActionDash);
                dashPending = false;
            }

            if (mover.State != previousState
                && (mover.State == ParkourState.Vaulting || mover.State == ParkourState.Sliding
                    || mover.State == ParkourState.Blinking || mover.State == ParkourState.Stumbling
                    || mover.State == ParkourState.Airborne || mover.State == ParkourState.Crouching))
            {
                ResetUpperBody();
            }

            if (landingPending)
            {
                Cancel(ParkourAnimatorIds.Jump);

                bool isRunningLanding = (smoothedSpeed >= 0.2f || mover.HorizontalVelocity.magnitude >= 1.8f) && landingImpact >= RollLandingMinImpact;
                bool isHeavyDrop = landingImpact >= HeavyLandingImpact;

                if (isRunningLanding || isHeavyDrop)
                {
                    Fire(ParkourAnimatorIds.HardLand);
                }
                else if (smoothedSpeed < SoftLandingMaxSpeed)
                {
                    Fire(ParkourAnimatorIds.Land);
                }

                landingPending = false;
            }

            if (input.AttackPressed() && mover.State == ParkourState.Grounded)
            {
                targetUpperBodyWeight = 1f;
                Fire(useSecondAttack ? ParkourAnimatorIds.Attack2 : ParkourAnimatorIds.Attack);
                useSecondAttack = !useSecondAttack;
            }
        }

        private void PlayAction(int index)
        {
            animator.CrossFadeInFixedTime(actionIds[index], ActionBlendDurations[index], 0);
        }

        private void ResetUpperBody()
        {
            targetUpperBodyWeight = 0f;
            if (animator.layerCount > UpperBodyLayer)
            {
                animator.CrossFadeInFixedTime(UpperBodyIdleId, 0.08f, UpperBodyLayer);
            }
        }

        private void UpdateUpperBodyLayer(float deltaTime)
        {
            if (animator.layerCount <= UpperBodyLayer)
            {
                return;
            }

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(UpperBodyLayer);
            bool inTransition = animator.IsInTransition(UpperBodyLayer);

            if (current.shortNameHash == AttackShortHash || current.shortNameHash == Attack2ShortHash)
            {
                targetUpperBodyWeight = 1f;
            }
            else if (inTransition)
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(UpperBodyLayer);
                if (next.shortNameHash == AttackShortHash || next.shortNameHash == Attack2ShortHash)
                {
                    targetUpperBodyWeight = 1f;
                }
                else if (next.shortNameHash == UpperBodyIdleShortHash)
                {
                    targetUpperBodyWeight = 0f;
                }
            }
            else if (current.shortNameHash == UpperBodyIdleShortHash)
            {
                targetUpperBodyWeight = 0f;
            }

            if (Mathf.Abs(upperBodyWeight - targetUpperBodyWeight) > 0.001f)
            {
                float speed = targetUpperBodyWeight > upperBodyWeight ? UpperBodyFadeInSpeed : UpperBodyFadeOutSpeed;
                upperBodyWeight = Mathf.MoveTowards(upperBodyWeight, targetUpperBodyWeight, speed * deltaTime);
                animator.SetLayerWeight(UpperBodyLayer, upperBodyWeight);
            }
            else if (upperBodyWeight != targetUpperBodyWeight)
            {
                upperBodyWeight = targetUpperBodyWeight;
                animator.SetLayerWeight(UpperBodyLayer, upperBodyWeight);
            }
        }

        private void Fire(string trigger)
        {
            int index = System.Array.IndexOf(ParkourAnimatorIds.Triggers, trigger);
            if (index < 0)
            {
                Debug.LogError("[AnimatorDriver] Unknown trigger '" + trigger + "'.");
                return;
            }

            animator.SetTrigger(triggerIds[index]);
            triggerTimers[index] = TriggerLifetime;
        }

        private void Cancel(string trigger)
        {
            int index = System.Array.IndexOf(ParkourAnimatorIds.Triggers, trigger);
            if (index < 0)
            {
                return;
            }

            animator.ResetTrigger(triggerIds[index]);
            triggerTimers[index] = 0f;
        }

        private void ExpireTriggers(float deltaTime)
        {
            for (int i = 0; i < triggerTimers.Length; i++)
            {
                if (triggerTimers[i] <= 0f)
                {
                    continue;
                }

                triggerTimers[i] -= deltaTime;
                if (triggerTimers[i] <= 0f)
                {
                    animator.ResetTrigger(triggerIds[i]);
                }
            }
        }

        private static float ResolveClipLength(Animator animator, string clipName, float fallback)
        {
            RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
            if (controller == null)
            {
                return fallback;
            }

            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip != null && clip.name.EndsWith(clipName))
                {
                    return clip.length;
                }
            }

            return fallback;
        }
    }
}
