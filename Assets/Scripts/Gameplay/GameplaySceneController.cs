using System.Collections.Generic;
using UnityEngine;

public class GameplaySceneController : MonoBehaviour
{
    [SerializeField] private GameObject starNodePrefab;
    [SerializeField] private Transform starParent;
    [SerializeField] private ConstellationController constellationController;
    [SerializeField] private HUDController hudController;
    [SerializeField] private ResultScreen resultScreen;
    [SerializeField] private RectTransform playAreaRect;

    [Header("Fallback if no calibration")]
    [SerializeField] private float defaultPlayAreaHalfX = 5.5f;
    [SerializeField] private float defaultPlayAreaHalfY = 3.5f;

    private List<StarNode> spawnedNodes = new List<StarNode>();

    void Start()
    {
        // 컴포넌트 없으면 씬에서 자동 탐색
        if (constellationController == null)
            constellationController = FindObjectOfType<ConstellationController>();
        if (hudController == null)
            hudController = FindObjectOfType<HUDController>();
        if (resultScreen == null)
            resultScreen = FindObjectOfType<ResultScreen>();
        if (VirtualCursor.Instance != null && playAreaRect != null)
            VirtualCursor.Instance.SetPlayAreaBounds(playAreaRect);

        var pattern = SelectPattern();
        if (pattern == null)
        {
            Debug.LogError("[GameplaySceneController] 패턴을 찾을 수 없습니다.");
            return;
        }

        SpawnStars(pattern);
        StartGame(pattern);
    }

    private ConstellationPattern SelectPattern()
    {
        DifficultyTier tier = GameManager.Instance != null
            ? GameManager.Instance.SelectedTier
            : DifficultyTier.Beginner;

        var patterns = Resources.LoadAll<ConstellationPattern>("Patterns");
        foreach (var p in patterns)
            if (p.tier == tier) return p;

        // 없으면 아무거나
        return patterns.Length > 0 ? patterns[0] : null;
    }

    private void SpawnStars(ConstellationPattern pattern)
    {
        foreach (var node in spawnedNodes)
            if (node != null) Destroy(node.gameObject);
        spawnedNodes.Clear();

        float halfX = defaultPlayAreaHalfX;
        float halfY = defaultPlayAreaHalfY;
        float coverageRatio = 0.6f;

        if (GameManager.Instance != null)
        {
            coverageRatio = GameManager.Instance.SelectedTier switch
            {
                DifficultyTier.Beginner => 0.35f,
                DifficultyTier.Basic => 0.6f,
                DifficultyTier.Advanced => 0.9f,
                _ => 0.6f
            };
        }

        if (ROMCalibrator.Instance != null && ROMCalibrator.Instance.IsCalibrated)
        {
            halfX = ROMCalibrator.Instance.patientMaxReachX * coverageRatio;
            halfY = ROMCalibrator.Instance.patientMaxReachY * coverageRatio;
            ROMCalibrator.Instance.ApplyToVirtualCursor(coverageRatio);
        }

        GameObject prefabToUse = starNodePrefab != null
            ? starNodePrefab
            : Resources.Load<GameObject>("Prefabs/StarNode");

        for (int i = 0; i < pattern.starPositions.Count; i++)
        {
            Vector2 norm = pattern.starPositions[i];
            Vector3 worldPos = new Vector3(norm.x * halfX, norm.y * halfY, 0f);

            GameObject go = prefabToUse != null
                ? Instantiate(prefabToUse, worldPos, Quaternion.identity, starParent)
                : CreateFallbackStar(worldPos);

            go.name = $"Star_{i}";
            var node = go.GetComponent<StarNode>();
            if (node == null) node = go.AddComponent<StarNode>();
            spawnedNodes.Add(node);
        }
    }

    private void StartGame(ConstellationPattern pattern)
    {
        if (constellationController == null) return;

        DifficultyTier tier = GameManager.Instance?.SelectedTier ?? DifficultyTier.Beginner;
        IGameMode mode = GameManager.Instance?.CreateCurrentMode() ?? new BasicMode();
        GameModeType modeType = GameManager.Instance?.SelectedMode ?? GameModeType.Basic;

        float judgeRadius = tier switch
        {
            DifficultyTier.Beginner => 0.8f,
            DifficultyTier.Basic => 0.6f,
            DifficultyTier.Advanced => 0.45f,
            _ => 0.6f
        };

        constellationController.SetJudgeRadius(judgeRadius);
        constellationController.SetNodes(spawnedNodes);
        constellationController.StartConstellation(mode, tier, modeType);

        hudController?.UpdateProgress(0, spawnedNodes.Count);
        hudController?.StartSession();
    }

    public void ShowResult(SessionResult result)
    {
        hudController?.StopSession();
        string patientId = SessionContext.Current?.UserId ?? "unknown";
        SessionResult prev = DataExporter.Instance?.GetPreviousSession(patientId);
        resultScreen?.Display(result, prev);
    }

    private GameObject CreateFallbackStar(Vector3 worldPos)
    {
        var go = new GameObject("Star");
        go.transform.position = worldPos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite(32, Color.white);
        sr.color = new Color(0.4f, 0.4f, 0.6f);
        go.transform.localScale = Vector3.one * 0.35f;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;

        return go;
    }

    private Sprite CreateCircleSprite(int radius, Color color)
    {
        int size = radius * 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - radius + 0.5f;
                float dy = y - radius + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - Mathf.Max(0f, dist - radius + 1.5f));
                pixels[y * size + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
