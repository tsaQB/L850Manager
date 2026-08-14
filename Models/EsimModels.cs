namespace L850Manager.Models;

public sealed record EsimAvailability(
    bool IsDetected,
    bool HasManagementAccess,
    string Status,
    string Detail,
    int ProfileCount = 0);
