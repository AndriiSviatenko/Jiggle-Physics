using UnityEngine;
using UnityEngine.InputSystem;

namespace JigglePhysics.Demo
{
    public sealed class SlowMotionToggle : MonoBehaviour
    {
        [SerializeField] private float slowMotionScale = 0.25f;
        [SerializeField] private Key toggleKey = Key.F5;

        private bool slowMotion;
        private GUIStyle hintStyle;

        public bool IsSlowMotion
        {
            get { return slowMotion; }
        }

        public void Toggle()
        {
            slowMotion = !slowMotion;
            Time.timeScale = slowMotion ? slowMotionScale : 1f;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
            {
                Toggle();
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        private void OnGUI()
        {
            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label);
                hintStyle.fontSize = 13;
                hintStyle.fontStyle = FontStyle.Bold;
            }

            hintStyle.normal.textColor = slowMotion ? new Color(1f, 0.85f, 0.2f) : new Color(0.78f, 0.83f, 0.92f);
            string state = slowMotion ? "УВІМКНЕНО x" + slowMotionScale.ToString("0.##") : "вимкнено";
            GUI.Label(new Rect(18f, Screen.height - 100f, 460f, 24f), "F5 — слоумо: " + state, hintStyle);
        }
    }
}
