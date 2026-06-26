using System.Collections.Generic;
using UnityEngine;

public static class PatternValidator
{
    public static List<string> Validate(ConstellationPattern pattern)
    {
        var errors = new List<string>();
        if (pattern == null || pattern.starPositions == null || pattern.starPositions.Count == 0)
        {
            errors.Add("패턴이 null이거나 별이 없습니다.");
            return errors;
        }

        var coveredZones = new HashSet<Vector2Int>();
        foreach (var pos in pattern.starPositions)
            coveredZones.Add(GetZone(pos));
        if (coveredZones.Count < 9)
            errors.Add($"3x3 구역 중 {9 - coveredZones.Count}개 구역에 별이 없음");

        int sameDirCount = 1;
        Vector2 prevDir = Vector2.zero;
        for (int i = 1; i < pattern.starPositions.Count; i++)
        {
            Vector2 diff = pattern.starPositions[i] - pattern.starPositions[i - 1];

            if (diff.sqrMagnitude < 0.0001f)
            {
                errors.Add($"인덱스 {i - 1}, {i}: 별 좌표가 동일함 (이동 불가능한 패턴)");
                continue;
            }

            Vector2 dir = diff.normalized;
            if (prevDir != Vector2.zero && Vector2.Dot(dir, prevDir) > 0.9f)
                sameDirCount++;
            else
                sameDirCount = 1;

            if (sameDirCount >= 3)
                errors.Add($"인덱스 {i - 2}~{i}: 같은 방향 3회 이상 연속 이동");

            prevDir = dir;
        }
        return errors;
    }

    private static Vector2Int GetZone(Vector2 normalizedPos)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt((normalizedPos.x + 1f) / 2f * 3f), 0, 2);
        int y = Mathf.Clamp(Mathf.FloorToInt((normalizedPos.y + 1f) / 2f * 3f), 0, 2);
        return new Vector2Int(x, y);
    }
}
