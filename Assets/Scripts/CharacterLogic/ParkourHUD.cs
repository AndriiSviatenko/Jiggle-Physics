using UnityEngine;

namespace CharacterLogic
{
    public sealed class ParkourHUD : MonoBehaviour
    {
        private CharacterMover mover;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle stateStyle;
        private GUIStyle scoreStyle;
        private GUIStyle comboStyle;
        private GUIStyle stuntStyle;
        private GUIStyle speedStyle;
        private Texture2D panelTexture;
        private Texture2D accentTexture;
        private Texture2D magentaTexture;
        private Texture2D goldTexture;

        public void Configure(CharacterMover characterMover)
        {
            mover = characterMover;
        }

        private void OnGUI()
        {
            EnsureStyles();

            float leftWidth = Mathf.Min(430f, Screen.width - 32f);
            GUI.DrawTexture(new Rect(16f, 16f, leftWidth, 114f), panelTexture);
            GUI.Label(new Rect(28f, 22f, leftWidth - 24f, 28f), "NEON RUNNER", titleStyle);
            GUI.Label(new Rect(28f, 50f, leftWidth - 24f, 48f),
                "WASD — рух   •   Shift — спринт   •   Space — стрибок\nC / Ctrl — підкат / присісти   •   E — Blink", bodyStyle);

            if (mover != null)
            {
                string stateText = mover.State switch
                {
                    ParkourState.Sliding => "ПІДКАТ (SLIDE)",
                    ParkourState.Crouching => "ПРИСІДАННЯ (CROUCH)",
                    ParkourState.Stumbling => "ЗБИТО (STUMBLE)",
                    ParkourState.WallRunLeft => "СТІНОБІГ ЛІВОРУЧ",
                    ParkourState.WallRunRight => "СТІНОБІГ ПРАВОРУЧ",
                    ParkourState.Vaulting => "ВОЛЬТ (VAULT)",
                    ParkourState.Blinking => "БЛІНК (BLINK)",
                    ParkourState.Airborne => "У ПОВІТРІ",
                    _ => mover.IsMoving ? (mover.NormalizedSpeed > 0.65f ? "СПРИНТ" : "БІГ") : "ОЧІКУВАННЯ"
                };

                stateStyle.normal.textColor = mover.State == ParkourState.Sliding ? new Color(1f, 0.85f, 0.2f) :
                                              mover.State == ParkourState.Crouching ? new Color(0.35f, 0.85f, 1f) :
                                              mover.State == ParkourState.Stumbling ? new Color(1f, 0.25f, 0.25f) :
                                              new Color(1f, 0.3f, 0.72f);
                GUI.Label(new Rect(28f, 98f, 260f, 26f), stateText, stateStyle);
            }

            float rightWidth = 240f;
            float rx = Screen.width - rightWidth - 16f;
            GUI.DrawTexture(new Rect(rx, 16f, rightWidth, 126f), panelTexture);

            var scoreSys = RunnerScoreSystem.Instance;
            if (scoreSys != null)
            {
                GUI.Label(new Rect(rx + 14f, 22f, rightWidth - 28f, 26f), $"РАХУНОК: {scoreSys.Score:N0}", scoreStyle);
                GUI.Label(new Rect(rx + 14f, 48f, rightWidth - 28f, 22f), $"РЕКОРД: {scoreSys.HighScore:N0}   •   {scoreSys.Distance:F0} М", bodyStyle);
                GUI.Label(new Rect(rx + 14f, 70f, rightWidth - 28f, 22f), $"💎 ГЕМИ: {scoreSys.Gems}", bodyStyle);
            }

            if (mover != null)
            {
                float cooldown = mover.BlinkCooldownNormalized;
                GUI.DrawTexture(new Rect(rx + 14f, 96f, rightWidth - 28f, 7f), panelTexture);
                GUI.DrawTexture(new Rect(rx + 14f, 96f, (rightWidth - 28f) * (1f - cooldown), 7f), accentTexture);
                GUI.Label(new Rect(rx + 14f, 106f, rightWidth - 28f, 20f), cooldown <= 0f ? "⚡ БЛІНК ГОТОВИЙ" : "⚡ ПЕРЕЗАРЯДКА...", bodyStyle);
            }

            if (scoreSys != null)
            {
                float cx = Screen.width * 0.5f;

                if (scoreSys.Combo > 1)
                {
                    string comboStr = $"x{scoreSys.Combo} КОМБО!";
                    GUI.Label(new Rect(cx - 120f, 18f, 240f, 32f), comboStr, comboStyle);

                    float comboBarWidth = 140f;
                    GUI.DrawTexture(new Rect(cx - comboBarWidth * 0.5f, 48f, comboBarWidth, 5f), panelTexture);
                    GUI.DrawTexture(new Rect(cx - comboBarWidth * 0.5f, 48f, comboBarWidth * scoreSys.ComboNormalized, 5f), goldTexture);
                }

                if (scoreSys.ShowStuntBanner)
                {
                    GUI.Label(new Rect(cx - 200f, 58f, 400f, 32f), $"✦ {scoreSys.LatestStunt} ✦", stuntStyle);
                }
            }

            if (mover != null)
            {
                float cx = Screen.width * 0.5f;
                float bottomY = Screen.height - 56f;
                float speedKmh = mover.HorizontalVelocity.magnitude * 3.6f;

                GUI.DrawTexture(new Rect(cx - 110f, bottomY - 6f, 220f, 46f), panelTexture);
                speedStyle.normal.textColor = speedKmh > 35f ? new Color(1f, 0.25f, 0.45f) :
                                              speedKmh > 20f ? new Color(1f, 0.85f, 0.15f) :
                                              new Color(0.2f, 0.95f, 1f);
                GUI.Label(new Rect(cx - 100f, bottomY - 2f, 200f, 28f), $"{speedKmh:F0} КМ/ГОД", speedStyle);

                float speedBar = Mathf.Clamp01(speedKmh / 50f);
                GUI.DrawTexture(new Rect(cx - 90f, bottomY + 26f, 180f, 6f), panelTexture);
                GUI.DrawTexture(new Rect(cx - 90f, bottomY + 26f, 180f * speedBar, 6f), speedKmh > 35f ? magentaTexture : accentTexture);
            }

            float midX = Screen.width * 0.5f;
            float midY = Screen.height * 0.5f;
            GUI.DrawTexture(new Rect(midX - 1f, midY - 6f, 2f, 12f), accentTexture);
            GUI.DrawTexture(new Rect(midX - 6f, midY - 1f, 12f, 2f), accentTexture);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            panelTexture = CreateTexture(new Color(0.015f, 0.02f, 0.045f, 0.84f));
            accentTexture = CreateTexture(new Color(0.1f, 0.95f, 1f, 0.95f));
            magentaTexture = CreateTexture(new Color(1f, 0.15f, 0.65f, 0.95f));
            goldTexture = CreateTexture(new Color(1f, 0.85f, 0.2f, 0.95f));

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.2f, 0.95f, 1f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.86f, 0.9f, 1f) }
            };
            stateStyle = new GUIStyle(bodyStyle)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.3f, 0.72f) }
            };
            scoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.9f, 0.3f) }
            };
            comboStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.8f, 0.1f) }
            };
            stuntStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.1f, 1f, 0.85f) }
            };
            speedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.95f, 1f) }
            };
        }

        private static Texture2D CreateTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            if (panelTexture != null) Destroy(panelTexture);
            if (accentTexture != null) Destroy(accentTexture);
            if (magentaTexture != null) Destroy(magentaTexture);
            if (goldTexture != null) Destroy(goldTexture);
        }
    }
}
