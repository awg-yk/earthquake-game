using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EarthquakeGame
{
    // Top-level orchestrator for Phase 1: owns the in-game calendar,
    // wires PlayerManager / EarthquakeManager / MapManager together,
    // decides GAME OVER, and drives the minimal UI.
    public class GameManager : MonoBehaviour
    {
        [Header("Managers")]
        public PlayerManager playerManager;
        public EarthquakeManager earthquakeManager;
        public MapManager mapManager;

        [Header("Config")]
        [Tooltip("The in-game calendar starts on this date.")]
        public string startDateString = "2000-01-01";
        public string startingPrefecture = "東京都";

        [Tooltip("Minimum intensity rank that ends the game. 5弱 = 5.")]
        public int gameOverIntensityRank = 5;

        [Header("UI (Text can be swapped for TMP_Text)")]
        public Text dateText;
        public Text survivalDaysText;
        public Text currentPrefectureText;
        public Text nextMoveText;
        public Text latestEarthquakeText;
        public GameObject gameOverPanel;
        public Button advanceDayButton;

        private DateTime currentDate;
        private int survivalDays;
        private bool isGameOver;

        void Start()
        {
            StartNewGame();
        }

        public void StartNewGame()
        {
            playerManager.LoadData();
            earthquakeManager.LoadData();

            currentDate = DateTime.Parse(startDateString);
            survivalDays = 0;
            isGameOver = false;

            playerManager.StartAt(startingPrefecture);

            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (advanceDayButton != null) advanceDayButton.interactable = true;

            if (mapManager != null)
            {
                mapManager.Init(this, playerManager.AllPrefectureNames);
            }

            RefreshUI(null);
        }

        // Called by the "next day" button.
        public void OnAdvanceDayClicked()
        {
            if (isGameOver) return;
            AdvanceDay();
        }

        // Called by MapManager when a prefecture button is clicked.
        public void OnPrefectureClicked(string prefectureName)
        {
            if (isGameOver) return;
            playerManager.TryMoveTo(prefectureName);
            RefreshUI(null);
        }

        private void AdvanceDay()
        {
            currentDate = currentDate.AddDays(1);
            survivalDays++;

            var todaysEvents = earthquakeManager.GetEarthquakesOn(currentDate);
            EarthquakeEvent relevantForDisplay = todaysEvents.Count > 0 ? todaysEvents[0] : null;

            bool playerHit = false;
            foreach (var ev in todaysEvents)
            {
                // Rule: judge by the intensity actually observed in the
                // player's prefecture, never by where the epicenter was.
                string intensity = ev.GetIntensityFor(playerManager.CurrentPrefecture);
                if (intensity == null) continue;

                if (IntensityScale.ToRank(intensity) >= gameOverIntensityRank)
                {
                    playerHit = true;
                    relevantForDisplay = ev;
                }
            }

            playerManager.AdvanceOneDay();
            RefreshUI(relevantForDisplay);

            if (playerHit)
            {
                TriggerGameOver();
            }
        }

        private void TriggerGameOver()
        {
            isGameOver = true;
            if (advanceDayButton != null) advanceDayButton.interactable = false;
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
        }

        private void RefreshUI(EarthquakeEvent latestEvent)
        {
            if (dateText != null) dateText.text = $"日付：{currentDate:yyyy年M月d日}";
            if (survivalDaysText != null) survivalDaysText.text = $"生存日数：{survivalDays}日";
            if (currentPrefectureText != null) currentPrefectureText.text = $"現在地：{playerManager.CurrentPrefecture}";
            if (nextMoveText != null)
            {
                nextMoveText.text = playerManager.CanMoveNow
                    ? "移動可能"
                    : $"次回移動可能：あと{playerManager.DaysUntilNextMove}日";
            }

            if (latestEarthquakeText != null)
            {
                latestEarthquakeText.text = latestEvent == null
                    ? "最新の地震：なし"
                    : $"震央：{latestEvent.epicenter}\nM：{latestEvent.magnitude}\n" +
                      $"あなたの地域の震度：{latestEvent.GetIntensityFor(playerManager.CurrentPrefecture) ?? "観測なし"}";
            }

            if (mapManager != null)
            {
                mapManager.Refresh(playerManager.CurrentPrefecture,
                    playerManager.CanMoveNow ? playerManager.GetNeighbors(playerManager.CurrentPrefecture) : new List<string>());
            }
        }

        // Called by a "リスタート" button.
        public void OnRestartClicked()
        {
            StartNewGame();
        }
    }
}
