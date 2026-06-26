using UnityEngine;

public class ROMUsageTracker : MonoBehaviour
{
    public static ROMUsageTracker Instance { get; private set; }

    [SerializeField] private int gridCols = 12;
    [SerializeField] private int gridRows = 8;
    [SerializeField] private RectTransform playAreaBounds;

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
        if (VirtualCursor.Instance == null) return;

        Vector2 normalizedPos = GetNormalizedPositionInPlayArea(VirtualCursor.Instance.ScreenPosition);
        int col = Mathf.Clamp((int)(normalizedPos.x * gridCols), 0, gridCols - 1);
        int row = Mathf.Clamp((int)(normalizedPos.y * gridRows), 0, gridRows - 1);
        visitedCells[col, row] = true;
    }

    public float GetUsageRatio()
    {
        int visited = 0;
        foreach (bool cell in visitedCells)
            if (cell) visited++;
        return (float)visited / (gridCols * gridRows);
    }

    private Vector2 GetNormalizedPositionInPlayArea(Vector2 screenPos)
    {
        if (playAreaBounds == null)
        {
            return new Vector2(
                Mathf.InverseLerp(0f, Screen.width, screenPos.x),
                Mathf.InverseLerp(0f, Screen.height, screenPos.y));
        }

        Vector3[] corners = new Vector3[4];
        playAreaBounds.GetWorldCorners(corners);
        float minX = corners[0].x;
        float minY = corners[0].y;
        float maxX = corners[2].x;
        float maxY = corners[2].y;

        return new Vector2(
            Mathf.InverseLerp(minX, maxX, screenPos.x),
            Mathf.InverseLerp(minY, maxY, screenPos.y));
    }
}
