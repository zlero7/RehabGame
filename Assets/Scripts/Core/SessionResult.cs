using System;

[Serializable]
public class SessionResult
{
    public string sessionId;
    public string patientId;
    public string timestampIso;
    public DifficultyTier tier;
    public GameModeType mode;
    public float totalRawDistance;
    public float romUsageRatio;
    public float avgTimePerStar;
    public float[] directionFrequency;
    public bool completed;
    public float elapsedSeconds;
}
