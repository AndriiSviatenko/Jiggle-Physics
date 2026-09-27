namespace CharacterLogic
{

    public static class ParkourAnimatorIds
    {
        public const string Speed = "Speed";
        public const string VerticalSpeed = "VerticalSpeed";
        public const string LocomotionCadence = "LocomotionCadence";
        public const string ActionSpeed = "ActionSpeed";
        public const string WallSide = "WallSide";
        public const string Grounded = "Grounded";
        public const string Falling = "Falling";
        public const string WallRunning = "WallRunning";
        public const string Sliding = "Sliding";
        public const string Crouching = "Crouching";

        public const string Jump = "Jump";
        public const string Land = "Land";
        public const string HardLand = "HardLand";
        public const string Flip = "Flip";
        public const string WallJump = "WallJump";
        public const string Slide = "Slide";
        public const string Vault = "Vault";
        public const string Dash = "Dash";
        public const string Stumble = "Stumble";
        public const string Attack = "Attack";
        public const string Attack2 = "Attack2";

        public const string StateLocomotion = "Locomotion";
        public const string StateCrouch = "Crouch";
        public const string StateJumpStart = "Jump Start";
        public const string StateAirborne = "Falling";
        public const string StateLand = "Land";
        public const string StateRoll = "Roll";
        public const string StateFlip = "Flip";
        public const string StateWallJump = "Wall Jump";
        public const string StateWallRun = "Wall Run";
        public const string StateSlideStart = "Slide Start";
        public const string StateSlideLoop = "Slide Loop";
        public const string StateSlideExit = "Slide Exit";
        public const string StateVault = "Vault";
        public const string StateDash = "Dash";
        public const string StateStumble = "Stumble";
        public const string StateAttack = "Attack";
        public const string StateAttack2 = "Attack2";
        public const string StateUpperIdle = "Empty";

        public const string LayerBase = "Base Layer";
        public const string LayerUpperBody = "UpperBody";
        public const string SubAirborne = "Airborne";
        public const string SubTraversal = "Traversal";
        public const string SubSlide = "Slide";
        public const string SubFullBodyActions = "FullBodyActions";

        public const string UpperBodyMaskPath = "Assets/Settings/Character/UpperBody.mask";
        public const string VaultClipName = "ClimbUp_1m";

        public const string PathVault = LayerBase + "." + StateVault;
        public const string PathDash = LayerBase + "." + StateDash;
        public const string PathFlip = LayerBase + "." + StateFlip;
        public const string PathStumble = LayerBase + "." + StateStumble;

        public static readonly string[] Triggers =
        {
            Jump, Land, HardLand, WallJump, Slide, Attack, Attack2
        };

        public static readonly string[] ActionStatePaths =
        {
            PathVault, PathDash, PathFlip, PathStumble
        };
    }
}
