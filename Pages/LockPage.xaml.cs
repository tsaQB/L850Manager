using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using L850Manager.Models;
namespace L850Manager.Pages;
public sealed partial class LockPage : Page
{
    private bool _updatingBands;
    public LockPage() => InitializeComponent();

    private CheckBox[] BandBoxes => [Band1, Band3, Band5, Band7, Band8, Band20, Band28, Band40, Band41];

    private void BandAny_Checked(object sender, RoutedEventArgs e)
    {
        if (_updatingBands) return;
        _updatingBands = true;
        foreach (var box in BandBoxes) box.IsChecked = false;
        _updatingBands = false;
        UpdateBandHint();
    }

    private void Band_Checked(object sender, RoutedEventArgs e)
    {
        if (_updatingBands) return;
        _updatingBands = true;
        BandAny.IsChecked = false;
        _updatingBands = false;
        UpdateBandHint();
    }

    private void Band_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_updatingBands) return;
        if (BandBoxes.All(box => box.IsChecked != true)) BandAny.IsChecked = true;
        UpdateBandHint();
    }

    private void UpdateBandHint()
    {
        var selected = BandBoxes.Where(box => box.IsChecked == true).Select(box => box.Content?.ToString()).ToArray();
        BandSelectionHint.Text = selected.Length == 0
            ? "Automatic lets the network choose any supported LTE band."
            : $"Locked selection: {string.Join(", ", selected)}";
    }

    private async void ScanCells_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<CellSnapshot> cells = await App.Radio.ScanCellsAsync();
            CellList.ItemsSource = cells.Select(x => new CellRow(x)).ToArray();
            CellList.SelectedIndex = cells.Count > 0 ? 0 : -1;
        }
        catch (Exception ex) { await ShowAsync("Cell scan failed", ex.Message); }
    }

    private void CellList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CellList.SelectedItem is not CellRow row) return;
        EarfcnBox.Value = row.Earfcn;
        PciBox.Value = row.Pci;
    }

    private async void ApplyBands_Click(object sender, RoutedEventArgs e)
    {
        NetworkMode mode = NetworkModeSelector.SelectedIndex switch { 0 => NetworkMode.ThreeGOnly, 1 => NetworkMode.FourGOnly, _ => NetworkMode.ThreeGAndFourG };
        List<int> bands = [];
        if (BandAny.IsChecked != true)
        {
            foreach ((CheckBox box, int band) in new[] { (Band1,1),(Band3,3),(Band5,5),(Band7,7),(Band8,8),(Band20,20),(Band28,28),(Band40,40),(Band41,41) }) if (box.IsChecked == true) bands.Add(band);
        }
        if (!await ConfirmAsync("Apply radio settings?", "The modem may briefly lose registration while applying the requested mode and bands.")) return;
        RadioOperationResult result = await App.Radio.ApplyModeAndBandsAsync(mode, bands);
        await ShowAsync(result.Success ? "Settings applied" : $"Failed at {result.Stage}", result.Message);
    }

    private async void ApplyLock_Click(object sender, RoutedEventArgs e)
    {
        if (double.IsNaN(EarfcnBox.Value)) { await ShowAsync("Invalid EARFCN", "Enter a valid LTE EARFCN."); return; }
        int earfcn = checked((int)EarfcnBox.Value);
        int? pci = double.IsNaN(PciBox.Value) ? null : checked((int)PciBox.Value);
        if (!await ConfirmAsync("Apply cell lock?", $"The modem will restart.\n\nEARFCN: {earfcn}\nPCI: {(pci?.ToString() ?? "Any")}")) return;
        RadioOperationResult result = await App.Radio.SetCellLockAsync(earfcn, pci);
        await ShowAsync(result.Success ? "Cell lock verified" : $"Failed at {result.Stage}", result.Message);
    }

    private async void ClearLock_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmAsync("Clear cell lock?", "The modem will restart and return to automatic cell selection.")) return;
        RadioOperationResult result = await App.Radio.ClearCellLockAsync();
        await ShowAsync(result.Success ? "Cell lock cleared" : $"Failed at {result.Stage}", result.Message);
    }

    private async Task<bool> ConfirmAsync(string title, string message) =>
        await new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = message, PrimaryButtonText = "Continue", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close }.ShowAsync() == ContentDialogResult.Primary;

    private async Task ShowAsync(string title, string message) =>
        await new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = message, CloseButtonText = "Close" }.ShowAsync();

    private sealed record CellRow(CellSnapshot Cell)
    {
        public string RoleText => Cell.IsServing ? "Serving" : "Neighbor";
        public string BandText => $"B{Cell.Band}";
        public int Earfcn => Cell.Earfcn;
        public int Pci => Cell.Pci;
        public string RsrpText => Cell.Rsrp is double value ? $"{value:0.#} dBm" : "—";
    }
}
