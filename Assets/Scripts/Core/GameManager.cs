using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Current Session Config")]
    [SerializeField] private DifficultyTier selectedTier = DifficultyTier.Beginner;
    [SerializeField] private GameModeType selectedMode = GameModeType.Basic;
    [SerializeField] private float timerModeSeconds = 60f;

    private SessionResult lastResult;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public DifficultyTier SelectedTier => selectedTier;
    public GameModeType SelectedMode => selectedMode;
    public SessionResult LastResult => lastResult;

    public void SetDifficulty(DifficultyTier tier) => selectedTier = tier;
    public void SetMode(GameModeType mode) => selectedMode = mode;
    public void SetTimerDuration(float seconds) => timerModeSeconds = seconds;

    public IGameMode CreateCurrentMode()
    {
        return selectedMode switch
        {
            GameModeType.Mirror => new MirrorMode(),
            GameModeType.Timer => new TimerMode { TimeLimitSeconds = timerModeSeconds },
            _ => new BasicMode()
        };
    }

    public void StartGameplay()
    {
        SceneManager.LoadScene("Gameplay");
    }

    public void GoToTitle()
    {
        SceneManager.LoadScene("Title");
    }

    public void GoToDifficultySelect()
    {
        SceneManager.LoadScene("DifficultySelect");
    }

    public void OnConstellationCompleted(SessionResult result)
    {
        lastResult = result;
        var sceneController = FindObjectOfType<GameplaySceneController>();
        sceneController?.ShowResult(result);
    }
}
