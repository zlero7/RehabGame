using UnityEngine;

public interface IGameMode
{
    void OnModeStart();
    Vector2 TransformCursorPosition(Vector2 worldPos);
    bool IsTimeLimited { get; }
}
