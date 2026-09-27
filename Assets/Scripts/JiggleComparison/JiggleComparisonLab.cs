using System.Collections;
using System.Collections.Generic;
using System.Text;
using JigglePhysics;
using JigglePhysics.Demo;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JiggleComparison
{

    [DefaultExecutionOrder(9001)]
    public sealed class JiggleComparisonLab : MonoBehaviour
    {
        private sealed class BenchmarkRow
        {
            public string Name;
            public JiggleMetricsProbe.Report Report;
        }

        [SerializeField] private bool showPanel;
        [SerializeField] private float warmupSeconds = 0.75f;

        private readonly List<IJiggleBackend> backends = new List<IJiggleBackend>();
        private readonly List<BenchmarkRow> benchmarkRows = new List<BenchmarkRow>();

        private CustomJiggleBackend customBackend;
        private JiggleMetricsProbe probe;
        private JiggleStressTester stressTester;
        private IJiggleBackend activeBackend;
        private Coroutine benchmarkRoutine;
        private string benchmarkStatus = string.Empty;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle activeButtonStyle;

        public IJiggleBackend ActiveBackend
        {
            get { return activeBackend; }
        }

        public IReadOnlyList<IJiggleBackend> Backends
        {
            get { return backends; }
        }

        public bool IsBenchmarkRunning
        {
            get { return benchmarkRoutine != null; }
        }

        public void Configure(JiggleRig rig)
        {
            if (rig == null)
            {
                return;
            }

            JiggleBoneSet bones = JiggleBoneSet.FromRig(rig);
            JiggleChain firstChain = rig.GetChain(0);
            JiggleProfile profile = firstChain != null ? firstChain.Profile : rig.DefaultProfile;

            customBackend = GetOrAdd<CustomJiggleBackend>();
            customBackend.Configure(rig);
            RigidbodyJiggleBackend rigidbodyBackend = GetOrAdd<RigidbodyJiggleBackend>();
            rigidbodyBackend.Configure(bones, profile);
            FrameworkJiggleBackend frameworkBackend = GetOrAdd<FrameworkJiggleBackend>();
            frameworkBackend.Configure(bones);

            backends.Clear();
            backends.Add(customBackend);
            backends.Add(rigidbodyBackend);
            backends.Add(frameworkBackend);

            probe = GetOrAdd<JiggleMetricsProbe>();
            probe.Configure(bones);

            stressTester = GetComponent<JiggleStressTester>();
            if (stressTester != null)
            {
                stressTester.ScenarioChanged -= OnScenarioChanged;
                stressTester.ScenarioChanged += OnScenarioChanged;
                stressTester.ShowHud = false;

                stressTester.enabled = false;
            }

            Activate(customBackend);
        }

        public void Activate(IJiggleBackend backend)
        {
            for (int i = 0; i < backends.Count; i++)
            {
                if (backends[i] != backend)
                {
                    backends[i].SetActive(false);
                }
            }

            activeBackend = backend != null && backend.IsAvailable ? backend : null;
            if (activeBackend != null)
            {
                activeBackend.SetActive(true);
                activeBackend.ResetSimulation();
            }

            if (probe != null)
            {
                probe.SetSource(activeBackend);
            }
        }

        public void SetCustomSolver(JiggleSolverKind kind)
        {
            if (customBackend == null || customBackend.Rig == null)
            {
                return;
            }

            if (!ReferenceEquals(activeBackend, customBackend))
            {
                Activate(customBackend);
            }

            customBackend.Rig.SetSolverKind(kind);
            customBackend.ResetSimulation();
            probe.ResetStatistics();
        }

        public void StartBenchmark()
        {
            if (benchmarkRoutine != null || stressTester == null)
            {
                return;
            }

            benchmarkRoutine = StartCoroutine(RunBenchmark());
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                showPanel = !showPanel;
            }

            if (benchmarkRoutine != null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame) ActivateIndex(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) ActivateIndex(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) ActivateIndex(2);
            else if (keyboard.digit0Key.wasPressedThisFrame) Activate(null);
            else if (keyboard.digit4Key.wasPressedThisFrame) SetCustomSolver(JiggleSolverKind.SpringDamper);
            else if (keyboard.digit5Key.wasPressedThisFrame) SetCustomSolver(JiggleSolverKind.MinimalSpring);
            else if (keyboard.digit6Key.wasPressedThisFrame) SetCustomSolver(JiggleSolverKind.StableVerlet);
            else if (keyboard.bKey.wasPressedThisFrame) StartBenchmark();
        }

        private void ActivateIndex(int index)
        {
            if (index >= 0 && index < backends.Count)
            {
                Activate(backends[index]);
            }
        }

        private void OnScenarioChanged()
        {
            if (activeBackend != null)
            {
                activeBackend.ResetSimulation();
            }

            if (probe != null)
            {
                probe.ResetStatistics();
            }
        }

        private IEnumerator RunBenchmark()
        {
            IJiggleBackend previous = activeBackend;
            JiggleSolverKind? previousSolver = customBackend != null && customBackend.Rig != null
                ? customBackend.Rig.SolverKindOverride
                : null;
            benchmarkRows.Clear();
            stressTester.enabled = true;

            List<KeyValuePair<string, System.Action>> runs = new List<KeyValuePair<string, System.Action>>();
            for (int i = 0; i < backends.Count; i++)
            {
                IJiggleBackend backend = backends[i];
                if (!backend.IsAvailable)
                {
                    continue;
                }

                if (ReferenceEquals(backend, customBackend))
                {
                    AddSolverRun(runs, "Наша · Spring-Damper", JiggleSolverKind.SpringDamper);
                    AddSolverRun(runs, "Наша · Minimal Spring", JiggleSolverKind.MinimalSpring);
                    AddSolverRun(runs, "Наша · Stable Verlet", JiggleSolverKind.StableVerlet);
                    continue;
                }

                runs.Add(new KeyValuePair<string, System.Action>(backend.DisplayName, () => Activate(backend)));
            }

            for (int i = 0; i < runs.Count; i++)
            {
                benchmarkStatus = "Бенчмарк " + (i + 1) + "/" + runs.Count + ": " + runs[i].Key;
                runs[i].Value();
                stressTester.StartScenario(JiggleStressTester.Scenario.FullSequence);
                yield return new WaitForSecondsRealtime(warmupSeconds);
                probe.BeginRecording();
                yield return new WaitForSecondsRealtime(stressTester.FullSequenceDuration);
                benchmarkRows.Add(new BenchmarkRow { Name = runs[i].Key, Report = probe.EndRecording() });
                stressTester.StopScenario();
                yield return null;
            }

            if (customBackend != null && customBackend.Rig != null)
            {
                customBackend.Rig.SetSolverKind(previousSolver);
            }

            stressTester.enabled = false;
            Activate(previous);
            benchmarkStatus = "Бенчмарк завершено (" + runs.Count + " прогонів)";
            Debug.Log("[JiggleComparison]\n" + FormatBenchmark());
            benchmarkRoutine = null;
        }

        private void AddSolverRun(List<KeyValuePair<string, System.Action>> runs, string label, JiggleSolverKind kind)
        {
            runs.Add(new KeyValuePair<string, System.Action>(label, () => SetCustomSolver(kind)));
        }

        private string FormatBenchmark()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Реалізація | кадрів | ms сер. | ms пік | відхил. пік мм | RMS мм | різкість м/с²");
            for (int i = 0; i < benchmarkRows.Count; i++)
            {
                JiggleMetricsProbe.Report report = benchmarkRows[i].Report;
                builder.AppendLine(string.Format("{0} | {1} | {2:0.000} | {3:0.000} | {4:0.0} | {5:0.0} | {6:0.0}",
                    benchmarkRows[i].Name, report.Frames, report.AverageCostMilliseconds, report.PeakCostMilliseconds,
                    report.PeakDeviationMillimeters, report.RmsDeviationMillimeters, report.RmsAcceleration));
            }

            return builder.ToString();
        }

        private T GetOrAdd<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private void OnDestroy()
        {
            if (stressTester != null)
            {
                stressTester.ScenarioChanged -= OnScenarioChanged;
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
            titleStyle.normal.textColor = new Color(0.35f, 0.95f, 1f);
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            smallStyle.normal.textColor = new Color(0.78f, 0.8f, 0.88f);
            activeButtonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            activeButtonStyle.normal.textColor = new Color(0.35f, 1f, 0.6f);
        }

        private void OnGUI()
        {
            if (backends.Count == 0)
            {
                return;
            }

            EnsureStyles();
            if (!showPanel)
            {
                string active = activeBackend != null ? activeBackend.DisplayName : "вимкнено";
                GUI.Label(new Rect(18f, 150f, 420f, 22f), "F9 · Jiggle Lab — " + active, smallStyle);
                return;
            }

            float height = benchmarkRows.Count > 0 ? 560f : 400f;
            GUILayout.BeginArea(new Rect(16f, 150f, 340f, height), GUI.skin.box);
            GUILayout.Label("JIGGLE LAB · порівняння реалізацій", titleStyle);

            for (int i = 0; i < backends.Count; i++)
            {
                IJiggleBackend backend = backends[i];
                GUI.enabled = backend.IsAvailable && benchmarkRoutine == null;
                GUIStyle style = backend == activeBackend ? activeButtonStyle : GUI.skin.button;
                if (GUILayout.Button((i + 1) + " · " + backend.DisplayName, style))
                {
                    Activate(backend);
                }
            }

            GUI.enabled = benchmarkRoutine == null;
            if (GUILayout.Button("0 · Вимкнути (чиста анімація)", activeBackend == null ? activeButtonStyle : GUI.skin.button))
            {
                Activate(null);
            }

            if (customBackend != null && ReferenceEquals(activeBackend, customBackend))
            {
                JiggleChain chain = customBackend.Rig.GetChain(0);
                JiggleSolverKind kind = chain != null ? chain.ActiveSolverKind : JiggleSolverKind.SpringDamper;
                GUILayout.BeginHorizontal();
                SolverButton("4 Spring", JiggleSolverKind.SpringDamper, kind);
                SolverButton("5 Minimal", JiggleSolverKind.MinimalSpring, kind);
                SolverButton("6 Verlet", JiggleSolverKind.StableVerlet, kind);
                GUILayout.EndHorizontal();
            }

            GUI.enabled = true;
            GUILayout.Space(4f);
            if (activeBackend != null)
            {
                GUILayout.Label(activeBackend.Summary, smallStyle);
                GUILayout.Label(string.Format("Ціна: {0:0.000} ms/кадр", probe.SmoothedCostMilliseconds), labelStyle);
                GUILayout.Label(string.Format("Відхилення: {0:0.0} мм · пік {1:0.0} · RMS {2:0.0}",
                    probe.CurrentDeviationMillimeters, probe.PeakDeviationMillimeters, probe.RmsDeviationMillimeters), labelStyle);
                GUILayout.Label(string.Format("Різкість (RMS прискорення): {0:0.0} м/с²", probe.RmsAcceleration), labelStyle);
            }
            else
            {
                GUILayout.Label("Вторинний рух вимкнено: кістки грудей у позі анімації.", smallStyle);
            }

            GUILayout.Space(4f);
            GUI.enabled = stressTester != null && benchmarkRoutine == null;
            if (GUILayout.Button("B · Бенчмарк усіх (скриптований рух на місці)"))
            {
                StartBenchmark();
            }

            GUI.enabled = true;
            if (benchmarkStatus.Length > 0)
            {
                GUILayout.Label(benchmarkStatus, smallStyle);
            }

            if (benchmarkRows.Count > 0)
            {
                DrawBenchmarkTable();
            }

            GUILayout.Label("1-3 реалізація · 0 вимк · 4-6 солвер · B бенчмарк · F9 панель", smallStyle);
            GUILayout.EndArea();
        }

        private void SolverButton(string label, JiggleSolverKind kind, JiggleSolverKind current)
        {
            if (GUILayout.Button(label, kind == current ? activeButtonStyle : GUI.skin.button))
            {
                SetCustomSolver(kind);
            }
        }

        private void DrawBenchmarkTable()
        {
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Реалізація", smallStyle, GUILayout.Width(128f));
            GUILayout.Label("ms", smallStyle, GUILayout.Width(44f));
            GUILayout.Label("пік мм", smallStyle, GUILayout.Width(48f));
            GUILayout.Label("RMS", smallStyle, GUILayout.Width(40f));
            GUILayout.Label("м/с²", smallStyle, GUILayout.Width(44f));
            GUILayout.EndHorizontal();

            for (int i = 0; i < benchmarkRows.Count; i++)
            {
                JiggleMetricsProbe.Report report = benchmarkRows[i].Report;
                GUILayout.BeginHorizontal();
                GUILayout.Label(benchmarkRows[i].Name, smallStyle, GUILayout.Width(128f));
                GUILayout.Label(report.AverageCostMilliseconds.ToString("0.000"), smallStyle, GUILayout.Width(44f));
                GUILayout.Label(report.PeakDeviationMillimeters.ToString("0.0"), smallStyle, GUILayout.Width(48f));
                GUILayout.Label(report.RmsDeviationMillimeters.ToString("0.0"), smallStyle, GUILayout.Width(40f));
                GUILayout.Label(report.RmsAcceleration.ToString("0.0"), smallStyle, GUILayout.Width(44f));
                GUILayout.EndHorizontal();
            }
        }
    }
}
