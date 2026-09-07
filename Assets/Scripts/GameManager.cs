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
    // Player and NPC share one tower and alternate turns dropping blocks.
    // The moment any block falls - whether from a bad placement or from an
    // earthquake shake - whoever was "responsible" loses immediately:
    // the mover who dropped the block that started it, or the player if an
    // earthquake (tied to the player's own location) causes the fall.
    public class GameManager : MonoBehaviour
    {
        private enum Turn { Player, Npc }
        private enum FallCause { PlayerPlacement, NpcPlacement, Earthquake }
        public enum Difficulty { Easy, Normal, Hard }

        [Header("Managers")]
        public PlayerManager playerManager;
        public EarthquakeManager earthquakeManager;
        public MapManager mapManager;
        public FortuneTeller fortuneTeller;
        public BlockTowerManager blockTowerManager;
        public CameraShaker cameraShaker;
        public EarthquakeSoundPlayer earthquakeSoundPlayer;
        public FortuneChimePlayer fortuneChimePlayer;
        public BGMPlayer bgmPlayer;

        [Header("Config")]
        [Tooltip("Each new game starts on January 1st of a random year in this range, so different playthroughs sample different real earthquakes.")]
        public int minStartYear = 2000;
        public int maxStartYear = 2022;
        public string startingPrefecture = "東京都";

        [Tooltip("Minimum felt intensity rank (3 = shindo 3) that actually shakes the tower - matches the real-world threshold where people notice shaking.")]
        public int minFeltRankToShake = 2;

        [Tooltip("Seconds the NPC waits before dropping its block, so its turn reads clearly instead of happening instantly.")]
        public float npcThinkDelay = 0.8f;
        public Difficulty difficulty = Difficulty.Easy;
        public Text difficultyText;

        [Header("UI (Text can be swapped for TMP_Text)")]
        public Text dateText;
        public Text survivalDaysText;
        public Text currentPrefectureText;
        public Text latestEarthquakeText;
        public Text turnText;
        public GameObject roundEndPanel;
        public Text roundEndScoreText;
        public Transform dropIndicator;
        public GameObject shapePreview;
        public GameObject titleScreenPanel;
        public Text earthquakeAlertText;
        public IntensityMapView intensityMapView;
        [Tooltip("Persistent left-side map showing this month's forecasted warning areas - stays visible all month, unlike intensityMapView's brief alert flashes.")]
        public IntensityMapView forecastMapView;
        [Tooltip("How long the earthquake alert banner/intensity map stays visible, in seconds.")]
        public float earthquakeAlertDuration = 3.5f;

        [Header("Fortune teller animation")]
        public GameObject fortuneAnimationPanel;
        public Text fortuneAnimationText;
        public Transform fortuneAnimationIcon;
        public float fortuneAnimationDuration = 3f;

        [Header("Manual camera control")]
        [Tooltip("How fast the arrow keys / WASD pan the camera, in world units per second.")]
        public float cameraPanSpeed = 6f;
        [Tooltip("How fast the scroll wheel zooms the camera in/out.")]
        public float cameraZoomSpeed = 8f;
        public float manualZoomMin = -3f;
        public float manualZoomMax = 6f;

        private DateTime currentDate;
        private int survivalDays;
        private bool isGameOver;
        private Turn currentTurn;
        private FallCause pendingFallCause;
        private Coroutine npcTurnCoroutine;
        private Vector2 selectedSize = Vector2.one;
        private float selectedRotation = 0f;
        private Coroutine earthquakeAlertCoroutine;
        private Coroutine fortuneAnimationCoroutine;
        private string currentForecast = "";

        // Manual offsets the player adds on top of the automatic
        // tower-height follow, so they can freely look up/down/around the
        // tower instead of being locked to the auto-framed view.
        private float manualPanYOffset = 0f;
        private float manualZoomOffset = 0f;

        void Start()
        {
            if (blockTowerManager != null) blockTowerManager.OnBlockFell += HandleBlockFell;
            StartNewGame();
            if (titleScreenPanel != null) titleScreenPanel.SetActive(true);
            if (bgmPlayer != null) bgmPlayer.Play();
        }

        // Sudden death: the first block to fall ends the game. Whoever
        // caused it - the mover who placed the block that started the
        // topple, or the player if it was an earthquake - loses.
        private void HandleBlockFell()
        {
            if (isGameOver) return;
            isGameOver = true;
            if (npcTurnCoroutine != null) { StopCoroutine(npcTurnCoroutine); npcTurnCoroutine = null; }

            bool playerLost = pendingFallCause != FallCause.NpcPlacement;
            ShowGameOver(playerLost);
        }

        // Called by the title screen's "スタート" button.
        public void OnStartButtonClicked()
        {
            if (titleScreenPanel != null) titleScreenPanel.SetActive(false);
        }

        // Called by the title screen's difficulty buttons.
        public void SetDifficultyEasy() => SetDifficulty(Difficulty.Easy);
        public void SetDifficultyNormal() => SetDifficulty(Difficulty.Normal);
        public void SetDifficultyHard() => SetDifficulty(Difficulty.Hard);

        private void SetDifficulty(Difficulty value)
        {
            difficulty = value;
            if (difficultyText != null)
            {
                string label = value == Difficulty.Easy ? "EASY" : value == Difficulty.Normal ? "NORMAL" : "HARD";
                difficultyText.text = $"難易度：{label}";
            }
        }

        // Follows the mouse to preview where a block will drop, and places
        // one (which also advances the day) on left click - as long as the
        // click isn't on top of a UI element (shape buttons, panels, etc).
        void Update()
        {
            if (isGameOver || blockTowerManager == null || Camera.main == null) return;

            FollowTowerHeight();

            bool isPlayerTurn = currentTurn == Turn.Player;

            float distanceFromCamera = Camera.main.transform.position.z * -1f;
            Vector3 screenPos = Input.mousePosition;
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distanceFromCamera));
            float x = blockTowerManager.ClampX(worldPos.x);

            float spawnY = blockTowerManager.GetNextSpawnY();

            if (dropIndicator != null)
            {
                dropIndicator.gameObject.SetActive(isPlayerTurn);
                dropIndicator.position = new Vector3(x, spawnY + 1.1f, -0.5f);
            }

            if (shapePreview != null)
            {
                shapePreview.SetActive(isPlayerTurn);
                shapePreview.transform.position = new Vector3(x, spawnY + 0.5f, -0.5f);
                shapePreview.transform.rotation = Quaternion.Euler(0, 0, selectedRotation);
            }

            if (!isPlayerTurn) return;

            if (Input.GetKeyDown(KeyCode.Q)) RotateLeft();
            if (Input.GetKeyDown(KeyCode.E)) RotateRight();

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUI && Input.GetMouseButtonDown(0) && blockTowerManager.IsSettled())
            {
                pendingFallCause = FallCause.PlayerPlacement;
                blockTowerManager.PlaceBlock(selectedSize, x, selectedRotation, BlockTowerManager.PlayerBlockColor);
                BeginNpcTurn();
            }
        }

        // Hands the turn to the NPC: it "thinks" briefly, then drops a
        // block at a randomized position/rotation of its own.
        private void BeginNpcTurn()
        {
            currentTurn = Turn.Npc;
            RefreshUI(null);
            if (npcTurnCoroutine != null) StopCoroutine(npcTurnCoroutine);
            npcTurnCoroutine = StartCoroutine(NpcTurnRoutine());
        }

        private IEnumerator NpcTurnRoutine()
        {
            yield return new WaitForSeconds(npcThinkDelay);
            while (!blockTowerManager.IsSettled()) yield return null;
            if (isGameOver) yield break;

            Vector2 npcSize = blockTowerManager.GetBlockSize();
            // Harder difficulties aim closer to center and rotate less
            // wildly, so the NPC is less likely to knock itself over.
            float aimJitter = difficulty == Difficulty.Hard ? 0.15f : difficulty == Difficulty.Normal ? 0.4f : 0.8f;
            float rotationRange = difficulty == Difficulty.Hard ? 5f : difficulty == Difficulty.Normal ? 15f : 30f;
            float x = UnityEngine.Random.Range(-aimJitter, aimJitter) * blockTowerManager.baseHalfWidth;
            float rotation = UnityEngine.Random.Range(-rotationRange, rotationRange);

            pendingFallCause = FallCause.NpcPlacement;
            blockTowerManager.PlaceBlock(npcSize, x, rotation, BlockTowerManager.NpcBlockColor);

            // Let the NPC's own block finish settling (and any resulting
            // fall get attributed to it) before the day advances and a
            // possible earthquake could otherwise steal the blame.
            yield return null;
            while (!blockTowerManager.IsSettled()) yield return null;
            if (isGameOver) yield break;

            AdvanceDay();
            PickNextShape();
            currentTurn = Turn.Player;
            npcTurnCoroutine = null;
            RefreshUI(null);
        }

        public void StartNewGame()
        {
            playerManager.LoadData();
            earthquakeManager.LoadData();

            int year = UnityEngine.Random.Range(minStartYear, maxStartYear + 1);
            currentDate = new DateTime(year, 1, 1);
            survivalDays = 0;
            isGameOver = false;
            currentTurn = Turn.Player;
            if (npcTurnCoroutine != null) { StopCoroutine(npcTurnCoroutine); npcTurnCoroutine = null; }
            PickNextShape();

            // Start wherever that year's single strongest earthquake hit,
            // rather than a fixed prefecture - falls back to the default if
            // the year happened to have no recorded quakes at all.
            string startPrefecture = earthquakeManager != null ? earthquakeManager.GetPrefectureWithStrongestQuake(year) : null;
            playerManager.StartAt(startPrefecture ?? startingPrefecture);

            if (blockTowerManager != null) blockTowerManager.ClearAllBlocks();
            if (roundEndPanel != null) roundEndPanel.SetActive(false);
            if (earthquakeAlertCoroutine != null) { StopCoroutine(earthquakeAlertCoroutine); earthquakeAlertCoroutine = null; }
            if (earthquakeAlertText != null) earthquakeAlertText.gameObject.SetActive(false);
            if (intensityMapView != null) intensityMapView.ClearAll();

            currentForecast = fortuneTeller != null ? fortuneTeller.GetMonthlyForecast(currentDate) : "";
            if (forecastMapView != null && fortuneTeller != null) forecastMapView.SetIntensities(fortuneTeller.LastForecastIntensities);
            PlayFortuneAnimation();

            if (mapManager != null)
            {
                mapManager.Init(this, playerManager.AllPrefectureNames);
            }

            RefreshUI(null);
        }

        // Picks a random elongation for the next block (the player no
        // longer chooses a shape - only its rotation and drop position).
        private void PickNextShape()
        {
            selectedSize = blockTowerManager != null ? blockTowerManager.GetBlockSize() : Vector2.one * 0.6f;
            selectedRotation = 0f;
            RebuildShapePreview();
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

            Color color = BlockTowerManager.PlayerBlockColor;
            color.a = 0.6f;
            ShapeMeshFactory.Apply(shapePreview, selectedSize, color, addCollider: false);
        }

        // Zooms out and pans up as the tower grows, so tall towers never
        // scroll out of view above the camera's default frame. The player
        // can additionally pan (arrow keys / WASD) and zoom (scroll wheel)
        // on top of this auto-framing to freely look around the tower.
        private void FollowTowerHeight()
        {
            Camera cam = Camera.main;

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            float vertical = 0f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) vertical += 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) vertical -= 1f;
            manualPanYOffset += vertical * cameraPanSpeed * Time.deltaTime;

            if (!overUI)
            {
                float scroll = Input.mouseScrollDelta.y;
                manualZoomOffset -= scroll * cameraZoomSpeed * Time.deltaTime * 60f * 0.02f;
            }
            manualZoomOffset = Mathf.Clamp(manualZoomOffset, manualZoomMin, manualZoomMax);

            float top = blockTowerManager.CurrentHeight; // above the base, base is at world Y ~ 0
            float desiredHalfHeight = Mathf.Max(6f, (top + 3f) * 0.5f + 1f) + manualZoomOffset;
            float desiredCenterY = Mathf.Max(3f, top * 0.5f + 1f) + manualPanYOffset;

            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, desiredHalfHeight, Time.deltaTime * 4f);

            Vector3 pos = cam.transform.position;
            pos.y = Mathf.Lerp(pos.y, desiredCenterY, Time.deltaTime * 4f);
            cam.transform.position = pos;
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

            // Real-world threshold: people generally don't notice shaking
            // below shindo 3, so neither the tower nor the camera/sound
            // react below that, even if the JSON technically recorded a
            // weaker intensity (1/2) for this prefecture.
            bool feltShake = playerFeltRank >= minFeltRankToShake;
            if (feltShake)
            {
                // Any block that falls during this shake is the player's
                // loss - it's their location choice that exposed the tower.
                pendingFallCause = FallCause.Earthquake;
                if (blockTowerManager != null) blockTowerManager.Shake(playerFeltRank);
                if (cameraShaker != null) cameraShaker.Shake(playerFeltRank);
                if (earthquakeSoundPlayer != null) earthquakeSoundPlayer.PlayRumble(playerFeltRank);
            }

            if (mostNotableEvent != null)
            {
                if (earthquakeAlertCoroutine != null) StopCoroutine(earthquakeAlertCoroutine);
                earthquakeAlertCoroutine = StartCoroutine(ShowEarthquakeAlert(mostNotableEvent, playerEvent, feltShake));
            }

            playerManager.AdvanceOneDay();

            if (fortuneTeller != null && currentDate.Day == 1)
            {
                currentForecast = fortuneTeller.GetMonthlyForecast(currentDate);
                if (forecastMapView != null) forecastMapView.SetIntensities(fortuneTeller.LastForecastIntensities);
                PlayFortuneAnimation();
            }

            RefreshUI(playerEvent);
        }

        private IEnumerator ShowEarthquakeAlert(EarthquakeEvent displayEvent, EarthquakeEvent playerEvent, bool feltShake)
        {
            float duration = earthquakeAlertDuration + (feltShake ? 1f : 0f);

            if (earthquakeAlertText != null)
            {
                // Use playerEvent (the one that actually hit the player's
                // prefecture) for the "your area" line, never displayEvent -
                // they can be different earthquakes on the same day, and
                // mixing them up caused the text to contradict the shake.
                string intensityLabel = playerEvent?.GetIntensityFor(playerManager.CurrentPrefecture);
                string yourAreaLine = feltShake && intensityLabel != null
                    ? $"あなたの地域の震度：{intensityLabel}"
                    : "あなたの地域では揺れは観測されませんでした";
                earthquakeAlertText.text = $"地震発生！ 震央：{displayEvent.epicenter}　M{displayEvent.magnitude}\n{yourAreaLine}";
                earthquakeAlertText.gameObject.SetActive(true);
            }

            if (intensityMapView != null)
            {
                intensityMapView.SetIntensities(displayEvent.intensities);
                intensityMapView.BringToFront();
            }

            yield return new WaitForSeconds(duration);

            if (earthquakeAlertText != null) earthquakeAlertText.gameObject.SetActive(false);
            if (intensityMapView != null) intensityMapView.ClearAll();
            earthquakeAlertCoroutine = null;
        }

        // A dedicated "the forecaster has spoken" moment on the 1st of every
        // month: a full-screen overlay with a spinning icon and a chime, so
        // the forecast doesn't just quietly appear in the corner of the HUD.
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

        private void ShowGameOver(bool playerLost)
        {
            if (roundEndPanel != null) roundEndPanel.SetActive(true);
            if (roundEndScoreText != null)
            {
                int finalCount = blockTowerManager != null ? blockTowerManager.AliveBlockCount : 0;
                roundEndScoreText.text = playerLost
                    ? $"あなたの積んだブロックが崩れました…\nNPCの勝ち！\n（{survivalDays}日目、{finalCount}個まで積み上がっていました）"
                    : $"NPCの積んだブロックが崩れました！\nあなたの勝ち！\n（{survivalDays}日目、{finalCount}個まで積み上がっていました）";
            }
        }

        private void RefreshUI(EarthquakeEvent latestEvent)
        {
            if (dateText != null) dateText.text = $"日付：{currentDate:yyyy年M月d日}";
            if (survivalDaysText != null) survivalDaysText.text = $"経過日数：{survivalDays}日目";
            if (currentPrefectureText != null) currentPrefectureText.text = $"現在地：{playerManager.CurrentPrefecture}";
            if (turnText != null) turnText.text = currentTurn == Turn.Player ? "あなたの番です" : "NPCの番です…";

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

            if (intensityMapView != null)
            {
                intensityMapView.SetPlayerPosition(playerManager.CurrentPrefecture);
            }
            if (forecastMapView != null)
            {
                forecastMapView.SetPlayerPosition(playerManager.CurrentPrefecture);
            }
        }

        // Called by a "リスタート" button.
        public void OnRestartClicked()
        {
            StartNewGame();
        }
    }
}
