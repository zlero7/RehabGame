using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TrendDirection { Improving, Stable, Declining, InsufficientData }

public class TrendAnalyzer
{
    public TrendDirection AnalyzeRomTrend(List<SessionResult> ordered, int window = 5)
    {
        if (ordered.Count < 3) return TrendDirection.InsufficientData;

        var recent = ordered.Skip(Mathf.Max(0, ordered.Count - window)).ToList();
        float slope = LinearRegressionSlope(recent.Select(s => s.romUsageRatio).ToList());

        if (slope > 0.01f) return TrendDirection.Improving;
        if (slope < -0.01f) return TrendDirection.Declining;
        return TrendDirection.Stable;
    }

    public List<float> MovingAverage(List<float> values, int window = 3)
    {
        var result = new List<float>();
        for (int i = 0; i < values.Count; i++)
        {
            int start = Mathf.Max(0, i - window + 1);
            float avg = values.Skip(start).Take(i - start + 1).Average();
            result.Add(avg);
        }
        return result;
    }

    public float DirectionBalanceScore(float[] directionFrequency)
    {
        if (directionFrequency == null || directionFrequency.Length == 0) return 0f;
        float mean = directionFrequency.Average();
        float variance = directionFrequency.Select(f => (f - mean) * (f - mean)).Average();
        return Mathf.Sqrt(variance);
    }

    public float ImprovementVsBaseline(List<SessionResult> ordered)
    {
        if (ordered.Count < 2) return 0f;
        float baseline = ordered.Take(Mathf.Min(3, ordered.Count)).Average(s => s.romUsageRatio);
        float recent = ordered.Last().romUsageRatio;
        return baseline > 0f ? (recent - baseline) / baseline : 0f;
    }

    private float LinearRegressionSlope(List<float> y)
    {
        int n = y.Count;
        float sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
        for (int i = 0; i < n; i++)
        {
            sumX += i; sumY += y[i];
            sumXY += i * y[i]; sumXX += i * i;
        }
        float denom = n * sumXX - sumX * sumX;
        return Mathf.Abs(denom) < 0.0001f ? 0f : (n * sumXY - sumX * sumY) / denom;
    }
}
