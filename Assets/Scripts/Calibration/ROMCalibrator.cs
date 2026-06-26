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

    public float CalculateSensitivityMultiplier(float coverageRatio)
    {
        return Mathf.Lerp(2.0f, 1.0f, coverageRatio);
    }

    public void ApplyToVirtualCursor(float coverageRatio)
    {
        if (VirtualCursor.Instance == null) return;
        float multiplier = CalculateSensitivityMultiplier(coverageRatio);
        VirtualCursor.Instance.SetSensitivityMultiplier(multiplier);
    }

    public bool IsCalibrated => patientMaxReachX > 0f && patientMaxReachY > 0f;
}
