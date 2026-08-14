namespace L850Manager.Models;

public sealed record ModemSnapshot
{
    public bool IsAvailable { get; init; }
    public bool IsConnected { get; init; }
    public string PortName { get; init; } = "—";
    public string Manufacturer { get; init; } = "—";
    public string Model { get; init; } = "—";
    public string Firmware { get; init; } = "—";
    public string Provider { get; init; } = "—";
    public string AccessTechnology { get; init; } = "—";
    public string Apn { get; init; } = "—";
    public int? VoltageMv { get; init; }
    public int? Band { get; init; }
    public int? Earfcn { get; init; }
    public int? Pci { get; init; }
    public double? Rsrp { get; init; }
    public double? Rsrq { get; init; }
    public double? Sinr { get; init; }
    public int ActiveCarriers { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
    public string? Error { get; init; }
}

public sealed record WwanSnapshot(
    bool IsConnected,
    string Provider,
    string AccessTechnology,
    string Apn);

public sealed record CellSnapshot(
    bool IsServing,
    int Band,
    int Earfcn,
    int Pci,
    double? Rsrp,
    double? Rsrq,
    double? Sinr);
