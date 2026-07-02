using UnityEngine;

public class ROMCalibrator : MonoBehaviour
{
    public static ROMCalibrator Instance { get; private set; }

    [Header("Measured ROM (set during calibration)")]
    public float patientMaxReachX;
    public float patientMaxReachY;

    [Header("Safety Clamp - Max allowed reach (% of screen world units)")]
    [Tooltip("캘리브레이션 측정값에 적용할 상한. 비현실적으로 큰 값을 방지하여 환자 안전 보장.")]
    [SerializeField] private float maxSafeReachX = 10f;
    [SerializeField] private float maxSafeReachY = 10f;

    private bool isCalibrating = false;
    private Vector2 peakReach = Vector2.zero;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartCalibrationAxis()
    {
        isCalibrating = true;
        peakReach = Vector2.zero;

        if (VirtualCursor.Instance != null)
        {
            Vector2 center = VirtualCursor.Instance.ScreenPosition;
            peakReach = Vector2.zero;
        }
    }

    void Update()
    {
        if (!isCalibrating || VirtualCursor.Instance == null) return;

        Vector2 raw = VirtualCursor.Instance.RawDelta;
        peakReach.x = Mathf.Max(peakReach.x, Mathf.Abs(raw.x));
        peakReach.y = Mathf.Max(peakReach.y, Mathf.Abs(raw.y));
    }

    public void RecordMaxReach(float reachX, float reachY)
    {
        isCalibrating = false;

        // 안전 클램프: 비현실적으로 큰 값 차단
        patientMaxReachX = Mathf.Clamp(reachX, 0.1f, maxSafeReachX);
        patientMaxReachY = Mathf.Clamp(reachY, 0.1f, maxSafeReachY);

        Debug.Log($"[ROMCalibrator] 캘리브레이션 완료: X={patientMaxReachX:F2}, Y={patientMaxReachY:F2}");
    }

    public Vector2 ScaleToPatientRange(Vector2 normalizedStarPos, float coverageRatio)
    {
        return new Vector2(
            normalizedStarPos.x * patientMaxReachX * coverageRatio,
            normalizedStarPos.y * patientMaxReachY * coverageRatio);
    }

    // 설계 초안은 2.0~1.0배로 항상 기본 감도보다 증폭시켰으나, 실제 플레이에서 "너무 빠르다"는
    // 피드백을 받아 기본 감도(VirtualCursor.baseSensitivity)보다 절대 빨라지지 않도록 1.0~0.7배로 조정.
    // Beginner(커버리지 낮음)는 기본 그대로, Advanced(커버리지 높음·실제 리치를 더 많이 사용)는 더 정밀하게 느리게.
    public float CalculateSensitivityMultiplier(float coverageRatio)
    {
        return Mathf.Lerp(1.0f, 0.7f, coverageRatio);
    }

    // 난이도 진입 시 호출하여 측정된 커버리지 비율에 맞는 감도를 VirtualCursor에 실제로 반영한다.
    public void ApplyToVirtualCursor(float coverageRatio)
    {
        float multiplier = CalculateSensitivityMultiplier(coverageRatio);
        if (VirtualCursor.Instance != null)
            VirtualCursor.Instance.SetSensitivityMultiplier(multiplier);
    }

    public bool IsCalibrated => patientMaxReachX > 0f && patientMaxReachY > 0f;
}
