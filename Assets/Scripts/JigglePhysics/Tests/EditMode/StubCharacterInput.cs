using UnityEngine;

namespace JigglePhysics.Tests
{
    public class StubCharacterInput : CharacterLogic.ICharacterInput
    {
        public Vector2 Move { get; set; }
        public Vector2 Look { get; set; }
        public bool Jump { get; set; }
        public bool Running { get; set; }
        public bool Sit { get; set; }
        public bool SitHeldState { get; set; }
        public bool Attack { get; set; }
        public bool Blink { get; set; }
        public bool Parkour { get; set; }

        public bool IsReady
        {
            get { return true; }
        }

        public Vector2 ReadMove()
        {
            return Move;
        }

        public Vector2 ReadLook()
        {
            return Look;
        }

        public bool JumpPressed()
        {
            bool value = Jump;
            Jump = false;
            return value;
        }

        public bool IsRunning()
        {
            return Running;
        }

        public bool SitPressed()
        {
            bool value = Sit;
            Sit = false;
            return value;
        }

        public bool SitHeld()
        {
            return SitHeldState || Sit;
        }

        public bool AttackPressed()
        {
            bool value = Attack;
            Attack = false;
            return value;
        }

        public bool BlinkPressed()
        {
            bool value = Blink;
            Blink = false;
            return value;
        }

        public bool ParkourPressed()
        {
            bool value = Parkour;
            Parkour = false;
            return value;
        }

        public float Zoom { get; set; }

        public float ReadZoom()
        {
            return Zoom;
        }
    }
}
