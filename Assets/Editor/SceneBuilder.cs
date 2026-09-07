using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using EarthquakeGame;

// Editor-only tool that builds MainScene entirely from code, so the whole
// hierarchy (Canvas, panels, buttons, managers, wiring, block tower) is
// generated consistently instead of by hand-clicking through the Editor UI.
// Run via the menu: Earthquake Game > Build Main Scene
//
// The UI follows one small design system rather than raw default widgets:
// dark rounded cards with a soft drop shadow, an accent rule under every
// card title, a muted-label / bright-value type hierarchy, and one shared
// palette also used by the blocks themselves.
public static class SceneBuilder
{
    // ---- Design tokens ----------------------------------------------------
    private static readonly Color CardBg = new Color(0.086f, 0.114f, 0.161f, 0.94f);
    private static readonly Color CardBgSolid = new Color(0.086f, 0.114f, 0.161f, 1f);
    private static readonly Color CardShadow = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color TextPrimary = new Color(0.94f, 0.96f, 0.98f);
    private static readonly Color TextMuted = new Color(0.58f, 0.65f, 0.74f);
    private static readonly Color AccentCyan = new Color(0.31f, 0.76f, 0.97f);
    private static readonly Color AccentAmber = new Color(1f, 0.72f, 0.30f);
    private static readonly Color AccentGreen = new Color(0.35f, 0.77f, 0.45f);
    private static readonly Color AlertRed = new Color(0.72f, 0.17f, 0.16f, 0.96f);
    private static readonly Color ButtonNeutral = new Color(0.20f, 0.24f, 0.31f);
    private static readonly Color ButtonPrimary = new Color(0.20f, 0.62f, 0.86f);
    private static readonly Color ButtonGreen = new Color(0.18f, 0.49f, 0.32f);

    private static Color PlayerColor => BlockTowerManager.PlayerBlockColor;
    private static Color NpcColor => BlockTowerManager.NpcBlockColor;

    private const float FontSizeMultiplier = 1.35f;

    // Narrow pedestal: a badly placed block has to actually tip off it, which
    // is what makes a collapse a real risk rather than a formality.
    private const float BaseHalfWidth = 1.4f;
    // How far each firm's site sits from the middle of the screen.
    private const float SiteOffsetX = 2.3f;

    [MenuItem("Earthquake Game/Build Main Scene")]
    public static void BuildMainScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();

        // Clear everything currently in the scene so this can be re-run safely.
        foreach (var root in scene.GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }

        Camera mainCamera = CreateCamera();
        CreateSkyBackdrop(mainCamera);
        CreateGround();
        var (earthquakeSoundPlayer, fortuneChimePlayer, bgmPlayer) = CreateAudioPlayers();
        CreateLight();

        // Two sites side by side on the same ground: the same earthquake
        // shakes both, so only the two firms' decisions differ.
        Rigidbody2D playerBase = CreateBasePlatform("PlayerBasePlatform", -SiteOffsetX, BlockTowerManager.PlayerBlockColor);
        Rigidbody2D npcBase = CreateBasePlatform("NpcBasePlatform", SiteOffsetX, BlockTowerManager.NpcBlockColor);

        Canvas canvas = CreateCanvas();
        CreateEventSystem();

        Vector2 topLeft = new Vector2(0f, 1f);
        Vector2 topCenter = new Vector2(0.5f, 1f);
        Vector2 topRight = new Vector2(1f, 1f);
        Vector2 bottomLeft = new Vector2(0f, 0f);
        Vector2 bottomCenter = new Vector2(0.5f, 0f);
        Vector2 bottomRight = new Vector2(1f, 0f);

        // --- Top center: calendar, site, and the two firms' earnings ------
        RectTransform infoCard = CreateCard(canvas.transform, "InfoCard", topCenter, topCenter, 0, -18, 460, 140);
        Text dateText = CreateLabel(infoCard, "DateText", topCenter, topCenter, 0, -12, 420, 26, 15, "2000年1月1日", TextMuted, TextAnchor.UpperCenter);
        Text survivalDaysText = CreateLabel(infoCard, "SurvivalDaysText", topCenter, topCenter, 0, -36, 420, 26, 15, "残り 365日", TextMuted, TextAnchor.UpperCenter);
        Text currentPrefectureText = CreateLabel(infoCard, "CurrentPrefectureText", topCenter, topCenter, 0, -62, 420, 44, 26, "東京都", TextPrimary, TextAnchor.UpperCenter);
        currentPrefectureText.fontStyle = FontStyle.Bold;
        Text scoreText = CreateLabel(infoCard, "ScoreText", topCenter, topCenter, 0, -106, 430, 28, 15, "<あなた> 0万円　　<ライバル> 0万円", TextPrimary, TextAnchor.UpperCenter);

        Image siteRiskBadge = CreateBadge(canvas.transform, "SiteRiskBadge", topCenter, topCenter, 0, -170, 380, 44, new Color(0.20f, 0.40f, 0.32f), out Text siteRiskText);
        siteRiskText.text = "今月の予報 なし　報酬 ×1.0";

        // --- Top left: this month's forecast map + difficulty badge -------
        IntensityMapView forecastMapView = CreateIntensityMapPanel(canvas.transform, "ForecastMapPanel", topLeft, topLeft, 20, -18, "今月の警戒マップ", AccentAmber);
        RectTransform difficultyCard = CreateCard(canvas.transform, "DifficultyCard", topLeft, topLeft, 20, -282, 300, 44);
        Text difficultyBadgeText = CreateLabel(difficultyCard, "DifficultyBadgeText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, 280, 30, 16, "難易度　EASY", AccentCyan, TextAnchor.MiddleCenter);
        difficultyBadgeText.fontStyle = FontStyle.Bold;

        // --- Top right: today's quake map, latest quake, movement list ----
        IntensityMapView intensityMapView = CreateIntensityMapPanel(canvas.transform, "IntensityMapPanel", topRight, topRight, -20, -18, "今日の震度マップ", AccentCyan);

        RectTransform latestCard = CreateCard(canvas.transform, "LatestQuakeCard", topRight, topRight, -20, -282, 300, 104);
        CreateCardHeader(latestCard, "最新の地震", AccentCyan);
        Text latestEarthquakeText = CreateLabel(latestCard, "LatestEarthquakeText", topLeft, topLeft, 16, -42, 268, 56, 14, "まだ地震は起きていません", TextPrimary, TextAnchor.UpperLeft);

        RectTransform moveCard = CreateCard(canvas.transform, "MoveCard", topRight, topRight, -20, -398, 300, 286, out GameObject moveCardRoot);
        CreateCardHeader(moveCard, "次の現場へ移る", AccentGreen);
        CreateLabel(moveCard, "MoveCardNote", topLeft, topLeft, 16, -38, 268, 20, 11, "3日かかり、建設中のビルは放棄します", TextMuted, TextAnchor.UpperLeft);
        GameObject buttonContainer = CreateButtonContainer(moveCard);
        Button prefectureButtonTemplate = CreatePrefectureButtonTemplate(canvas.transform);

        // --- Bottom right: the turn's action menu -------------------------
        RectTransform controlsCard = CreateCard(canvas.transform, "ControlsCard", bottomRight, bottomRight, -20, 20, 300, 214);
        CreateCardHeader(controlsCard, "この手で何をする", AccentCyan);
        Button rotateLeftButton = CreateStyledButton(controlsCard, "RotateLeftButton", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), -58, -42, 104, 42, "⟲", ButtonNeutral, 20);
        Button rotateRightButton = CreateStyledButton(controlsCard, "RotateRightButton", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 58, -42, 104, 42, "⟳", ButtonNeutral, 20);
        Button waitButton = CreateStyledButton(controlsCard, "WaitButton", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0, -92, 268, 44, "揺れを待つ", ButtonNeutral, 17);
        Button completeButton = CreateStyledButton(controlsCard, "CompleteButton", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0, -142, 268, 44, "竣工して引き渡す", ButtonGreen, 17);
        CreateLabel(controlsCard, "PlacementHintText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 0, 8, 268, 22, 12, "画面をクリック：1段積む（1日）", TextMuted, TextAnchor.LowerCenter);

        // --- Bottom left: the building under construction ------------------
        RectTransform buildingCard = CreateCard(canvas.transform, "BuildingCard", bottomLeft, bottomLeft, 20, 20, 300, 170);
        CreateCardHeader(buildingCard, "建設中のビル", PlayerColor);
        Text buildingInfoText = CreateLabel(buildingCard, "BuildingInfoText", topLeft, topLeft, 16, -44, 268, 110, 14,
            "更地です。\nクリックして建て始めましょう。", TextPrimary, TextAnchor.UpperLeft);

        // --- Bottom center: earthquake alert banner (hidden by default) ---
        GameObject earthquakeAlertPanel = CreateAlertBanner(canvas.transform, bottomCenter, out Text earthquakeAlertText);

        // --- World-space helpers ------------------------------------------
        Transform dropIndicator = CreateDropIndicator();
        GameObject shapePreview = new GameObject("ShapePreview");
        shapePreview.transform.position = new Vector3(0, 7f, -0.5f);

        // --- Overlays ------------------------------------------------------
        GameObject roundEndPanel = CreateRoundEndPanel(canvas.transform, out Text roundEndTitleText, out Text roundEndScoreText, out Button restartButton);
        GameObject fortuneAnimationPanel = CreateFortuneAnimationPanel(canvas.transform, out Text fortuneAnimationText, out Transform fortuneAnimationIcon);
        GameObject titleScreenPanel = CreateTitleScreenPanel(canvas.transform, out Button startButton, out Button easyButton, out Button normalButton, out Button hardButton, out Text difficultyText);

        // --- Managers ------------------------------------------------------
        GameObject gameManagerObj = new GameObject("GameManager");
        GameObject playerManagerObj = new GameObject("PlayerManager");
        GameObject earthquakeManagerObj = new GameObject("EarthquakeManager");
        GameObject mapManagerObj = new GameObject("MapManager");
        GameObject fortuneTellerObj = new GameObject("FortuneTeller");
        GameObject playerTowerObj = new GameObject("PlayerTower");
        GameObject npcTowerObj = new GameObject("NpcTower");

        var gameManager = gameManagerObj.AddComponent<GameManager>();
        var playerManager = playerManagerObj.AddComponent<PlayerManager>();
        playerManager.moveIntervalDays = 5; // explicit, so a stale saved scene value can never override this
        var earthquakeManager = earthquakeManagerObj.AddComponent<EarthquakeManager>();
        var mapManager = mapManagerObj.AddComponent<MapManager>();
        var fortuneTeller = fortuneTellerObj.AddComponent<FortuneTeller>();

        var playerTower = playerTowerObj.AddComponent<BlockTowerManager>();
        playerTower.baseRigidbody = playerBase;
        playerTower.baseHalfWidth = BaseHalfWidth;

        var npcTower = npcTowerObj.AddComponent<BlockTowerManager>();
        npcTower.baseRigidbody = npcBase;
        npcTower.baseHalfWidth = BaseHalfWidth;

        gameManager.playerManager = playerManager;
        gameManager.earthquakeManager = earthquakeManager;
        gameManager.mapManager = mapManager;
        gameManager.fortuneTeller = fortuneTeller;
        gameManager.playerTower = playerTower;
        gameManager.npcTower = npcTower;
        gameManager.dateText = dateText;
        gameManager.survivalDaysText = survivalDaysText;
        gameManager.currentPrefectureText = currentPrefectureText;
        gameManager.siteRiskText = siteRiskText;
        gameManager.siteRiskBadge = siteRiskBadge;
        gameManager.scoreText = scoreText;
        gameManager.buildingInfoText = buildingInfoText;
        gameManager.latestEarthquakeText = latestEarthquakeText;
        gameManager.waitButton = waitButton;
        gameManager.completeButton = completeButton;
        gameManager.difficultyText = difficultyText;
        gameManager.difficultyBadgeText = difficultyBadgeText;
        gameManager.easyButton = easyButton;
        gameManager.normalButton = normalButton;
        gameManager.hardButton = hardButton;
        gameManager.roundEndPanel = roundEndPanel;
        gameManager.roundEndTitleText = roundEndTitleText;
        gameManager.roundEndScoreText = roundEndScoreText;
        gameManager.dropIndicator = dropIndicator;
        gameManager.shapePreview = shapePreview;
        gameManager.cameraShaker = mainCamera.GetComponent<CameraShaker>();
        gameManager.earthquakeSoundPlayer = earthquakeSoundPlayer;
        gameManager.fortuneChimePlayer = fortuneChimePlayer;
        gameManager.bgmPlayer = bgmPlayer;
        gameManager.earthquakeAlertPanel = earthquakeAlertPanel;
        gameManager.earthquakeAlertText = earthquakeAlertText;
        gameManager.intensityMapView = intensityMapView;
        gameManager.forecastMapView = forecastMapView;
        gameManager.titleScreenPanel = titleScreenPanel;
        gameManager.fortuneAnimationPanel = fortuneAnimationPanel;
        gameManager.fortuneAnimationText = fortuneAnimationText;
        gameManager.fortuneAnimationIcon = fortuneAnimationIcon;

        mapManager.prefectureButtonPrefab = prefectureButtonTemplate;
        mapManager.buttonContainer = buttonContainer.transform;
        mapManager.panelRoot = moveCardRoot;

        fortuneTeller.earthquakeManager = earthquakeManager;

        UnityEventTools.AddVoidPersistentListener(rotateLeftButton.onClick, gameManager.RotateLeft);
        UnityEventTools.AddVoidPersistentListener(rotateRightButton.onClick, gameManager.RotateRight);
        UnityEventTools.AddVoidPersistentListener(waitButton.onClick, gameManager.OnWaitClicked);
        UnityEventTools.AddVoidPersistentListener(completeButton.onClick, gameManager.OnCompleteClicked);
        UnityEventTools.AddVoidPersistentListener(restartButton.onClick, gameManager.OnRestartClicked);
        UnityEventTools.AddVoidPersistentListener(startButton.onClick, gameManager.OnStartButtonClicked);
        UnityEventTools.AddVoidPersistentListener(easyButton.onClick, gameManager.SetDifficultyEasy);
        UnityEventTools.AddVoidPersistentListener(normalButton.onClick, gameManager.SetDifficultyNormal);
        UnityEventTools.AddVoidPersistentListener(hardButton.onClick, gameManager.SetDifficultyHard);

        roundEndPanel.SetActive(false);
        earthquakeAlertPanel.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("SceneBuilder: MainScene built and saved successfully.");
    }

    // =====================================================================
    // World
    // =====================================================================

    private static Camera CreateCamera()
    {
        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 4f;
        cam.transform.position = new Vector3(0, 2.2f, -10);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.36f, 0.58f);
        camObj.AddComponent<AudioListener>();
        camObj.AddComponent<CameraShaker>();
        return cam;
    }

    // A vertical sky gradient parented to the camera, rescaled every frame
    // by CameraBackdrop so it always fills the view no matter how far the
    // camera has zoomed out to follow the tower.
    private static void CreateSkyBackdrop(Camera cam)
    {
        Sprite sky = CreateOrLoadSkySprite();
        if (sky == null) return;

        GameObject obj = new GameObject("SkyBackdrop");
        obj.transform.SetParent(cam.transform, false);
        obj.transform.localPosition = new Vector3(0f, 0f, 20f);

        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sky;
        renderer.sortingOrder = -200;

        var backdrop = obj.AddComponent<CameraBackdrop>();
        backdrop.backdrop = renderer;
    }

    // Generates (once) a small vertical gradient PNG asset. It has to be a
    // real asset rather than a runtime Texture2D, otherwise the sprite
    // reference would not survive saving the scene.
    private static Sprite CreateOrLoadSkySprite()
    {
        const string folder = "Assets/Generated";
        const string path = folder + "/sky_gradient.png";

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "Generated");
        }

        var texture = new Texture2D(4, 256, TextureFormat.RGBA32, false);
        Color horizonWarm = new Color(0.96f, 0.86f, 0.74f);
        Color horizonBlue = new Color(0.76f, 0.87f, 0.94f);
        Color zenith = new Color(0.16f, 0.33f, 0.56f);

        for (int y = 0; y < texture.height; y++)
        {
            float t = y / (float)(texture.height - 1);
            Color c = t < 0.16f
                ? Color.Lerp(horizonWarm, horizonBlue, t / 0.16f)
                : Color.Lerp(horizonBlue, zenith, Mathf.Pow((t - 0.16f) / 0.84f, 0.85f));
            for (int x = 0; x < texture.width; x++) texture.SetPixel(x, y, c);
        }
        texture.Apply();

        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Soil + a grass edge right under the platform, so the tower reads as
    // standing on ground instead of floating in empty color.
    private static void CreateGround()
    {
        GameObject soil = new GameObject("Ground");
        soil.transform.position = new Vector3(0f, -30.15f, 1f);
        ShapeMeshFactory.Apply(soil, new Vector2(400f, 60f), new Color(0.13f, 0.16f, 0.14f), addCollider: false);
        soil.GetComponent<MeshRenderer>().sortingOrder = -120;

        GameObject grass = new GameObject("GroundEdge");
        grass.transform.position = new Vector3(0f, -0.30f, 0.9f);
        ShapeMeshFactory.Apply(grass, new Vector2(400f, 0.3f), new Color(0.27f, 0.44f, 0.30f), addCollider: false);
        grass.GetComponent<MeshRenderer>().sortingOrder = -110;
    }

    // A wide, thin, kinematic platform that the block tower is built on.
    // BlockTowerManager moves it side-to-side to simulate an earthquake.
    private static Rigidbody2D CreateBasePlatform(string name, float centerX, Color siteColor)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = new Vector3(centerX, 0, 0);
        ShapeMeshFactory.Apply(obj, new Vector2(1f, 1f), new Color(0.19f, 0.23f, 0.29f));
        // Height stays 0.3 so the top surface lands exactly where
        // BlockTowerManager.GetBaseTopY() expects it (+0.15).
        obj.transform.localScale = new Vector3(BaseHalfWidth * 2f, 0.3f, 1f);

        // A lighter cap along the top edge reads as a lit surface. It is a
        // child so it rides along with the platform while it shakes; its
        // local scale is chosen to cancel out the parent's non-uniform one.
        GameObject cap = new GameObject("Cap");
        cap.transform.SetParent(obj.transform, false);
        cap.transform.localPosition = new Vector3(0f, 0.38f, -0.01f);
        cap.transform.localScale = new Vector3(1f, 0.22f, 1f);
        ShapeMeshFactory.Apply(cap, Vector2.one, new Color(0.31f, 0.37f, 0.45f), addCollider: false);

        // Re-fit the collider to the platform's actual scaled size instead
        // of the generic 1x1 square used by ShapeMeshFactory.
        Object.DestroyImmediate(obj.GetComponent<BoxCollider2D>());
        var box = obj.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
        box.sharedMaterial = BlockTowerManager.HighFrictionMaterial;

        var rb = obj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        // A colored strip in front of the pedestal marks whose site it is.
        GameObject marker = new GameObject(name + "Marker");
        marker.transform.position = new Vector3(centerX, -0.28f, 0.5f);
        ShapeMeshFactory.Apply(marker, new Vector2(BaseHalfWidth * 2f, 0.12f), siteColor, addCollider: false);

        return rb;
    }

    // A downward-pointing arrow floating above the tower, showing exactly
    // where the next block will drop. No collider/rigidbody - it is pure
    // visual guidance and must never interact with the physics blocks.
    private static Transform CreateDropIndicator()
    {
        GameObject obj = new GameObject("DropIndicator");
        var meshFilter = obj.AddComponent<MeshFilter>();
        var meshRenderer = obj.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.sharedMaterial.color = PlayerColor;

        float w = 0.32f;
        float h = 0.42f;
        var mesh = new Mesh();
        mesh.vertices = new[]
        {
            new Vector3(-w, h, 0), new Vector3(w, h, 0), new Vector3(0, 0, 0),
            new Vector3(-w * 0.32f, h, 0), new Vector3(w * 0.32f, h, 0),
            new Vector3(w * 0.32f, h * 1.9f, 0), new Vector3(-w * 0.32f, h * 1.9f, 0)
        };
        mesh.triangles = new[] { 0, 1, 2, 3, 5, 4, 3, 6, 5 };
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;

        obj.transform.position = new Vector3(0, 7f, -0.5f);
        return obj.transform;
    }

    private static (EarthquakeSoundPlayer, FortuneChimePlayer, BGMPlayer) CreateAudioPlayers()
    {
        // Each audio player gets its own GameObject/AudioSource. Putting them
        // all on one object made every script's Awake() find and reuse the
        // SAME AudioSource (GetComponent finds whichever was added first),
        // which caused the BGM to get stepped on by one-shot sounds.
        GameObject earthquakeSoundObj = new GameObject("EarthquakeSoundPlayer");
        var earthquakeSoundPlayer = earthquakeSoundObj.AddComponent<EarthquakeSoundPlayer>();

        GameObject fortuneChimeObj = new GameObject("FortuneChimePlayer");
        var fortuneChimePlayer = fortuneChimeObj.AddComponent<FortuneChimePlayer>();

        GameObject bgmObj = new GameObject("BGMPlayer");
        var bgmPlayer = bgmObj.AddComponent<BGMPlayer>();

        return (earthquakeSoundPlayer, fortuneChimePlayer, bgmPlayer);
    }

    private static void CreateLight()
    {
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
    }

    // =====================================================================
    // UI foundations
    // =====================================================================

    private static Canvas CreateCanvas()
    {
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static GameObject CreateEventSystem()
    {
        GameObject obj = new GameObject("EventSystem");
        obj.AddComponent<EventSystem>();
        obj.AddComponent<StandaloneInputModule>();
        return obj;
    }

    private static Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    // Unity's built-in rounded box, used sliced, gives every card and button
    // soft corners without shipping any art of our own.
    private static Sprite RoundedSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static Sprite CircleSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
    }

    private static RectTransform SetupRect(GameObject obj, Transform parent, float x, float y, float w, float h)
    {
        return SetupRectAnchored(obj, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), x, y, w, h);
    }

    // Anchors the RectTransform at a specific point of its parent (e.g.
    // (0,1) = top-left, (1,1) = top-right) instead of always the center.
    // This keeps HUD elements fully on-screen even when the actual game
    // window aspect ratio doesn't match the 1280x720 reference resolution.
    private static RectTransform SetupRectAnchored(GameObject obj, Transform parent, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h)
    {
        RectTransform rt = obj.GetComponent<RectTransform>();
        if (rt == null) rt = obj.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private static RectTransform Stretch(GameObject obj, Transform parent, Vector2 offset)
    {
        RectTransform rt = obj.GetComponent<RectTransform>();
        if (rt == null) rt = obj.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offset;
        rt.offsetMax = offset;
        return rt;
    }

    private static RectTransform CreateCard(Transform parent, string name, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h)
    {
        return CreateCard(parent, name, anchor, pivot, x, y, w, h, CardBg, out _);
    }

    private static RectTransform CreateCard(Transform parent, string name, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h, out GameObject root)
    {
        return CreateCard(parent, name, anchor, pivot, x, y, w, h, CardBg, out root);
    }

    // A card = drop shadow + rounded body. Children are added to the body,
    // while `root` is what callers show/hide so the shadow travels with it.
    private static RectTransform CreateCard(Transform parent, string name, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h, Color color, out GameObject root)
    {
        root = new GameObject(name);
        RectTransform group = SetupRectAnchored(root, parent, anchor, pivot, x, y, w, h);

        GameObject shadowObj = new GameObject("Shadow");
        Stretch(shadowObj, group, new Vector2(3f, -5f));
        Image shadow = shadowObj.AddComponent<Image>();
        shadow.sprite = RoundedSprite();
        shadow.type = Image.Type.Sliced;
        shadow.color = CardShadow;
        shadow.raycastTarget = false;

        GameObject bodyObj = new GameObject("Body");
        RectTransform body = Stretch(bodyObj, group, Vector2.zero);
        Image image = bodyObj.AddComponent<Image>();
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = color;

        return body;
    }

    // Card title: a short accent rule with the title above it, so every
    // panel is labelled the same way.
    private static Text CreateCardHeader(RectTransform body, string title, Color accent)
    {
        Text label = CreateLabel(body, "Header", new Vector2(0f, 1f), new Vector2(0f, 1f), 16, -10, 240, 24, 14, title, TextPrimary, TextAnchor.UpperLeft);
        label.fontStyle = FontStyle.Bold;

        GameObject rule = new GameObject("HeaderRule");
        SetupRectAnchored(rule, body, new Vector2(0f, 1f), new Vector2(0f, 1f), 16, -32, 34, 3);
        Image ruleImage = rule.AddComponent<Image>();
        ruleImage.color = accent;
        ruleImage.raycastTarget = false;

        return label;
    }

    // A colored pill with centered text - used for the turn indicator.
    private static Image CreateBadge(Transform parent, string name, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h, Color color, out Text label)
    {
        GameObject obj = new GameObject(name);
        SetupRectAnchored(obj, parent, anchor, pivot, x, y, w, h);
        Image image = obj.AddComponent<Image>();
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = color;

        label = CreateLabel(obj.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, w - 16, h - 10, 18, "", Color.white, TextAnchor.MiddleCenter);
        label.fontStyle = FontStyle.Bold;
        return image;
    }

    private static Text CreateLabel(Transform parent, string name, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h, int fontSize, string content, Color color, TextAnchor alignment)
    {
        GameObject obj = new GameObject(name);
        SetupRectAnchored(obj, parent, anchor, pivot, x, y, w, h);
        Text text = obj.AddComponent<Text>();
        text.font = GetDefaultFont();
        // Legacy Text at small point sizes reads as crushed once the canvas
        // is scaled, so every requested size is bumped up uniformly.
        text.fontSize = Mathf.RoundToInt(fontSize * FontSizeMultiplier);
        text.text = content;
        text.color = color;
        text.alignment = alignment;
        text.lineSpacing = 1.15f;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateStyledButton(Transform parent, string name, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h, string label, Color baseColor, int fontSize = 18)
    {
        GameObject obj = new GameObject(name);
        SetupRectAnchored(obj, parent, anchor, pivot, x, y, w, h);

        Image image = obj.AddComponent<Image>();
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = Color.white;

        Button button = obj.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = baseColor * 1.25f;
        colors.pressedColor = baseColor * 0.8f;
        colors.selectedColor = baseColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Text text = CreateLabel(obj.transform, "Text (Legacy)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, w - 12, h - 8, fontSize, label, Color.white, TextAnchor.MiddleCenter);
        text.fontStyle = FontStyle.Bold;

        return button;
    }

    // =====================================================================
    // Panels
    // =====================================================================

    private static GameObject CreateAlertBanner(Transform parent, Vector2 bottomCenter, out Text alertText)
    {
        RectTransform body = CreateCard(parent, "EarthquakeAlertPanel", bottomCenter, bottomCenter, 0, 26, 620, 88, AlertRed, out GameObject root);

        GameObject iconObj = new GameObject("AlertIcon");
        SetupRectAnchored(iconObj, body, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 18, 0, 46, 46);
        Image icon = iconObj.AddComponent<Image>();
        icon.sprite = CircleSprite();
        icon.color = new Color(1f, 0.85f, 0.4f);
        CreateLabel(iconObj.transform, "Glyph", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, 46, 46, 22, "!", new Color(0.4f, 0.12f, 0.05f), TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;

        CreateLabel(body, "AlertTitle", new Vector2(0f, 1f), new Vector2(0f, 1f), 76, -12, 480, 22, 13, "地震速報", new Color(1f, 0.82f, 0.78f), TextAnchor.UpperLeft).fontStyle = FontStyle.Bold;
        alertText = CreateLabel(body, "EarthquakeAlertText", new Vector2(0f, 1f), new Vector2(0f, 1f), 76, -36, 524, 46, 14, "地震発生", Color.white, TextAnchor.UpperLeft);

        return root;
    }

    private static GameObject CreateTitleScreenPanel(Transform parent, out Button startButton, out Button easyButton, out Button normalButton, out Button hardButton, out Text difficultyText)
    {
        GameObject panel = new GameObject("TitleScreenPanel");
        SetupRect(panel, parent, 0, 0, 1280, 720);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.055f, 0.078f, 0.118f, 0.98f);

        // Two thin accent rules in the player/NPC colors frame the title.
        GameObject topRule = new GameObject("TopRule");
        SetupRectAnchored(topRule, panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 168, 420, 4);
        topRule.AddComponent<Image>().color = PlayerColor;

        GameObject bottomRule = new GameObject("BottomRule");
        SetupRectAnchored(bottomRule, panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 40, 420, 4);
        bottomRule.AddComponent<Image>().color = NpcColor;

        CreateLabel(panel.transform, "Kicker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 200, 900, 30, 16, "実際の震度データで戦う　耐震ビル建設1年勝負", AccentCyan, TextAnchor.MiddleCenter);

        Text titleText = CreateLabel(panel.transform, "TitleText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 108, 900, 100, 46, "日本地震サバイバル", TextPrimary, TextAnchor.MiddleCenter);
        titleText.fontStyle = FontStyle.Bold;

        CreateLabel(panel.transform, "SubtitleText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, -24, 900, 130, 16,
            "報酬 ＝ 高さ × 現場のリスク倍率 × 耐えた最大震度\n\n" +
            "クリックで1段積む（1日）／「揺れを待つ」で地震が来るまで日を進める。\n" +
            "揺れに耐えた建物ほど高く売れるが、崩れたら全損。ここぞで「竣工」して確定させる。\n" +
            "予報マップで揺れる県へ移動すれば報酬倍率が上がる。12月31日時点で稼いだ額の多い方が勝ち。",
            TextMuted, TextAnchor.MiddleCenter);

        difficultyText = CreateLabel(panel.transform, "DifficultyText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, -96, 900, 28, 15, "難易度：EASY", AccentCyan, TextAnchor.MiddleCenter);

        Vector2 center = new Vector2(0.5f, 0.5f);
        easyButton = CreateStyledButton(panel.transform, "EasyButton", center, center, -180, -148, 160, 52, "EASY", ButtonPrimary);
        normalButton = CreateStyledButton(panel.transform, "NormalButton", center, center, 0, -148, 160, 52, "NORMAL", ButtonNeutral);
        hardButton = CreateStyledButton(panel.transform, "HardButton", center, center, 180, -148, 160, 52, "HARD", ButtonNeutral);

        startButton = CreateStyledButton(panel.transform, "StartButton", center, center, 0, -232, 260, 66, "ゲーム開始", ButtonGreen, 22);

        return panel;
    }

    private static GameObject CreateRoundEndPanel(Transform parent, out Text titleText, out Text detailText, out Button restartButton)
    {
        GameObject panel = new GameObject("RoundEndPanel");
        SetupRect(panel, parent, 0, 0, 1280, 720);
        Image scrim = panel.AddComponent<Image>();
        scrim.color = new Color(0.03f, 0.04f, 0.06f, 0.78f);

        RectTransform body = CreateCard(panel.transform, "RoundEndCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, 560, 330, CardBgSolid, out _);

        CreateLabel(body, "RoundEndKicker", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0, -26, 480, 26, 14, "年末決算", TextMuted, TextAnchor.UpperCenter);

        titleText = CreateLabel(body, "RoundEndTitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0, -56, 480, 60, 40, "勝　利", PlayerColor, TextAnchor.UpperCenter);
        titleText.fontStyle = FontStyle.Bold;

        GameObject rule = new GameObject("Rule");
        SetupRectAnchored(rule, body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0, -126, 120, 3);
        rule.AddComponent<Image>().color = AccentCyan;

        detailText = CreateLabel(body, "RoundEndScoreText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0, -150, 480, 100, 16, "", TextPrimary, TextAnchor.UpperCenter);

        restartButton = CreateStyledButton(body, "RestartButton", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 0, 26, 260, 58, "次の年に挑む", ButtonGreen);

        return panel;
    }

    // A full-screen overlay that appears whenever the forecaster has a fresh
    // monthly forecast - meant to feel like a distinct event, not just a
    // quiet text update.
    private static GameObject CreateFortuneAnimationPanel(Transform parent, out Text forecastText, out Transform icon)
    {
        GameObject panel = new GameObject("FortuneAnimationPanel");
        SetupRect(panel, parent, 0, 0, 1280, 720);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.04f, 0.05f, 0.10f, 0.90f);
        panel.SetActive(false);

        GameObject halo = new GameObject("Halo");
        SetupRectAnchored(halo, panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 60, 210, 210);
        Image haloImage = halo.AddComponent<Image>();
        haloImage.sprite = CircleSprite();
        haloImage.color = new Color(0.31f, 0.76f, 0.97f, 0.16f);

        GameObject iconObj = new GameObject("FortuneIcon");
        RectTransform iconRect = SetupRectAnchored(iconObj, panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 60, 118, 118);
        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.sprite = RoundedSprite();
        iconImage.type = Image.Type.Sliced;
        iconImage.color = AccentCyan;
        iconRect.localRotation = Quaternion.Euler(0, 0, 45f);
        icon = iconRect;

        Text title = CreateLabel(panel.transform, "FortuneAnimationTitle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 190, 700, 50, 26, "地震予報士の予報", TextPrimary, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;

        forecastText = CreateLabel(panel.transform, "FortuneAnimationForecastText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, -70, 820, 60, 20, "", TextPrimary, TextAnchor.MiddleCenter);

        CreateLabel(panel.transform, "FortuneAnimationHint", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, -130, 820, 30, 14, "左上のマップで色が濃い県ほど、今月の工事は高く売れます", TextMuted, TextAnchor.MiddleCenter);

        return panel;
    }

    private static GameObject CreateButtonContainer(RectTransform moveCardBody)
    {
        GameObject container = new GameObject("ButtonContainer");
        SetupRectAnchored(container, moveCardBody, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0, -62, 268, 214);

        GridLayoutGroup grid = container.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(268, 34);
        grid.spacing = new Vector2(0, 6);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1; // single vertical column of reachable prefectures

        return container;
    }

    // A disabled template button that MapManager.Instantiate()s from at
    // runtime. Kept inactive so it never shows up itself; it does not need
    // to be a saved .prefab asset since Instantiate() works on any source
    // GameObject reference.
    private static Button CreatePrefectureButtonTemplate(Transform canvasParent)
    {
        Button button = CreateStyledButton(canvasParent, "PrefectureButtonTemplate", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, 0, 268, 34, "都道府県", ButtonGreen, 15);
        // MapManager tints the instantiated copies through this Image, so
        // the ColorBlock must not override it back to a flat neutral.
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.gameObject.SetActive(false);
        return button;
    }

    // =====================================================================
    // Intensity map
    // =====================================================================

    private static readonly (string label, string hex)[] IntensityLegendEntries =
    {
        ("1/2", "4CAF50"),
        ("3/4", "FFC107"),
        ("5弱/5強", "FF9800"),
        ("6弱/6強", "F44336"),
        ("7", "9C27B0"),
    };

    // A geographically real map of Japan: every prefecture is its own filled
    // polygon, colored by intensity.
    private static IntensityMapView CreateIntensityMapPanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, float x, float y, string label, Color accent)
    {
        RectTransform body = CreateCard(parent, name, anchor, pivot, x, y, 300, 252, CardBg, out GameObject root);
        CreateCardHeader(body, label, accent);

        GameObject mapAreaObj = new GameObject("MapArea");
        RectTransform mapAreaRect = SetupRectAnchored(mapAreaObj, body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0, -42, 264, 152);

        IntensityMapView view = root.AddComponent<IntensityMapView>();
        view.mapArea = mapAreaRect;
        view.seaColor = new Color(0.07f, 0.16f, 0.25f);
        view.defaultColor = new Color(0.33f, 0.39f, 0.45f);

        CreateIntensityLegend(body);

        return view;
    }

    // A color-swatch + label row along the bottom of the map card, so the
    // color coding (green/amber/orange/red/purple) is explained.
    private static void CreateIntensityLegend(RectTransform body)
    {
        GameObject legendRow = new GameObject("IntensityLegend");
        RectTransform legendRect = SetupRectAnchored(legendRow, body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 0, 10, 268, 42);

        var grid = legendRow.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(52, 40);
        grid.spacing = new Vector2(1, 0);
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;

        foreach (var (label, hex) in IntensityLegendEntries)
        {
            GameObject entry = new GameObject($"Legend_{label}");
            var entryRect = entry.AddComponent<RectTransform>();
            entryRect.SetParent(legendRect, false);

            GameObject swatch = new GameObject("Swatch");
            RectTransform swatchRect = SetupRectAnchored(swatch, entry.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0, 0, 30, 8);
            var swatchImage = swatch.AddComponent<Image>();
            swatchImage.sprite = RoundedSprite();
            swatchImage.type = Image.Type.Sliced;
            if (ColorUtility.TryParseHtmlString("#" + hex, out var color)) swatchImage.color = color;

            CreateLabel(entry.transform, "Label", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0, -12, 52, 18, 10, label, TextMuted, TextAnchor.UpperCenter);
        }
    }
}
