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

        CreateCamera();
        CreateLight();
        Rigidbody2D basePlatform = CreateBasePlatform();

        Canvas canvas = CreateCanvas();
        CreateEventSystem();

        Text dateText = CreateText(canvas.transform, "DateText", -450, 330, 400, 40, 22, "日付：2000年1月1日");
        Text survivalDaysText = CreateText(canvas.transform, "SurvivalDaysText", -450, 295, 400, 40, 22, "経過日数：0/360日");
        Text currentPrefectureText = CreateText(canvas.transform, "CurrentPrefectureText", -450, 260, 400, 40, 22, "現在地：東京都");
        Text nextMoveText = CreateText(canvas.transform, "NextMoveText", -450, 225, 400, 40, 22, "次回移動可能：あと10日");
        Text scoreText = CreateText(canvas.transform, "ScoreText", -450, 190, 400, 40, 22, "現在のスコア：0点（積み木0個）");
        Text latestEarthquakeText = CreateText(canvas.transform, "LatestEarthquakeText", -450, 100, 400, 140, 18, "最新の地震：なし");
        Text fortuneText = CreateText(canvas.transform, "FortuneText", -450, -10, 400, 90, 16, "占い師：…");

        GameObject buttonContainer = CreateButtonContainer(canvas.transform);
        Button prefectureButtonTemplate = CreatePrefectureButtonTemplate(canvas.transform);

        // --- Block placement controls (replaces the old "next day" button) ---
        Button squareButton = CreateButton(canvas.transform, "SquareButton", 100, -260, 120, 50, "四角");
        Button triangleButton = CreateButton(canvas.transform, "TriangleButton", 230, -260, 120, 50, "三角");
        Button circleButton = CreateButton(canvas.transform, "CircleButton", 360, -260, 120, 50, "丸");

        Slider placementSlider = CreateSlider(canvas.transform, "PlacementSlider", 230, -320, 380, 30, -4f, 4f, 0f);
        Text placementPositionText = CreateText(canvas.transform, "PlacementPositionText", 230, -285, 380, 30, 18, "配置位置：0.0");
        placementPositionText.alignment = TextAnchor.MiddleCenter;
        Button placeButton = CreateButton(canvas.transform, "PlaceButton", 230, -370, 200, 50, "積む（次の日へ）");
        Transform dropIndicator = CreateDropIndicator();

        GameObject roundEndPanel = CreateRoundEndPanel(canvas.transform, out Text roundEndScoreText, out Button restartButton);

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
        gameManager.placementSlider = placementSlider;
        gameManager.placementPositionText = placementPositionText;
        gameManager.dropIndicator = dropIndicator;

        mapManager.prefectureButtonPrefab = prefectureButtonTemplate;
        mapManager.buttonContainer = buttonContainer.transform;

        fortuneTeller.earthquakeManager = earthquakeManager;
        fortuneTeller.playerManager = playerManager;

        UnityEventTools.AddVoidPersistentListener(squareButton.onClick, gameManager.SelectSquare);
        UnityEventTools.AddVoidPersistentListener(triangleButton.onClick, gameManager.SelectTriangle);
        UnityEventTools.AddVoidPersistentListener(circleButton.onClick, gameManager.SelectCircle);
        UnityEventTools.AddVoidPersistentListener(placeButton.onClick, gameManager.OnPlaceBlockClicked);
        UnityEventTools.AddVoidPersistentListener(restartButton.onClick, gameManager.OnRestartClicked);

        roundEndPanel.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("SceneBuilder: MainScene built and saved successfully.");
    }

    private static void CreateCamera()
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
        RectTransform rt = obj.GetComponent<RectTransform>();
        if (rt == null) rt = obj.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private static Text CreateText(Transform parent, string name, float x, float y, float w, float h, int fontSize, string content)
    {
        GameObject obj = new GameObject(name);
        SetupRect(obj, parent, x, y, w, h);
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

    // Full-stretch RectTransform (anchors 0..1) inset by the given margins,
    // used for slider sub-parts so the Fill bar actually resizes with value
    // instead of staying a fixed size like a center-anchored Rect would.
    private static RectTransform SetupStretchRect(GameObject obj, Transform parent, float insetLeft, float insetRight, float insetTop, float insetBottom)
    {
        RectTransform rt = obj.GetComponent<RectTransform>();
        if (rt == null) rt = obj.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(insetLeft, insetBottom);
        rt.offsetMax = new Vector2(-insetRight, -insetTop);
        return rt;
    }

    // A slider redesigned to make "where the block will land" obvious:
    // a thick, high-contrast track with tick marks at each whole unit and a
    // bold circular handle, plus (wired up separately in BuildMainScene) a
    // world-space drop marker and a live numeric readout above it.
    private static Slider CreateSlider(Transform parent, string name, float x, float y, float w, float h, float min, float max, float defaultValue)
    {
        GameObject sliderObj = new GameObject(name);
        SetupRect(sliderObj, parent, x, y, w, h);
        Slider slider = sliderObj.AddComponent<Slider>();

        GameObject background = new GameObject("Background");
        SetupStretchRect(background, sliderObj.transform, 0, 0, 0, 0);
        var bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0.25f, 0.28f, 0.32f);

        // Tick marks at every integer position so the player can see how the
        // slider range maps onto discrete-looking spots along the base.
        int tickCount = Mathf.RoundToInt(max - min) + 1;
        for (int i = 0; i < tickCount; i++)
        {
            float t = tickCount > 1 ? (float)i / (tickCount - 1) : 0.5f;
            GameObject tick = new GameObject("Tick");
            RectTransform tickRect = tick.AddComponent<RectTransform>();
            tickRect.SetParent(background.transform, false);
            tickRect.anchorMin = new Vector2(t, 0);
            tickRect.anchorMax = new Vector2(t, 1);
            tickRect.pivot = new Vector2(0.5f, 0.5f);
            tickRect.sizeDelta = new Vector2(2, 0);
            tickRect.anchoredPosition = Vector2.zero;
            var tickImage = tick.AddComponent<Image>();
            tickImage.color = new Color(1f, 1f, 1f, 0.35f);
        }

        GameObject fillArea = new GameObject("Fill Area");
        SetupStretchRect(fillArea, sliderObj.transform, 10, 10, 0, 0);

        GameObject fill = new GameObject("Fill");
        var fillRect = SetupStretchRect(fill, fillArea.transform, 0, 0, 0, 0);
        var fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.95f, 0.55f, 0.2f);

        GameObject handleArea = new GameObject("Handle Slide Area");
        SetupStretchRect(handleArea, sliderObj.transform, 10, 10, 0, 0);

        GameObject handle = new GameObject("Handle");
        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.SetParent(handleArea.transform, false);
        handleRect.sizeDelta = new Vector2(28, h + 10);
        var handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.95f, 0.95f, 0.95f);

        slider.targetGraphic = handleImage;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;
        slider.value = defaultValue;

        return slider;
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
        SetupRect(container, parent, 250, 150, 480, 380);

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
