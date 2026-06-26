using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public enum AuditAction
{
    SignIn, SignOut, AccessDenied,
    SessionSaved, SaveFailed, BackupRestored,
    PatientDataViewed, DataExported, DataDeleted,
    PrescriptionChanged
}

[Serializable]
public class AuditEntry
{
    public string timestampIso;
    public string actorUserId;
    public string action;
    public string targetPatientId;
    public string targetSessionId;
    public string detail;
    public string prevHash;
    public string entryHash;
}

public class AuditLogger : MonoBehaviour
{
    public static AuditLogger Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private string LogPath => Path.Combine(Application.persistentDataPath, "audit.log.enc");

    public void Log(AuditAction action, string patientId, string sessionId, string detail = "")
    {
        var entry = new AuditEntry
        {
            timestampIso = DateTime.UtcNow.ToString("o"),
            actorUserId = SessionContext.Current?.UserId ?? "system",
            action = action.ToString(),
            targetPatientId = patientId ?? "",
            targetSessionId = sessionId ?? "",
            detail = detail ?? ""
        };

        var log = LoadLog();
        entry.prevHash = log.Count > 0 ? log[^1].entryHash : "GENESIS";
        entry.entryHash = ComputeHash(entry);
        log.Add(entry);

        SaveLog(log);
    }

    public bool VerifyIntegrity()
    {
        var log = LoadLog();
        string prev = "GENESIS";
        foreach (var e in log)
        {
            if (e.prevHash != prev) return false;
            if (e.entryHash != ComputeHash(e)) return false;
            prev = e.entryHash;
        }
        return true;
    }

    private string ComputeHash(AuditEntry e)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        string payload = $"{e.timestampIso}|{e.actorUserId}|{e.action}|{e.targetPatientId}|{e.targetSessionId}|{e.detail}|{e.prevHash}";
        byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }

    private List<AuditEntry> LoadLog()
    {
        if (!File.Exists(LogPath)) return new List<AuditEntry>();
        try
        {
            string json = SecureStorage.Decrypt(File.ReadAllBytes(LogPath));
            return JsonConvert.DeserializeObject<List<AuditEntry>>(json) ?? new List<AuditEntry>();
        }
        catch (Exception e)
        {
            Debug.LogError($"감사 로그 로드 실패: {e.Message}");
            return new List<AuditEntry>();
        }
    }

    private void SaveLog(List<AuditEntry> log)
    {
        try
        {
            string json = JsonConvert.SerializeObject(log, Formatting.None);
            byte[] encrypted = SecureStorage.Encrypt(json);
            File.WriteAllBytes(LogPath, encrypted);
        }
        catch (Exception e)
        {
            Debug.LogError($"감사 로그 저장 실패: {e.Message}");
        }
    }
}
