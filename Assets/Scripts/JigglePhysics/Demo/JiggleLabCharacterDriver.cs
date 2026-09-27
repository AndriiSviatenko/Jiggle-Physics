using UnityEngine;

namespace JigglePhysics.Demo
{
    public sealed class JiggleLabCharacterDriver : MonoBehaviour
    {
        public enum LabAction
        {
            Idle = 0,
            Walk = 1,
            Jog = 2,
            Sprint = 3,
            Jump = 4,
            FallLand = 5,
            FallRoll = 6,
            Slide = 7,
            Attack = 8,
            Flip = 9,
            Dash = 10,
            Vault = 11,
            Stumble = 12,
            CrouchOn = 13,
            CrouchOff = 14,
            WallRunOn = 15,
            WallRunOff = 16
        }

        public static readonly string[] LocomotionNames = { "Стоїть", "Ходьба", "Біг", "Спринт" };
        public static readonly string[] ActionNames =
        {
            "Стрибок", "Падіння", "Перекат", "Слайд", "Удар", "Сальто", "Ривок", "Переліз", "Струс"
        };

        private static readonly float[] LocomotionSpeeds = { 0f, 0.35f, 0.7f, 1f };
        private static readonly string[] AutoShowNames =
        {
            "Ходьба", "Біг", "Стрибок", "Спринт", "Слайд", "Удар", "Сальто", "Присісти", "Встати",
            "Біг по стіні", "Зійти зі стіни", "Падіння", "Перекат", "Ривок", "Переліз", "Струс", "Стоїть"
        };
        private static readonly float[] AutoShowDurations =
        {
            3f, 3f, 2.6f, 3f, 2.4f, 2.4f, 2.4f, 2.4f, 0.8f, 3f, 0.8f, 2.6f, 2.6f, 2.4f, 2.6f, 2.4f, 2f
        };

        private const float JumpVelocity = 3.1f;
        private const float Gravity = 9.81f;
        private const float SlideDuration = 1.15f;
        private const float AttackLayerDuration = 1.1f;
        private const float FallHeight = 1.1f;

        [SerializeField] private Transform[] roots = new Transform[0];
        [SerializeField] private Animator[] animators = new Animator[0];
        [SerializeField] private float bounceFrequency = 2.4f;
        [SerializeField] private float bounceHeight = 0.09f;
        [SerializeField] private float swayAmplitude = 0.14f;
        [SerializeField] private float swayFrequency = 0.55f;
        [SerializeField] private float yawAmplitude = 18f;
        [SerializeField] private float yawFrequency = 0.18f;
        [SerializeField] private float speedSmoothing = 3.5f;
        [SerializeField] private float wallRunHeight = 0.45f;
        [SerializeField] private float wallRunRoll = 18f;

        private Vector3[] origins = new Vector3[0];
        private Quaternion[] baseRotations = new Quaternion[0];
        private float elapsed;
        private float currentSpeed;
        private int locomotionIndex;
        private Vector3 teleportOffset;
        private bool originsCaptured;

        private bool airborne;
        private float verticalVelocity;
        private float heightOffset;
        private float wallHeight;
        private bool hardLandPending;
        private bool crouching;
        private bool wallRunning;
        private float wallSide = 1f;
        private bool sliding;
        private float slideTimer;
        private float attackLayerTimer;

        private bool autoShow;
        private float autoShowTimer;
        private int autoShowIndex;
        private bool autoShowInvoking;

        public bool Configured
        {
            get { return roots.Length > 0; }
        }

        public int LocomotionIndex
        {
            get { return locomotionIndex; }
        }

        public string LocomotionName
        {
            get { return LocomotionNames[Mathf.Clamp(locomotionIndex, 0, LocomotionNames.Length - 1)]; }
        }

        public bool IsCrouching
        {
            get { return crouching; }
        }

        public bool IsWallRunning
        {
            get { return wallRunning; }
        }

        public bool AutoShow
        {
            get { return autoShow; }
        }

        public string AutoShowName
        {
            get { return autoShow ? AutoShowNames[Mathf.Clamp(autoShowIndex, 0, AutoShowNames.Length - 1)] : string.Empty; }
        }

        public void Configure(JiggleRig[] rigs)
        {
            if (rigs == null || rigs.Length == 0)
            {
                return;
            }

            roots = new Transform[rigs.Length];
            animators = new Animator[rigs.Length];
            for (int i = 0; i < rigs.Length; i++)
            {
                roots[i] = rigs[i] != null ? rigs[i].transform : null;
                animators[i] = rigs[i] != null ? rigs[i].GetComponentInChildren<Animator>() : null;
                if (animators[i] != null)
                {
                    animators[i].cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animators[i].updateMode = AnimatorUpdateMode.Normal;
                }
            }

            CaptureOrigins();
        }

        public void SetLocomotion(int index)
        {
            StopAutoShow();
            locomotionIndex = Mathf.Clamp(index, 0, LocomotionNames.Length - 1);
        }

        public void PlayJump()
        {
            StopAutoShow();
            if (airborne)
            {
                return;
            }

            airborne = true;
            verticalVelocity = JumpVelocity;
            SetTrigger("Jump");
        }

        public void PlayFall(bool roll)
        {
            StopAutoShow();
            if (airborne || wallRunning)
            {
                return;
            }

            airborne = true;
            heightOffset = Mathf.Max(heightOffset, FallHeight);
            verticalVelocity = 0f;
            hardLandPending = roll;
        }

        public void PlaySlide()
        {
            StopAutoShow();
            if (airborne || wallRunning)
            {
                return;
            }

            crouching = false;
            sliding = true;
            slideTimer = SlideDuration;
            SetTrigger("Slide");
        }

        public void PlayAttack()
        {
            StopAutoShow();
            if (airborne || sliding)
            {
                return;
            }

            if (attackLayerTimer > 0f)
            {
                SetTrigger("Attack2");
            }
            else
            {
                SetTrigger("Attack");
            }

            attackLayerTimer = AttackLayerDuration;
        }

        public void PlayFlip()
        {
            PlayAnimatorState("Flip");
        }

        public void PlayDash()
        {
            PlayAnimatorState("Dash");
        }

        public void PlayVault()
        {
            PlayAnimatorState("Vault");
        }

        public void PlayStumble()
        {
            PlayAnimatorState("Stumble");
        }

        public void SetCrouch(bool value)
        {
            StopAutoShow();
            if (airborne || wallRunning)
            {
                return;
            }

            crouching = value;
            if (value)
            {
                sliding = false;
            }
        }

        public void SetWallRun(bool value)
        {
            StopAutoShow();
            wallRunning = value;
            if (value)
            {
                crouching = false;
                sliding = false;
                airborne = true;
                verticalVelocity = 0f;
                wallSide = -wallSide;
                return;
            }

            airborne = false;
            heightOffset = 0f;
            verticalVelocity = 0f;
        }

        public void ToggleAutoShow()
        {
            autoShow = !autoShow;
            autoShowTimer = 0f;
            autoShowIndex = 0;
            if (!autoShow)
            {
                return;
            }

            RunAutoShowStep();
        }

        public void Teleport(float distance)
        {
            teleportOffset += Vector3.right * distance;
        }

        public void ClearTeleportOffset()
        {
            teleportOffset = Vector3.zero;
        }

        public void ResetMotion()
        {
            elapsed = 0f;
            currentSpeed = 0f;
            locomotionIndex = 0;
            teleportOffset = Vector3.zero;
            airborne = false;
            verticalVelocity = 0f;
            heightOffset = 0f;
            wallHeight = 0f;
            crouching = false;
            wallRunning = false;
            sliding = false;
            slideTimer = 0f;
            attackLayerTimer = 0f;
            hardLandPending = false;
            autoShow = false;
            autoShowInvoking = false;
            ApplyPose(0f);
        }

        public void TriggerJump()
        {
            PlayJump();
        }

        public void TriggerAttack()
        {
            PlayAttack();
        }

        private void Awake()
        {
            CaptureOrigins();
        }

        private void Update()
        {
            if (!Configured)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;
            UpdateAutoShow(deltaTime);
            UpdateAirState(deltaTime);
            UpdateTimers(deltaTime);
            currentSpeed = Mathf.MoveTowards(currentSpeed, LocomotionSpeeds[locomotionIndex], deltaTime * speedSmoothing);
            ApplyAnimatorState();
            ApplyPose(deltaTime);
        }

        private void UpdateAirState(float deltaTime)
        {
            if (!airborne || wallRunning)
            {
                return;
            }

            heightOffset += verticalVelocity * deltaTime;
            verticalVelocity -= Gravity * deltaTime;
            if (heightOffset > 0f)
            {
                return;
            }

            heightOffset = 0f;
            verticalVelocity = 0f;
            airborne = false;
            SetTrigger(hardLandPending ? "HardLand" : "Land");
            hardLandPending = false;
        }

        private void UpdateTimers(float deltaTime)
        {
            if (sliding)
            {
                slideTimer -= deltaTime;
                if (slideTimer <= 0f)
                {
                    sliding = false;
                }
            }

            if (attackLayerTimer > 0f)
            {
                attackLayerTimer = Mathf.Max(0f, attackLayerTimer - deltaTime);
            }
        }

        private void UpdateAutoShow(float deltaTime)
        {
            if (!autoShow)
            {
                return;
            }

            autoShowTimer -= deltaTime;
            if (autoShowTimer > 0f)
            {
                return;
            }

            RunAutoShowStep();
        }

        private void RunAutoShowStep()
        {
            autoShowInvoking = true;
            switch (autoShowIndex)
            {
                case 0: SetLocomotion(1); break;
                case 1: SetLocomotion(2); break;
                case 2: PlayJump(); break;
                case 3: SetLocomotion(3); break;
                case 4: PlaySlide(); break;
                case 5: PlayAttack(); break;
                case 6: PlayFlip(); break;
                case 7: SetCrouch(true); break;
                case 8: SetCrouch(false); break;
                case 9: SetWallRun(true); break;
                case 10: SetWallRun(false); break;
                case 11: PlayFall(false); break;
                case 12: PlayFall(true); break;
                case 13: PlayDash(); break;
                case 14: PlayVault(); break;
                case 15: PlayStumble(); break;
                default: SetLocomotion(0); break;
            }

            autoShowInvoking = false;
            autoShowTimer = AutoShowDurations[Mathf.Clamp(autoShowIndex, 0, AutoShowDurations.Length - 1)];
            autoShowIndex = (autoShowIndex + 1) % AutoShowNames.Length;
        }

        private void PlayAnimatorState(string stateName)
        {
            StopAutoShow();
            if (airborne || sliding || wallRunning)
            {
                return;
            }

            crouching = false;
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                {
                    animators[i].Play(stateName, 0, 0f);
                }
            }
        }

        private void StopAutoShow()
        {
            if (autoShowInvoking)
            {
                return;
            }

            autoShow = false;
        }

        private void ApplyAnimatorState()
        {
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                animator.SetFloat("Speed", currentSpeed);
                animator.SetFloat("LocomotionCadence", 1f);
                animator.SetFloat("VerticalSpeed", verticalVelocity);
                animator.SetBool("Grounded", !airborne);
                animator.SetBool("Falling", airborne && verticalVelocity < 0f);
                animator.SetBool("Crouching", crouching);
                animator.SetBool("Sliding", sliding);
                animator.SetBool("WallRunning", wallRunning);
                animator.SetFloat("WallSide", wallSide);
                animator.SetLayerWeight(1, attackLayerTimer > 0f ? 1f : 0f);
            }
        }

        private void ApplyPose(float deltaTime)
        {
            wallHeight = Mathf.MoveTowards(wallHeight, wallRunning ? wallRunHeight : 0f, deltaTime * 2f);
            bool groundedMotion = !airborne && !wallRunning;
            float motionScale = Mathf.Clamp01(currentSpeed / LocomotionSpeeds[1]);
            float bounce = groundedMotion ? Mathf.Abs(Mathf.Sin(elapsed * bounceFrequency * Mathf.PI)) * bounceHeight * motionScale : 0f;
            float sway = Mathf.Sin(elapsed * swayFrequency * Mathf.PI * 2f) * swayAmplitude * motionScale;
            float yaw = Mathf.Sin(elapsed * yawFrequency * Mathf.PI * 2f) * yawAmplitude * motionScale;
            float roll = wallRunning ? wallSide * wallRunRoll : 0f;
            Vector3 offset = teleportOffset + new Vector3(sway, bounce + heightOffset + wallHeight, 0f);

            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null)
                {
                    continue;
                }

                Vector3 origin = i < origins.Length ? origins[i] : roots[i].position;
                Quaternion baseRotation = i < baseRotations.Length ? baseRotations[i] : roots[i].rotation;
                roots[i].position = origin + offset;
                roots[i].rotation = baseRotation * Quaternion.Euler(0f, yaw, roll);
            }
        }

        private void CaptureOrigins()
        {
            if (originsCaptured || roots.Length == 0)
            {
                return;
            }

            origins = new Vector3[roots.Length];
            baseRotations = new Quaternion[roots.Length];
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null)
                {
                    continue;
                }

                origins[i] = roots[i].position;
                baseRotations[i] = roots[i].rotation;
            }

            originsCaptured = true;
        }

        private void SetTrigger(string trigger)
        {
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                {
                    animators[i].SetTrigger(trigger);
                }
            }
        }
    }
}
