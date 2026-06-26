using UnityEngine;

public class BasicMode : IGameMode
{
    public bool IsTimeLimited => false;

    public void OnModeStart()
    {
        if (VirtualCursor.Instance != null)
            VirtualCursor.Instance.SetHorizontalInvert(false);
    }

    public Vector2 TransformCursorPosition(Vector2 worldPos) => worldPos;
}
