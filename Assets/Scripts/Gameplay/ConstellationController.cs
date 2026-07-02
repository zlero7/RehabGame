using System.Collections.Generic;
using UnityEngine;

public class ConstellationController : MonoBehaviour
{
    public static ConstellationController Instance { get; private set; }

    [SerializeField] private List<StarNode> nodes;
    [SerializeField] private float judgeRadius = 0.6f;

    private int currentIndex = 0;
    private GameState state = GameState.Ready;
    private IGameMode currentMode;
    private Vector2 lastCursorWorldPos;

    // 커서가 활성 별의 judgeRadius 밖에 있었던 적이 최소 한 번은 있어야 연결을 허용한다.
    // 이게 없으면 게임 시작 시 커서 시작 위치(화면 중앙)가 우연히 별의 판정 반경 안에 들어오는 경우
    // 플레이어가 전혀 움직이지 않아도 별이 즉시(그리고 연쇄적으로) 연결되어 버린다.
    private bool targetArmed = false;

    private DifficultyTier currentTier;
    private GameModeType currentModeType;

    public Vector2 CurrentTargetPosition => nodes != null && currentIndex < nodes.Count
        ? nodes[currentIndex].WorldPosition
        : Vector2.zero;

    public bool IsPlaying => state == GameState.Playing;
    public int TotalNodes => nodes != null ? nodes.Count : 0;
    public int CurrentIndex => currentIndex;

    void Awake() => Instance = this;

    public void SetJudgeRadius(float radius) => judgeRadius = radius;

    public void SetNodes(List<StarNode> newNodes) => nodes = newNodes;

    public void StartConstellation(IGameMode mode, DifficultyTier tier, GameModeType modeType)
    {
        if (nodes == null || nodes.Count == 0) return;

        currentMode = mode;
        currentTier = tier;
        currentModeType = modeType;
        currentIndex = 0;
        state = GameState.Playing;

        foreach (var node in nodes)
            node.SetState(NodeState.Idle);

        nodes[0].SetState(NodeState.Active);
        lastCursorWorldPos = GetTransformedCursorPos();
        targetArmed = Vector2.Distance(lastCursorWorldPos, nodes[0].WorldPosition) > judgeRadius;

        currentMode?.OnModeStart();

        if (ConnectionLineRenderer.Instance != null)
            ConnectionLineRenderer.Instance.Clear();

        if (DataLogger.Instance != null)
            DataLogger.Instance.BeginSession(SessionContext.Current?.UserId ?? "unknown", tier, modeType);
    }

    void Update()
    {
        if (state != GameState.Playing) return;

        Vector2 cursorWorldPos = GetTransformedCursorPos();
        StarNode target = nodes[currentIndex];

        if (!targetArmed && Vector2.Distance(cursorWorldPos, target.WorldPosition) > judgeRadius)
            targetArmed = true;

        float dist = DistancePointToSegment(target.WorldPosition, lastCursorWorldPos, cursorWorldPos);

        if (targetArmed && dist <= judgeRadius)
            ConnectNode(target);

        if (VirtualCursor.Instance != null)
            DataLogger.Instance?.AccumulateRawDistance(VirtualCursor.Instance.RawDelta.magnitude);

        lastCursorWorldPos = cursorWorldPos;
    }

    private Vector2 GetTransformedCursorPos()
    {
        Vector2 pos = VirtualCursor.Instance != null
            ? VirtualCursor.Instance.WorldPosition
            : Vector2.zero;

        return currentMode != null ? currentMode.TransformCursorPosition(pos) : pos;
    }

    private float DistancePointToSegment(Vector2 point, Vector2 segA, Vector2 segB)
    {
        Vector2 ab = segB - segA;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f) return Vector2.Distance(point, segA);
        float t = Mathf.Clamp01(Vector2.Dot(point - segA, ab) / lenSq);
        Vector2 projection = segA + t * ab;
        return Vector2.Distance(point, projection);
    }

    private void ConnectNode(StarNode node)
    {
        node.SetState(NodeState.Connected);

        if (currentIndex > 0 && ConnectionLineRenderer.Instance != null)
            ConnectionLineRenderer.Instance.Draw(nodes[currentIndex - 1], node);

        if (DataLogger.Instance != null)
            DataLogger.Instance.LogStarConnected(currentIndex, Time.unscaledTime);

        if (HUDController.Instance != null)
            HUDController.Instance.UpdateProgress(currentIndex + 1, nodes.Count);

        currentIndex++;
        if (currentIndex >= nodes.Count)
            OnConstellationComplete();
        else
        {
            nodes[currentIndex].SetState(NodeState.Active);
            targetArmed = Vector2.Distance(lastCursorWorldPos, nodes[currentIndex].WorldPosition) > judgeRadius;
        }
    }

    private void OnConstellationComplete()
    {
        state = GameState.Completed;

        SessionResult result = DataLogger.Instance?.FinalizeSession(currentTier, currentModeType, true);
        if (result != null)
            DataExporter.Instance?.SaveSession(result);

        if (GameManager.Instance != null)
            GameManager.Instance.OnConstellationCompleted(result);
    }
}
