using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EarthquakeGame
{
    // A one-year contest between two earthquake-proof construction firms.
    //
    // The player and the NPC each raise a building on their own pedestal at
    // the same site, so the same real earthquake shakes both. A building is
    // worth nothing until it is handed over (竣工); the payout is
    //     高さ(段) × 現場のリスク倍率 × 耐震ボーナス
    // so the money is in building tall, in a prefecture the forecast says
    // will shake, and standing through that shaking before cashing out.
    //
    // Every action costs days off the calendar, which is how a whole year of
    // real earthquake data gets played through:
    //   積む   - 1日、1段高くなる
    //   待つ   - 次の揺れが来るまで日を進める（耐震実績を稼ぎに行く）
    //   竣工   - 1日、今の建物を金額に換えて更地に戻す
    //   移動   - 数日、現場を変える（建設中の建物は放棄）
    public class GameManager : MonoBehaviour
    {
        public enum Difficulty { Easy, Normal, Hard }
        private enum ActionKind { Build, Wait, Complete, Move }

        [Header("Managers")]
        public PlayerManager playerManager;
        public EarthquakeManager earthquakeManager;
        public MapManager mapManager;
        public FortuneTeller fortuneTeller;
        public BlockTowerManager playerTower;
        public BlockTowerManager npcTower;
        public CameraShaker cameraShaker;
        public EarthquakeSoundPlayer earthquakeSoundPlayer;
        public FortuneChimePlayer fortuneChimePlayer;
        public BGMPlayer bgmPlayer;

        [Header("Config")]
        [Tooltip("Each season starts on January 1st of a random year in this range, so different playthroughs sample different real earthquakes.")]
        public int minStartYear = 2000;
        public int maxStartYear = 2022;
        public string startingPrefecture = "東京都";

        [Tooltip("Minimum felt intensity rank (2 = shindo 2) that actually shakes the buildings.")]
        public int minFeltRankToShake = 2;

        [Tooltip("Days spent relocating to another prefecture. The building under construction is abandoned.")]
        public int moveCostDays = 3;
        [Tooltip("Upper bound on how far a single 待つ can skip when no earthquake arrives.")]
        public int maxWaitDays = 40;
        [Tooltip("Real seconds per in-game day while the calendar runs forward, so skipping time reads as time passing.")]
        public float dayStepSeconds = 0.045f;

        [Header("Difficulty")]
        public Difficulty difficulty = Difficulty.Easy;
        public NpcSkill easySkill = new NpcSkill { aimSpreadBlocks = 0.95f, rotationRange = 26f, dropHeight = 1.6f, blunderChance = 0.3f, targetHeight = 5, cashOutShindoRank = 0 };
        public NpcSkill normalSkill = new NpcSkill { aimSpreadBlocks = 0.28f, rotationRange = 9f, dropHeight = 0.9f, blunderChance = 0.1f, targetHeight = 8, cashOutShindoRank = 3 };
        public NpcSkill hardSkill = new NpcSkill { aimSpreadBlocks = 0.06f, rotationRange = 2f, dropHeight = 0.45f, blunderChance = 0f, targetHeight = 12, cashOutShindoRank = 5 };

        // How the rival firm plays: how well it stacks, how tall it builds
        // before handing over, and whether it is patient enough to wait for a
        // real earthquake to certify the building first.
        [System.Serializable]
        public class NpcSkill
        {
            [Tooltip("How far off its tower's top center the NPC aims, in block widths.")]
            public float aimSpreadBlocks = 1f;
            [Tooltip("Maximum random tilt, in degrees.")]
            public float rotationRange = 25f;
            [Tooltip("Height above the tower the block is released from - a longer fall hits harder.")]
            public float dropHeight = 1.5f;
            [Range(0f, 1f)]
            [Tooltip("Chance of a careless throw with roughly double the usual spread.")]
            public float blunderChance = 0.3f;
            [Tooltip("Floors the NPC builds before it is willing to hand the building over.")]
            public int targetHeight = 6;
            [Tooltip("Shindo rank the NPC wants its building to have survived before cashing out. 0 = hands over as soon as it is tall enough.")]
            public int cashOutShindoRank = 0;
        }

        [Header("UI")]
        public Text dateText;
        public Text survivalDaysText;
        public Text currentPrefectureText;
        public Text siteRiskText;
        public Image siteRiskBadge;
        public Text scoreText;
        public Text buildingInfoText;
        public Text latestEarthquakeText;
        public Text difficultyText;
        public Text difficultyBadgeText;
        public Button easyButton;
        public Button normalButton;
        public Button hardButton;
        public Button waitButton;
        public Button completeButton;
        public GameObject roundEndPanel;
        public Text roundEndTitleText;
        public Text roundEndScoreText;
        public Transform dropIndicator;
        public GameObject shapePreview;
        public GameObject titleScreenPanel;
        public GameObject earthquakeAlertPanel;
        public Text earthquakeAlertText;
        public IntensityMapView intensityMapView;
        [Tooltip("Persistent left-side map of this month's forecast - the contract board.")]
        public IntensityMapView forecastMapView;
        public float earthquakeAlertDuration = 2.6f;

        [Header("Forecast presentation")]
        public GameObject fortuneAnimationPanel;
        public Text fortuneAnimationText;
        public Transform fortuneAnimationIcon;
        public float fortuneAnimationDuration = 2.6f;

        [Header("Manual camera control")]
        public float cameraPanSpeed = 6f;
        public float cameraZoomSpeed = 8f;
        public float manualZoomMin = -3f;
        public float manualZoomMax = 8f;

        private DateTime currentDate;
        private int daysInSeason;
        private int elapsedDays;
        private bool isSeasonOver;
        private bool isResolving;
        private int playerScore;
        private int npcScore;
        private int completedByPlayer;
        private int completedByNpc;
        private Vector2 selectedSize = Vector2.one;
        private float selectedRotation = 0f;
        private string lastEventSummary = "まだ地震は起きていません";
        private string currentForecast = "";
        private Coroutine earthquakeAlertCoroutine;
        private Coroutine fortuneAnimationCoroutine;
        private float manualPanYOffset = 0f;
        private float manualZoomOffset = 0f;

        void Start()
        {
            if (playerTower != null) playerTower.OnBlockFell += () => OnTowerCollapsed(true);
            if (npcTower != null) npcTower.OnBlockFell += () => OnTowerCollapsed(false);
            SetDifficulty(difficulty);
            StartNewSeason();
            if (titleScreenPanel != null) titleScreenPanel.SetActive(true);
            if (bgmPlayer != null) bgmPlayer.Play();
        }

        // =================================================================
        // Season setup
        // =================================================================

        public void StartNewSeason()
        {
            playerManager.LoadData();
            earthquakeManager.LoadData();

            int year = UnityEngine.Random.Range(minStartYear, maxStartYear + 1);
            currentDate = new DateTime(year, 1, 1);
            // Minus one so the last day advanced onto is December 31st.
            daysInSeason = (DateTime.IsLeapYear(year) ? 366 : 365) - 1;
            elapsedDays = 0;
            isSeasonOver = false;
            isResolving = false;
            playerScore = 0;
            npcScore = 0;
            completedByPlayer = 0;
            completedByNpc = 0;
            lastEventSummary = "まだ地震は起きていません";
            PickNextShape();

            // Open the season where that year's strongest earthquake struck -
            // the most lucrative, most dangerous contract on the board.
            string startPrefecture = earthquakeManager != null ? earthquakeManager.GetPrefectureWithStrongestQuake(year) : null;
            playerManager.StartAt(startPrefecture ?? startingPrefecture);

            if (playerTower != null) playerTower.ClearAllBlocks();
            if (npcTower != null) npcTower.ClearAllBlocks();
            if (roundEndPanel != null) roundEndPanel.SetActive(false);
            if (earthquakeAlertCoroutine != null) { StopCoroutine(earthquakeAlertCoroutine); earthquakeAlertCoroutine = null; }
            if (earthquakeAlertPanel != null) earthquakeAlertPanel.SetActive(false);
            if (intensityMapView != null) intensityMapView.ClearAll();

            PublishMonthlyForecast(announce: true);

            if (mapManager != null) mapManager.Init(this, playerManager.AllPrefectureNames);

            RefreshUI();
        }

        public void OnStartButtonClicked()
        {
            if (titleScreenPanel != null) titleScreenPanel.SetActive(false);
        }

        public void OnRestartClicked() => StartNewSeason();

        // =================================================================
        // Difficulty
        // =================================================================

        public void SetDifficultyEasy() => SetDifficulty(Difficulty.Easy);
        public void SetDifficultyNormal() => SetDifficulty(Difficulty.Normal);
        public void SetDifficultyHard() => SetDifficulty(Difficulty.Hard);

        private void SetDifficulty(Difficulty value)
        {
            difficulty = value;

            string label = value == Difficulty.Easy ? "EASY" : value == Difficulty.Normal ? "NORMAL" : "HARD";
            string blurb = value == Difficulty.Easy
                ? "ライバルは雑に建てて自滅しがち"
                : value == Difficulty.Normal
                    ? "ライバルは堅実。実力は互角"
                    : "ライバルは高く建て、揺れを待って稼ぐ";

            if (difficultyText != null) difficultyText.text = $"難易度：{label}　-　{blurb}";
            if (difficultyBadgeText != null) difficultyBadgeText.text = $"難易度　{label}";

            TintButton(easyButton, value == Difficulty.Easy);
            TintButton(normalButton, value == Difficulty.Normal);
            TintButton(hardButton, value == Difficulty.Hard);
        }

        private void TintButton(Button button, bool selected)
        {
            if (button == null) return;
            Color baseColor = selected ? new Color(0.20f, 0.62f, 0.86f) : new Color(0.20f, 0.24f, 0.31f);
            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = baseColor * 1.25f;
            colors.pressedColor = baseColor * 0.8f;
            colors.selectedColor = baseColor;
            button.colors = colors;
        }

        private NpcSkill CurrentSkill()
        {
            return difficulty == Difficulty.Hard ? hardSkill
                 : difficulty == Difficulty.Normal ? normalSkill
                 : easySkill;
        }

        // =================================================================
        // Input
        // =================================================================

        void Update()
        {
            if (playerTower == null || Camera.main == null) return;

            FollowTowers();

            bool canAct = CanAct();

            float distanceFromCamera = Camera.main.transform.position.z * -1f;
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, distanceFromCamera));
            float x = playerTower.ClampX(worldPos.x);
            float spawnY = playerTower.GetNextSpawnY();

            if (dropIndicator != null)
            {
                dropIndicator.gameObject.SetActive(canAct);
                dropIndicator.position = new Vector3(x, spawnY + 1.1f, -0.5f);
            }

            if (shapePreview != null)
            {
                shapePreview.SetActive(canAct);
                shapePreview.transform.position = new Vector3(x, spawnY + 0.5f, -0.5f);
                shapePreview.transform.rotation = Quaternion.Euler(0, 0, selectedRotation);
            }

            if (!canAct) return;

            if (Input.GetKeyDown(KeyCode.Q)) RotateLeft();
            if (Input.GetKeyDown(KeyCode.E)) RotateRight();

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUI && Input.GetMouseButtonDown(0))
            {
                StartCoroutine(ResolveTurn(ActionKind.Build, x));
            }
        }

        private bool CanAct()
        {
            return !isSeasonOver
                && !isResolving
                && playerTower != null
                && playerTower.IsSettled()
                && (titleScreenPanel == null || !titleScreenPanel.activeSelf);
        }

        public void RotateLeft() => selectedRotation = (selectedRotation + 15f) % 360f;
        public void RotateRight() => selectedRotation = (selectedRotation - 15f + 360f) % 360f;

        public void OnWaitClicked()
        {
            if (!CanAct()) return;
            StartCoroutine(ResolveTurn(ActionKind.Wait, 0f));
        }

        public void OnCompleteClicked()
        {
            if (!CanAct() || playerTower.AliveBlockCount == 0) return;
            StartCoroutine(ResolveTurn(ActionKind.Complete, 0f));
        }

        // Called by MapManager when a prefecture button is clicked.
        public void OnPrefectureClicked(string prefectureName)
        {
            if (!CanAct() || !playerManager.CanMoveNow) return;
            pendingMoveTarget = prefectureName;
            StartCoroutine(ResolveTurn(ActionKind.Move, 0f));
        }

        private string pendingMoveTarget;

        // =================================================================
        // Turn resolution
        // =================================================================

        private IEnumerator ResolveTurn(ActionKind kind, float placementX)
        {
            isResolving = true;

            int days = 1;
            bool stopAtQuake = false;

            switch (kind)
            {
                case ActionKind.Build:
                    playerTower.PlaceBlock(selectedSize, placementX, selectedRotation, BlockTowerManager.PlayerBlockColor);
                    PickNextShape();
                    break;

                case ActionKind.Wait:
                    days = maxWaitDays;
                    stopAtQuake = true;
                    break;

                case ActionKind.Complete:
                    HandOverBuilding(playerTower, isPlayer: true);
                    break;

                case ActionKind.Move:
                    playerManager.TryMoveTo(pendingMoveTarget);
                    playerTower.ClearAllBlocks(); // the half-built site is left behind
                    days = moveCostDays;
                    break;
            }

            RefreshUI();

            // The rival firm gets to work for the same stretch of calendar,
            // so buying yourself time also hands time to the opponent.
            NpcTakeActions(Mathf.Clamp(days, 1, 3));

            yield return AdvanceCalendar(days, stopAtQuake);

            // Let physics settle before the next action is allowed.
            float guard = 0f;
            while (!playerTower.IsSettled() && guard < 4f)
            {
                guard += Time.deltaTime;
                yield return null;
            }

            RefreshUI();
            isResolving = false;

            if (elapsedDays >= daysInSeason) EndSeason();
        }

        private IEnumerator AdvanceCalendar(int days, bool stopAtFeltQuake)
        {
            for (int i = 0; i < days && elapsedDays < daysInSeason; i++)
            {
                currentDate = currentDate.AddDays(1);
                elapsedDays++;
                playerManager.AdvanceOneDay();

                if (currentDate.Day == 1) PublishMonthlyForecast(announce: true);

                bool felt = ResolveTodaysEarthquakes();
                RefreshUI();

                if (felt && stopAtFeltQuake) break;
                if (dayStepSeconds > 0f) yield return new WaitForSeconds(dayStepSeconds);
            }
        }

        // Shakes both sites with whatever the player's prefecture felt today.
        private bool ResolveTodaysEarthquakes()
        {
            var todaysEvents = earthquakeManager.GetEarthquakesOn(currentDate);

            int feltRank = 0;
            EarthquakeEvent feltEvent = null;
            EarthquakeEvent mostNotableEvent = null;
            int mostNotableRank = -1;

            foreach (var ev in todaysEvents)
            {
                // Judge by the intensity observed in the prefecture we are
                // building in, never by where the epicenter was.
                string intensity = ev.GetIntensityFor(playerManager.CurrentPrefecture);
                int rank = intensity != null ? IntensityScale.ToRank(intensity) : 0;
                if (rank > feltRank)
                {
                    feltRank = rank;
                    feltEvent = ev;
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

            bool felt = feltRank >= minFeltRankToShake;
            if (felt)
            {
                if (playerTower != null) { playerTower.Shake(feltRank); playerTower.RegisterShake(feltRank); }
                if (npcTower != null) { npcTower.Shake(feltRank); npcTower.RegisterShake(feltRank); }
                if (cameraShaker != null) cameraShaker.Shake(feltRank);
                if (earthquakeSoundPlayer != null) earthquakeSoundPlayer.PlayRumble(feltRank);

                lastEventSummary = $"{currentDate:M月d日}　震央 {feltEvent.epicenter}　M{feltEvent.magnitude}\n現場の震度 {feltEvent.GetIntensityFor(playerManager.CurrentPrefecture)}";
            }

            // Only interrupt with the banner for shaking we actually felt, or
            // for genuinely major news elsewhere - otherwise fast-forwarding
            // through a month would flash a banner every single day.
            bool worthReporting = felt || mostNotableRank >= 5;
            if (mostNotableEvent != null && worthReporting)
            {
                if (!felt)
                {
                    lastEventSummary = $"{currentDate:M月d日}　震央 {mostNotableEvent.epicenter}　M{mostNotableEvent.magnitude}\n現場では揺れを観測せず";
                }
                if (earthquakeAlertCoroutine != null) StopCoroutine(earthquakeAlertCoroutine);
                earthquakeAlertCoroutine = StartCoroutine(ShowEarthquakeAlert(mostNotableEvent, feltEvent, felt));
            }

            return felt;
        }

        // =================================================================
        // Buildings
        // =================================================================

        private int CurrentSiteForecastRank()
        {
            if (fortuneTeller == null) return 0;
            var forecast = fortuneTeller.LastForecastIntensities;
            if (forecast != null && forecast.TryGetValue(playerManager.CurrentPrefecture, out string intensity))
            {
                return IntensityScale.ToRank(intensity);
            }
            return 0;
        }

        private int EstimateReward(BlockTowerManager tower)
        {
            if (tower == null) return 0;
            return RewardTable.Reward(tower.AliveBlockCount, CurrentSiteForecastRank(), tower.MaxShindoRankSurvived);
        }

        private void HandOverBuilding(BlockTowerManager tower, bool isPlayer)
        {
            int reward = EstimateReward(tower);
            if (isPlayer)
            {
                playerScore += reward;
                completedByPlayer++;
            }
            else
            {
                npcScore += reward;
                completedByNpc++;
            }
            tower.ClearAllBlocks();
        }

        private void OnTowerCollapsed(bool isPlayer)
        {
            BlockTowerManager tower = isPlayer ? playerTower : npcTower;
            if (tower == null || tower.AliveBlockCount == 0) return;

            // Total loss: whatever was standing is written off, and the site
            // is cleared for a fresh start. No score either way.
            int lost = tower.AliveBlockCount;
            tower.ClearAllBlocks();
            lastEventSummary = isPlayer
                ? $"あなたのビル（{lost}段）が倒壊しました\n全損 - 報酬はありません"
                : $"ライバルのビル（{lost}段）が倒壊しました";
            RefreshUI();
        }

        // =================================================================
        // NPC
        // =================================================================

        private void NpcTakeActions(int actions)
        {
            if (npcTower == null) return;
            NpcSkill skill = CurrentSkill();

            for (int i = 0; i < actions; i++)
            {
                bool tallEnough = npcTower.AliveBlockCount >= skill.targetHeight;
                bool certified = npcTower.MaxShindoRankSurvived >= skill.cashOutShindoRank;

                if (tallEnough && certified && npcTower.AliveBlockCount > 0)
                {
                    HandOverBuilding(npcTower, isPlayer: false);
                    continue;
                }

                // Keep building otherwise - including while patiently holding
                // a tall tower that has not felt its certifying quake yet.
                if (tallEnough && !certified) continue;

                Vector2 size = npcTower.GetBlockSize();
                float spread = skill.aimSpreadBlocks * (UnityEngine.Random.value < skill.blunderChance ? 2.2f : 1f);
                float x = npcTower.GetTowerTopCenterX() + UnityEngine.Random.Range(-spread, spread) * size.x;
                float rotation = UnityEngine.Random.Range(-skill.rotationRange, skill.rotationRange);
                npcTower.PlaceBlock(size, x, rotation, BlockTowerManager.NpcBlockColor, skill.dropHeight);
            }
        }

        // =================================================================
        // Presentation
        // =================================================================

        private void PickNextShape()
        {
            selectedSize = playerTower != null ? playerTower.GetBlockSize() : Vector2.one * 0.6f;
            selectedRotation = 0f;
            RebuildShapePreview();
        }

        private void RebuildShapePreview()
        {
            if (shapePreview == null) return;

            foreach (var comp in shapePreview.GetComponents<Component>())
            {
                if (comp is Transform) continue;
                DestroyImmediate(comp);
            }
            for (int i = shapePreview.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(shapePreview.transform.GetChild(i).gameObject);
            }

            Color color = BlockTowerManager.PlayerBlockColor;
            color.a = 0.55f;
            ShapeMeshFactory.ApplyBlock(shapePreview, selectedSize, color, addCollider: false);
        }

        // Frames both sites, zooming out as the taller of the two grows.
        private void FollowTowers()
        {
            Camera cam = Camera.main;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            float vertical = 0f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) vertical += 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) vertical -= 1f;
            manualPanYOffset += vertical * cameraPanSpeed * Time.deltaTime;

            if (!overUI)
            {
                manualZoomOffset -= Input.mouseScrollDelta.y * cameraZoomSpeed * Time.deltaTime * 1.2f;
            }
            manualZoomOffset = Mathf.Clamp(manualZoomOffset, manualZoomMin, manualZoomMax);

            float top = Mathf.Max(
                playerTower != null ? playerTower.CurrentHeight : 0f,
                npcTower != null ? npcTower.CurrentHeight : 0f);

            float desiredHalfHeight = Mathf.Max(4.6f, (top + 2.5f) * 0.5f + 1.2f) + manualZoomOffset;
            float desiredCenterY = Mathf.Max(2.6f, top * 0.5f + 1f) + manualPanYOffset;

            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, desiredHalfHeight, Time.deltaTime * 4f);
            Vector3 pos = cam.transform.position;
            pos.y = Mathf.Lerp(pos.y, desiredCenterY, Time.deltaTime * 4f);
            cam.transform.position = pos;
        }

        private IEnumerator ShowEarthquakeAlert(EarthquakeEvent displayEvent, EarthquakeEvent feltEvent, bool felt)
        {
            if (earthquakeAlertText != null)
            {
                string intensityLabel = feltEvent?.GetIntensityFor(playerManager.CurrentPrefecture);
                string siteLine = felt && intensityLabel != null
                    ? $"現場は震度 {intensityLabel}"
                    : "現場では揺れを観測せず";
                earthquakeAlertText.text = $"{currentDate:M月d日}　震央 {displayEvent.epicenter}　M{displayEvent.magnitude}　／　{siteLine}";
            }
            if (earthquakeAlertPanel != null) earthquakeAlertPanel.SetActive(true);
            if (intensityMapView != null) intensityMapView.SetIntensities(displayEvent.intensities);

            yield return new WaitForSeconds(earthquakeAlertDuration + (felt ? 0.8f : 0f));

            if (earthquakeAlertPanel != null) earthquakeAlertPanel.SetActive(false);
            if (intensityMapView != null) intensityMapView.ClearAll();
            earthquakeAlertCoroutine = null;
        }

        private void PublishMonthlyForecast(bool announce)
        {
            if (fortuneTeller == null) return;

            currentForecast = fortuneTeller.GetMonthlyForecast(currentDate);
            if (forecastMapView != null) forecastMapView.SetIntensities(fortuneTeller.LastForecastIntensities);
            if (announce) PlayFortuneAnimation();
        }

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
                    fortuneAnimationIcon.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(elapsed * 6f));
                }
                yield return null;
            }

            fortuneAnimationPanel.SetActive(false);
            fortuneAnimationCoroutine = null;
        }

        private void EndSeason()
        {
            if (isSeasonOver) return;
            isSeasonOver = true;

            if (roundEndPanel != null) roundEndPanel.SetActive(true);

            bool playerWon = playerScore > npcScore;
            bool draw = playerScore == npcScore;

            if (roundEndTitleText != null)
            {
                roundEndTitleText.text = draw ? "引き分け" : playerWon ? "勝　利" : "敗　北";
                roundEndTitleText.color = draw
                    ? Color.white
                    : playerWon ? BlockTowerManager.PlayerBlockColor : BlockTowerManager.NpcBlockColor;
            }

            if (roundEndScoreText != null)
            {
                roundEndScoreText.text =
                    $"{currentDate:yyyy年}の営業成績\n\n" +
                    $"あなた　{playerScore:N0}万円（{completedByPlayer}棟）\n" +
                    $"ライバル　{npcScore:N0}万円（{completedByNpc}棟）";
            }
        }

        // =================================================================
        // UI
        // =================================================================

        private void RefreshUI()
        {
            if (dateText != null) dateText.text = $"{currentDate:yyyy年M月d日}";
            if (survivalDaysText != null) survivalDaysText.text = $"残り {Mathf.Max(0, daysInSeason - elapsedDays)}日";
            if (currentPrefectureText != null) currentPrefectureText.text = playerManager.CurrentPrefecture;

            if (scoreText != null)
            {
                scoreText.text = $"<あなた> {playerScore:N0}万円　　<ライバル> {npcScore:N0}万円";
            }

            int forecastRank = CurrentSiteForecastRank();
            if (siteRiskText != null)
            {
                siteRiskText.text = forecastRank > 0
                    ? $"今月の予報 震度{RewardTable.RankLabel(forecastRank)}　報酬 ×{RewardTable.RiskMultiplier(forecastRank):0.0}"
                    : $"今月の予報 なし　報酬 ×{RewardTable.RiskMultiplier(0):0.0}";
            }
            if (siteRiskBadge != null)
            {
                siteRiskBadge.color = forecastRank >= 5
                    ? new Color(0.72f, 0.22f, 0.20f)
                    : forecastRank >= 3
                        ? new Color(0.72f, 0.50f, 0.16f)
                        : new Color(0.20f, 0.40f, 0.32f);
            }

            if (buildingInfoText != null && playerTower != null)
            {
                int height = playerTower.AliveBlockCount;
                int survived = playerTower.MaxShindoRankSurvived;
                buildingInfoText.text = height == 0
                    ? "更地です。\nクリックして建て始めましょう。"
                    : $"高さ　{height}段\n" +
                      $"耐えた最大震度　{RewardTable.RankLabel(survived)}\n" +
                      $"今 竣工すると　{EstimateReward(playerTower):N0}万円";
            }

            if (latestEarthquakeText != null) latestEarthquakeText.text = lastEventSummary;

            if (completeButton != null)
            {
                completeButton.interactable = !isSeasonOver && playerTower != null && playerTower.AliveBlockCount > 0;
            }
            if (waitButton != null) waitButton.interactable = !isSeasonOver;

            if (mapManager != null)
            {
                mapManager.Refresh(playerManager.CurrentPrefecture,
                    playerManager.GetNeighbors(playerManager.CurrentPrefecture),
                    playerManager.CanMoveNow);
            }

            if (intensityMapView != null) intensityMapView.SetPlayerPosition(playerManager.CurrentPrefecture);
            if (forecastMapView != null) forecastMapView.SetPlayerPosition(playerManager.CurrentPrefecture);
        }
    }
}
