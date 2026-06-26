using UnityEngine;
using UnityEngine.InputSystem;

public class VirtualCursor : MonoBehaviour
{
    public static VirtualCursor Instance { get; private set; }

    [SerializeField] private RectTransform cursorVisual;
    [SerializeField] private RectTransform playAreaBounds;

    public Vector2 ScreenPosition { get; private set; }
    public Vector2 WorldPosition { get; private set; }
    public Vector2 RawDelta { get; private set; }
    public Vector2 ScaledDelta { get; private set; }

    private float sensitivityMultiplier = 1f;
    private bool horizontalInvert = false;
    private Camera mainCamera;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ScreenPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
    }

    void Start()
    {
        mainCamera = Camera.main;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void SetSensitivityMultiplier(float multiplier) => sensitivityMultiplier = multiplier;
    public void SetHorizontalInvert(bool invert) => horizontalInvert = invert;

    public void SetPlayAreaBounds(RectTransform bounds) => playAreaBounds = bounds;

    void Update()
    {
        Vector2 rawDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        if (horizontalInvert) rawDelta.x = -rawDelta.x;

        RawDelta = rawDelta;
        ScaledDelta = rawDelta * sensitivityMultiplier;

        ScreenPosition += ScaledDelta;
        ScreenPosition = ClampToPlayArea(ScreenPosition);

        if (cursorVisual != null)
            cursorVisual.position = ScreenPosition;

        if (mainCamera != null)
            WorldPosition = mainCamera.ScreenToWorldPoint(
                new Vector3(ScreenPosition.x, ScreenPosition.y, Mathf.Abs(mainCamera.transform.position.z)));

        DirectionAnalyzer.Instance?.RecordMovement(RawDelta);
    }

    private Vector2 ClampToPlayArea(Vector2 pos)
    {
        if (playAreaBounds == null)
            return new Vector2(Mathf.Clamp(pos.x, 0f, Screen.width), Mathf.Clamp(pos.y, 0f, Screen.height));

        Vector3[] corners = new Vector3[4];
        playAreaBounds.GetWorldCorners(corners);
        return new Vector2(
            Mathf.Clamp(pos.x, corners[0].x, corners[2].x),
            Mathf.Clamp(pos.y, corners[0].y, corners[2].y));
    }
}
