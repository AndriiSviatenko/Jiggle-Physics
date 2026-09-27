using System;
using UnityEngine;

namespace CharacterLogic
{

    public sealed class RunnerScoreSystem : MonoBehaviour
    {
        public static RunnerScoreSystem Instance { get; private set; }

        private const string HighScoreKey = "Runner_HighScore";
        private const float ComboTimeout = 3.5f;

        private float distanceTraveled;
        private int gemsCollected;
        private int currentScore;
        private int highScore;
        private int comboMultiplier = 1;
        private float comboTimer;
        private string latestStuntText = "";
        private float stuntBannerTimer;
        private Vector3 lastPlayerPosition;
        private bool hasLastPosition;

        public event Action<string, int> StuntPerformed;
        public event Action<int> GemCollected;
        public event Action<int> ComboChanged;

        public float Distance => distanceTraveled;
        public int Gems => gemsCollected;
        public int Score => currentScore;
        public int HighScore => highScore;
        public int Combo => comboMultiplier;
        public float ComboNormalized => Mathf.Clamp01(comboTimer / ComboTimeout);
        public string LatestStunt => latestStuntText;
        public bool ShowStuntBanner => stuntBannerTimer > 0f;

        private void Awake()
        {
            Instance = this;
            highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void TrackPlayerPosition(Vector3 playerPosition)
        {
            if (!hasLastPosition)
            {
                lastPlayerPosition = playerPosition;
                hasLastPosition = true;
                return;
            }

            Vector3 delta = playerPosition - lastPlayerPosition;
            delta.y = 0f;
            float movement = delta.magnitude;
            if (movement > 0.001f && movement < 50f)
            {
                distanceTraveled += movement;
                AddScore(Mathf.RoundToInt(movement * 2f));
            }

            lastPlayerPosition = playerPosition;
        }

        public void CollectGem(int pointValue = 100)
        {
            gemsCollected++;
            IncrementCombo();
            int points = pointValue * comboMultiplier;
            AddScore(points);
            ShowStunt($"ГЕМ +{points}", points);
            GemCollected?.Invoke(gemsCollected);
        }

        public void RegisterStunt(string stuntName, int basePoints)
        {
            IncrementCombo();
            int points = basePoints * comboMultiplier;
            AddScore(points);
            ShowStunt($"{stuntName} +{points}", points);
            StuntPerformed?.Invoke(stuntName, points);
        }

        public void ResetComboOnStumble()
        {
            if (comboMultiplier > 1)
            {
                comboMultiplier = 1;
                comboTimer = 0f;
                ShowStunt("ЗБИВСЯ! x1", 0);
                ComboChanged?.Invoke(comboMultiplier);
            }
        }

        private void IncrementCombo()
        {
            comboMultiplier = Mathf.Min(8, comboMultiplier + 1);
            comboTimer = ComboTimeout;
            ComboChanged?.Invoke(comboMultiplier);
        }

        private void AddScore(int points)
        {
            currentScore += points;
            if (currentScore > highScore)
            {
                highScore = currentScore;
                PlayerPrefs.SetInt(HighScoreKey, highScore);
            }
        }

        private void ShowStunt(string text, int points)
        {
            latestStuntText = text;
            stuntBannerTimer = 1.6f;
        }

        private void Update()
        {
            if (comboMultiplier > 1)
            {
                comboTimer -= Time.deltaTime;
                if (comboTimer <= 0f)
                {
                    comboMultiplier = 1;
                    ComboChanged?.Invoke(comboMultiplier);
                }
            }

            if (stuntBannerTimer > 0f)
            {
                stuntBannerTimer -= Time.deltaTime;
            }
        }

        public void ResetSession(Vector3 startPosition)
        {
            distanceTraveled = 0f;
            gemsCollected = 0;
            currentScore = 0;
            comboMultiplier = 1;
            comboTimer = 0f;
            lastPlayerPosition = startPosition;
            hasLastPosition = true;
        }
    }
}
