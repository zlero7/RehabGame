using UnityEngine;

public class MirrorMode : IGameMode
{
    public bool IsTimeLimited => false;

    public void OnModeStart()
    {
        if (VirtualCursor.Instance != null)
            VirtualCursor.Instance.SetHorizontalInvert(true);
    }

    public Vector2 TransformCursorPosition(Vector2 worldPos) => worldPos;
}
