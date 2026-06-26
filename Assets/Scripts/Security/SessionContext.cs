using System.Collections.Generic;

public enum UserRole { Patient, Therapist, Admin }

public class SessionContext
{
    public static SessionContext Current { get; private set; }

    public string UserId { get; private set; }
    public UserRole Role { get; private set; }
    public List<string> AssignedPatientIds { get; private set; }

    public static void SignIn(string userId, UserRole role, List<string> assignedPatients = null)
    {
        Current = new SessionContext
        {
            UserId = userId,
            Role = role,
            AssignedPatientIds = assignedPatients ?? new List<string>()
        };

        AuditLogger.Instance?.Log(AuditAction.SignIn, null, null, $"role={role}");
    }

    public static void SignOut()
    {
        AuditLogger.Instance?.Log(AuditAction.SignOut, Current?.UserId, null);
        Current = null;
    }

    public bool CanAccessPatient(string patientId)
    {
        return Role switch
        {
            UserRole.Admin => true,
            UserRole.Therapist => AssignedPatientIds.Contains(patientId),
            UserRole.Patient => UserId == patientId,
            _ => false
        };
    }
}
