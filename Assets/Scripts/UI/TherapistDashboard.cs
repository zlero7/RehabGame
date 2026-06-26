using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TherapistDashboard : MonoBehaviour
{
    [SerializeField] private TMP_Text trendSummaryText;
    [SerializeField] private TMP_Text improvementText;
    [SerializeField] private TMP_Text balanceScoreText;
    [SerializeField] private TMP_Text accessDeniedText;
    [SerializeField] private GameObject dashboardPanel;

    private TrendAnalyzer trendAnalyzer = new TrendAnalyzer();

    public void LoadPatientData(string patientId)
    {
        if (SessionContext.Current == null || !SessionContext.Current.CanAccessPatient(patientId))
        {
            AuditLogger.Instance?.Log(AuditAction.AccessDenied, patientId, null);
            ShowAccessDeniedMessage();
            return;
        }

        AuditLogger.Instance?.Log(AuditAction.PatientDataViewed, patientId, null);

        var sessions = DataExporter.Instance?.LoadPatientSessions(patientId);

        if (sessions == null || sessions.Count == 0)
        {
            if (trendSummaryText != null)
                trendSummaryText.text = "이 환자의 세션 데이터가 없습니다.";
            return;
        }

        if (dashboardPanel != null) dashboardPanel.SetActive(true);
        if (accessDeniedText != null) accessDeniedText.gameObject.SetActive(false);

        DisplayTrend(sessions);
    }

    private void DisplayTrend(List<SessionResult> sessions)
    {
        TrendDirection trend = trendAnalyzer.AnalyzeRomTrend(sessions);
        float improvement = trendAnalyzer.ImprovementVsBaseline(sessions);
        float balanceNow = sessions.Last().directionFrequency != null
            ? trendAnalyzer.DirectionBalanceScore(sessions.Last().directionFrequency)
            : 0f;

        string trendText = trend switch
        {
            TrendDirection.Improving => "개선 추세",
            TrendDirection.Stable => "정체 (변화 적음)",
            TrendDirection.Declining => "주의: 하락 추세 — 난이도/피로도 점검 권장",
            _ => "데이터 부족 (3회 이상 필요)"
        };

        if (trendSummaryText != null)
            trendSummaryText.text = $"추세: {trendText}";

        if (improvementText != null)
            improvementText.text = $"기준선 대비 변화: {improvement * 100f:+0.0;-0.0}%";

        if (balanceScoreText != null)
            balanceScoreText.text = $"방향 균형 점수(낮을수록 균형): {balanceNow:F3}";
    }

    private void ShowAccessDeniedMessage()
    {
        if (dashboardPanel != null) dashboardPanel.SetActive(false);
        if (accessDeniedText != null)
        {
            accessDeniedText.gameObject.SetActive(true);
            accessDeniedText.text = "접근 권한이 없습니다.";
        }
    }
}
