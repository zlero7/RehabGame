using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class VirtualCursor : MonoBehaviour
{
    public static VirtualCursor Instance { get; private set; }

    [SerializeField] private RectTransform cursorVisual;
    [SerializeField] private RectTransform playAreaBounds;
    private Canvas playAreaCanvas;

    public Vector2 ScreenPosition { get; private set; }
    public Vector2 WorldPosition { get; private set; }
    public Vector2 RawDelta { get; private set; }
    public Vector2 ScaledDelta { get; private set; }

    // 실제 플레이 피드백에 따라 단계적으로 조정 중인 값 (0.0267 → 0.04 → 0.06).
    // 주의: 씬(Gameplay.unity)의 VirtualCursor 컴포넌트에 직렬화된 값이 이 기본값을 덮어쓰므로,
    // 이 값을 바꿀 때는 씬의 직렬화 값도 함께 갱신해야 실제 플레이에 반영된다.
    // CalibrationController가 동일 값을 참조하므로(BaseSensitivity 프로퍼티) 캘리브레이션은 자동으로 함께 맞춰진다.
    [SerializeField] private float baseSensitivity = 0.06f;
    private float sensitivityMultiplier = 1f;

    public float BaseSensitivity => baseSensitivity;
    private bool horizontalInvert = false;
    private Camera mainCamera;
    private bool didHideCursor = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ScreenPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
    }

    void Start()
    {
        mainCamera = Camera.main;
        string scene = SceneManager.GetActiveScene().name;
        if (scene == "Gameplay" || scene == "TherapistMonitor")
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Confined;
            didHideCursor = true;
        }
    }

    void OnDestroy()
    {
        if (Instance == this && didHideCursor)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void SetSensitivityMultiplier(float multiplier) => sensitivityMultiplier = multiplier;
    public void SetHorizontalInvert(bool invert) => horizontalInvert = invert;

    public void SetPlayAreaBounds(RectTransform bounds)
    {
        playAreaBounds = bounds;
        playAreaCanvas = bounds != null ? bounds.GetComponentInParent<Canvas>() : null;
    }

    // Screen Space-Overlay는 카메라가 필요 없고(null), Screen Space-Camera/World Space는 canvas.worldCamera가 필요하다.
    private Camera UiCamera => playAreaCanvas != null && playAreaCanvas.renderMode != RenderMode.ScreenSpaceOverlay
        ? playAreaCanvas.worldCamera
        : null;

    void Update()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        Vector2 rawDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        if (horizontalInvert) rawDelta.x = -rawDelta.x;

        RawDelta = rawDelta;
        ScaledDelta = rawDelta * baseSensitivity * sensitivityMultiplier;

        ScreenPosition += ScaledDelta;
        ScreenPosition = ClampToPlayArea(ScreenPosition);

        if (cursorVisual != null)
        {
            RectTransform canvasRect = playAreaCanvas != null ? playAreaCanvas.transform as RectTransform : null;
            if (canvasRect != null &&
                RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, ScreenPosition, UiCamera, out Vector3 worldPoint))
                cursorVisual.position = worldPoint;
            else
                cursorVisual.position = ScreenPosition;
        }

        if (mainCamera != null)
            WorldPosition = mainCamera.ScreenToWorldPoint(
                new Vector3(ScreenPosition.x, ScreenPosition.y, Mathf.Abs(mainCamera.transform.position.z)));

        DirectionAnalyzer.Instance?.RecordMovement(RawDelta);
    }

    // playAreaBounds.GetWorldCorners()는 Canvas 렌더 모드에 따라 실제 화면 픽셀과 전혀 다른 값(Screen Space-Camera의 경우
    // 카메라 기준 월드 좌표)을 반환하므로, RectTransformUtility.WorldToScreenPoint로 반드시 실제 화면 픽셀로 변환한 뒤 클램프해야 한다.
    // 이 변환 없이 그대로 클램프하면(과거 버그) ScreenPosition이 화면 구석 근처의 작은 범위로 즉시 압축되어 버린다.
    private Vector2 ClampToPlayArea(Vector2 pos)
    {
        if (playAreaBounds == null)
            return new Vector2(Mathf.Clamp(pos.x, 0f, Screen.width), Mathf.Clamp(pos.y, 0f, Screen.height));

        Vector3[] corners = new Vector3[4];
        playAreaBounds.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(UiCamera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(UiCamera, corners[2]);
        return new Vector2(
            Mathf.Clamp(pos.x, min.x, max.x),
            Mathf.Clamp(pos.y, min.y, max.y));
    }
}
