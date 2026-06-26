using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public class DataExporter : MonoBehaviour
{
    public static DataExporter Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private string SavePath => Path.Combine(Application.persistentDataPath, "sessions.enc");
    private string BackupPath => Path.Combine(Application.persistentDataPath, "sessions.enc.bak");

    public bool SaveSession(SessionResult result)
    {
        try
        {
            var all = LoadAll();
            all.Add(result);
            string json = JsonConvert.SerializeObject(all, Formatting.None);
            byte[] encrypted = SecureStorage.Encrypt(json);

            string tempPath = SavePath + ".tmp";
            File.WriteAllBytes(tempPath, encrypted);

            if (File.Exists(SavePath)) File.Copy(SavePath, BackupPath, true);
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(tempPath, SavePath);

            AuditLogger.Instance?.Log(AuditAction.SessionSaved, result.patientId, result.sessionId);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"세션 저장 실패: {e.Message}");
            AuditLogger.Instance?.Log(AuditAction.SaveFailed, result.patientId, result.sessionId, e.Message);
            return false;
        }
    }

    public List<SessionResult> LoadAll()
    {
        if (!File.Exists(SavePath)) return new List<SessionResult>();
        try
        {
            byte[] encrypted = File.ReadAllBytes(SavePath);
            string json = SecureStorage.Decrypt(encrypted);
            return JsonConvert.DeserializeObject<List<SessionResult>>(json) ?? new List<SessionResult>();
        }
        catch (Exception e)
        {
            Debug.LogError($"주 파일 손상, 백업 복구 시도: {e.Message}");
            return LoadFromBackup();
        }
    }

    public List<SessionResult> LoadPatientSessions(string patientId)
    {
        return LoadAll()
            .Where(s => s.patientId == patientId)
            .OrderBy(s => s.timestampIso)
            .ToList();
    }

    private List<SessionResult> LoadFromBackup()
    {
        if (!File.Exists(BackupPath)) return new List<SessionResult>();
        try
        {
            string json = SecureStorage.Decrypt(File.ReadAllBytes(BackupPath));
            AuditLogger.Instance?.Log(AuditAction.BackupRestored, null, null);
            return JsonConvert.DeserializeObject<List<SessionResult>>(json) ?? new List<SessionResult>();
        }
        catch
        {
            return new List<SessionResult>();
        }
    }

    public SessionResult GetPreviousSession(string patientId)
    {
        var sessions = LoadPatientSessions(patientId);
        return sessions.Count >= 2 ? sessions[^2] : null;
    }
}
