using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DifficultySelectController : MonoBehaviour
{
    [SerializeField] private Button beginnerButton;
    [SerializeField] private Button basicButton;
    [SerializeField] private Button advancedButton;
    [SerializeField] private Button basicModeButton;
    [SerializeField] private Button mirrorModeButton;
    [SerializeField] private Button timerModeButton;
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_Text selectedInfoText;

    private DifficultyTier selectedTier = DifficultyTier.Beginner;
    private GameModeType selectedMode = GameModeType.Basic;

    void Start()
    {
        if (beginnerButton != null)
            beginnerButton.onClick.AddListener(() => SelectTier(DifficultyTier.Beginner));
        if (basicButton != null)
            basicButton.onClick.AddListener(() => SelectTier(DifficultyTier.Basic));
        if (advancedButton != null)
            advancedButton.onClick.AddListener(() => SelectTier(DifficultyTier.Advanced));

        if (basicModeButton != null)
            basicModeButton.onClick.AddListener(() => SelectMode(GameModeType.Basic));
        if (mirrorModeButton != null)
            mirrorModeButton.onClick.AddListener(() => SelectMode(GameModeType.Mirror));
        if (timerModeButton != null)
            timerModeButton.onClick.AddListener(() => SelectMode(GameModeType.Timer));

        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        UpdateInfoText();
    }

    private void SelectTier(DifficultyTier tier)
    {
        selectedTier = tier;
        GameManager.Instance?.SetDifficulty(tier);
        UpdateInfoText();
    }

    private void SelectMode(GameModeType mode)
    {
        selectedMode = mode;
        GameManager.Instance?.SetMode(mode);
        UpdateInfoText();
    }

    private void OnStartClicked()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.SetDifficulty(selectedTier);
        GameManager.Instance.SetMode(selectedMode);
        GameManager.Instance.StartGameplay();
    }

    private void UpdateInfoText()
    {
        if (selectedInfoText == null) return;
        string tierName = selectedTier switch
        {
            DifficultyTier.Beginner => "1단계(입문)",
            DifficultyTier.Basic => "2단계(기본)",
            DifficultyTier.Advanced => "3단계(심화)",
            _ => ""
        };
        string modeName = selectedMode switch
        {
            GameModeType.Mirror => "거울 모드",
            GameModeType.Timer => "타이머 모드",
            _ => "기본 모드"
        };
        selectedInfoText.text = $"선택: {tierName} / {modeName}";
    }
}
