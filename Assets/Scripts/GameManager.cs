using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EarthquakeGame
{
    // Top-level orchestrator: owns the in-game calendar, wires
    // PlayerManager / EarthquakeManager / MapManager / BlockTowerManager
    // together, and drives the minimal UI.
    //
    // There is no GAME OVER anymore. Placing a block (choosing a shape and
    // an X position) is the player's one action per day. If an earthquake
    // hits the player's prefecture, the block tower's base shakes instead
    // of ending the game; blocks can topple and are lost. After one
    // in-game year (360 days) the round ends and the score is the total
    // of every block still standing (fewer corners = worth more).
    public class GameManager : MonoBehaviour
    {
        [Header("Managers")]
        public PlayerManager playerManager;
        public EarthquakeManager earthquakeManager;
        public MapManager mapManager;
        public FortuneTeller fortuneTeller;
        public BlockTowerManager blockTowerManager;

        [Header("Config")]
        [Tooltip("Each new game starts on January 1st of a random year in this range, so different playthroughs sample different real earthquakes.")]
        public int minStartYear = 2000;
        public int maxStartYear = 2024;
        public string startingPrefecture = "東京都";

        [Tooltip("Length of one round, in in-game days.")]
        public int daysPerRound = 360;

        [Header("UI (Text can be swapped for TMP_Text)")]
        public Text dateText;
        public Text survivalDaysText;
        public Text currentPrefectureText;
        public Text nextMoveText;
        public Text latestEarthquakeText;
        public Text fortuneText;
        public Text scoreText;
        public GameObject roundEndPanel;
        public Text roundEndScoreText;
        public Slider placementSlider;
        public Text placementPositionText;
        public Transform dropIndicator;

        private DateTime currentDate;
        private int survivalDays;
        private bool isRoundOver;
        private BlockShape selectedShape = BlockShape.Square;

        void Start()
        {
            StartNewGame();
        }

        // Keeps the drop-preview marker and position readout in sync with
        // the slider every frame, so the player always sees exactly where
        // the next block will land before pressing "積む".
        void Update()
        {
            if (isRoundOver || placementSlider == null) return;

            float x = placementSlider.value;

            if (placementPositionText != null)
            {
                placementPositionText.text = $"配置位置：{x:0.0}";
            }

            if (dropIndicator != null)
            {
                float y = blockTowerManager != null ? blockTowerManager.GetNextSpawnY() + 0.6f : 5f;
                dropIndicator.position = new Vector3(x, y, -0.5f);
            }
        }

        public void StartNewGame()
        {
            playerManager.LoadData();
            earthquakeManager.LoadData();

            int year = UnityEngine.Random.Range(minStartYear, maxStartYear + 1);
            currentDate = new DateTime(year, 1, 1);
            survivalDays = 0;
            isRoundOver = false;
            selectedShape = BlockShape.Square;

            playerManager.StartAt(startingPrefecture);

            if (blockTowerManager != null) blockTowerManager.ClearAllBlocks();
            if (roundEndPanel != null) roundEndPanel.SetActive(false);

            if (mapManager != null)
            {
                mapManager.Init(this, playerManager.AllPrefectureNames);
            }

            RefreshUI(null);
        }

        // Called by the shape-select buttons.
        public void SelectSquare() => selectedShape = BlockShape.Square;
        public void SelectTriangle() => selectedShape = BlockShape.Triangle;
        public void SelectCircle() => selectedShape = BlockShape.Circle;

        // Called by the "積む" button. Placing a block is the day's action.
        public void OnPlaceBlockClicked()
        {
            if (isRoundOver) return;

            float xPosition = placementSlider != null ? placementSlider.value : 0f;
            blockTowerManager.PlaceBlock(selectedShape, xPosition);

            AdvanceDay();
        }

        // Called by MapManager when a prefecture button is clicked.
        public void OnPrefectureClicked(string prefectureName)
        {
            if (isRoundOver) return;
            playerManager.TryMoveTo(prefectureName);
            RefreshUI(null);
        }

        private void AdvanceDay()
        {
            currentDate = currentDate.AddDays(1);
            survivalDays++;

            var todaysEvents = earthquakeManager.GetEarthquakesOn(currentDate);
            EarthquakeEvent relevantForDisplay = todaysEvents.Count > 0 ? todaysEvents[0] : null;

            int shakeRank = 0;
            foreach (var ev in todaysEvents)
            {
                // Rule: judge by the intensity actually observed in the
                // player's prefecture, never by where the epicenter was.
                string intensity = ev.GetIntensityFor(playerManager.CurrentPrefecture);
                if (intensity == null) continue;

                int rank = IntensityScale.ToRank(intensity);
                if (rank > shakeRank)
                {
                    shakeRank = rank;
                    relevantForDisplay = ev;
                }
            }

            if (shakeRank > 0 && blockTowerManager != null)
            {
                blockTowerManager.Shake(shakeRank);
            }

            playerManager.AdvanceOneDay();
            RefreshUI(relevantForDisplay);

            if (survivalDays >= daysPerRound)
            {
                EndRound();
            }
        }

        private void EndRound()
        {
            isRoundOver = true;
            int finalScore = blockTowerManager != null ? blockTowerManager.GetScore() : 0;

            if (roundEndPanel != null) roundEndPanel.SetActive(true);
            if (roundEndScoreText != null)
            {
                roundEndScoreText.text = $"1年間、生き延びました。\n最終スコア：{finalScore}点\n残った積み木：{(blockTowerManager != null ? blockTowerManager.AliveBlockCount : 0)}個";
            }
        }

        private void RefreshUI(EarthquakeEvent latestEvent)
        {
            if (dateText != null) dateText.text = $"日付：{currentDate:yyyy年M月d日}";
            if (survivalDaysText != null) survivalDaysText.text = $"経過日数：{survivalDays}/{daysPerRound}日";
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

            if (fortuneText != null && fortuneTeller != null)
            {
                fortuneText.text = fortuneTeller.GetFortune(currentDate, playerManager.CurrentPrefecture);
            }

            if (scoreText != null && blockTowerManager != null)
            {
                scoreText.text = $"現在のスコア：{blockTowerManager.GetScore()}点（積み木{blockTowerManager.AliveBlockCount}個）";
            }
        }

        // Called by a "リスタート" button.
        public void OnRestartClicked()
        {
            StartNewGame();
        }
    }
}
