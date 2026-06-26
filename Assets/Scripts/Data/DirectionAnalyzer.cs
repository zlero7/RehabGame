using UnityEngine;

public class DirectionAnalyzer : MonoBehaviour
{
    public static DirectionAnalyzer Instance { get; private set; }

    private float[] accumulated = new float[8];

    void Awake() => Instance = this;

    public void ResetTracking() => accumulated = new float[8];

    public void RecordMovement(Vector2 rawDelta)
    {
        if (rawDelta.sqrMagnitude < 0.0001f) return;
        int sector = ClassifyDirection(rawDelta);
        accumulated[sector] += rawDelta.magnitude;
    }

    private int ClassifyDirection(Vector2 delta)
    {
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;
        return Mathf.RoundToInt(angle / 45f) % 8;
    }

    public float[] GetNormalizedFrequency()
    {
        float total = 0f;
        foreach (float v in accumulated) total += v;

        float[] result = new float[8];
        for (int i = 0; i < 8; i++)
            result[i] = total > 0f ? accumulated[i] / total : 0f;
        return result;
    }

    public float[] GetRawAccumulated() => (float[])accumulated.Clone();
}
