using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JigglePhysics.Demo
{
    public sealed class JiggleLabHud : MonoBehaviour
    {
        private const int TabControl = 0;
        private const int TabTuning = 1;
        private const int TabSolvers = 2;
        private const int TabVisual = 3;
        private static readonly string[] TabNames = { "Керування", "Тюнінг", "Солвери", "Візуалізація" };
        private static readonly string[] SolverNames = { "Spring", "Minimal", "Verlet" };
        private static readonly JiggleSolverKind[] SolverKinds = { JiggleSolverKind.SpringDamper, JiggleSolverKind.MinimalSpring, JiggleSolverKind.StableVerlet };
        private const float MainWidth = 404f;
        private const float LegendWidth = 336f;
        private const float LegendHeight = 470f;

        [SerializeField] private bool showWindow = true;
        [SerializeField] private bool showLegend = true;
        [SerializeField] private int selectedTab;
        [SerializeField] private int tuningTargetIndex;

        private JiggleLabController controller;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private GUIStyle activeButtonStyle;
        private Texture2D panelTexture;
        private Vector2 scrollPosition;
        private string statusMessage = string.Empty;
        private float statusTimestamp;
        private float controllerLookupTimestamp;
        private bool slowMotion;

        private void Start()
        {
            controller = FindFirstObjectByType<JiggleLabController>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || controller == null)
            {
                return;
            }

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                showWindow = !showWindow;
            }

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                showLegend = !showLegend;
            }

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                ToggleSlowMotion();
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                controller.ResetAll();
                SetStatus("Симуляцію скинуто");
            }

            if (keyboard.tKey.wasPressedThisFrame)
            {
                controller.TeleportTest();
                SetStatus("Телепорт-тест: дивись, як точки снапнулись до цілей");
            }

            if (keyboard.spaceKey.wasPressedThisFrame) controller.PlayAction(JiggleLabCharacterDriver.LabAction.Jump);
            if (keyboard.jKey.wasPressedThisFrame) controller.PlayAction(JiggleLabCharacterDriver.LabAction.Attack);
            if (keyboard.kKey.wasPressedThisFrame) controller.PlayAction(JiggleLabCharacterDriver.LabAction.Flip);
            if (keyboard.lKey.wasPressedThisFrame) controller.PlayAction(JiggleLabCharacterDriver.LabAction.Slide);
            if (keyboard.fKey.wasPressedThisFrame) controller.PlayAction(JiggleLabCharacterDriver.LabAction.FallLand);
            if (keyboard.gKey.wasPressedThisFrame) controller.PlayAction(JiggleLabCharacterDriver.LabAction.FallRoll);
            if (keyboard.hKey.wasPressedThisFrame) controller.PlayAction(JiggleLabCharacterDriver.LabAction.Dash);
            if (keyboard.uKey.wasPressedThisFrame) controller.PlayAction(JiggleLabCharacterDriver.LabAction.Vault);
            if (keyboard.nKey.wasPressedThisFrame) controller.PlayAction(JiggleLabCharacterDriver.LabAction.Stumble);
            if (keyboard.cKey.wasPressedThisFrame) controller.PlayAction(controller.IsCrouching ? JiggleLabCharacterDriver.LabAction.CrouchOff : JiggleLabCharacterDriver.LabAction.CrouchOn);
            if (keyboard.vKey.wasPressedThisFrame) controller.PlayAction(controller.IsWallRunning ? JiggleLabCharacterDriver.LabAction.WallRunOff : JiggleLabCharacterDriver.LabAction.WallRunOn);
            if (keyboard.bKey.wasPressedThisFrame) controller.ToggleAutoShow();

            if (keyboard.digit1Key.wasPressedThisFrame) ApplySolver(controller.DuelRigA, JiggleSolverKind.SpringDamper);
            if (keyboard.digit2Key.wasPressedThisFrame) ApplySolver(controller.DuelRigA, JiggleSolverKind.MinimalSpring);
            if (keyboard.digit3Key.wasPressedThisFrame) ApplySolver(controller.DuelRigA, JiggleSolverKind.StableVerlet);
            if (keyboard.digit4Key.wasPressedThisFrame) ApplySolver(controller.DuelRigB, JiggleSolverKind.SpringDamper);
            if (keyboard.digit5Key.wasPressedThisFrame) ApplySolver(controller.DuelRigB, JiggleSolverKind.MinimalSpring);
            if (keyboard.digit6Key.wasPressedThisFrame) ApplySolver(controller.DuelRigB, JiggleSolverKind.StableVerlet);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (panelTexture != null)
            {
                Destroy(panelTexture);
            }
        }

        private void OnGUI()
        {
            if (controller == null && Time.realtimeSinceStartup - controllerLookupTimestamp > 1f)
            {
                controller = FindFirstObjectByType<JiggleLabController>();
                controllerLookupTimestamp = Time.realtimeSinceStartup;
            }

            EnsureStyles();

            if (controller == null || !controller.IsReady)
            {
                GUI.Label(new Rect(18f, 18f, 460f, 24f), "Jiggle Lab: немає JiggleLabController у сцені", labelStyle);
                return;
            }

            if (showWindow)
            {
                DrawMainWindow();
            }
            else
            {
                DrawCollapsedBar();
            }

            if (showLegend)
            {
                DrawLegend();
            }

            DrawStatusToast();
        }

        private Rect MainRect()
        {
            float height = Mathf.Min(MainHeightForTab(), Screen.height - 32f);
            return new Rect(16f, 16f, MainWidth, height);
        }

        private float MainHeightForTab()
        {
            if (selectedTab == TabTuning)
            {
                return 660f;
            }

            if (selectedTab == TabSolvers)
            {
                return 560f;
            }

            return selectedTab == TabVisual ? 540f : 560f;
        }

        private Rect LegendRect()
        {
            float x = Screen.width - LegendWidth - 16f;
            if (x < MainRect().xMax + 12f)
            {
                float below = MainRect().yMax + 12f;
                if (below + LegendHeight > Screen.height)
                {
                    below = Mathf.Max(16f, Screen.height - LegendHeight - 16f);
                }

                return new Rect(16f, below, Mathf.Min(LegendWidth, Screen.width - 32f), LegendHeight);
            }

            return new Rect(x, 16f, LegendWidth, Mathf.Min(LegendHeight, Screen.height - 32f));
        }

        private void DrawCollapsedBar()
        {
            GUILayout.BeginArea(new Rect(16f, 16f, 320f, 30f), panelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("JIGGLE LAB", sectionStyle, GUILayout.Width(96f));
            if (GUILayout.Button("Панель (F1)", buttonStyle))
            {
                showWindow = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawMainWindow()
        {
            GUILayout.BeginArea(MainRect(), panelStyle);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false);

            GUILayout.Label("JIGGLE LAB", titleStyle);
            GUILayout.Label("<i>Якір → ціль анімації → фізична точка. Все живе — тюнь і порівнюй.</i>", subtitleStyle);
            GUILayout.Space(4f);

            int nextTab = GUILayout.Toolbar(selectedTab, TabNames, buttonStyle);
            if (nextTab != selectedTab)
            {
                selectedTab = nextTab;
            }

            GUILayout.Space(6f);
            if (selectedTab == TabControl)
            {
                DrawControlTab();
            }
            else if (selectedTab == TabTuning)
            {
                DrawTuningTab();
            }
            else if (selectedTab == TabSolvers)
            {
                DrawSolversTab();
            }
            else
            {
                DrawVisualTab();
            }

            if (statusMessage.Length > 0 && Time.realtimeSinceStartup - statusTimestamp < 5f)
            {
                GUILayout.Space(6f);
                GUILayout.Label(statusMessage, subtitleStyle);
            }

            GUILayout.Space(4f);
            GUILayout.Label("F1 панель · F2 легенда · F5 слоу-мо · R reset · T телепорт · Space стрибок · J удар · K сальто · L слайд · F падіння · C присісти · V стіна · B авто-шоу · 1-3 солвер A · 4-6 солвер B", smallStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawControlTab()
        {
            GUILayout.Label("КАМЕРА", sectionStyle);
            GUILayout.BeginHorizontal();
            JiggleLabCameraRig cameraRig = controller.CameraRig;
            for (int i = 0; i < JiggleLabCameraRig.ViewNames.Length; i++)
            {
                bool active = cameraRig != null && cameraRig.ActiveView == i && !cameraRig.IsUserControlled;
                if (GUILayout.Button(JiggleLabCameraRig.ViewNames[i], active ? activeButtonStyle : buttonStyle))
                {
                    if (cameraRig != null)
                    {
                        cameraRig.ApplyView(i);
                    }
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Середня кнопка миші — орбіта, колесо — зум.", smallStyle);

            GUILayout.Space(8f);
            DrawMotionSection();

            GUILayout.Space(8f);
            GUILayout.Label("ФІКСИ", sectionStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(slowMotion ? "Слоу-мо ×0.25 [ON]" : "Слоу-мо (F5)", slowMotion ? activeButtonStyle : buttonStyle))
            {
                ToggleSlowMotion();
            }

            if (GUILayout.Button("Reset (R)", buttonStyle))
            {
                controller.ResetAll();
                SetStatus("Симуляцію скинуто");
            }

            if (GUILayout.Button("Телепорт-тест (T)", buttonStyle))
            {
                controller.TeleportTest();
                SetStatus("Телепорт-тест: точки снапнулись до цілей");
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            DrawDuelSummary();
        }

        private void DrawMotionSection()
        {
            GUILayout.Label("РУХ ТА ДІЇ", sectionStyle);
            JiggleLabCharacterDriver driver = controller.CharacterDriver;
            if (driver == null || !driver.Configured)
            {
                GUILayout.Label("Немає драйвера руху — у сцені лише пресети.", smallStyle);
                return;
            }

            GUILayout.BeginHorizontal();
            for (int i = 0; i < JiggleLabCharacterDriver.LocomotionNames.Length; i++)
            {
                bool active = driver.LocomotionIndex == i;
                if (GUILayout.Button(JiggleLabCharacterDriver.LocomotionNames[i], active ? activeButtonStyle : buttonStyle))
                {
                    controller.SetLocomotion(i);
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawActionButton("Стрибок (Space)", JiggleLabCharacterDriver.LabAction.Jump);
            DrawActionButton("Слайд (L)", JiggleLabCharacterDriver.LabAction.Slide);
            DrawActionButton("Удар (J)", JiggleLabCharacterDriver.LabAction.Attack);
            DrawActionButton("Падіння (F)", JiggleLabCharacterDriver.LabAction.FallLand);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawActionButton("Перекат (G)", JiggleLabCharacterDriver.LabAction.FallRoll);
            DrawActionButton("Сальто (K)", JiggleLabCharacterDriver.LabAction.Flip);
            DrawActionButton("Ривок (H)", JiggleLabCharacterDriver.LabAction.Dash);
            DrawActionButton("Переліз (U)", JiggleLabCharacterDriver.LabAction.Vault);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawActionButton(controller.IsCrouching ? "Встати (C)" : "Присісти (C)", controller.IsCrouching ? JiggleLabCharacterDriver.LabAction.CrouchOff : JiggleLabCharacterDriver.LabAction.CrouchOn, controller.IsCrouching);
            DrawActionButton(controller.IsWallRunning ? "Зійти (V)" : "Стіна (V)", controller.IsWallRunning ? JiggleLabCharacterDriver.LabAction.WallRunOff : JiggleLabCharacterDriver.LabAction.WallRunOn, controller.IsWallRunning);
            DrawActionButton("Струс (N)", JiggleLabCharacterDriver.LabAction.Stumble);
            if (GUILayout.Button("Авто-шоу (B)", controller.AutoShow ? activeButtonStyle : buttonStyle))
            {
                controller.ToggleAutoShow();
            }

            GUILayout.EndHorizontal();

            if (controller.AutoShow)
            {
                GUILayout.Label($"Авто-шоу: <b>{controller.AutoShowName}</b> — будь-яка дія зупинить показ", labelStyle);
            }
        }

        private void DrawActionButton(string label, JiggleLabCharacterDriver.LabAction action, bool highlighted = false)
        {
            if (GUILayout.Button(label, highlighted ? activeButtonStyle : buttonStyle))
            {
                controller.PlayAction(action);
            }
        }

        private void DrawDuelSummary()
        {
            if (!controller.HasDuel)
            {
                return;
            }

            GUILayout.Label("ЗАРАЗ", sectionStyle);
            GUILayout.Label($"A · {controller.GetSolver(controller.DuelRigA)} · {controller.GetDeviationMillimeters(controller.DuelRigA):F1} мм", labelStyle);
            GUILayout.Label($"B · {controller.GetSolver(controller.DuelRigB)} · {controller.GetDeviationMillimeters(controller.DuelRigB):F1} мм", labelStyle);
            GUILayout.Label($"Δ зміщення A↔B: <b>{controller.GetDuelOffsetDeltaMillimeters():F1} мм</b>", labelStyle);
        }

        private void DrawTuningTab()
        {
            if (controller.AllRigs.Count == 0)
            {
                GUILayout.Label("Немає ригів у сцені.", smallStyle);
                return;
            }

            tuningTargetIndex = Mathf.Clamp(tuningTargetIndex, 0, controller.AllRigs.Count - 1);
            GUILayout.Label("ЩО ТЮНИМО", sectionStyle);
            DrawTuningTargets();

            JiggleRig target = controller.AllRigs[tuningTargetIndex];
            JiggleProfile profile = controller.GetProfile(target);
            if (profile == null)
            {
                GUILayout.Label("У рига немає профілю.", smallStyle);
                return;
            }

            if (controller.HasDuel && (target == controller.DuelRigA || target == controller.DuelRigB))
            {
                GUILayout.Label("A і B ділять один профіль — умови дуелі завжди однакові.", smallStyle);
            }

            GUILayout.Space(6f);
            GUILayout.Label("ПАРАМЕТРИ (живуть лише в Play Mode)", sectionStyle);
            profile.Frequency = DrawSlider("Частота f", profile.Frequency, 0.5f, 8f, " Гц");
            profile.DampingRatio = DrawSlider("Затухання ζ", profile.DampingRatio, 0f, 1.5f, string.Empty);
            profile.GravityMultiplier = DrawSlider("Гравітація", profile.GravityMultiplier, -1f, 2f, " ×");
            profile.MaxOffset = DrawSlider("Ліміт зсуву", profile.MaxOffset, 0.01f, 0.3f, " м");
            profile.MaxAngle = DrawSlider("Кут конуса", profile.MaxAngle, 5f, 90f, "°");
            profile.SquashStretch = DrawSlider("Squash & Stretch", profile.SquashStretch, 0f, 0.5f, string.Empty);
            profile.Blend = DrawSlider("Вплив фізики (Blend)", profile.Blend, 0f, 1f, string.Empty);

            int substeps = DrawIntSlider("Підкроки (Max Substeps)", profile.MaxSubsteps, 1, 8);
            if (substeps != profile.MaxSubsteps)
            {
                profile.MaxSubsteps = substeps;
                controller.RebuildRig(target);
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Скинути профіль з ассета", buttonStyle))
            {
                controller.ResetProfileFromSource(target);
                SetStatus("Профіль повернуто до значень ассета");
            }

            if (GUILayout.Button("Солвер: " + controller.GetSolver(target), buttonStyle))
            {
                CycleSolver(target);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("ФОРМУЛА", sectionStyle);
            float omega = 2f * Mathf.PI * profile.Frequency;
            GUILayout.Label($"a = ω²·(x_target − x) − 2ζω·v + g", subtitleStyle);
            GUILayout.Label($"ω = 2π·f = {omega:F1} рад/с · ω² = {omega * omega:F0} · 2ζω = {2f * profile.DampingRatio * omega:F1}", smallStyle);
        }

        private void DrawSolversTab()
        {
            GUILayout.Label("ДУЕЛЬ СОЛВЕРІВ", sectionStyle);
            if (!controller.HasDuel)
            {
                GUILayout.Label("У сцені немає двох моделей-дуелянтів.", smallStyle);
                return;
            }

            GUILayout.Label("Однаковий рух і профіль — різна математика інтегрування.", smallStyle);
            GUILayout.Space(4f);

            GUILayout.Label($"Модель A · {controller.DuelRigA.name}", labelStyle);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < SolverKinds.Length; i++)
            {
                DrawSolverButton(controller.DuelRigA, SolverKinds[i], SolverNames[i]);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label($"Модель B · {controller.DuelRigB.name}", labelStyle);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < SolverKinds.Length; i++)
            {
                DrawSolverButton(controller.DuelRigB, SolverKinds[i], SolverNames[i]);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            if (GUILayout.Button("B = A (перевірка синхронізації)", buttonStyle))
            {
                controller.SetSolver(controller.DuelRigB, controller.GetSolver(controller.DuelRigA));
                SetStatus("Однаковий солвер: Δ зміщення має впасти до ~0");
            }

            GUILayout.Space(8f);
            GUILayout.Label("МЕТРИКИ", sectionStyle);
            float targetDelta = controller.GetDuelTargetDeltaMillimeters();
            float offsetDelta = controller.GetDuelOffsetDeltaMillimeters();
            float tipDelta = controller.GetDuelTipDeltaMillimeters();

            string targetColor = targetDelta < 1.5f ? "#59ff8a" : "#ff5544";
            GUILayout.Label($"Δ цілей (синхрон анімації): <color={targetColor}><b>{targetDelta:F2} мм</b></color>", labelStyle);
            GUILayout.Label($"Δ зміщення (різниця солверів): <b>{offsetDelta:F2} мм</b>", labelStyle);
            GUILayout.Label($"Δ точок (що бачиш на екрані): <b>{tipDelta:F2} мм</b>", labelStyle);

            GUILayout.Space(4f);
            DrawRigMetrics("A", controller.DuelRigA);
            DrawRigMetrics("B", controller.DuelRigB);

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Скинути піки", buttonStyle))
            {
                controller.ResetPeaks();
            }

            if (GUILayout.Button("Reset A+B", buttonStyle))
            {
                controller.ResetRig(controller.DuelRigA);
                controller.ResetRig(controller.DuelRigB);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawTuningTargets()
        {
            for (int row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < 3; column++)
                {
                    int index = row * 3 + column;
                    if (index >= controller.AllRigs.Count)
                    {
                        GUILayout.FlexibleSpace();
                        continue;
                    }

                    JiggleRig rig = controller.AllRigs[index];
                    bool active = tuningTargetIndex == index;
                    if (GUILayout.Button(ShortRigName(rig), active ? activeButtonStyle : buttonStyle))
                    {
                        tuningTargetIndex = index;
                    }
                }

                GUILayout.EndHorizontal();
            }
        }

        private void DrawRigMetrics(string prefix, JiggleRig rig)
        {
            GUILayout.Label($"{prefix}: {controller.GetSolver(rig)} · зараз {controller.GetDeviationMillimeters(rig):F1} мм · пік {controller.GetPeakDeviationMillimeters(rig):F1} мм · крок {controller.GetStepMilliseconds(rig):F3} мс", smallStyle);
        }

        private void DrawSolverButton(JiggleRig rig, JiggleSolverKind kind, string name)
        {
            bool active = controller.GetSolver(rig) == kind;
            if (GUILayout.Button(name, active ? activeButtonStyle : buttonStyle))
            {
                ApplySolver(rig, kind);
            }
        }

        private void DrawVisualTab()
        {
            GUILayout.Label("ЩО МАЛЮЄМО В ГРІ", sectionStyle);
            DrawVisualToggle("Стрічки: план (біла) + факт (рожева)", JiggleLabVisualSettings.ShowBones, JiggleLabVisualSettings.SimulatedBoneColor, value => JiggleLabVisualSettings.ShowBones = value);
            DrawVisualToggle("Відхилення план→факт", JiggleLabVisualSettings.ShowDeviation, JiggleLabVisualSettings.LowDeviationColor, value => JiggleLabVisualSettings.ShowDeviation = value);
            DrawVisualToggle("Точки цілі та фізики", JiggleLabVisualSettings.ShowPoints, JiggleLabVisualSettings.TargetColor, value => JiggleLabVisualSettings.ShowPoints = value);
            DrawVisualToggle("Ліміт MaxOffset (коло)", JiggleLabVisualSettings.ShowLimit, JiggleLabVisualSettings.LimitColor, value => JiggleLabVisualSettings.ShowLimit = value);
            DrawVisualToggle("Конус MaxAngle", JiggleLabVisualSettings.ShowCone, JiggleLabVisualSettings.ConeColor, value => JiggleLabVisualSettings.ShowCone = value);
            DrawVisualToggle("Швидкість точки", JiggleLabVisualSettings.ShowVelocity, JiggleLabVisualSettings.VelocityColor, value => JiggleLabVisualSettings.ShowVelocity = value);
            DrawVisualToggle("Сили (пружина + гравітація)", JiggleLabVisualSettings.ShowForces, JiggleLabVisualSettings.SpringColor, value => JiggleLabVisualSettings.ShowForces = value);
            DrawVisualToggle("Трейли (ціль + точка)", JiggleLabVisualSettings.ShowTrails, JiggleLabVisualSettings.SimulatedColor, value => JiggleLabVisualSettings.ShowTrails = value);
            DrawVisualToggle("Підписи відхилень", JiggleLabVisualSettings.ShowLabels, JiggleLabVisualSettings.MediumDeviationColor, value => JiggleLabVisualSettings.ShowLabels = value);
            DrawVisualToggle("Лінія A↔B (дуель)", JiggleLabVisualSettings.ShowDuelLink, JiggleLabVisualSettings.DuelLinkColor, value => JiggleLabVisualSettings.ShowDuelLink = value);

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Увімкнути все", buttonStyle))
            {
                JiggleLabVisualSettings.EnableAll();
            }

            if (GUILayout.Button("Мінімум", buttonStyle))
            {
                JiggleLabVisualSettings.EnableMinimum();
            }

            if (GUILayout.Button(showLegend ? "Легенда [ON]" : "Легенда [OFF]", showLegend ? activeButtonStyle : buttonStyle))
            {
                showLegend = !showLegend;
            }

            GUILayout.EndHorizontal();
        }

        private void DrawVisualToggle(string label, bool value, Color color, System.Action<bool> apply)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>●</color> {label}", labelStyle, GUILayout.Width(300f));
            bool next = GUILayout.Toggle(value, string.Empty);
            if (next != value)
            {
                apply(next);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawLegend()
        {
            GUILayout.BeginArea(LegendRect(), panelStyle);
            GUILayout.Label("ЛЕГЕНДА", titleStyle);
            GUILayout.Space(4f);
            DrawLegendRow(JiggleLabVisualSettings.AnchorColor, "якір кістки — старт ланцюжка");
            DrawLegendRow(JiggleLabVisualSettings.TargetColor, "ціль анімації — біла точка і стрічка «план»");
            DrawLegendRow(JiggleLabVisualSettings.SimulatedColor, "фізична точка — рожева точка і стрічка «факт»");
            DrawLegendRow(JiggleLabVisualSettings.LowDeviationColor, "відхилення план→факт < 50 % ліміту");
            DrawLegendRow(JiggleLabVisualSettings.MediumDeviationColor, "відхилення 50-80 %");
            DrawLegendRow(JiggleLabVisualSettings.HighDeviationColor, "відхилення > 80 % — майже ліміт");
            DrawLegendRow(JiggleLabVisualSettings.LimitColor, "коло ліміту MaxOffset");
            DrawLegendRow(JiggleLabVisualSettings.ConeColor, "конус MaxAngle");
            DrawLegendRow(JiggleLabVisualSettings.VelocityColor, "вектор швидкості точки");
            DrawLegendRow(JiggleLabVisualSettings.SpringColor, "сила пружини (до цілі)");
            DrawLegendRow(JiggleLabVisualSettings.GravityColor, "гравітація");
            DrawLegendRow(JiggleLabVisualSettings.DuelLinkColor, "зв'язок A↔B — різниця моделей");

            GUILayout.Space(6f);
            JiggleProfile profile = controller != null ? controller.SharedDuelProfile : null;
            if (profile != null)
            {
                float omega = 2f * Mathf.PI * profile.Frequency;
                GUILayout.Label("<b>ЖИВА ФОРМУЛА</b>", sectionStyle);
                GUILayout.Label("a = ω²·(x_target − x) − 2ζω·v + g", subtitleStyle);
                GUILayout.Label($"f = {profile.Frequency:F2} Гц · ω = {omega:F1} рад/с · ζ = {profile.DampingRatio:F2} · g×{profile.GravityMultiplier:F2}", smallStyle);
            }

            GUILayout.Space(6f);
            GUILayout.Label("F1 панель · F2 легенда · F5 слоу-мо · R reset · T телепорт · B авто-шоу", smallStyle);
            GUILayout.EndArea();
        }

        private void DrawLegendRow(Color color, string text)
        {
            GUILayout.Label($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>●</color> {text}", labelStyle);
        }

        private void DrawStatusToast()
        {
            if (statusMessage.Length == 0 || Time.realtimeSinceStartup - statusTimestamp >= 5f)
            {
                return;
            }

            GUI.Label(new Rect(MainRect().x, Mathf.Min(MainRect().yMax + 8f, Screen.height - 26f), 700f, 22f), statusMessage, smallStyle);
        }

        private float DrawSlider(string label, float value, float minimum, float maximum, string unit)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: <b>{value:F2}{unit}</b>", smallStyle, GUILayout.Width(220f));
            float next = GUILayout.HorizontalSlider(value, minimum, maximum);
            GUILayout.EndHorizontal();
            return next;
        }

        private int DrawIntSlider(string label, int value, int minimum, int maximum)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: <b>{value}</b>", smallStyle, GUILayout.Width(220f));
            int next = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, minimum, maximum));
            GUILayout.EndHorizontal();
            return next;
        }

        private void ApplySolver(JiggleRig rig, JiggleSolverKind kind)
        {
            if (rig == null)
            {
                return;
            }

            controller.SetSolver(rig, kind);
            SetStatus($"{ShortRigName(rig)} → {kind}");
        }

        private void CycleSolver(JiggleRig rig)
        {
            JiggleSolverKind current = controller.GetSolver(rig);
            int next = ((int)current + 1) % SolverKinds.Length;
            ApplySolver(rig, SolverKinds[next]);
        }

        private void ToggleSlowMotion()
        {
            slowMotion = !slowMotion;
            Time.timeScale = slowMotion ? 0.25f : 1f;
        }

        private void SetStatus(string message)
        {
            statusMessage = message;
            statusTimestamp = Time.realtimeSinceStartup;
        }

        private static string ShortRigName(JiggleRig rig)
        {
            if (rig == null)
            {
                return "—";
            }

            if (rig.name.EndsWith("_A"))
            {
                return "A";
            }

            if (rig.name.EndsWith("_B"))
            {
                return "B";
            }

            int separator = rig.name.IndexOf('_');
            return separator >= 0 && separator + 1 < rig.name.Length ? rig.name.Substring(separator + 1) : rig.name;
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0.05f, 0.07f, 0.11f, 1f));
            panelTexture.Apply();

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTexture;
            panelStyle.padding = new RectOffset(14, 14, 12, 12);

            titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold, fontSize = 16 };
            titleStyle.normal.textColor = new Color(0.4f, 0.9f, 1f);
            subtitleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 };
            subtitleStyle.normal.textColor = new Color(0.95f, 0.85f, 0.4f);
            sectionStyle = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold, fontSize = 13 };
            sectionStyle.normal.textColor = new Color(0.65f, 0.85f, 1f);
            labelStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 };
            labelStyle.normal.textColor = new Color(0.9f, 0.93f, 0.97f);
            smallStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11, wordWrap = true };
            smallStyle.normal.textColor = new Color(0.72f, 0.76f, 0.84f);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };
            activeButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
            activeButtonStyle.normal.textColor = new Color(0.45f, 1f, 0.65f);
        }
    }
}
