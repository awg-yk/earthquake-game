using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EarthquakeGame
{
    // Top-level orchestrator: owns the in-game calendar, wires
    // PlayerManager / EarthquakeManager / MapManager / BlockTowerManager
    // together, and drives the minimal UI.
    //
    // There is no GAME OVER anymore. Placing a block (choosing a shape,
    // then clicking on the tower) is the player's one action per day. If an
    // earthquake hits the player's prefecture, the block tower's base
    // shakes instead of ending the game; blocks can topple and are lost.
    // After one in-game year (360 days) the round ends and the score is the
    // total of every block still standing (fewer corners = worth more).
    public class GameManager : MonoBehaviour
    {
        [Header("Managers")]
        public PlayerManager playerManager;
        public EarthquakeManager earthquakeManager;
        public MapManager mapManager;
        public FortuneTeller fortuneTeller;
        public BlockTowerManager blockTowerManager;
        public CameraShaker cameraShaker;
        public EarthquakeSoundPlayer earthquakeSoundPlayer;

        [Header("Config")]
        [Tooltip("Each new game starts on January 1st of a random year in this range, so different playthroughs sample different real earthquakes.")]
        public int minStartYear = 2000;
        public int maxStartYear = 2022;
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
        public Transform dropIndicator;
        public GameObject titleScreenPanel;
        public Text earthquakeAlertText;
        public Text intensityMapText;
        [Tooltip("How long the earthquake alert banner/intensity map stays visible, in seconds.")]
        public float earthquakeAlertDuration = 3.5f;

        private DateTime currentDate;
        private int survivalDays;
        private bool isRoundOver;
        private BlockShape selectedShape = BlockShape.Square;
        private Coroutine earthquakeAlertCoroutine;

        void Start()
        {
            StartNewGame();
            if (titleScreenPanel != null) titleScreenPanel.SetActive(true);
        }

        // Called by the title screen's "スタート" button.
        public void OnStartButtonClicked()
        {
            if (titleScreenPanel != null) titleScreenPanel.SetActive(false);
        }

        // Follows the mouse to preview where a block will drop, and places
        // one (which also advances the day) on left click - as long as the
        // click isn't on top of a UI element (shape buttons, panels, etc).
        void Update()
        {
            if (isRoundOver || blockTowerManager == null || Camera.main == null) return;

            float distanceFromCamera = Camera.main.transform.position.z * -1f;
            Vector3 screenPos = Input.mousePosition;
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distanceFromCamera));
            float x = blockTowerManager.ClampX(worldPos.x);

            if (dropIndicator != null)
            {
                float y = blockTowerManager.GetNextSpawnY() + 0.6f;
                dropIndicator.position = new Vector3(x, y, -0.5f);
            }

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUI && Input.GetMouseButtonDown(0))
            {
                blockTowerManager.PlaceBlock(selectedShape, x);
                AdvanceDay();
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
            if (earthquakeAlertCoroutine != null) { StopCoroutine(earthquakeAlertCoroutine); earthquakeAlertCoroutine = null; }
            if (earthquakeAlertText != null) earthquakeAlertText.gameObject.SetActive(false);
            if (intensityMapText != null) intensityMapText.gameObject.SetActive(false);

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
            EarthquakeEvent shakingEvent = null;
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
                    shakingEvent = ev;
                    relevantForDisplay = ev;
                }
            }

            if (shakeRank > 0)
            {
                if (blockTowerManager != null) blockTowerManager.Shake(shakeRank);
                if (cameraShaker != null) cameraShaker.Shake(shakeRank);
                if (earthquakeSoundPlayer != null) earthquakeSoundPlayer.PlayRumble(shakeRank);

                if (earthquakeAlertCoroutine != null) StopCoroutine(earthquakeAlertCoroutine);
                earthquakeAlertCoroutine = StartCoroutine(ShowEarthquakeAlert(shakingEvent, shakeRank));
            }

            playerManager.AdvanceOneDay();
            RefreshUI(relevantForDisplay);

            if (survivalDays >= daysPerRound)
            {
                EndRound();
            }
        }

        private IEnumerator ShowEarthquakeAlert(EarthquakeEvent ev, int shakeRank)
        {
            if (earthquakeAlertText != null)
            {
                string intensityLabel = ev != null ? ev.GetIntensityFor(playerManager.CurrentPrefecture) : null;
                earthquakeAlertText.text = $"地震発生！ 震度{intensityLabel ?? "?"}";
                earthquakeAlertText.gameObject.SetActive(true);
            }

            if (intensityMapText != null && ev != null)
            {
                intensityMapText.text = BuildIntensityMapText(ev);
                intensityMapText.gameObject.SetActive(true);
            }

            yield return new WaitForSeconds(earthquakeAlertDuration);

            if (earthquakeAlertText != null) earthquakeAlertText.gameObject.SetActive(false);
            if (intensityMapText != null) intensityMapText.gameObject.SetActive(false);
            earthquakeAlertCoroutine = null;
        }

        // A simple text-based "intensity map": every affected prefecture,
        // colored and sorted by how strong the shaking was there.
        private string BuildIntensityMapText(EarthquakeEvent ev)
        {
            var entries = new List<(string pref, string intensity, int rank)>();
            foreach (var kv in ev.intensities)
            {
                entries.Add((kv.Key, kv.Value, IntensityScale.ToRank(kv.Value)));
            }
            entries.Sort((a, b) => b.rank.CompareTo(a.rank));

            var sb = new StringBuilder();
            sb.AppendLine($"震央：{ev.epicenter}　M{ev.magnitude}");
            foreach (var e in entries)
            {
                string hex = IntensityScale.GetColorHex(e.intensity);
                sb.AppendLine($"<color=#{hex}>{e.pref}：震度{e.intensity}</color>");
            }
            return sb.ToString();
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
                    playerManager.GetNeighbors(playerManager.CurrentPrefecture),
                    playerManager.CanMoveNow);
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
