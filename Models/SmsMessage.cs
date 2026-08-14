namespace L850Manager.Models;

public sealed record SmsMessage(
    int Index,
    string Status,
    string Sender,
    DateTimeOffset? Timestamp,
    string Body)
{
    public bool IsSent => Index < 0 || Status.Contains("SENT", StringComparison.OrdinalIgnoreCase);
    public bool IsStoredOnSim => Index >= 0;
    public string DisplaySender => string.IsNullOrWhiteSpace(Sender) ? "Unknown" : Sender;
    public string DisplayTime => Timestamp?.ToLocalTime().ToString("HH:mm") ?? "—";
    public string Preview => Body.Length > 45 ? Body[..45] + "…" : Body;
}
