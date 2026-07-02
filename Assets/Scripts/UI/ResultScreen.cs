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
        // 주의: 이 컴포넌트는 panel(ResultPanel) 오브젝트 자신에 붙어있고, 씬에서 비활성 상태로 시작한다.
        // 따라서 Awake()는 씬 로드 시점이 아니라 Display()가 SetActive(true)를 호출하는 "첫 표시 순간"에 실행된다.
        // 여기서 panel.SetActive(false)를 호출하면 방금 켠 패널을 즉시 다시 꺼버리므로 절대 넣으면 안 된다.
        // (초기 숨김 상태는 씬에 저장된 비활성 상태가 보장한다.)

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
