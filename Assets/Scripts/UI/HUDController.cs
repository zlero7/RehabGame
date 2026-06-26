using System;
using TMPro;
using UnityEngine;

public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text elapsedTimeText;
    [SerializeField] private TMP_Text romUsageText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private GameObject timerObject;

    private float sessionStartTime;
    private bool isRunning = false;

    void Awake() => Instance = this;

    public void StartSession()
    {
        sessionStartTime = Time.unscaledTime;
        isRunning = true;
    }

    public void StopSession() => isRunning = false;

    void Update()
    {
        if (!isRunning) return;

        float elapsed = Time.unscaledTime - sessionStartTime;
        UpdateElapsedTime(elapsed);

        if (ROMUsageTracker.Instance != null)
            UpdateROMUsage(ROMUsageTracker.Instance.GetUsageRatio());
    }

    public void UpdateProgress(int current, int total)
    {
        if (progressText != null)
            progressText.text = $"진행도 {current}/{total}";
    }

    public void UpdateElapsedTime(float seconds)
    {
        if (elapsedTimeText != null)
            elapsedTimeText.text = $"경과 {TimeSpan.FromSeconds(seconds):mm\\:ss}";
    }

    public void UpdateROMUsage(float ratio)
    {
        if (romUsageText != null)
            romUsageText.text = $"가동 영역 사용 비율: {ratio * 100f:F0}%";
    }

    public void ShowTimer(float remainingSeconds)
    {
        if (timerObject != null) timerObject.SetActive(true);
        if (timerText != null)
            timerText.text = $"남은 시간: {TimeSpan.FromSeconds(remainingSeconds):mm\\:ss}";
    }

    public void HideTimer()
    {
        if (timerObject != null) timerObject.SetActive(false);
    }
}
