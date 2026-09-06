using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using EarthquakeGame;

// Editor-only tool that builds MainScene entirely from code, so the whole
// hierarchy (Canvas, texts, buttons, managers, wiring, block tower) is
// generated consistently instead of by hand-clicking through the Editor UI.
// Run via the menu: Earthquake Game > Build Main Scene
public static class SceneBuilder
{
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
        CreateLight();
        Rigidbody2D basePlatform = CreateBasePlatform();

        Canvas canvas = CreateCanvas();
        CreateEventSystem();

        Vector2 topLeft = new Vector2(0f, 1f);
        Text dateText = CreateTextAnchored(canvas.transform, "DateText", topLeft, topLeft, 20, -20, 380, 40, 22, "日付：2000年1月1日");
        Text survivalDaysText = CreateTextAnchored(canvas.transform, "SurvivalDaysText", topLeft, topLeft, 20, -55, 380, 40, 22, "経過日数：0/360日");
        Text currentPrefectureText = CreateTextAnchored(canvas.transform, "CurrentPrefectureText", topLeft, topLeft, 20, -90, 380, 40, 22, "現在地：東京都");
        Text nextMoveText = CreateTextAnchored(canvas.transform, "NextMoveText", topLeft, topLeft, 20, -125, 380, 40, 22, "次回移動可能：あと10日");
        Text scoreText = CreateTextAnchored(canvas.transform, "ScoreText", topLeft, topLeft, 20, -160, 380, 40, 22, "現在のスコア：0点（積み木0個）");
        Text latestEarthquakeText = CreateTextAnchored(canvas.transform, "LatestEarthquakeText", topLeft, topLeft, 20, -200, 380, 140, 18, "最新の地震：なし");
        Text fortuneText = CreateTextAnchored(canvas.transform, "FortuneText", topLeft, topLeft, 20, -350, 380, 90, 16, "占い師：…");

        GameObject buttonContainer = CreateButtonContainer(canvas.transform);
        Button prefectureButtonTemplate = CreatePrefectureButtonTemplate(canvas.transform);

        // --- Block placement: shape is random, player only rotates + aims ---
        Text nextShapeInfoText = CreateText(canvas.transform, "NextShapeInfoText", 230, -260, 380, 30, 18, "次の形：□（3点）");
        nextShapeInfoText.alignment = TextAnchor.MiddleCenter;
        Button rotateLeftButton = CreateButton(canvas.transform, "RotateLeftButton", 140, -300, 90, 50, "⟲");
        Button rotateRightButton = CreateButton(canvas.transform, "RotateRightButton", 320, -300, 90, 50, "⟳");
        Text placementHintText = CreateText(canvas.transform, "PlacementHintText", 230, -230, 380, 30, 16, "回転(Q/E)して、土台をクリックすると積めます");
        placementHintText.alignment = TextAnchor.MiddleCenter;
        Transform dropIndicator = CreateDropIndicator();
        GameObject shapePreview = new GameObject("ShapePreview");
        shapePreview.transform.position = new Vector3(0, 7f, -0.5f);

        GameObject roundEndPanel = CreateRoundEndPanel(canvas.transform, out Text roundEndScoreText, out Button restartButton);

        Text earthquakeAlertText = CreateText(canvas.transform, "EarthquakeAlertText", 0, 320, 600, 60, 32, "地震発生！");
        earthquakeAlertText.alignment = TextAnchor.MiddleCenter;
        earthquakeAlertText.color = new Color(0.85f, 0.1f, 0.1f);
        earthquakeAlertText.fontStyle = FontStyle.Bold;
        earthquakeAlertText.gameObject.SetActive(false);

        IntensityMapView intensityMapView = CreateIntensityMapPanel(canvas.transform);

        GameObject fortuneAnimationPanel = CreateFortuneAnimationPanel(canvas.transform, out Text fortuneAnimationText, out Transform fortuneAnimationIcon);

        GameObject titleScreenPanel = CreateTitleScreenPanel(canvas.transform, out Button startButton);

        // --- Managers ---
        GameObject gameManagerObj = new GameObject("GameManager");
        GameObject playerManagerObj = new GameObject("PlayerManager");
        GameObject earthquakeManagerObj = new GameObject("EarthquakeManager");
        GameObject mapManagerObj = new GameObject("MapManager");
        GameObject fortuneTellerObj = new GameObject("FortuneTeller");
        GameObject blockTowerManagerObj = new GameObject("BlockTowerManager");

        var gameManager = gameManagerObj.AddComponent<GameManager>();
        var playerManager = playerManagerObj.AddComponent<PlayerManager>();
        var earthquakeManager = earthquakeManagerObj.AddComponent<EarthquakeManager>();
        var mapManager = mapManagerObj.AddComponent<MapManager>();
        var fortuneTeller = fortuneTellerObj.AddComponent<FortuneTeller>();
        var blockTowerManager = blockTowerManagerObj.AddComponent<BlockTowerManager>();

        blockTowerManager.baseRigidbody = basePlatform;
        blockTowerManager.baseHalfWidth = 4f;

        gameManager.playerManager = playerManager;
        gameManager.earthquakeManager = earthquakeManager;
        gameManager.mapManager = mapManager;
        gameManager.fortuneTeller = fortuneTeller;
        gameManager.blockTowerManager = blockTowerManager;
        gameManager.dateText = dateText;
        gameManager.survivalDaysText = survivalDaysText;
        gameManager.currentPrefectureText = currentPrefectureText;
        gameManager.nextMoveText = nextMoveText;
        gameManager.latestEarthquakeText = latestEarthquakeText;
        gameManager.fortuneText = fortuneText;
        gameManager.scoreText = scoreText;
        gameManager.roundEndPanel = roundEndPanel;
        gameManager.roundEndScoreText = roundEndScoreText;
        gameManager.dropIndicator = dropIndicator;
        gameManager.shapePreview = shapePreview;
        gameManager.cameraShaker = mainCamera.GetComponent<CameraShaker>();
        gameManager.earthquakeSoundPlayer = mainCamera.GetComponent<EarthquakeSoundPlayer>();
        gameManager.fortuneChimePlayer = mainCamera.GetComponent<FortuneChimePlayer>();
        gameManager.earthquakeAlertText = earthquakeAlertText;
        gameManager.intensityMapView = intensityMapView;
        gameManager.titleScreenPanel = titleScreenPanel;
        gameManager.nextShapeInfoText = nextShapeInfoText;
        gameManager.fortuneAnimationPanel = fortuneAnimationPanel;
        gameManager.fortuneAnimationText = fortuneAnimationText;
        gameManager.fortuneAnimationIcon = fortuneAnimationIcon;

        mapManager.prefectureButtonPrefab = prefectureButtonTemplate;
        mapManager.buttonContainer = buttonContainer.transform;

        fortuneTeller.earthquakeManager = earthquakeManager;

        UnityEventTools.AddVoidPersistentListener(rotateLeftButton.onClick, gameManager.RotateLeft);
        UnityEventTools.AddVoidPersistentListener(rotateRightButton.onClick, gameManager.RotateRight);
        UnityEventTools.AddVoidPersistentListener(restartButton.onClick, gameManager.OnRestartClicked);
        UnityEventTools.AddVoidPersistentListener(startButton.onClick, gameManager.OnStartButtonClicked);

        roundEndPanel.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("SceneBuilder: MainScene built and saved successfully.");
    }

    private static Camera CreateCamera()
    {
        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.transform.position = new Vector3(0, 3, -10);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.75f, 0.85f, 0.95f);
        camObj.AddComponent<AudioListener>();
        camObj.AddComponent<CameraShaker>();
        camObj.AddComponent<EarthquakeSoundPlayer>();
        camObj.AddComponent<FortuneChimePlayer>();
        return cam;
    }

    private static void CreateLight()
    {
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
    }

    // A wide, thin, kinematic platform that the block tower is built on.
    // BlockTowerManager moves it side-to-side to simulate an earthquake.
    private static Rigidbody2D CreateBasePlatform()
    {
        GameObject obj = new GameObject("BasePlatform");
        obj.transform.position = new Vector3(0, 0, 0);
        ShapeMeshFactory.Apply(obj, BlockShape.Square, 1f, new Color(0.5f, 0.4f, 0.3f));
        obj.transform.localScale = new Vector3(8f, 0.3f, 1f);

        // Re-fit the collider to the platform's actual scaled size instead
        // of the generic 1x1 square used by ShapeMeshFactory.
        Object.DestroyImmediate(obj.GetComponent<BoxCollider2D>());
        var box = obj.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;

        var rb = obj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        return rb;
    }

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

    private static RectTransform SetupRect(GameObject obj, Transform parent, float x, float y, float w, float h)
    {
        return SetupRectAnchored(obj, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), x, y, w, h);
    }

    // Anchors the RectTransform at a specific point of its parent (e.g.
    // (0,1) = top-left, (1,1) = top-right) instead of always the center.
    // This keeps HUD elements fully on-screen even when the actual game
    // window aspect ratio doesn't match the 1280x720 reference resolution -
    // a center-anchored element far from center can otherwise be pushed
    // past the visible edge.
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

    private static Text CreateText(Transform parent, string name, float x, float y, float w, float h, int fontSize, string content)
    {
        return CreateTextAnchored(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), x, y, w, h, fontSize, content);
    }

    private static Text CreateTextAnchored(Transform parent, string name, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h, int fontSize, string content)
    {
        GameObject obj = new GameObject(name);
        SetupRectAnchored(obj, parent, anchor, pivot, x, y, w, h);
        Text text = obj.AddComponent<Text>();
        text.font = GetDefaultFont();
        text.fontSize = fontSize;
        text.text = content;
        text.color = Color.black;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, float x, float y, float w, float h, string label)
    {
        GameObject obj = new GameObject(name);
        SetupRect(obj, parent, x, y, w, h);
        Image image = obj.AddComponent<Image>();
        image.color = Color.white;
        Button button = obj.AddComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(obj.transform, "Text (Legacy)", 0, 0, w, h, 22, label);
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;

        return button;
    }

    // A full-screen title panel shown on top of everything at launch. The
    // game underneath is already initialized (Start() runs StartNewGame()
    // as usual); this panel just blocks input until the player presses
    // start, then hides itself.
    private static GameObject CreateTitleScreenPanel(Transform parent, out Button startButton)
    {
        GameObject panel = new GameObject("TitleScreenPanel");
        SetupRect(panel, parent, 0, 0, 1280, 720);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.1f, 0.16f, 0.97f);

        Text titleText = CreateText(panel.transform, "TitleText", 0, 80, 900, 140, 48, "日本地震サバイバル\n積み木タワー");
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.fontStyle = FontStyle.Bold;

        Text subtitleText = CreateText(panel.transform, "SubtitleText", 0, -20, 900, 100, 20,
            "都道府県を移動しながら、四角・三角・丸の積み木を積み上げよう。\n" +
            "地震が来ると土台が揺れ、積み木が崩れることがある。\n" +
            "1年生き延びた時点で、残った積み木の数と形からスコアが決まる。");
        subtitleText.alignment = TextAnchor.MiddleCenter;
        subtitleText.color = new Color(0.85f, 0.85f, 0.9f);

        startButton = CreateButton(panel.transform, "StartButton", 0, -150, 240, 70, "スタート");

        return panel;
    }

    private static GameObject CreateRoundEndPanel(Transform parent, out Text scoreText, out Button restartButton)
    {
        GameObject panel = new GameObject("RoundEndPanel");
        SetupRect(panel, parent, 0, 0, 500, 320);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.82f);

        Text titleText = CreateText(panel.transform, "RoundEndTitleText", 0, 100, 460, 60, 32, "今年の記録");
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;

        scoreText = CreateText(panel.transform, "RoundEndScoreText", 0, 10, 460, 130, 22, "");
        scoreText.alignment = TextAnchor.MiddleCenter;
        scoreText.color = Color.white;

        restartButton = CreateButton(panel.transform, "RestartButton", 0, -110, 200, 60, "リスタート");

        return panel;
    }

    private static GameObject CreateButtonContainer(Transform parent)
    {
        GameObject container = new GameObject("ButtonContainer");
        SetupRectAnchored(container, parent, new Vector2(1f, 1f), new Vector2(1f, 1f), -20, -20, 460, 380);

        GridLayoutGroup grid = container.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(110, 36);
        grid.spacing = new Vector2(6, 6);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        return container;
    }

    // A small downward-pointing arrow floating above the tower, showing
    // exactly where the next block will drop. No collider/rigidbody - it's
    // pure visual guidance and must never interact with the physics blocks.
    private static Transform CreateDropIndicator()
    {
        GameObject obj = new GameObject("DropIndicator");
        var meshFilter = obj.AddComponent<MeshFilter>();
        var meshRenderer = obj.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.sharedMaterial.color = new Color(0.9f, 0.15f, 0.15f);

        float w = 0.35f;
        float h = 0.45f;
        var mesh = new Mesh();
        mesh.vertices = new[]
        {
            new Vector3(-w, h, 0), new Vector3(w, h, 0), new Vector3(0, 0, 0)
        };
        mesh.triangles = new[] { 0, 1, 2 };
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;

        obj.transform.position = new Vector3(0, 7f, -0.5f);
        return obj.transform;
    }

    // A schematic "intensity map": a fixed-size panel with one small marker
    // per prefecture, positioned by projecting real lat/lon (no map artwork
    // needed - the dots alone trace roughly the shape of Japan). Markers
    // light up by intensity via IntensityMapView.SetIntensities().
    private static IntensityMapView CreateIntensityMapPanel(Transform parent)
    {
        GameObject panel = new GameObject("IntensityMapPanel");
        RectTransform panelRect = SetupRectAnchored(panel, parent, new Vector2(1f, 1f), new Vector2(1f, 1f), -20, -420, 300, 300);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.93f, 0.93f, 0.95f);

        Text label = CreateTextAnchored(panel.transform, "IntensityMapLabel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0, -4, 280, 24, 14, "震度マップ");
        label.alignment = TextAnchor.UpperCenter;
        label.color = Color.black;

        GameObject mapAreaObj = new GameObject("MapArea");
        RectTransform mapAreaRect = SetupRectAnchored(mapAreaObj, panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0, -14, 280, 264);

        IntensityMapView view = panel.AddComponent<IntensityMapView>();
        view.mapArea = mapAreaRect;
        return view;
    }

    // A full-screen overlay that appears whenever the fortune teller has a
    // fresh forecast, with a spinning/pulsing icon and a chime - meant to
    // feel like a distinct "event", not just a quiet text update.
    private static GameObject CreateFortuneAnimationPanel(Transform parent, out Text forecastText, out Transform icon)
    {
        GameObject panel = new GameObject("FortuneAnimationPanel");
        SetupRect(panel, parent, 0, 0, 1280, 720);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.02f, 0.12f, 0.88f);
        panel.SetActive(false);

        GameObject iconObj = new GameObject("FortuneIcon");
        var iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.SetParent(panel.transform, false);
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0, 90);
        iconRect.sizeDelta = new Vector2(120, 120);
        var iconImage = iconObj.AddComponent<Image>();
        iconImage.color = new Color(0.85f, 0.7f, 0.95f);
        // A simple diamond so the spin/pulse animation is visible without needing a sprite asset.
        iconRect.localRotation = Quaternion.Euler(0, 0, 45f);
        icon = iconRect;

        Text title = CreateText(panel.transform, "FortuneAnimationTitle", 0, 200, 700, 50, 28, "占い師のお告げ");
        title.alignment = TextAnchor.MiddleCenter;
        title.color = new Color(0.9f, 0.8f, 1f);
        title.fontStyle = FontStyle.Bold;

        forecastText = CreateText(panel.transform, "FortuneAnimationForecastText", 0, -60, 800, 100, 24, "");
        forecastText.alignment = TextAnchor.MiddleCenter;
        forecastText.color = Color.white;

        return panel;
    }

    // A disabled template button that MapManager.Instantiate()s from at
    // runtime. Kept inactive so it never shows up itself; it does not need
    // to be a saved .prefab asset since Instantiate() works on any source
    // GameObject reference.
    private static Button CreatePrefectureButtonTemplate(Transform canvasParent)
    {
        Button button = CreateButton(canvasParent, "PrefectureButtonTemplate", 0, 0, 110, 36, "都道府県");
        button.gameObject.SetActive(false);
        return button;
    }
}
