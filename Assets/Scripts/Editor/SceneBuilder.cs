using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/";
    private const string ArtPath = "Assets/Art/Sprites/";
    private const string PrefabPath = "Assets/Resources/Prefabs/";
    private const string PatternPath = "Assets/Resources/Patterns/";

    [MenuItem("Tools/별자리잇기/Build All Scenes")]
    public static void BuildAll()
    {
        EnsureFolders();
        CreateDefaultPatterns();
        CreateStarNodePrefab();

        BuildTitleScene();
        BuildDifficultySelectScene();
        BuildCalibrationScene();
        BuildGameplayScene();
        BuildTherapistMonitorScene();

        UpdateBuildSettings();

        Debug.Log("[SceneBuilder] 모든 씬 빌드 완료.");
        EditorUtility.DisplayDialog("완료", "5개 씬이 생성되었습니다.\n빌드 설정도 업데이트되었습니다.", "확인");
    }

    // ─────────────────────────────────────────────
    // Folder setup
    // ─────────────────────────────────────────────
    static void EnsureFolders()
    {
        string[] paths = {
            ScenePath, ArtPath, PrefabPath, PatternPath,
            "Assets/Art/Sprites/Stars", "Assets/Art/Sprites/UI"
        };
        foreach (var path in paths)
        {
            var parts = path.TrimEnd('/').Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }

    // ─────────────────────────────────────────────
    // Sprite helpers
    // ─────────────────────────────────────────────
    static Sprite CreateAndSaveCircleSprite(string fileName, int radius, Color color)
    {
        string assetPath = ArtPath + fileName + ".png";
        int size = radius * 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - radius + 0.5f;
                float dy = y - radius + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - Mathf.Max(0f, dist - radius + 2f));
                pixels[y * size + x] = new Color(color.r, color.g, color.b, alpha);
            }
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(assetPath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = size;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    static Sprite CreateAndSaveArrowSprite()
    {
        string assetPath = ArtPath + "UI/arrow.png";
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Right-pointing triangle arrow
                float cx = x - size * 0.1f;
                float cy = y - size * 0.5f;
                bool inArrow = cx >= 0 && Mathf.Abs(cy) <= (size * 0.5f - cx * 0.5f);
                pixels[y * size + x] = inArrow ? Color.yellow : Color.clear;
            }
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(assetPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = size;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    // ─────────────────────────────────────────────
    // Default Constellation Patterns
    // ─────────────────────────────────────────────
    static void CreateDefaultPatterns()
    {
        CreatePattern("Pattern_Beginner_Cross", DifficultyTier.Beginner, ThemeType.Shape, new[]
        {
            new Vector2(-0.3f,  0.4f),
            new Vector2( 0.3f,  0.4f),
            new Vector2( 0.3f, -0.4f),
            new Vector2(-0.3f, -0.4f),
        });

        CreatePattern("Pattern_Basic_Zigzag", DifficultyTier.Basic, ThemeType.Shape, new[]
        {
            new Vector2(-0.6f,  0.5f),
            new Vector2(-0.1f, -0.3f),
            new Vector2( 0.4f,  0.5f),
            new Vector2( 0.7f,  0.0f),
            new Vector2( 0.2f, -0.5f),
            new Vector2(-0.5f, -0.1f),
        });

        CreatePattern("Pattern_Advanced_Star", DifficultyTier.Advanced, ThemeType.RealConstellation, new[]
        {
            new Vector2( 0.0f,  0.8f),
            new Vector2(-0.5f,  0.2f),
            new Vector2(-0.8f, -0.5f),
            new Vector2( 0.0f, -0.1f),
            new Vector2( 0.8f, -0.5f),
            new Vector2( 0.5f,  0.2f),
            new Vector2( 0.3f,  0.6f),
            new Vector2(-0.3f,  0.6f),
        });
    }

    static void CreatePattern(string fileName, DifficultyTier tier, ThemeType theme, Vector2[] positions)
    {
        string path = PatternPath + fileName + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<ConstellationPattern>(path);
        if (existing != null) return;

        var pattern = ScriptableObject.CreateInstance<ConstellationPattern>();
        pattern.patternName = fileName;
        pattern.tier = tier;
        pattern.theme = theme;
        pattern.starPositions = new List<Vector2>(positions);
        AssetDatabase.CreateAsset(pattern, path);
    }

    // ─────────────────────────────────────────────
    // StarNode Prefab
    // ─────────────────────────────────────────────
    static void CreateStarNodePrefab()
    {
        string path = PrefabPath + "StarNode.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        Sprite starSprite = CreateAndSaveCircleSprite("Stars/star", 48, Color.white);

        var go = new GameObject("StarNode");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = starSprite;
        sr.color = new Color(0.4f, 0.4f, 0.6f);
        go.transform.localScale = Vector3.one * 0.35f;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;

        go.AddComponent<StarNode>();

        // Glow child
        var glow = new GameObject("Glow");
        glow.transform.SetParent(go.transform, false);
        var glowSr = glow.AddComponent<SpriteRenderer>();
        glowSr.sprite = starSprite;
        glowSr.color = new Color(1f, 1f, 0.5f, 0f);
        glow.transform.localScale = Vector3.one * 1.8f;
        glowSr.sortingOrder = -1;

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    // ─────────────────────────────────────────────
    // Canvas helper
    // ─────────────────────────────────────────────
    static GameObject CreateCanvas(string name, Camera cam)
    {
        var canvasGO = new GameObject(name);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        return canvasGO;
    }

    static GameObject CreateEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
        return go;
    }

    static Camera CreateCamera(Color bgColor, bool orthographic = true)
    {
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.backgroundColor = bgColor;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.orthographic = orthographic;
        cam.orthographicSize = 5f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        camGO.AddComponent<AudioListener>();
        return cam;
    }

    static (GameObject go, RectTransform rt) CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return (go, rt);
    }

    static TMP_Text CreateText(Transform parent, string name, string text, float fontSize,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return tmp;
    }

    static Button CreateButton(Transform parent, string name, string label, float fontSize,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
        Color? bgColor = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = bgColor ?? new Color(0.2f, 0.4f, 0.8f);
        var btn = go.AddComponent<Button>();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        return btn;
    }

    // ─────────────────────────────────────────────
    // Title Scene
    // ─────────────────────────────────────────────
    static void BuildTitleScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = CreateCamera(new Color(0.02f, 0.02f, 0.08f));
        var canvas = CreateCanvas("Canvas", cam);
        CreateEventSystem();

        // Singletons
        var sysRoot = new GameObject("SystemRoot");
        sysRoot.AddComponent<GameManager>();
        sysRoot.AddComponent<DataExporter>();
        sysRoot.AddComponent<AuditLogger>();
        sysRoot.AddComponent<ROMCalibrator>();

        // Title Text
        CreateText(canvas.transform, "TitleText", "별자리 잇기\nConstellation Trace", 72,
            new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.85f), Vector2.zero, Vector2.zero);

        CreateText(canvas.transform, "SubText", "재활 훈련 게임", 32,
            new Vector2(0.2f, 0.46f), new Vector2(0.8f, 0.56f), Vector2.zero, Vector2.zero);

        var startBtn = CreateButton(canvas.transform, "StartButton", "시작하기", 40,
            new Vector2(0.35f, 0.28f), new Vector2(0.65f, 0.42f), Vector2.zero, Vector2.zero);

        var therapistBtn = CreateButton(canvas.transform, "TherapistButton", "치료사 모드", 30,
            new Vector2(0.35f, 0.12f), new Vector2(0.65f, 0.24f), Vector2.zero, Vector2.zero,
            new Color(0.3f, 0.3f, 0.5f));

        var titleCtrl = new GameObject("TitleController").AddComponent<TitleController>();
        var so = new SerializedObject(titleCtrl);
        so.FindProperty("startButton").objectReferenceValue = startBtn;
        so.FindProperty("therapistButton").objectReferenceValue = therapistBtn;
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, ScenePath + "Title.unity");
    }

    // ─────────────────────────────────────────────
    // DifficultySelect Scene
    // ─────────────────────────────────────────────
    static void BuildDifficultySelectScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = CreateCamera(new Color(0.02f, 0.02f, 0.08f));
        var canvas = CreateCanvas("Canvas", cam);
        CreateEventSystem();

        var sysRoot = new GameObject("SystemRoot");
        sysRoot.AddComponent<GameManager>();
        sysRoot.AddComponent<DataExporter>();
        sysRoot.AddComponent<AuditLogger>();

        CreateText(canvas.transform, "TitleText", "난이도 선택", 52,
            new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.95f), Vector2.zero, Vector2.zero);

        CreateText(canvas.transform, "DifficultyLabel", "난이도", 30,
            new Vector2(0.05f, 0.66f), new Vector2(0.35f, 0.74f), Vector2.zero, Vector2.zero,
            TextAlignmentOptions.Left);

        var beginnerBtn = CreateButton(canvas.transform, "BeginnerButton", "1단계 (입문)", 28,
            new Vector2(0.05f, 0.53f), new Vector2(0.32f, 0.65f), Vector2.zero, Vector2.zero,
            new Color(0.2f, 0.6f, 0.3f));
        var basicBtn = CreateButton(canvas.transform, "BasicButton", "2단계 (기본)", 28,
            new Vector2(0.36f, 0.53f), new Vector2(0.63f, 0.65f), Vector2.zero, Vector2.zero,
            new Color(0.2f, 0.4f, 0.7f));
        var advancedBtn = CreateButton(canvas.transform, "AdvancedButton", "3단계 (심화)", 28,
            new Vector2(0.67f, 0.53f), new Vector2(0.94f, 0.65f), Vector2.zero, Vector2.zero,
            new Color(0.7f, 0.2f, 0.2f));

        CreateText(canvas.transform, "ModeLabel", "게임 모드", 30,
            new Vector2(0.05f, 0.40f), new Vector2(0.35f, 0.48f), Vector2.zero, Vector2.zero,
            TextAlignmentOptions.Left);

        var basicModeBtn = CreateButton(canvas.transform, "BasicModeButton", "기본", 26,
            new Vector2(0.05f, 0.28f), new Vector2(0.30f, 0.39f), Vector2.zero, Vector2.zero,
            new Color(0.25f, 0.45f, 0.75f));
        var mirrorModeBtn = CreateButton(canvas.transform, "MirrorModeButton", "거울 모드", 26,
            new Vector2(0.34f, 0.28f), new Vector2(0.62f, 0.39f), Vector2.zero, Vector2.zero,
            new Color(0.55f, 0.25f, 0.65f));
        var timerModeBtn = CreateButton(canvas.transform, "TimerModeButton", "타이머", 26,
            new Vector2(0.66f, 0.28f), new Vector2(0.94f, 0.39f), Vector2.zero, Vector2.zero,
            new Color(0.75f, 0.4f, 0.1f));

        var infoText = CreateText(canvas.transform, "InfoText", "선택: 1단계(입문) / 기본 모드", 26,
            new Vector2(0.1f, 0.16f), new Vector2(0.9f, 0.24f), Vector2.zero, Vector2.zero);

        var startBtn = CreateButton(canvas.transform, "StartButton", "게임 시작", 38,
            new Vector2(0.3f, 0.04f), new Vector2(0.7f, 0.14f), Vector2.zero, Vector2.zero,
            new Color(0.8f, 0.5f, 0.1f));

        var ctrl = new GameObject("DifficultySelectController").AddComponent<DifficultySelectController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("beginnerButton").objectReferenceValue = beginnerBtn;
        so.FindProperty("basicButton").objectReferenceValue = basicBtn;
        so.FindProperty("advancedButton").objectReferenceValue = advancedBtn;
        so.FindProperty("basicModeButton").objectReferenceValue = basicModeBtn;
        so.FindProperty("mirrorModeButton").objectReferenceValue = mirrorModeBtn;
        so.FindProperty("timerModeButton").objectReferenceValue = timerModeBtn;
        so.FindProperty("startButton").objectReferenceValue = startBtn;
        so.FindProperty("selectedInfoText").objectReferenceValue = infoText;
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, ScenePath + "DifficultySelect.unity");
    }

    // ─────────────────────────────────────────────
    // Calibration Scene
    // ─────────────────────────────────────────────
    static void BuildCalibrationScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = CreateCamera(new Color(0.02f, 0.02f, 0.08f));
        var canvas = CreateCanvas("Canvas", cam);
        CreateEventSystem();

        var sysRoot = new GameObject("SystemRoot");
        sysRoot.AddComponent<GameManager>();
        sysRoot.AddComponent<DataExporter>();
        sysRoot.AddComponent<AuditLogger>();

        var calibratorGO = new GameObject("ROMCalibrator");
        var calibrator = calibratorGO.AddComponent<ROMCalibrator>();

        // VirtualCursor (캘리브레이션 씬에서도 필요)
        var cursorGO = new GameObject("VirtualCursor");
        cursorGO.AddComponent<VirtualCursor>();
        var dirAnalyzerGO = new GameObject("DirectionAnalyzer");
        dirAnalyzerGO.AddComponent<DirectionAnalyzer>();

        CreateText(canvas.transform, "TitleText", "ROM 캘리브레이션", 52,
            new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.95f), Vector2.zero, Vector2.zero);

        var instrText = CreateText(canvas.transform, "InstructionText",
            "마우스를 최대한 넓게 움직여보세요.\n[측정 시작] 버튼을 누른 뒤 상하좌우로 크게 움직이고\n[측정 완료] 버튼을 누르세요.", 28,
            new Vector2(0.1f, 0.52f), new Vector2(0.9f, 0.78f), Vector2.zero, Vector2.zero);

        var reachText = CreateText(canvas.transform, "ReachValueText", "", 24,
            new Vector2(0.2f, 0.42f), new Vector2(0.8f, 0.52f), Vector2.zero, Vector2.zero);

        var measureBtn = CreateButton(canvas.transform, "MeasureButton", "측정 시작", 34,
            new Vector2(0.25f, 0.27f), new Vector2(0.5f, 0.40f), Vector2.zero, Vector2.zero,
            new Color(0.2f, 0.6f, 0.3f));

        var completeBtn = CreateButton(canvas.transform, "CompleteButton", "측정 완료", 34,
            new Vector2(0.52f, 0.27f), new Vector2(0.77f, 0.40f), Vector2.zero, Vector2.zero,
            new Color(0.2f, 0.4f, 0.8f));

        var skipBtn = CreateButton(canvas.transform, "SkipButton", "건너뛰기", 26,
            new Vector2(0.35f, 0.10f), new Vector2(0.65f, 0.22f), Vector2.zero, Vector2.zero,
            new Color(0.3f, 0.3f, 0.3f));

        var ctrl = new GameObject("CalibrationController").AddComponent<CalibrationController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("measureButton").objectReferenceValue = measureBtn;
        so.FindProperty("completeButton").objectReferenceValue = completeBtn;
        so.FindProperty("skipButton").objectReferenceValue = skipBtn;
        so.FindProperty("instructionText").objectReferenceValue = instrText;
        so.FindProperty("reachValueText").objectReferenceValue = reachText;
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, ScenePath + "Calibration.unity");
    }

    // ─────────────────────────────────────────────
    // Gameplay Scene
    // ─────────────────────────────────────────────
    static void BuildGameplayScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = CreateCamera(new Color(0.01f, 0.01f, 0.06f));
        cam.gameObject.transform.position = new Vector3(0, 0, -10);
        CreateEventSystem();

        // Systems
        var sysRoot = new GameObject("SystemRoot");
        sysRoot.AddComponent<GameManager>();
        sysRoot.AddComponent<DataLogger>();
        sysRoot.AddComponent<DataExporter>();
        sysRoot.AddComponent<AuditLogger>();
        sysRoot.AddComponent<DirectionAnalyzer>();
        sysRoot.AddComponent<ROMCalibrator>();

        // Constellation Controller
        var constellGO = new GameObject("ConstellationController");
        var constellCtrl = constellGO.AddComponent<ConstellationController>();
        var lineRenderer = constellGO.AddComponent<LineRenderer>();
        var connRenderer = constellGO.AddComponent<ConnectionLineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = new Color(0.6f, 0.8f, 1f, 0.9f);
        lineRenderer.endColor = new Color(0.6f, 0.8f, 1f, 0.9f);
        lineRenderer.sortingOrder = 1;

        // ROM Tracker
        var trackerGO = new GameObject("ROMUsageTracker");
        var romTracker = trackerGO.AddComponent<ROMUsageTracker>();

        // Canvas
        var canvas = CreateCanvas("Canvas", cam.GetComponent<Camera>());
        var canvasRT = canvas.GetComponent<RectTransform>();

        // TopBar
        var (topBar, _) = CreatePanel(canvas.transform, "TopBar",
            new Vector2(0, 0.92f), new Vector2(1, 1f), Vector2.zero, Vector2.zero,
            new Color(0, 0, 0, 0.6f));
        var progressText = CreateText(topBar.transform, "ProgressText", "진행도 0/0", 28,
            new Vector2(0, 0), new Vector2(0.4f, 1f), Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineLeft);
        progressText.margin = new Vector4(16, 0, 0, 0);
        var elapsedText = CreateText(topBar.transform, "ElapsedTimeText", "경과 00:00", 28,
            new Vector2(0.6f, 0), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineRight);
        elapsedText.margin = new Vector4(0, 0, 16, 0);

        // PlayArea (fills most of screen)
        var (playAreaGO, playAreaRT) = CreatePanel(canvas.transform, "PlayArea",
            new Vector2(0, 0.08f), new Vector2(1, 0.92f), Vector2.zero, Vector2.zero,
            new Color(0, 0, 0, 0));

        // Stars parent (world space, not canvas)
        var starsParent = new GameObject("StarsParent");

        // VirtualCursor
        var cursorVisualGO = new GameObject("CursorVisual");
        cursorVisualGO.transform.SetParent(canvas.transform, false);
        var cursorImg = cursorVisualGO.AddComponent<Image>();
        cursorImg.color = new Color(1f, 1f, 1f, 0.9f);
        var cursorSprite = CreateAndSaveCircleSprite("UI/cursor", 16, Color.white);
        cursorImg.sprite = cursorSprite;
        var cursorRT = cursorVisualGO.GetComponent<RectTransform>();
        cursorRT.sizeDelta = new Vector2(20, 20);
        cursorRT.anchorMin = Vector2.zero;
        cursorRT.anchorMax = Vector2.zero;
        cursorRT.pivot = new Vector2(0.5f, 0.5f);
        cursorRT.anchoredPosition = new Vector2(960, 540);

        var cursorGO = new GameObject("VirtualCursor");
        var virtualCursor = cursorGO.AddComponent<VirtualCursor>();
        var vcso = new SerializedObject(virtualCursor);
        vcso.FindProperty("cursorVisual").objectReferenceValue = cursorRT;
        vcso.FindProperty("playAreaBounds").objectReferenceValue = playAreaRT;
        vcso.ApplyModifiedProperties();

        // BottomBar
        var (bottomBar, _) = CreatePanel(canvas.transform, "BottomBar",
            new Vector2(0, 0), new Vector2(1, 0.08f), Vector2.zero, Vector2.zero,
            new Color(0, 0, 0, 0.6f));
        var romUsageText = CreateText(bottomBar.transform, "ROMUsageText", "가동 영역 사용 비율: 0%", 26,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);

        // Range Warning Arrow
        var arrowSprite = CreateAndSaveArrowSprite();
        var arrowGO = new GameObject("RangeWarningArrow");
        arrowGO.transform.SetParent(canvas.transform, false);
        var arrowImg = arrowGO.AddComponent<Image>();
        arrowImg.sprite = arrowSprite;
        var arrowRT = arrowGO.GetComponent<RectTransform>();
        arrowRT.sizeDelta = new Vector2(80, 60);
        arrowRT.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRT.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRT.anchoredPosition = Vector2.zero;
        arrowGO.AddComponent<RangeWarningArrow>();
        arrowGO.SetActive(false);

        // ResultPanel
        var (resultPanel, _) = CreatePanel(canvas.transform, "ResultPanel",
            new Vector2(0.2f, 0.15f), new Vector2(0.8f, 0.85f), Vector2.zero, Vector2.zero,
            new Color(0.05f, 0.05f, 0.15f, 0.95f));

        CreateText(resultPanel.transform, "ResultTitle", "결과", 48,
            new Vector2(0, 0.8f), new Vector2(1, 1f), Vector2.zero, Vector2.zero);
        var distText = CreateText(resultPanel.transform, "TotalDistanceText", "", 26,
            new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.74f), Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
        var romResultText = CreateText(resultPanel.transform, "ROMUsageText", "", 26,
            new Vector2(0.05f, 0.50f), new Vector2(0.95f, 0.62f), Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
        var avgTimeText = CreateText(resultPanel.transform, "AvgTimeText", "", 26,
            new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.50f), Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
        var deltaText = CreateText(resultPanel.transform, "DeltaText", "", 26,
            new Vector2(0.05f, 0.26f), new Vector2(0.95f, 0.38f), Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);

        var retryBtn = CreateButton(resultPanel.transform, "RetryButton", "다시 하기", 28,
            new Vector2(0.05f, 0.04f), new Vector2(0.45f, 0.20f), Vector2.zero, Vector2.zero,
            new Color(0.2f, 0.6f, 0.3f));
        var menuBtn = CreateButton(resultPanel.transform, "MenuButton", "난이도 선택", 28,
            new Vector2(0.55f, 0.04f), new Vector2(0.95f, 0.20f), Vector2.zero, Vector2.zero,
            new Color(0.3f, 0.3f, 0.5f));

        var resultScreen = resultPanel.AddComponent<ResultScreen>();
        var rso = new SerializedObject(resultScreen);
        rso.FindProperty("panel").objectReferenceValue = resultPanel;
        rso.FindProperty("totalDistanceText").objectReferenceValue = distText;
        rso.FindProperty("romUsageText").objectReferenceValue = romResultText;
        rso.FindProperty("avgTimeBetweenStarsText").objectReferenceValue = avgTimeText;
        rso.FindProperty("deltaFromLastSessionText").objectReferenceValue = deltaText;
        rso.FindProperty("retryButton").objectReferenceValue = retryBtn;
        rso.FindProperty("menuButton").objectReferenceValue = menuBtn;
        rso.ApplyModifiedProperties();
        resultPanel.SetActive(false);

        // HUDController
        var hudGO = new GameObject("HUDController");
        var hudCtrl = hudGO.AddComponent<HUDController>();
        var hso = new SerializedObject(hudCtrl);
        hso.FindProperty("progressText").objectReferenceValue = progressText;
        hso.FindProperty("elapsedTimeText").objectReferenceValue = elapsedText;
        hso.FindProperty("romUsageText").objectReferenceValue = romUsageText;
        hso.ApplyModifiedProperties();

        // ROMUsageTracker SerializedObject
        var rtso = new SerializedObject(romTracker);
        rtso.FindProperty("playAreaBounds").objectReferenceValue = playAreaRT;
        rtso.ApplyModifiedProperties();

        // GameplaySceneController
        var starPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "StarNode.prefab");
        var gameplaySC = new GameObject("GameplaySceneController").AddComponent<GameplaySceneController>();
        var gso = new SerializedObject(gameplaySC);
        gso.FindProperty("starNodePrefab").objectReferenceValue = starPrefab;
        gso.FindProperty("starParent").objectReferenceValue = starsParent.transform;
        gso.FindProperty("constellationController").objectReferenceValue = constellCtrl;
        gso.FindProperty("hudController").objectReferenceValue = hudCtrl;
        gso.FindProperty("resultScreen").objectReferenceValue = resultScreen;
        gso.FindProperty("playAreaRect").objectReferenceValue = playAreaRT;
        gso.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, ScenePath + "Gameplay.unity");
    }

    // ─────────────────────────────────────────────
    // TherapistMonitor Scene
    // ─────────────────────────────────────────────
    static void BuildTherapistMonitorScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = CreateCamera(new Color(0.02f, 0.02f, 0.1f));
        var canvas = CreateCanvas("Canvas", cam);
        CreateEventSystem();

        var sysRoot = new GameObject("SystemRoot");
        sysRoot.AddComponent<GameManager>();
        sysRoot.AddComponent<DataExporter>();
        sysRoot.AddComponent<AuditLogger>();

        CreateText(canvas.transform, "TitleText", "치료사 모니터링", 48,
            new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero);

        // Patient ID input area
        var (inputPanel, _) = CreatePanel(canvas.transform, "InputPanel",
            new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.87f), Vector2.zero, Vector2.zero,
            new Color(0.1f, 0.1f, 0.2f));

        CreateText(inputPanel.transform, "InputLabel", "환자 ID:", 26,
            new Vector2(0, 0), new Vector2(0.15f, 1), Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineLeft);

        // TMP InputField - use Image+AddComponent approach for RectTransform
        var inputGO = new GameObject("PatientIdInput");
        inputGO.transform.SetParent(inputPanel.transform, false);
        var inputImg = inputGO.AddComponent<Image>(); // Image adds RectTransform automatically
        inputImg.color = new Color(0.2f, 0.2f, 0.35f);
        var inputField = inputGO.AddComponent<TMP_InputField>();
        var inputRT = (RectTransform)inputGO.transform;
        inputRT.anchorMin = new Vector2(0.15f, 0.1f);
        inputRT.anchorMax = new Vector2(0.7f, 0.9f);
        inputRT.offsetMin = Vector2.zero;
        inputRT.offsetMax = Vector2.zero;

        // Text Area needs RectTransform - add Image first to ensure it
        var textAreaGO = new GameObject("Text Area");
        textAreaGO.transform.SetParent(inputGO.transform, false);
        textAreaGO.AddComponent<Image>().color = Color.clear; // ensures RectTransform is created
        var taRT = (RectTransform)textAreaGO.transform;
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(4, 2); taRT.offsetMax = new Vector2(-4, -2);
        textAreaGO.AddComponent<RectMask2D>();

        var inputTextGO = new GameObject("Text");
        inputTextGO.transform.SetParent(textAreaGO.transform, false);
        var inputText = inputTextGO.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 24;
        inputText.color = Color.white;
        var itRT = (RectTransform)inputTextGO.transform;
        itRT.anchorMin = Vector2.zero; itRT.anchorMax = Vector2.one;
        itRT.offsetMin = Vector2.zero; itRT.offsetMax = Vector2.zero;

        inputField.textComponent = inputText;
        inputField.textViewport = taRT;

        var loadBtn = CreateButton(inputPanel.transform, "LoadButton", "조회", 26,
            new Vector2(0.72f, 0.1f), new Vector2(0.98f, 0.9f), Vector2.zero, Vector2.zero,
            new Color(0.2f, 0.4f, 0.8f));

        // Dashboard panel
        var (dashPanel, _) = CreatePanel(canvas.transform, "DashboardPanel",
            new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.77f), Vector2.zero, Vector2.zero,
            new Color(0.05f, 0.05f, 0.15f, 0.9f));

        var trendText = CreateText(dashPanel.transform, "TrendSummaryText", "추세: —", 30,
            new Vector2(0.03f, 0.72f), new Vector2(0.97f, 0.87f), Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
        var improveText = CreateText(dashPanel.transform, "ImprovementText", "기준선 대비: —", 26,
            new Vector2(0.03f, 0.55f), new Vector2(0.97f, 0.70f), Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
        var balanceText = CreateText(dashPanel.transform, "BalanceScoreText", "방향 균형: —", 26,
            new Vector2(0.03f, 0.38f), new Vector2(0.97f, 0.53f), Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);

        var deniedText = CreateText(canvas.transform, "AccessDeniedText", "접근 권한이 없습니다.", 36,
            new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.6f), Vector2.zero, Vector2.zero);
        deniedText.color = Color.red;
        deniedText.gameObject.SetActive(false);

        var backBtn = CreateButton(canvas.transform, "BackButton", "← 뒤로", 26,
            new Vector2(0.02f, 0.01f), new Vector2(0.18f, 0.08f), Vector2.zero, Vector2.zero,
            new Color(0.3f, 0.3f, 0.5f));

        // TherapistDashboard component
        var dashboard = new GameObject("TherapistDashboard").AddComponent<TherapistDashboard>();
        var dso = new SerializedObject(dashboard);
        dso.FindProperty("trendSummaryText").objectReferenceValue = trendText;
        dso.FindProperty("improvementText").objectReferenceValue = improveText;
        dso.FindProperty("balanceScoreText").objectReferenceValue = balanceText;
        dso.FindProperty("accessDeniedText").objectReferenceValue = deniedText;
        dso.FindProperty("dashboardPanel").objectReferenceValue = dashPanel;
        dso.ApplyModifiedProperties();

        // Load button listener (wired at runtime via component)
        var therapistLoader = new GameObject("TherapistSceneHelper").AddComponent<TherapistSceneHelper>();
        var thlso = new SerializedObject(therapistLoader);
        thlso.FindProperty("patientIdField").objectReferenceValue = inputField;
        thlso.FindProperty("loadButton").objectReferenceValue = loadBtn;
        thlso.FindProperty("backButton").objectReferenceValue = backBtn;
        thlso.FindProperty("dashboard").objectReferenceValue = dashboard;
        thlso.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, ScenePath + "TherapistMonitor.unity");
    }

    // ─────────────────────────────────────────────
    // Build Settings
    // ─────────────────────────────────────────────
    static void UpdateBuildSettings()
    {
        string[] sceneNames = { "Title", "DifficultySelect", "Calibration", "Gameplay", "TherapistMonitor" };
        var scenes = new EditorBuildSettingsScene[sceneNames.Length];
        for (int i = 0; i < sceneNames.Length; i++)
            scenes[i] = new EditorBuildSettingsScene(ScenePath + sceneNames[i] + ".unity", true);
        EditorBuildSettings.scenes = scenes;
    }
}
