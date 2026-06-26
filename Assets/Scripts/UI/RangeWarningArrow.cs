using UnityEngine;

public class RangeWarningArrow : MonoBehaviour
{
    [SerializeField] private float lowMovementThreshold = 0.05f;
    [SerializeField] private float warningTriggerSeconds = 3f;

    private float lowMovementTimer = 0f;

    void Update()
    {
        if (VirtualCursor.Instance == null) return;

        if (VirtualCursor.Instance.RawDelta.magnitude < lowMovementThreshold)
            lowMovementTimer += Time.deltaTime;
        else
            lowMovementTimer = 0f;

        bool shouldWarn = lowMovementTimer >= warningTriggerSeconds
            && ConstellationController.Instance != null
            && ConstellationController.Instance.IsPlaying;

        gameObject.SetActive(shouldWarn);

        if (shouldWarn)
            RotateTowardTarget();
    }

    private void RotateTowardTarget()
    {
        if (ConstellationController.Instance == null || VirtualCursor.Instance == null) return;

        Vector2 dir = ConstellationController.Instance.CurrentTargetPosition
            - (Vector2)VirtualCursor.Instance.WorldPosition;

        if (dir.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
