using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultScreen : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text totalDistanceText;
    [SerializeField] private TMP_Text romUsageText;
    [SerializeField] private TMP_Text avgTimeBetweenStarsText;
    [SerializeField] private TMP_Text deltaFromLastSessionText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button menuButton;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);

        if (retryButton != null)
            retryButton.onClick.AddListener(() => GameManager.Instance?.StartGameplay());

        if (menuButton != null)
            menuButton.onClick.AddListener(() => GameManager.Instance?.GoToDifficultySelect());
    }

    public void Display(SessionResult result, SessionResult previousResult = null)
    {
        if (panel != null) panel.SetActive(true);

        if (totalDistanceText != null)
            totalDistanceText.text = $"총 이동량: {result.totalRawDistance:F0} units";

        if (romUsageText != null)
            romUsageText.text = $"가동 영역 사용 비율: {result.romUsageRatio * 100f:F0}%";

        if (avgTimeBetweenStarsText != null)
            avgTimeBetweenStarsText.text = $"별 사이 평균 이동 시간: {result.avgTimePerStar:F1}s";

        if (deltaFromLastSessionText != null)
        {
            if (previousResult != null)
            {
                float delta = result.romUsageRatio - previousResult.romUsageRatio;
                deltaFromLastSessionText.text = $"직전 회차 대비: {(delta >= 0 ? "+" : "")}{delta * 100f:F1}%p";
            }
            else
            {
                deltaFromLastSessionText.text = "직전 회차 데이터 없음";
            }
        }
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }
}
