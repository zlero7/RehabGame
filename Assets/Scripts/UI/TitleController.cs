using UnityEngine;
using UnityEngine.UI;

public class TitleController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button therapistButton;

    void Start()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (therapistButton != null)
            therapistButton.onClick.AddListener(OnTherapistClicked);
    }

    private void OnStartClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.GoToDifficultySelect();
    }

    private void OnTherapistClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("TherapistMonitor");
    }
}
