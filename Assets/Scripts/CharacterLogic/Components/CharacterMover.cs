using System;
using ComponentLogic;
using UnityEngine;

namespace CharacterLogic
{
    public enum ParkourState
    {
        Grounded,
        Airborne,
        WallRunLeft,
        WallRunRight,
        Vaulting,
        Blinking,
        Sliding,
        Crouching,
        Stumbling
    }

    public class CharacterMover : IComponent, ITickableComponent
    {
        private const float MinLandingImpactSpeed = 3f;
        private const float WallRunEntryLift = 1.6f;
        private const float FallRespawnDepth = 20f;
        private const float WallJumpControlLock = 0.5f;

        private readonly CharacterController controller;
        private readonly Transform transform;
        private readonly CharacterControllerConfig config;
        private readonly ICharacterInput input;
        private readonly IGroundSource ground;

        private Vector3 horizontalVelocity;
        private Vector3 wallNormal;
        private Vector3 wallContactPoint;
        private readonly Vector3 spawnPosition;
        private readonly Quaternion spawnRotation;
        private Vector3 checkpointPosition;
        private Quaternion checkpointRotation;
        private Collider lastWallCollider;
        private readonly float originalControllerHeight;
        private readonly Vector3 originalControllerCenter;
        private Vector3 vaultStart;
        private Vector3 vaultEnd;
        private float verticalVelocity;
        private float coyoteTimer;
        private float jumpBufferTimer;
        private float wallRunTimer;
        private float blinkCooldownTimer;
        private float vaultTimer;
        private float flipTimer;
        private float slideTimer;
        private float stumbleTimer;
        private float boostTimer;
        private float airControlLockTimer;
        private float momentumTimer;
        private const float MomentumRampTime = 6f;
        private const float MomentumBonus = 0.45f;
        private bool isFlipping;
        private bool wasGrounded = true;
        private bool canDoubleJump = true;
        private float currentTurnLean;

        public event Action<float> Landed;
        public event Action Jumped;
        public event Action DoubleJumped;
        public event Action WallJumped;
        public event Action<Vector3, Vector3> Blinked;
        public event Action Slid;
        public event Action Stumbled;

        public CharacterMover(CharacterController controller, Transform transform, CharacterControllerConfig config, ICharacterInput input, IGroundSource ground)
        {
            this.controller = controller;
            this.transform = transform;
            this.config = config;
            this.input = input;
            this.ground = ground;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            checkpointPosition = spawnPosition;
            checkpointRotation = spawnRotation;
            originalControllerHeight = controller.height;
            originalControllerCenter = controller.center;
        }

        public float NormalizedSpeed { get; private set; }
        public Vector3 HorizontalVelocity => horizontalVelocity;
        public float VerticalVelocity => verticalVelocity;
        public Vector3 WallNormal => wallNormal;
        public Vector3 WallContactPoint => wallContactPoint;
        public bool IsGrounded => ground.IsGrounded && State != ParkourState.Vaulting && State != ParkourState.Sliding && State != ParkourState.Airborne;
        public bool IsMoving => horizontalVelocity.sqrMagnitude > 0.01f;
        public bool IsWallRunning => State == ParkourState.WallRunLeft || State == ParkourState.WallRunRight;
        public bool IsVaulting => State == ParkourState.Vaulting;
        public bool IsSliding => State == ParkourState.Sliding;
        public bool IsCrouching => State == ParkourState.Crouching;
        public bool IsStumbling => State == ParkourState.Stumbling;
        public bool IsFlipping => isFlipping;
        public float BlinkCooldownNormalized => config.BlinkCooldown <= 0f ? 0f : Mathf.Clamp01(blinkCooldownTimer / config.BlinkCooldown);
        public ParkourState State { get; private set; } = ParkourState.Grounded;

        public Quaternion VisualRotationOffset
        {
            get
            {
                float wallLean = State == ParkourState.WallRunLeft ? -18f : State == ParkourState.WallRunRight ? 18f : 0f;
                float slideLean = State == ParkourState.Sliding ? -8f : 0f;
                float stumbleLean = State == ParkourState.Stumbling ? 10f : 0f;
                float runLean = NormalizedSpeed * 6.5f;

                return Quaternion.Euler(
                    runLean + slideLean + stumbleLean,
                    0f,
                    wallLean + currentTurnLean);
            }
        }

        public void Tick(float deltaTime)
        {
            if (config == null || controller == null || deltaTime <= 0f)
            {
                return;
            }

            blinkCooldownTimer = Mathf.Max(0f, blinkCooldownTimer - deltaTime);
            airControlLockTimer = Mathf.Max(0f, airControlLockTimer - deltaTime);
            UpdateFlip(deltaTime);
            UpdateTimers(deltaTime);

            bool groundedThisFrame = ground.IsGrounded;
            if (groundedThisFrame && !wasGrounded)
            {
                float impactSpeed = Mathf.Abs(verticalVelocity);
                if (impactSpeed >= MinLandingImpactSpeed)
                {
                    Landed?.Invoke(impactSpeed);
                }
            }
            wasGrounded = groundedThisFrame;

            if (transform.position.y < checkpointPosition.y - FallRespawnDepth)
            {
                Respawn();
                return;
            }

            if (State == ParkourState.Stumbling)
            {
                UpdateStumble(deltaTime);
                return;
            }

            if (State == ParkourState.Vaulting)
            {
                UpdateVault(deltaTime);
                return;
            }

            if (State == ParkourState.Sliding)
            {
                UpdateSlide(deltaTime);
                return;
            }

            Vector3 desiredDirection = ReadDesiredDirection();

            if (State == ParkourState.Crouching)
            {
                UpdateCrouch(desiredDirection, deltaTime);
                return;
            }

            if (input.BlinkPressed() && blinkCooldownTimer <= 0f)
            {
                PerformBlink();
                return;
            }

            float targetTurnLean = 0f;
            if (horizontalVelocity.sqrMagnitude > 1f && desiredDirection.sqrMagnitude > 0.05f)
            {
                float steerAngle = Vector3.SignedAngle(transform.forward, desiredDirection, Vector3.up);
                targetTurnLean = Mathf.Clamp(steerAngle / 45f, -1f, 1f) * -14f;
            }
            currentTurnLean = Mathf.MoveTowards(currentTurnLean, targetTurnLean, 65f * deltaTime);

            if (input.ParkourPressed() && TryStartVault(desiredDirection))
            {
                return;
            }

            bool wantsCrouchOrSlide = input.SitPressed() || input.ParkourPressed();
            if (wantsCrouchOrSlide && ground.IsGrounded)
            {
                bool isMovingFast = horizontalVelocity.sqrMagnitude > 3.2f || (input.IsRunning() && desiredDirection.sqrMagnitude > 0.05f);
                if (isMovingFast)
                {
                    StartSlide();
                    return;
                }
                else
                {
                    StartCrouch();
                    return;
                }
            }

            bool wallRunning = TryUpdateWallRun(desiredDirection, deltaTime);
            if (!wallRunning)
            {
                UpdateHorizontalVelocity(desiredDirection, deltaTime);
                UpdateVerticalVelocity(deltaTime);
                bool supportedByGround = ground.IsGrounded && verticalVelocity <= 0f;
                State = supportedByGround ? ParkourState.Grounded : ParkourState.Airborne;
            }

            UpdateOrientation(deltaTime);
            if (!config.AnimationDrivenLocomotion)
            {
                controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * deltaTime);
            }

            NormalizedSpeed = Mathf.Clamp01(horizontalVelocity.magnitude / Mathf.Max(0.01f, config.RunSpeed));
            RunnerScoreSystem.Instance?.TrackPlayerPosition(transform.position);
        }

        private void UpdateTimers(float deltaTime)
        {
            if (ground.IsGrounded)
            {
                coyoteTimer = config.CoyoteTime;
                wallRunTimer = 0f;
                lastWallCollider = null;
                canDoubleJump = true;
            }
            else
            {
                coyoteTimer = Mathf.Max(0f, coyoteTimer - deltaTime);
            }

            if (input.JumpPressed())
            {
                jumpBufferTimer = config.JumpBufferTime;
            }
            else
            {
                jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - deltaTime);
            }
        }

        private Vector3 ReadDesiredDirection()
        {
            Vector2 moveInput = Vector2.ClampMagnitude(input.ReadMove(), 1f);
            if (moveInput.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            Transform cameraTransform = ResolveViewTransform();
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            right.y = 0f;
            right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;

            Vector3 desired = forward * moveInput.y + right * moveInput.x;
            desired.y = 0f;
            return desired.sqrMagnitude > 1f ? desired.normalized : desired;
        }

        private void UpdateHorizontalVelocity(Vector3 desiredDirection, float deltaTime)
        {
            bool wantsMove = desiredDirection.sqrMagnitude > 0.0001f;
            if (ground.IsGrounded && wantsMove && input.IsRunning())
            {
                momentumTimer = Mathf.Min(momentumTimer + deltaTime, MomentumRampTime);
            }
            else if (ground.IsGrounded && wantsMove)
            {
                momentumTimer = Mathf.Max(momentumTimer - deltaTime, 0f);
            }
            else if (ground.IsGrounded)
            {
                momentumTimer = 0f;
            }

            float momentum = 1f + Mathf.Clamp01(momentumTimer / MomentumRampTime) * MomentumBonus;
            float targetSpeed = (input.IsRunning() ? config.RunSpeed : config.WalkSpeed) * momentum;
            Vector3 desiredVelocity = desiredDirection * targetSpeed;
            if (airControlLockTimer > 0f && !ground.IsGrounded)
            {

                return;
            }
            float acceleration = wantsMove
                ? (ground.IsGrounded ? config.GroundAcceleration : config.GroundAcceleration * config.AirControl)
                : (ground.IsGrounded ? config.GroundDeceleration : config.GroundAcceleration * config.AirControl * 0.25f);
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, acceleration * deltaTime);
        }

        private void UpdateVerticalVelocity(float deltaTime)
        {
            if (jumpBufferTimer > 0f)
            {
                if (coyoteTimer > 0f)
                {

                    verticalVelocity = config.JumpVelocity;
                    jumpBufferTimer = 0f;
                    coyoteTimer = 0f;
                    Jumped?.Invoke();
                }
                else if (canDoubleJump && !ground.IsGrounded)
                {

                    canDoubleJump = false;
                    jumpBufferTimer = 0f;
                    verticalVelocity = config.JumpVelocity * 1.10f;
                    if (horizontalVelocity.sqrMagnitude > 1f)
                    {
                        horizontalVelocity += transform.forward * 2.2f;
                    }
                    StartFlip();
                    DoubleJumped?.Invoke();
                    RunnerScoreSystem.Instance?.RegisterStunt("ПОДВІЙНИЙ СТРИБОК (САЛЬТО)", 150);
                }
            }

            verticalVelocity += Physics.gravity.y * config.GravityScale * deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, -config.MaxFallSpeed);
            if (ground.IsGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
        }

        private bool TryUpdateWallRun(Vector3 desiredDirection, float deltaTime)
        {

            if (ground.IsGrounded || desiredDirection.sqrMagnitude < 0.25f)
            {
                return false;
            }

            Vector3 heading = desiredDirection.normalized;
            Vector3 lateral = Vector3.Cross(Vector3.up, heading);
            Vector3 center = transform.TransformPoint(controller.center);
            float distance = controller.radius + config.WallCheckDistance;
            bool left = Physics.Raycast(center, -lateral, out RaycastHit leftHit, distance, config.ParkourLayers, QueryTriggerInteraction.Ignore);
            bool right = Physics.Raycast(center, lateral, out RaycastHit rightHit, distance, config.ParkourLayers, QueryTriggerInteraction.Ignore);
            if (IsWallRunning && !left && !right)
            {

                Vector3 low = center + Vector3.down * (controller.height * 0.35f);
                left = Physics.Raycast(low, -lateral, out leftHit, distance, config.ParkourLayers, QueryTriggerInteraction.Ignore);
                right = Physics.Raycast(low, lateral, out rightHit, distance, config.ParkourLayers, QueryTriggerInteraction.Ignore);
            }

            RaycastHit hit;
            ParkourState wallState;
            if (left && !leftHit.collider.transform.IsChildOf(transform))
            {
                hit = leftHit;
                wallState = ParkourState.WallRunLeft;
            }
            else if (right && !rightHit.collider.transform.IsChildOf(transform))
            {
                hit = rightHit;
                wallState = ParkourState.WallRunRight;
            }
            else
            {
                return false;
            }

            if (hit.collider != lastWallCollider)
            {
                lastWallCollider = hit.collider;
                wallRunTimer = 0f;
            }

            if (wallRunTimer >= config.WallRunDuration)
            {
                return false;
            }

            if (!IsWallRunning)
            {

                verticalVelocity = Mathf.Max(verticalVelocity, WallRunEntryLift);
                jumpBufferTimer = 0f;
            }

            State = wallState;
            canDoubleJump = true;

            wallRunTimer += deltaTime;
            wallNormal = hit.normal;
            wallContactPoint = hit.point;
            Vector3 alongWall = Vector3.Cross(wallNormal, Vector3.up).normalized;
            if (Vector3.Dot(alongWall, desiredDirection) < 0f)
            {
                alongWall = -alongWall;
            }

            Quaternion wallFacing = Quaternion.LookRotation(alongWall, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wallFacing, config.TurnSpeed * deltaTime);

            Vector3 wallRunVelocity = alongWall * config.WallRunSpeed - wallNormal * 2.4f;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wallRunVelocity, config.GroundAcceleration * deltaTime);
            verticalVelocity = Mathf.MoveTowards(verticalVelocity, -config.WallRunGravity, 18f * deltaTime);

            if (jumpBufferTimer > 0f)
            {

                float currentAlongSpeed = Vector3.Dot(horizontalVelocity, alongWall);
                float forwardSpeed = Mathf.Max(currentAlongSpeed, config.WallRunSpeed);

                Vector3 jumpVelocity = alongWall * forwardSpeed + wallNormal * config.WallJumpAwaySpeed;
                if (desiredDirection.sqrMagnitude > 0.05f)
                {

                    jumpVelocity += desiredDirection.normalized * (config.RunSpeed * 0.35f);
                }

                horizontalVelocity = jumpVelocity;
                verticalVelocity = config.WallJumpUpSpeed;
                airControlLockTimer = 0.04f;
                jumpBufferTimer = 0f;
                wallRunTimer = config.WallRunDuration;
                State = ParkourState.Airborne;
                canDoubleJump = true;
                WallJumped?.Invoke();
                return false;
            }

            return true;
        }

        private bool TryStartVault(Vector3 desiredDirection)
        {
            if (!ground.IsGrounded || desiredDirection.sqrMagnitude < 0.2f)
            {
                return false;
            }

            Vector3 direction = desiredDirection.normalized;
            Vector3 lowOrigin = transform.position + Vector3.up * 0.65f;
            Vector3 highOrigin = transform.position + Vector3.up * 1.45f;
            bool lowBlocked = Physics.Raycast(lowOrigin, direction, out RaycastHit hit, config.VaultDistance, config.ParkourLayers, QueryTriggerInteraction.Ignore);
            bool highClear = !Physics.Raycast(highOrigin, direction, config.VaultDistance, config.ParkourLayers, QueryTriggerInteraction.Ignore);
            if (!lowBlocked || !highClear || hit.collider.transform == transform)
            {
                return false;
            }

            vaultStart = transform.position;
            vaultEnd = hit.point + direction * (controller.radius + 0.9f);
            vaultEnd.y = Mathf.Max(vaultStart.y, hit.collider.bounds.max.y + 0.05f);
            vaultTimer = 0f;
            verticalVelocity = 0f;
            State = ParkourState.Vaulting;
            return true;
        }

        private void UpdateVault(float deltaTime)
        {
            vaultTimer += deltaTime;
            float t = Mathf.Clamp01(vaultTimer / config.VaultDuration);
            Vector3 target = Vector3.Lerp(vaultStart, vaultEnd, t);
            target.y += Mathf.Sin(t * Mathf.PI) * config.VaultArcHeight;
            controller.Move(target - transform.position);

            if (t >= 1f)
            {
                State = ParkourState.Airborne;
                horizontalVelocity = transform.forward * config.RunSpeed;
                verticalVelocity = -1f;
            }
        }

        private void PerformBlink()
        {
            Vector3 start = transform.position;
            Transform cameraTransform = ResolveViewTransform();
            if (cameraTransform == null)
            {
                cameraTransform = transform;
            }
            Vector3 origin = transform.position + Vector3.up * (controller.height * 0.55f);
            Vector3 direction = cameraTransform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = transform.forward;
            }
            direction.Normalize();

            float distance = config.BlinkDistance;
            if (Physics.SphereCast(origin, config.BlinkRadius, direction, out RaycastHit hit, distance, config.ParkourLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.transform != transform)
            {
                distance = Mathf.Max(0f, hit.distance - controller.radius - 0.15f);
            }

            controller.Move(direction * distance);

            float forwardSpeed = Mathf.Max(horizontalVelocity.magnitude, config.RunSpeed * 1.15f);
            horizontalVelocity = direction * forwardSpeed;
            verticalVelocity = 0.5f;
            blinkCooldownTimer = config.BlinkCooldown;
            State = ground.IsGrounded ? ParkourState.Grounded : ParkourState.Airborne;
            Blinked?.Invoke(start, transform.position);
            RunnerScoreSystem.Instance?.RegisterStunt("ТЕЛЕПОРТ", 200);
        }

        private void UpdateOrientation(float deltaTime)
        {
            Vector3 targetDirection = horizontalVelocity;
            targetDirection.y = 0f;
            if (targetDirection.sqrMagnitude < 0.0001f || config.TurnSpeed <= 0f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(targetDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, config.TurnSpeed * deltaTime);
        }

        private void StartFlip()
        {
            isFlipping = true;
            flipTimer = 0f;
        }

        private void UpdateFlip(float deltaTime)
        {
            if (!isFlipping)
            {
                return;
            }

            flipTimer += deltaTime;
            if (flipTimer >= 0.75f || ground.IsGrounded)
            {
                isFlipping = false;
                flipTimer = 0f;
            }
        }

        private void StartSlide()
        {
            State = ParkourState.Sliding;
            slideTimer = 0.82f;
            controller.height = originalControllerHeight * 0.45f;
            controller.center = new Vector3(originalControllerCenter.x, originalControllerCenter.y * 0.45f, originalControllerCenter.z);

            Vector3 slideDir = transform.forward;
            float slideSpeed = Mathf.Max(horizontalVelocity.magnitude * 1.22f, config.RunSpeed * 1.35f);
            horizontalVelocity = slideDir * slideSpeed;
            verticalVelocity = -2f;

            Slid?.Invoke();
            RunnerScoreSystem.Instance?.RegisterStunt("ПІДКАТ", 100);
        }

        private void UpdateSlide(float deltaTime)
        {
            slideTimer -= deltaTime;

            if (jumpBufferTimer > 0f || input.JumpPressed())
            {
                EndSlide();
                verticalVelocity = config.JumpVelocity * 1.15f;
                horizontalVelocity += transform.forward * 2.5f;
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                State = ParkourState.Airborne;
                canDoubleJump = true;
                Jumped?.Invoke();
                RunnerScoreSystem.Instance?.RegisterStunt("ПІДКАТ-СТРИБОК", 180);
                return;
            }

            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, transform.forward * (config.RunSpeed * 0.65f), 5.5f * deltaTime);
            verticalVelocity = -4f;
            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * deltaTime);
            NormalizedSpeed = Mathf.Clamp01(horizontalVelocity.magnitude / Mathf.Max(0.01f, config.RunSpeed));

            if (slideTimer <= 0f || horizontalVelocity.sqrMagnitude < 1.5f || !ground.IsGrounded)
            {
                if (!ground.IsGrounded)
                {
                    EndSlide();
                    State = ParkourState.Airborne;
                }
                else
                {
                    Vector3 headOrigin = transform.position + Vector3.up * (originalControllerHeight * 0.45f);
                    float checkDistance = originalControllerHeight * 0.55f;
                    bool ceilingBlocked = Physics.SphereCast(headOrigin, controller.radius * 0.9f, Vector3.up, out _, checkDistance, config.ParkourLayers, QueryTriggerInteraction.Ignore);

                    if (ceilingBlocked || input.SitHeld())
                    {
                        EndSlide();
                        StartCrouch();
                    }
                    else
                    {
                        EndSlide();
                        State = ParkourState.Grounded;
                    }
                }
            }
        }

        private void EndSlide()
        {
            controller.height = originalControllerHeight;
            controller.center = originalControllerCenter;
        }

        private void StartCrouch()
        {
            State = ParkourState.Crouching;
            controller.height = originalControllerHeight * 0.55f;
            controller.center = new Vector3(originalControllerCenter.x, originalControllerCenter.y * 0.55f, originalControllerCenter.z);
        }

        private void EndCrouch()
        {
            controller.height = originalControllerHeight;
            controller.center = originalControllerCenter;
        }

        private bool CanUncrouch()
        {
            Vector3 headOrigin = transform.position + Vector3.up * (originalControllerHeight * 0.55f);
            float checkDistance = originalControllerHeight * 0.45f;
            return !Physics.SphereCast(headOrigin, controller.radius * 0.85f, Vector3.up, out _, checkDistance, config.ParkourLayers, QueryTriggerInteraction.Ignore);
        }

        private void UpdateCrouch(Vector3 desiredDirection, float deltaTime)
        {
            if (!ground.IsGrounded)
            {
                EndCrouch();
                State = ParkourState.Airborne;
                return;
            }

            if (input.JumpPressed())
            {
                if (CanUncrouch())
                {
                    EndCrouch();
                    verticalVelocity = config.JumpVelocity;
                    State = ParkourState.Airborne;
                    Jumped?.Invoke();
                    return;
                }
            }

            bool exitCrouchInput = input.SitPressed() || input.ParkourPressed() || (input.IsRunning() && desiredDirection.sqrMagnitude > 0.1f);
            if (exitCrouchInput)
            {
                if (CanUncrouch())
                {
                    EndCrouch();
                    State = ParkourState.Grounded;
                    return;
                }
            }

            float crouchSpeed = config.WalkSpeed * 0.85f;
            Vector3 desiredVelocity = desiredDirection * crouchSpeed;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, config.GroundAcceleration * deltaTime);
            verticalVelocity = -4f;
            UpdateOrientation(deltaTime);
            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * deltaTime);
            NormalizedSpeed = Mathf.Clamp01(horizontalVelocity.magnitude / Mathf.Max(0.01f, config.RunSpeed));
            RunnerScoreSystem.Instance?.TrackPlayerPosition(transform.position);
        }

        public void ApplySpeedBoost(Vector3 direction, float speed, float duration)
        {
            boostTimer = duration;
            Vector3 dir = direction.normalized;
            dir.y = 0f;
            horizontalVelocity = dir * speed;
        }

        public void Launch(float upVelocity, float forwardBoost)
        {
            if (State == ParkourState.Sliding)
            {
                EndSlide();
            }
            else if (State == ParkourState.Crouching)
            {
                EndCrouch();
            }

            verticalVelocity = upVelocity;
            horizontalVelocity += transform.forward * forwardBoost;
            State = ParkourState.Airborne;
            canDoubleJump = true;
            Jumped?.Invoke();
        }

        public void TriggerStumble(Vector3 impactNormal)
        {
            if (State == ParkourState.Sliding)
            {
                EndSlide();
            }
            else if (State == ParkourState.Crouching)
            {
                EndCrouch();
            }

            State = ParkourState.Stumbling;
            stumbleTimer = 0.5f;
            horizontalVelocity = -transform.forward * 2.8f;
            verticalVelocity = 1.5f;

            Stumbled?.Invoke();
            RunnerScoreSystem.Instance?.ResetComboOnStumble();
        }

        private void UpdateStumble(float deltaTime)
        {
            stumbleTimer -= deltaTime;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, 8f * deltaTime);
            verticalVelocity += Physics.gravity.y * deltaTime;
            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * deltaTime);
            NormalizedSpeed = 0f;

            if (stumbleTimer <= 0f)
            {
                State = ground.IsGrounded ? ParkourState.Grounded : ParkourState.Airborne;
            }
        }

        private void Respawn()
        {
            if (State == ParkourState.Sliding)
            {
                EndSlide();
            }
            else if (State == ParkourState.Crouching)
            {
                EndCrouch();
            }

            controller.enabled = false;
            transform.SetPositionAndRotation(checkpointPosition, checkpointRotation);
            controller.enabled = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            wallRunTimer = 0f;
            stumbleTimer = 0f;
            slideTimer = 0f;
            lastWallCollider = null;
            canDoubleJump = true;
            currentTurnLean = 0f;
            isFlipping = false;
            flipTimer = 0f;
            State = ParkourState.Grounded;
            if (checkpointPosition == spawnPosition)
            {
                RunnerScoreSystem.Instance?.ResetSession(spawnPosition);
            }
            else
            {
                RunnerScoreSystem.Instance?.ResetComboOnStumble();
            }
        }

        public Transform ViewReference { get; set; }

        private Transform ResolveViewTransform()
        {
            if (ViewReference != null)
            {
                return ViewReference;
            }

            return Camera.main != null ? Camera.main.transform : null;
        }

        public void SetCheckpoint(Vector3 position, Quaternion rotation)
        {
            checkpointPosition = position;
            checkpointRotation = rotation;
        }
    }
}
