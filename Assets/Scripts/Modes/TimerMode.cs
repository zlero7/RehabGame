using UnityEngine;

public class TimerMode : IGameMode
{
    public float TimeLimitSeconds = 60f;

    public bool IsTimeLimited => true;

    public void OnModeStart()
    {
        if (VirtualCursor.Instance != null)
            VirtualCursor.Instance.SetHorizontalInvert(false);
    }

    public Vector2 TransformCursorPosition(Vector2 worldPos) => worldPos;
}
