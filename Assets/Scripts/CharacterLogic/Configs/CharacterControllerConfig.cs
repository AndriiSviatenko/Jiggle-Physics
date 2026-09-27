using UnityEngine;

namespace CharacterLogic
{
    [CreateAssetMenu(fileName = "CharacterControllerConfig", menuName = "Character/Controller Config", order = 0)]
    public class CharacterControllerConfig : ScriptableObject
    {
        [Header("Ground movement")]
        [Tooltip("Walk speed in meters per second.")]
        [SerializeField] private float walkSpeed = 3.0f;

        [Tooltip("Run speed in meters per second.")]
        [SerializeField] private float runSpeed = 8.5f;

        [Tooltip("How fast horizontal velocity approaches the target, per second.")]
        [SerializeField] private float groundAcceleration = 18f;

        [Tooltip("How fast horizontal velocity decays when there is no input, per second.")]
        [SerializeField] private float groundDeceleration = 22f;

        [Header("Air movement")]
        [Tooltip("Fraction of ground acceleration applied while airborne.")]
        [Range(0f, 1f)]
        [SerializeField] private float airControl = 0.65f;

        [Tooltip("Maximum falling speed in meters per second (positive number).")]
        [SerializeField] private float maxFallSpeed = 40f;

        [Header("Jump")]
        [Tooltip("Initial upward jump velocity in meters per second.")]
        [SerializeField] private float jumpVelocity = 6.5f;

        [Tooltip("Grace period after leaving a ledge where a jump is still allowed, in seconds.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float coyoteTime = 0.15f;

        [Tooltip("How long a jump press is remembered before landing, in seconds.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float jumpBufferTime = 0.15f;

        [Header("Gravity and grounding")]
        [Tooltip("Multiplier applied to Physics.gravity.")]
        [SerializeField] private float gravityScale = 1.15f;

        [Tooltip("Downward offset from the controller center to start the ground probe, in meters.")]
        [SerializeField] private float groundCheckOffset = 0.05f;

        [Tooltip("Radius of the ground probe sphere, in meters.")]
        [SerializeField] private float groundCheckRadius = 0.15f;

        [Tooltip("Layers treated as walkable ground.")]
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Orientation")]
        [Tooltip("How fast the character rotates toward the movement direction, in degrees per second.")]
        [SerializeField] private float turnSpeed = 900f;

        [Header("Locomotion source")]
        [Tooltip("When true, forward/vertical movement comes from animation root motion; the controller only turns and grounds.")]
        [SerializeField] private bool animationDrivenLocomotion = false;

        [Header("Parkour")]
        [SerializeField] private LayerMask parkourLayers = ~0;
        [SerializeField] private float wallRunSpeed = 10.5f;
        [SerializeField] private float wallRunGravity = 1.0f;
        [SerializeField] private float wallCheckDistance = 0.85f;
        [SerializeField] private float wallRunDuration = 1.8f;
        [SerializeField] private float wallJumpUpSpeed = 6.2f;
        [SerializeField] private float wallJumpAwaySpeed = 3.8f;
        [SerializeField] private float vaultDistance = 1.6f;
        [SerializeField] private float vaultDuration = 0.38f;
        [SerializeField] private float vaultArcHeight = 0.85f;

        [Header("Blink")]
        [SerializeField] private float blinkDistance = 9f;
        [SerializeField] private float blinkRadius = 0.25f;
        [SerializeField] private float blinkCooldown = 0.65f;

        public float WalkSpeed
        {
            get { return walkSpeed; }
        }

        public float RunSpeed
        {
            get { return runSpeed; }
        }

        public float GroundAcceleration
        {
            get { return groundAcceleration; }
        }

        public float GroundDeceleration
        {
            get { return groundDeceleration; }
        }

        public float AirControl
        {
            get { return airControl; }
        }

        public float MaxFallSpeed
        {
            get { return maxFallSpeed; }
        }

        public float JumpVelocity
        {
            get { return jumpVelocity; }
        }

        public float CoyoteTime
        {
            get { return coyoteTime; }
        }

        public float JumpBufferTime
        {
            get { return jumpBufferTime; }
        }

        public float GravityScale
        {
            get { return gravityScale; }
        }

        public float GroundCheckOffset
        {
            get { return groundCheckOffset; }
        }

        public float GroundCheckRadius
        {
            get { return groundCheckRadius; }
        }

        public LayerMask GroundLayers
        {
            get { return groundLayers; }
        }

        public float TurnSpeed
        {
            get { return turnSpeed; }
        }

        public bool AnimationDrivenLocomotion
        {
            get { return animationDrivenLocomotion; }
            set { animationDrivenLocomotion = value; }
        }

        public LayerMask ParkourLayers => parkourLayers;
        public float WallRunSpeed => wallRunSpeed;
        public float WallRunGravity => wallRunGravity;
        public float WallCheckDistance => wallCheckDistance;
        public float WallRunDuration => wallRunDuration;
        public float WallJumpUpSpeed => wallJumpUpSpeed;
        public float WallJumpAwaySpeed => wallJumpAwaySpeed;
        public float VaultDistance => vaultDistance;
        public float VaultDuration => vaultDuration;
        public float VaultArcHeight => vaultArcHeight;
        public float BlinkDistance => blinkDistance;
        public float BlinkRadius => blinkRadius;
        public float BlinkCooldown => blinkCooldown;

        private void OnValidate()
        {
            walkSpeed = Mathf.Max(0f, walkSpeed);
            runSpeed = Mathf.Max(walkSpeed, runSpeed);
            groundAcceleration = Mathf.Max(0.01f, groundAcceleration);
            groundDeceleration = Mathf.Max(0.01f, groundDeceleration);
            airControl = Mathf.Clamp01(airControl);
            maxFallSpeed = Mathf.Max(0.01f, maxFallSpeed);
            jumpVelocity = Mathf.Max(0f, jumpVelocity);
            coyoteTime = Mathf.Clamp(coyoteTime, 0f, 0.5f);
            jumpBufferTime = Mathf.Clamp(jumpBufferTime, 0f, 0.5f);
            gravityScale = Mathf.Max(0f, gravityScale);
            groundCheckRadius = Mathf.Max(0.001f, groundCheckRadius);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            wallRunSpeed = Mathf.Max(runSpeed, wallRunSpeed);
            wallRunGravity = Mathf.Max(0f, wallRunGravity);
            wallCheckDistance = Mathf.Max(0.1f, wallCheckDistance);
            wallRunDuration = Mathf.Max(0.1f, wallRunDuration);
            wallJumpUpSpeed = Mathf.Max(0f, wallJumpUpSpeed);
            wallJumpAwaySpeed = Mathf.Max(0f, wallJumpAwaySpeed);
            vaultDistance = Mathf.Max(0.5f, vaultDistance);
            vaultDuration = Mathf.Max(0.1f, vaultDuration);
            vaultArcHeight = Mathf.Max(0f, vaultArcHeight);
            blinkDistance = Mathf.Max(1f, blinkDistance);
            blinkRadius = Mathf.Max(0.05f, blinkRadius);
            blinkCooldown = Mathf.Max(0f, blinkCooldown);
        }
    }
}
