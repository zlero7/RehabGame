using UnityEngine;

[CreateAssetMenu(fileName = "Difficulty_", menuName = "ConstellationTrace/DifficultyConfig")]
public class DifficultyConfig : ScriptableObject
{
    public DifficultyTier tier;
    public int starCount;
    [Range(0f, 1f)] public float minRomCoverage;
    [Range(0f, 1f)] public float maxRomCoverage;
    public float diagonalWeight;
    public float judgeRadius = 0.6f;
}
