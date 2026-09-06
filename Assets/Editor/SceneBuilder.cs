using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using EarthquakeGame;

// Editor-only tool that builds MainScene entirely from code, so the whole
// hierarchy (Canvas, texts, buttons, managers, wiring) is generated
// consistently instead of by hand-clicking through the Editor UI.
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

        Canvas canvas = CreateCanvas();
        GameObject eventSystem = CreateEventSystem();

        Text dateText = CreateText(canvas.transform, "DateText", -450, 330, 400, 40, 24, "日付：2000年1月1日");
        Text survivalDaysText = CreateText(canvas.transform, "SurvivalDaysText", -450, 290, 400, 40, 24, "生存日数：0日");
        Text currentPrefectureText = CreateText(canvas.transform, "CurrentPrefectureText", -450, 250, 400, 40, 24, "現在地：東京都");
        Text nextMoveText = CreateText(canvas.transform, "NextMoveText", -450, 210, 400, 40, 24, "次回移動可能：あと10日");
        Text latestEarthquakeText = CreateText(canvas.transform, "LatestEarthquakeText", 250, 150, 480, 260, 20, "最新の地震：なし");

        Button advanceDayButton = CreateButton(canvas.transform, "AdvanceDayButton", -450, -280, 200, 60, "次の日へ");

        GameObject gameOverPanel = CreateGameOverPanel(canvas.transform, out Button restartButton);

        GameObject buttonContainer = CreateButtonContainer(canvas.transform);
        Button prefectureButtonTemplate = CreatePrefectureButtonTemplate(canvas.transform);

        GameObject gameManagerObj = new GameObject("GameManager");
        GameObject playerManagerObj = new GameObject("PlayerManager");
        GameObject earthquakeManagerObj = new GameObject("EarthquakeManager");
        GameObject mapManagerObj = new GameObject("MapManager");

        var gameManager = gameManagerObj.AddComponent<GameManager>();
        var playerManager = playerManagerObj.AddComponent<PlayerManager>();
        var earthquakeManager = earthquakeManagerObj.AddComponent<EarthquakeManager>();
        var mapManager = mapManagerObj.AddComponent<MapManager>();

        gameManager.playerManager = playerManager;
        gameManager.earthquakeManager = earthquakeManager;
        gameManager.mapManager = mapManager;
        gameManager.dateText = dateText;
        gameManager.survivalDaysText = survivalDaysText;
        gameManager.currentPrefectureText = currentPrefectureText;
        gameManager.nextMoveText = nextMoveText;
        gameManager.latestEarthquakeText = latestEarthquakeText;
        gameManager.gameOverPanel = gameOverPanel;
        gameManager.advanceDayButton = advanceDayButton;

        mapManager.prefectureButtonPrefab = prefectureButtonTemplate;
        mapManager.buttonContainer = buttonContainer.transform;

        UnityEventTools.AddVoidPersistentListener(advanceDayButton.onClick, gameManager.OnAdvanceDayClicked);
        UnityEventTools.AddVoidPersistentListener(restartButton.onClick, gameManager.OnRestartClicked);

        gameOverPanel.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("SceneBuilder: MainScene built and saved successfully.");
    }

    private static void CreateCamera()
    {
        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        Camera cam = camObj.AddComponent<Camera>();
        cam.transform.position = new Vector3(0, 1, -10);
        camObj.AddComponent<AudioListener>();
    }

    private static void CreateLight()
    {
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
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

        Text text = CreateText(obj.transform, "Text (Legacy)", 0, 0, w, h, 24, label);
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;

        return button;
    }

    private static GameObject CreateGameOverPanel(Transform parent, out Button restartButton)
    {
        GameObject panel = new GameObject("GameOverPanel");
        SetupRect(panel, parent, 0, 0, 500, 300);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.78f);

        Text gameOverText = CreateText(panel.transform, "GameOverText", 0, 60, 460, 100, 36, "GAME OVER");
        gameOverText.alignment = TextAnchor.MiddleCenter;
        gameOverText.color = Color.white;

        restartButton = CreateButton(panel.transform, "RestartButton", 0, -60, 200, 60, "リスタート");

        return panel;
    }

    private static GameObject CreateButtonContainer(Transform parent)
    {
        GameObject container = new GameObject("ButtonContainer");
        SetupRect(container, parent, 250, -80, 480, 380);

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
