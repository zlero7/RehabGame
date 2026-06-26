using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TherapistSceneHelper : MonoBehaviour
{
    [SerializeField] private TMP_InputField patientIdField;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TherapistDashboard dashboard;

    void Start()
    {
        if (loadButton != null)
            loadButton.onClick.AddListener(OnLoadClicked);

        if (backButton != null)
            backButton.onClick.AddListener(() =>
                UnityEngine.SceneManagement.SceneManager.LoadScene("Title"));

        // 치료사 모드: 기본으로 Therapist 역할 로그인 (실제 인증은 확장 단계)
        if (SessionContext.Current == null)
            SessionContext.SignIn("therapist_default", UserRole.Therapist,
                new System.Collections.Generic.List<string>());
    }

    private void OnLoadClicked()
    {
        if (dashboard == null || patientIdField == null) return;
        string id = patientIdField.text.Trim();
        if (string.IsNullOrEmpty(id)) return;
        dashboard.LoadPatientData(id);
    }
}
