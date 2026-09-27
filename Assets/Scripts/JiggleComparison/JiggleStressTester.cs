using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JigglePhysics.Demo
{

    [DefaultExecutionOrder(9000)]
    public sealed class JiggleStressTester : MonoBehaviour
    {
        public enum Scenario
        {
            Manual,
            RunCadence,
            JumpAndLand,
            HardStop,
            FastTurn,
            Teleport,
            FullSequence
        }

        [SerializeField] private bool showHud = true;
        [SerializeField] private float runCadence = 2.7f;
        [SerializeField] private float runBounce = 0.055f;
        [SerializeField] private float testDistance = 2.5f;

        private Scenario scenario;
        private Vector3 originPosition;
        private Quaternion originRotation;
        private Vector3 basePosition;
        private Quaternion baseRotation = Quaternion.identity;
        private Vector3 lastAppliedPosition;
        private Quaternion lastAppliedRotation = Quaternion.identity;
        private int currentSectionIndex = -1;
        private float scenarioTime;
        private bool poseCaptured;

        public event Action ScenarioChanged;

        public Scenario ActiveScenario
        {
            get { return scenario; }
        }

        public float FullSequenceDuration
        {
            get { return 10f; }
        }

        public bool ShowHud
        {
            get { return showHud; }
            set { showHud = value; }
        }

        private void OnDisable()
        {
            if (scenario != Scenario.Manual)
            {
                StopScenario();
            }
        }

        private void LateUpdate()
        {
            if (scenario == Scenario.Manual)
            {
                return;
            }

            scenarioTime += Time.unscaledDeltaTime;
            ApplyScenario(scenario, scenarioTime);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f1Key.wasPressedThisFrame) StopScenario();
            else if (keyboard.f2Key.wasPressedThisFrame) StartScenario(Scenario.RunCadence);
            else if (keyboard.f3Key.wasPressedThisFrame) StartScenario(Scenario.JumpAndLand);
            else if (keyboard.f4Key.wasPressedThisFrame) StartScenario(Scenario.HardStop);
            else if (keyboard.f5Key.wasPressedThisFrame) StartScenario(Scenario.FastTurn);
            else if (keyboard.f6Key.wasPressedThisFrame) StartScenario(Scenario.Teleport);
            else if (keyboard.f7Key.wasPressedThisFrame) StartScenario(Scenario.FullSequence);
        }

        private void ApplyScenario(Scenario activeScenario, float time)
        {
            if (activeScenario == Scenario.FullSequence)
            {
                ApplyFullSequence(time);
            }
            else
            {
                basePosition = originPosition;
                baseRotation = originRotation;
                ApplySection(activeScenario, time);
            }

            lastAppliedPosition = transform.position;
            lastAppliedRotation = transform.rotation;
        }

        private void ApplySection(Scenario section, float time)
        {
            transform.SetPositionAndRotation(basePosition, baseRotation);

            switch (section)
            {
                case Scenario.RunCadence:
                    ApplyRun(time);
                    break;
                case Scenario.JumpAndLand:
                    ApplyJump(time);
                    break;
                case Scenario.HardStop:
                    ApplyHardStop(time);
                    break;
                case Scenario.FastTurn:
                    transform.rotation = baseRotation * Quaternion.Euler(0f, Mathf.SmoothStep(0f, 180f, Mathf.Clamp01(time / 0.18f)), 0f);
                    break;
                case Scenario.Teleport:
                    if (time >= 0.5f)
                    {
                        transform.position = basePosition + baseRotation * Vector3.forward * 8f;
                    }
                    break;
            }
        }

        private void ApplyRun(float time)
        {
            float phase = time * runCadence * Mathf.PI * 2f;
            Vector3 localOffset = new Vector3(
                Mathf.Sin(phase * 0.5f) * 0.025f,
                Mathf.Abs(Mathf.Sin(phase)) * runBounce,
                Mathf.Min(time * 1.4f, testDistance));
            transform.position = basePosition + baseRotation * localOffset;
        }

        private void ApplyJump(float time)
        {
            float t = Mathf.Clamp01(time / 1.15f);
            float height = 4f * 0.8f * t * (1f - t);
            transform.position = basePosition + Vector3.up * height;
        }

        private void ApplyHardStop(float time)
        {
            float t = Mathf.Clamp01(time / 0.55f);
            float distance = testDistance * (1f - (1f - t) * (1f - t));
            transform.position = basePosition + baseRotation * Vector3.forward * distance;
        }

        private void ApplyFullSequence(float time)
        {
            const float sectionLength = 2f;
            Scenario[] order = { Scenario.RunCadence, Scenario.JumpAndLand, Scenario.HardStop, Scenario.FastTurn, Scenario.Teleport };
            int sectionIndex = Mathf.FloorToInt(time / sectionLength);
            if (sectionIndex != currentSectionIndex)
            {
                currentSectionIndex = sectionIndex;
                basePosition = sectionIndex == 0 ? originPosition : lastAppliedPosition;
                baseRotation = sectionIndex == 0 ? originRotation : lastAppliedRotation;
                basePosition.y = originPosition.y;
            }

            ApplySection(order[sectionIndex % order.Length], time - sectionIndex * sectionLength);
        }

        public void StartScenario(Scenario next)
        {
            if (!poseCaptured || scenario == Scenario.Manual)
            {
                originPosition = transform.position;
                originRotation = transform.rotation;
                poseCaptured = true;
            }

            scenario = next;
            scenarioTime = 0f;
            currentSectionIndex = -1;
            ScenarioChanged?.Invoke();
        }

        public void StopScenario()
        {
            if (poseCaptured)
            {
                transform.SetPositionAndRotation(originPosition, originRotation);
            }

            scenario = Scenario.Manual;
            scenarioTime = 0f;
            ScenarioChanged?.Invoke();
        }

        private void OnGUI()
        {
            if (!showHud)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 250f, 292f), GUI.skin.box);
            GUILayout.Label("JIGGLE PHYSICS TEST LAB");
            GUILayout.Label("Mode: " + scenario);
            GUILayout.Space(4f);

            if (GUILayout.Button("Manual gameplay")) StopScenario();
            if (GUILayout.Button("Run cadence")) StartScenario(Scenario.RunCadence);
            if (GUILayout.Button("Jump + landing")) StartScenario(Scenario.JumpAndLand);
            if (GUILayout.Button("Sprint + hard stop")) StartScenario(Scenario.HardStop);
            if (GUILayout.Button("Fast 180 turn")) StartScenario(Scenario.FastTurn);
            if (GUILayout.Button("Teleport safety")) StartScenario(Scenario.Teleport);
            if (GUILayout.Button("Full loop")) StartScenario(Scenario.FullSequence);

            GUILayout.Space(4f);
            GUILayout.Label("F1 manual | F2-F7 scenarios");
            GUILayout.Label("Manual: WASD / Shift / Space / Mouse");
            GUILayout.Label("Esc unlocks cursor for buttons");
            GUILayout.EndArea();
        }
    }
}
