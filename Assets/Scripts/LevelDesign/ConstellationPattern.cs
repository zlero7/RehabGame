using System.Collections.Generic;
using UnityEngine;

public enum DifficultyTier { Beginner, Basic, Advanced }
public enum ThemeType { Shape, Object, RealConstellation }

[CreateAssetMenu(fileName = "Pattern_", menuName = "ConstellationTrace/Pattern")]
public class ConstellationPattern : ScriptableObject
{
    public string patternName;
    public DifficultyTier tier;
    public ThemeType theme;
    [Tooltip("정규화 좌표 (-1~1), 가동범위 스케일링 시 곱연산")]
    public List<Vector2> starPositions;
}
