namespace L850Manager.Models;

public enum NetworkMode { ThreeGOnly, FourGOnly, ThreeGAndFourG }

public sealed record CellLockState(bool IsClear, int? Earfcn, int? Pci, string RawState);

public sealed record RadioOperationResult(bool Success, string Stage, string Message);
