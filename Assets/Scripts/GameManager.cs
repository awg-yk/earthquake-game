using System;
using System.Collections;
using System.Collections.Generic;
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
        public FortuneChimePlayer fortuneChimePlayer;

        [Header("Config")]
        [Tooltip("Each new game starts on January 1st of a random year in this range, so different playthroughs sample different real earthquakes.")]
        public int minStartYear = 2000;
        public int maxStartYear = 2022;
        public string startingPrefecture = "東京都";

        [Tooltip("Length of one round, in in-game days.")]
        public int daysPerRound = 360;

        [Tooltip("The fortune teller gives a fresh precise forecast every this many days.")]
        public int forecastIntervalDays = 10;

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
        public GameObject shapePreview;
        public GameObject titleScreenPanel;
        public Text earthquakeAlertText;
        public IntensityMapView intensityMapView;
        [Tooltip("How long the earthquake alert banner/intensity map stays visible, in seconds.")]
        public float earthquakeAlertDuration = 3.5f;
        public Text nextShapeInfoText;

        [Header("Fortune teller animation")]
        public GameObject fortuneAnimationPanel;
        public Text fortuneAnimationText;
        public Transform fortuneAnimationIcon;
        public float fortuneAnimationDuration = 3f;

        private DateTime currentDate;
        private int survivalDays;
        private bool isRoundOver;
        private BlockShape selectedShape = BlockShape.Square;
        private float selectedRotation = 0f;
        private Coroutine earthquakeAlertCoroutine;
        private Coroutine fortuneAnimationCoroutine;
        private string currentForecast = "";

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

            float spawnY = blockTowerManager.GetNextSpawnY();

            if (dropIndicator != null)
            {
                dropIndicator.position = new Vector3(x, spawnY + 1.1f, -0.5f);
            }

            if (shapePreview != null)
            {
                shapePreview.transform.position = new Vector3(x, spawnY + 0.5f, -0.5f);
                shapePreview.transform.rotation = Quaternion.Euler(0, 0, selectedRotation);
            }

            if (Input.GetKeyDown(KeyCode.Q)) RotateLeft();
            if (Input.GetKeyDown(KeyCode.E)) RotateRight();

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUI && Input.GetMouseButtonDown(0))
            {
                blockTowerManager.PlaceBlock(selectedShape, x, selectedRotation);
                AdvanceDay();
                PickNextShape();
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
            PickNextShape();

            playerManager.StartAt(startingPrefecture);

            if (blockTowerManager != null) blockTowerManager.ClearAllBlocks();
            if (roundEndPanel != null) roundEndPanel.SetActive(false);
            if (earthquakeAlertCoroutine != null) { StopCoroutine(earthquakeAlertCoroutine); earthquakeAlertCoroutine = null; }
            if (earthquakeAlertText != null) earthquakeAlertText.gameObject.SetActive(false);
            if (intensityMapView != null) intensityMapView.ClearAll();

            currentForecast = fortuneTeller != null ? fortuneTeller.GetPeriodicForecast(currentDate) : "";
            PlayFortuneAnimation();

            if (mapManager != null)
            {
                mapManager.Init(this, playerManager.AllPrefectureNames);
            }

            RefreshUI(null);
        }

        // Picks a new random shape for the next block (the player doesn't
        // choose the shape anymore - only its rotation and drop position).
        private void PickNextShape()
        {
            selectedShape = (BlockShape)UnityEngine.Random.Range(0, 3);
            selectedRotation = 0f;
            RebuildShapePreview();

            if (nextShapeInfoText != null)
            {
                string label = BlockShapeInfo.GetLabel(selectedShape);
                int score = BlockShapeInfo.GetScore(selectedShape);
                nextShapeInfoText.text = $"次の形：{label}（{score}点）";
            }
        }

        // Called by the rotate buttons (and Q/E keys).
        public void RotateLeft() => selectedRotation = (selectedRotation + 15f) % 360f;
        public void RotateRight() => selectedRotation = (selectedRotation - 15f + 360f) % 360f;

        // Rebuilds the translucent shape preview shown just below the drop
        // arrow, so the player can see exactly what they're about to drop
        // before clicking. It has no collider - purely visual.
        private void RebuildShapePreview()
        {
            if (shapePreview == null) return;

            // DestroyImmediate (not Destroy) so the old MeshFilter/Renderer
            // are actually gone before AddComponent below runs in the same
            // frame - otherwise Unity would complain about duplicates.
            foreach (var comp in shapePreview.GetComponents<Component>())
            {
                if (comp is Transform) continue;
                DestroyImmediate(comp);
            }

            Color color = selectedShape switch
            {
                BlockShape.Square => new Color(0.85f, 0.4f, 0.4f, 0.6f),
                BlockShape.Triangle => new Color(0.4f, 0.75f, 0.85f, 0.6f),
                BlockShape.Circle => new Color(0.9f, 0.8f, 0.3f, 0.6f),
                _ => new Color(1f, 1f, 1f, 0.6f)
            };
            ShapeMeshFactory.Apply(shapePreview, selectedShape, 0.6f, color, addCollider: false);
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

            // What the player actually feels at their own location (used
            // for shake/sound and the "your area" line in the HUD).
            int playerFeltRank = 0;
            EarthquakeEvent playerEvent = null;

            // The day's most notable earthquake anywhere in Japan (used for
            // the alert banner + intensity map, shown even if it didn't
            // reach the player's own prefecture at all).
            EarthquakeEvent mostNotableEvent = null;
            int mostNotableRank = -1;

            foreach (var ev in todaysEvents)
            {
                // Rule: judge by the intensity actually observed in the
                // player's prefecture, never by where the epicenter was.
                string intensity = ev.GetIntensityFor(playerManager.CurrentPrefecture);
                int rank = intensity != null ? IntensityScale.ToRank(intensity) : 0;
                if (rank > playerFeltRank)
                {
                    playerFeltRank = rank;
                    playerEvent = ev;
                }

                int evMaxRank = 0;
                foreach (var kv in ev.intensities)
                {
                    int r = IntensityScale.ToRank(kv.Value);
                    if (r > evMaxRank) evMaxRank = r;
                }
                if (evMaxRank > mostNotableRank)
                {
                    mostNotableRank = evMaxRank;
                    mostNotableEvent = ev;
                }
            }

            if (playerFeltRank > 0)
            {
                if (blockTowerManager != null) blockTowerManager.Shake(playerFeltRank);
                if (cameraShaker != null) cameraShaker.Shake(playerFeltRank);
                if (earthquakeSoundPlayer != null) earthquakeSoundPlayer.PlayRumble(playerFeltRank);
            }

            if (mostNotableEvent != null)
            {
                if (earthquakeAlertCoroutine != null) StopCoroutine(earthquakeAlertCoroutine);
                earthquakeAlertCoroutine = StartCoroutine(ShowEarthquakeAlert(mostNotableEvent, playerFeltRank));
            }

            playerManager.AdvanceOneDay();

            if (fortuneTeller != null && forecastIntervalDays > 0 && survivalDays % forecastIntervalDays == 0)
            {
                currentForecast = fortuneTeller.GetPeriodicForecast(currentDate);
                PlayFortuneAnimation();
            }

            RefreshUI(playerEvent);

            if (survivalDays >= daysPerRound)
            {
                EndRound();
            }
        }

        private IEnumerator ShowEarthquakeAlert(EarthquakeEvent ev, int playerFeltRank)
        {
            float duration = earthquakeAlertDuration + playerFeltRank * 0.3f;

            if (earthquakeAlertText != null)
            {
                string intensityLabel = ev.GetIntensityFor(playerManager.CurrentPrefecture);
                string yourAreaLine = intensityLabel != null
                    ? $"あなたの地域の震度：{intensityLabel}"
                    : "あなたの地域では揺れは観測されませんでした";
                earthquakeAlertText.text = $"地震発生！ 震央：{ev.epicenter}　M{ev.magnitude}\n{yourAreaLine}";
                earthquakeAlertText.gameObject.SetActive(true);
            }

            if (intensityMapView != null)
            {
                intensityMapView.SetIntensities(ev.intensities);
            }

            yield return new WaitForSeconds(duration);

            if (earthquakeAlertText != null) earthquakeAlertText.gameObject.SetActive(false);
            if (intensityMapView != null) intensityMapView.ClearAll();
            earthquakeAlertCoroutine = null;
        }

        // A dedicated "the fortune teller has spoken" moment every
        // forecastIntervalDays: a full-screen overlay with a spinning icon
        // and a chime, so the forecast doesn't just quietly appear in the
        // corner of the HUD.
        private void PlayFortuneAnimation()
        {
            if (fortuneAnimationPanel == null) return;
            if (fortuneAnimationCoroutine != null) StopCoroutine(fortuneAnimationCoroutine);
            fortuneAnimationCoroutine = StartCoroutine(FortuneAnimationRoutine());
        }

        private IEnumerator FortuneAnimationRoutine()
        {
            fortuneAnimationPanel.SetActive(true);
            if (fortuneAnimationText != null) fortuneAnimationText.text = currentForecast;
            if (fortuneChimePlayer != null) fortuneChimePlayer.PlayChime();

            float elapsed = 0f;
            while (elapsed < fortuneAnimationDuration)
            {
                elapsed += Time.deltaTime;

                if (fortuneAnimationIcon != null)
                {
                    fortuneAnimationIcon.Rotate(0, 0, 90f * Time.deltaTime);
                    float pulse = 1f + 0.15f * Mathf.Sin(elapsed * 6f);
                    fortuneAnimationIcon.localScale = Vector3.one * pulse;
                }

                yield return null;
            }

            fortuneAnimationPanel.SetActive(false);
            fortuneAnimationCoroutine = null;
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

            if (fortuneText != null)
            {
                fortuneText.text = currentForecast;
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
