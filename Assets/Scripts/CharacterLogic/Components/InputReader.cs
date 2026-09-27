using ComponentLogic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CharacterLogic
{
    public class InputReader : ICharacterInput, IPlayableComponent
    {
        private const string MoveAction = "Move";
        private const string LookAction = "Look";
        private const string JumpAction = "Jump";
        private const string RunAction = "Run";
        private const string SitAction = "Sit";
        private const string AttackAction = "Attack";
        private const string BlinkAction = "Blink";
        private const string ParkourAction = "Parkour";

        private readonly InputActionAsset asset;
        private InputAction move;
        private InputAction look;
        private InputAction jump;
        private InputAction run;
        private InputAction sit;
        private InputAction attack;
        private InputAction blink;
        private InputAction parkour;

        public InputReader(InputActionAsset asset)
        {
            this.asset = asset;
        }

        public bool IsReady
        {
            get { return move != null; }
        }

        public void Play()
        {
            if (asset == null)
            {
                Debug.LogError("[InputReader] No InputActionAsset assigned.");
                return;
            }

            move = asset.FindAction(MoveAction);
            look = asset.FindAction(LookAction);
            jump = asset.FindAction(JumpAction);
            run = asset.FindAction(RunAction);
            sit = asset.FindAction(SitAction);
            attack = asset.FindAction(AttackAction);
            blink = asset.FindAction(BlinkAction);
            parkour = asset.FindAction(ParkourAction);

            if (move == null || look == null || jump == null)
            {
                Debug.LogError("[InputReader] InputActionAsset is missing Move/Look/Jump actions.");
                return;
            }

            asset.Enable();
        }

        public void Stop()
        {
            if (asset != null)
            {
                asset.Disable();
            }
        }

        public Vector2 ReadMove()
        {
            Vector2 val = move != null ? move.ReadValue<Vector2>() : Vector2.zero;
            if (val.sqrMagnitude < 0.001f && Keyboard.current != null)
            {
                float x = 0f;
                float y = 0f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
                Vector2 raw = new Vector2(x, y);
                if (raw.sqrMagnitude > 0.001f)
                {
                    val = raw.normalized;
                }
            }
            return val;
        }

        public Vector2 ReadLook()
        {
            Vector2 val = look != null ? look.ReadValue<Vector2>() : Vector2.zero;
            if (val.sqrMagnitude < 0.001f && Mouse.current != null)
            {
                val = Mouse.current.delta.ReadValue();
            }
            return val;
        }

        public bool JumpPressed()
        {
            bool pressed = jump != null && jump.triggered;
            if (!pressed && Keyboard.current != null)
            {
                pressed = Keyboard.current.spaceKey.wasPressedThisFrame;
            }
            return pressed;
        }

        public bool IsRunning()
        {
            bool pressed = run != null && run.IsPressed();
            if (!pressed && Keyboard.current != null)
            {
                pressed = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            }
            return pressed;
        }

        public bool SitPressed()
        {
            bool pressed = sit != null && sit.triggered;
            if (!pressed && Keyboard.current != null)
            {
                pressed = Keyboard.current.cKey.wasPressedThisFrame;
            }
            return pressed;
        }

        public bool SitHeld()
        {
            bool pressed = sit != null && sit.IsPressed();
            if (!pressed && Keyboard.current != null)
            {
                pressed = Keyboard.current.cKey.isPressed || Keyboard.current.leftCtrlKey.isPressed;
            }
            return pressed;
        }

        public bool AttackPressed()
        {
            bool pressed = attack != null && attack.triggered;
            if (!pressed && Mouse.current != null)
            {
                pressed = Mouse.current.leftButton.wasPressedThisFrame;
            }
            return pressed;
        }

        public bool BlinkPressed()
        {
            bool pressed = blink != null && blink.triggered;
            if (!pressed && Keyboard.current != null)
            {
                pressed = Keyboard.current.eKey.wasPressedThisFrame;
            }
            return pressed;
        }

        public bool ParkourPressed()
        {
            bool pressed = parkour != null && parkour.triggered;
            if (!pressed && Keyboard.current != null)
            {
                pressed = Keyboard.current.leftCtrlKey.wasPressedThisFrame || Keyboard.current.rightCtrlKey.wasPressedThisFrame;
            }
            return pressed;
        }

        public float ReadZoom()
        {
            if (Mouse.current != null)
            {
                return Mouse.current.scroll.ReadValue().y;
            }
            return 0f;
        }
    }
}
