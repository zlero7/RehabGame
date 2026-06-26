using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools/별자리잇기/1. Setup Fonts  → TMP SDF 에셋 생성 (Thaleah + MalgunGothic 폴백)
/// Tools/별자리잇기/2. Apply UI Style → 5개 씬에 폰트·스프라이트 일괄 적용
/// </summary>
public static class FontAndStyleSetup
{
    // ── 경로 상수 ────────────────────────────────────────
    private const string ThaleahTTF  = "Assets/Thaleah_PixelFont/Materials/ThaleahFat_TTF.ttf";
    private const string MalgunTTF   = "Assets/Fonts/MalgunGothicBold.ttf";
    private const string ThaleahSDF  = "Assets/Fonts/ThaleahFat SDF.asset";
    private const string MalgunSDF   = "Assets/Fonts/MalgunGothicBold SDF.asset";

    // GUI Kit 스프라이트 경로 (medium 크기 기준)
    private const string BtnBlue     = "Assets/Space_Exploration_GUI_Kit/Button_Images/Source_Image_Sprites/medium/large-blue-medium.png";
    private const string BtnPurple   = "Assets/Space_Exploration_GUI_Kit/Button_Images/Source_Image_Sprites/medium/large-purple-medium.png";
    private const string BtnYellow   = "Assets/Space_Exploration_GUI_Kit/Button_Images/Source_Image_Sprites/medium/large-yellow-medium.png";
    private const string BtnShortGray = "Assets/Space_Exploration_GUI_Kit/Button_Images/Source_Image_Sprites/medium/short-purple-medium.png";
    private const string PanelBg     = "Assets/Space_Exploration_GUI_Kit/Background_Images/large/background-overlay-large.png";
    private const string HomeBg      = "Assets/Space_Exploration_GUI_Kit/Background_Images/large/home-background-large.png";

    private const string Galmuri11Asset = "Assets/Galmuri-v2.40.3/Galmuri11.asset";

    private static readonly string[] SceneNames =
        { "Title", "DifficultySelect", "Calibration", "Gameplay", "TherapistMonitor" };

    // ─────────────────────────────────────────────────────
    // STEP 1: 폰트 에셋 생성
    // ─────────────────────────────────────────────────────
    [MenuItem("Tools/별자리잇기/1. Setup Fonts")]
    public static void SetupFonts()
    {
        AssetDatabase.Refresh();

        TMP_FontAsset thaleahFont = CreateDynamicSDF(ThaleahTTF, ThaleahSDF);
        TMP_FontAsset malgunFont  = CreateDynamicSDF(MalgunTTF,  MalgunSDF);

        if (thaleahFont != null && malgunFont != null)
        {
            // Thaleah → MalgunGothic 폴백 연결 (한글 글리프는 Malgun에서 공급)
            if (thaleahFont.fallbackFontAssetTable == null)
                thaleahFont.fallbackFontAssetTable = new List<TMP_FontAsset>();

            if (!thaleahFont.fallbackFontAssetTable.Contains(malgunFont))
            {
                thaleahFont.fallbackFontAssetTable.Add(malgunFont);
                EditorUtility.SetDirty(thaleahFont);
                AssetDatabase.SaveAssets();
            }
        }

        // 스프라이트 9-슬라이스 설정
        SetupButtonSprites();

        AssetDatabase.Refresh();
        Debug.Log("[FontSetup] 완료: ThaleahFat SDF + MalgunGothicBold SDF (폴백 연결됨)");
        EditorUtility.DisplayDialog("완료", "폰트 에셋 생성 및 스프라이트 설정 완료.", "확인");
    }

    static TMP_FontAsset CreateDynamicSDF(string ttfPath, string savePath)
    {
        // 이미 존재하면 재사용
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(savePath);
        if (existing != null) return existing;

        var fontObj = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (fontObj == null)
        {
            Debug.LogError($"[FontSetup] TTF를 찾을 수 없음: {ttfPath}");
            return null;
        }

        // Dynamic Atlas: 런타임에 필요한 글리프를 즉시 추가 → 한글 자동 지원
        // Unity 2022 TMP 3.x: 기본 오버로드로 생성 후 Dynamic 모드 설정
        var sdf = TMP_FontAsset.CreateFontAsset(fontObj);
        sdf.atlasPopulationMode = TMPro.AtlasPopulationMode.Dynamic;

        sdf.name = Path.GetFileNameWithoutExtension(savePath);
        AssetDatabase.CreateAsset(sdf, savePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FontSetup] 생성: {savePath}");
        return sdf;
    }

    // 버튼 스프라이트들을 9-슬라이스로 설정
    static void SetupButtonSprites()
    {
        string[] sprites = { BtnBlue, BtnPurple, BtnYellow, BtnShortGray, PanelBg, HomeBg };
        foreach (var path in sprites)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = new Vector2(0.5f, 0.5f);

            // 9-슬라이스 border (left, bottom, right, top) — 버튼 양끝 둥근 모서리 보존
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteBorder = path.Contains("background") || path.Contains("overlay")
                ? new Vector4(20, 20, 20, 20)
                : new Vector4(40, 14, 40, 14);
            importer.SetTextureSettings(settings);

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }

    // ─────────────────────────────────────────────────────
    // STEP 2: 5개 씬에 폰트·스프라이트 일괄 적용
    // ─────────────────────────────────────────────────────
    [MenuItem("Tools/별자리잇기/2. Apply UI Style")]
    public static void ApplyUIStyle()
    {
        var thaleahFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ThaleahSDF);
        if (thaleahFont == null)
        {
            EditorUtility.DisplayDialog("오류", "먼저 '1. Setup Fonts'를 실행하세요.", "확인");
            return;
        }

        var btnBlueSprite    = LoadSprite(BtnBlue);
        var btnPurpleSprite  = LoadSprite(BtnPurple);
        var btnYellowSprite  = LoadSprite(BtnYellow);
        var btnGraySprite    = LoadSprite(BtnShortGray);
        var panelSprite      = LoadSprite(PanelBg);
        var homeBgSprite     = LoadSprite(HomeBg);

        int updated = 0;
        foreach (var sceneName in SceneNames)
        {
            string path = $"Assets/Scenes/{sceneName}.unity";
            if (!File.Exists(path)) { Debug.LogWarning($"씬 없음: {path}"); continue; }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            // ── 폰트 적용 ──
            foreach (var tmp in Object.FindObjectsOfType<TextMeshProUGUI>(true))
            {
                tmp.font = thaleahFont;

                // 타이틀 텍스트는 더 크게
                if (tmp.name.Contains("Title") || tmp.name.Contains("TitleText"))
                    tmp.enableAutoSizing = false;

                EditorUtility.SetDirty(tmp);
            }

            // ── 버튼 스프라이트 적용 ──
            foreach (var btn in Object.FindObjectsOfType<Button>(true))
            {
                var img = btn.GetComponent<Image>();
                if (img == null) continue;

                // 버튼 이름으로 색상 구분
                string n = btn.gameObject.name.ToLower();
                Sprite sprite;
                Color  tint = Color.white;

                if (n.Contains("start") || n.Contains("complete") || n.Contains("load"))
                {
                    sprite = btnBlueSprite;
                }
                else if (n.Contains("beginner") || n.Contains("basic") && !n.Contains("mode")
                         || n.Contains("advanced") || n.Contains("measure"))
                {
                    sprite = btnPurpleSprite;
                }
                else if (n.Contains("retry") || n.Contains("mirror") || n.Contains("timer"))
                {
                    sprite = btnYellowSprite;
                }
                else
                {
                    sprite = btnGraySprite;
                }

                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                }
                EditorUtility.SetDirty(img);
            }

            // ── 패널·배경 스프라이트 적용 ──
            foreach (var img in Object.FindObjectsOfType<Image>(true))
            {
                if (img.GetComponent<Button>() != null) continue; // 버튼은 위에서 처리

                string n = img.gameObject.name.ToLower();

                if ((n.Contains("panel") || n.Contains("result") || n.Contains("dashboard"))
                    && panelSprite != null)
                {
                    img.sprite = panelSprite;
                    img.type = Image.Type.Sliced;
                    img.color = new Color(1f, 1f, 1f, 0.92f);
                    EditorUtility.SetDirty(img);
                }
                else if ((n.Contains("topbar") || n.Contains("bottombar") || n.Contains("inputpanel"))
                         && panelSprite != null)
                {
                    img.sprite = panelSprite;
                    img.type = Image.Type.Sliced;
                    img.color = new Color(0.6f, 0.6f, 0.9f, 0.7f);
                    EditorUtility.SetDirty(img);
                }
            }

            // ── Title·Home 씬 배경 이미지 적용 ──
            if ((sceneName == "Title" || sceneName == "DifficultySelect") && homeBgSprite != null)
            {
                ApplyHomeBg(homeBgSprite);
            }

            EditorSceneManager.SaveScene(scene);
            updated++;
            Debug.Log($"[UIStyle] {sceneName} 적용 완료");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[UIStyle] {updated}개 씬 스타일 적용 완료.");
        EditorUtility.DisplayDialog("완료", $"{updated}개 씬에 폰트·스프라이트 적용 완료.", "확인");
    }

    static void ApplyHomeBg(Sprite bgSprite)
    {
        // Canvas 아래에 배경 이미지 오브젝트 추가 (없으면 생성)
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var existing = canvas.transform.Find("HomeBg");
        GameObject bgGO;
        if (existing != null)
        {
            bgGO = existing.gameObject;
        }
        else
        {
            bgGO = new GameObject("HomeBg");
            bgGO.transform.SetParent(canvas.transform, false);
            bgGO.transform.SetAsFirstSibling(); // 가장 뒤로
        }

        var img = bgGO.GetComponent<Image>() ?? bgGO.AddComponent<Image>();
        img.sprite = bgSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(1f, 1f, 1f, 0.15f); // 은은하게

        var rt = bgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        EditorUtility.SetDirty(bgGO);
    }

    // ─────────────────────────────────────────────────────
    // STEP 3: SubText에 적용된 폰트를 모든 씬 텍스트에 일괄 적용
    // ─────────────────────────────────────────────────────
    [MenuItem("Tools/별자리잇기/3. Apply Font From SubText")]
    public static void ApplyFontFromSubText()
    {
        // 1. 어느 씬에서든 "SubText" 오브젝트의 폰트를 읽어온다
        TMP_FontAsset targetFont = null;
        string foundIn = null;

        foreach (var sceneName in SceneNames)
        {
            string path = $"Assets/Scenes/{sceneName}.unity";
            if (!File.Exists(path)) continue;

            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            foreach (var tmp in Object.FindObjectsOfType<TextMeshProUGUI>(true))
            {
                if (tmp.gameObject.name == "SubText" && tmp.font != null)
                {
                    targetFont = tmp.font;
                    foundIn = sceneName;
                    break;
                }
            }
            if (targetFont != null) break;
        }

        if (targetFont == null)
        {
            EditorUtility.DisplayDialog("오류", "'SubText' 오브젝트 또는 폰트를 찾을 수 없습니다.", "확인");
            return;
        }

        Debug.Log($"[FontCopy] '{targetFont.name}' 폰트를 {foundIn}의 SubText에서 발견. 전체 적용 시작.");

        // 2. 모든 씬에 동일 폰트 적용
        int updated = 0;
        foreach (var sceneName in SceneNames)
        {
            string path = $"Assets/Scenes/{sceneName}.unity";
            if (!File.Exists(path)) continue;

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            foreach (var tmp in Object.FindObjectsOfType<TextMeshProUGUI>(true))
            {
                tmp.font = targetFont;
                EditorUtility.SetDirty(tmp);
            }

            EditorSceneManager.SaveScene(scene);
            updated++;
            Debug.Log($"[FontCopy] {sceneName} 적용 완료");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[FontCopy] {updated}개 씬 폰트 교체 완료: {targetFont.name}");
        EditorUtility.DisplayDialog("완료", $"'{targetFont.name}' 폰트를 {updated}개 씬 전체에 적용 완료.", "확인");
    }

    static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) Debug.LogWarning($"[UIStyle] 스프라이트 없음: {path}");
        return sprite;
    }
}
