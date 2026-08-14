using System.ComponentModel;

namespace L850Manager.Models;

public enum NetworkMode { ThreeGOnly, FourGOnly, ThreeGAndFourG }

public sealed record CellLockState(bool IsClear, int? Earfcn, int? Pci, string RawState);

public sealed record RadioOperationResult(bool Success, string Stage, string Message);

public sealed record RadioPreferences(NetworkMode Mode, IReadOnlyList<int> Bands);

public sealed class BandChoice(int band, string label) : INotifyPropertyChanged
{
    private bool _isSelected;
    public int Band { get; } = band;
    public string Label { get; } = label;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
