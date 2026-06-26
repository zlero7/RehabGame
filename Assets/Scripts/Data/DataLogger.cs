using System;
using System.Collections.Generic;
using UnityEngine;

public class DataLogger : MonoBehaviour
{
    public static DataLogger Instance { get; private set; }

    private string sessionId;
    private string patientId;
    private float sessionStartTime;
    private List<float> starConnectionTimes = new List<float>();
    private float totalRawDistance;

    void Awake() => Instance = this;

    public void BeginSession(string patientId, DifficultyTier tier, GameModeType mode)
    {
        this.patientId = patientId;
        sessionId = Guid.NewGuid().ToString();
        sessionStartTime = Time.unscaledTime;
        starConnectionTimes.Clear();
        totalRawDistance = 0f;

        if (DirectionAnalyzer.Instance != null)
            DirectionAnalyzer.Instance.ResetTracking();

        if (ROMUsageTracker.Instance != null)
            ROMUsageTracker.Instance.ResetTracking();
    }

    public void LogStarConnected(int starIndex, float timeStamp)
    {
        starConnectionTimes.Add(timeStamp);
    }

    public void AccumulateRawDistance(float delta)
    {
        totalRawDistance += delta;
    }

    public SessionResult FinalizeSession(DifficultyTier tier, GameModeType mode, bool completed)
    {
        float elapsed = Time.unscaledTime - sessionStartTime;
        float avgTime = starConnectionTimes.Count > 1
            ? elapsed / (starConnectionTimes.Count - 1)
            : elapsed;

        float romUsage = ROMUsageTracker.Instance != null
            ? ROMUsageTracker.Instance.GetUsageRatio()
            : 0f;

        float[] dirFreq = DirectionAnalyzer.Instance != null
            ? DirectionAnalyzer.Instance.GetNormalizedFrequency()
            : new float[8];

        var result = new SessionResult
        {
            sessionId = sessionId,
            patientId = patientId,
            timestampIso = DateTime.UtcNow.ToString("o"),
            tier = tier,
            mode = mode,
            totalRawDistance = totalRawDistance,
            romUsageRatio = romUsage,
            avgTimePerStar = avgTime,
            directionFrequency = dirFreq,
            completed = completed,
            elapsedSeconds = elapsed
        };

        return result;
    }
}
