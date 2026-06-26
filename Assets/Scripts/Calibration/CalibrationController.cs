using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CalibrationController : MonoBehaviour
{
    [SerializeField] private Button measureButton;
    [SerializeField] private Button completeButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text reachValueText;

    private bool isMeasuring = false;
    private Vector2 peakReach = Vector2.zero;
    private Vector2 startPosition = Vector2.zero;

    void Start()
    {
        if (measureButton != null)
            measureButton.onClick.AddListener(OnMeasureClicked);
        if (completeButton != null)
        {
            completeButton.onClick.AddListener(OnCompleteClicked);
            completeButton.interactable = false;
        }
        if (skipButton != null)
            skipButton.onClick.AddListener(OnSkipClicked);

        SetInstruction("마우스를 최대한 넓게 움직여보세요.\n[측정 시작] 버튼을 누른 뒤 상하좌우로 크게 움직이고\n[측정 완료] 버튼을 누르세요.");
    }

    void Update()
    {
        if (!isMeasuring || VirtualCursor.Instance == null) return;

        Vector2 currentPos = VirtualCursor.Instance.ScreenPosition;
        peakReach.x = Mathf.Max(peakReach.x, Mathf.Abs(currentPos.x - startPosition.x));
        peakReach.y = Mathf.Max(peakReach.y, Mathf.Abs(currentPos.y - startPosition.y));

        if (reachValueText != null)
            reachValueText.text = $"측정 중 — X: {peakReach.x:F0}px / Y: {peakReach.y:F0}px";
    }

    private void OnMeasureClicked()
    {
        isMeasuring = true;
        peakReach = Vector2.zero;

        if (VirtualCursor.Instance != null)
            startPosition = VirtualCursor.Instance.ScreenPosition;

        SetInstruction("지금 상하좌우로 최대한 크게 움직여 주세요!");

        if (measureButton != null) measureButton.interactable = false;
        if (completeButton != null) completeButton.interactable = true;
    }

    private void OnCompleteClicked()
    {
        isMeasuring = false;

        // 화면 픽셀 → 월드 유닛 변환 (Camera.main 기준)
        float pixelsPerUnit = GetPixelsPerUnit();
        float reachXWorld = peakReach.x / pixelsPerUnit;
        float reachYWorld = peakReach.y / pixelsPerUnit;

        if (ROMCalibrator.Instance != null)
            ROMCalibrator.Instance.RecordMaxReach(reachXWorld, reachYWorld);

        SetInstruction($"캘리브레이션 완료!\nX: {reachXWorld:F2} / Y: {reachYWorld:F2}");

        if (GameManager.Instance != null)
            GameManager.Instance.GoToDifficultySelect();
    }

    private void OnSkipClicked()
    {
        // 기본값으로 설정 (중간 범위)
        if (ROMCalibrator.Instance != null)
            ROMCalibrator.Instance.RecordMaxReach(4f, 3f);

        if (GameManager.Instance != null)
            GameManager.Instance.GoToDifficultySelect();
    }

    private float GetPixelsPerUnit()
    {
        if (Camera.main == null) return 100f;
        float screenHeight = Screen.height;
        float worldHeight = Camera.main.orthographicSize * 2f;
        return screenHeight / worldHeight;
    }

    private void SetInstruction(string text)
    {
        if (instructionText != null)
            instructionText.text = text;
    }
}
