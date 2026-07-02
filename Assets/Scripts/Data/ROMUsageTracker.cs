using UnityEngine;

public class ROMUsageTracker : MonoBehaviour
{
    public static ROMUsageTracker Instance { get; private set; }

    [SerializeField] private int gridCols = 12;
    [SerializeField] private int gridRows = 8;

    // 가상 커서가 실제로 이동 가능한 영역 — VirtualCursor.playAreaBounds와 동일한 RectTransform을 참조해야
    // "커서가 갈 수 있는 범위"와 "사용 비율 측정 범위"가 항상 일치한다.
    [SerializeField] private RectTransform playAreaBounds;
    private Canvas playAreaCanvas;

    private bool[,] visitedCells;

    void Awake()
    {
        Instance = this;
        ResetTracking();
    }

    public void ResetTracking()
    {
        visitedCells = new bool[gridCols, gridRows];
    }

    void Update()
    {
        if (VirtualCursor.Instance == null || playAreaBounds == null) return;

        if (playAreaCanvas == null)
            playAreaCanvas = playAreaBounds.GetComponentInParent<Canvas>();

        Vector2 norm = GetNormalizedPosition(VirtualCursor.Instance.ScreenPosition);
        int col = Mathf.Clamp((int)(norm.x * gridCols), 0, gridCols - 1);
        int row = Mathf.Clamp((int)(norm.y * gridRows), 0, gridRows - 1);
        visitedCells[col, row] = true;
    }

    public float GetUsageRatio()
    {
        int visited = 0;
        foreach (bool cell in visitedCells)
            if (cell) visited++;
        return (float)visited / (gridCols * gridRows);
    }

    // 화면(스크린) 좌표를 실제 플레이 영역 기준 [0,1]×[0,1]로 정규화.
    // Screen Space-Camera 캔버스에서는 GetWorldCorners()가 실제 화면 픽셀과 다른 값(카메라 기준 월드 좌표)을 반환하므로
    // RectTransformUtility.WorldToScreenPoint로 반드시 화면 픽셀로 변환한 뒤 InverseLerp해야 한다 (VirtualCursor.ClampToPlayArea와 동일 원리).
    private Vector2 GetNormalizedPosition(Vector2 screenPos)
    {
        Camera uiCam = playAreaCanvas != null && playAreaCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? playAreaCanvas.worldCamera
            : null;

        Vector3[] corners = new Vector3[4];
        playAreaBounds.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(uiCam, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(uiCam, corners[2]);

        return new Vector2(
            Mathf.InverseLerp(min.x, max.x, screenPos.x),
            Mathf.InverseLerp(min.y, max.y, screenPos.y));
    }
}
