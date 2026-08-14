using System.Collections.ObjectModel;
using System.ComponentModel;
using L850Manager.Models;
using L850Manager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace L850Manager.Pages;

public sealed partial class RadioPage : Page
{
    private bool _updating;
    public ObservableCollection<BandChoice> BandChoices { get; } = new(RadioService.AllowedBands.Select(b => new BandChoice(b, LabelFor(b))));
    public IReadOnlyList<BandChoice> FddBands => BandChoices.Where(x => x.Band is < 38 or 66).ToArray();
    public IReadOnlyList<BandChoice> TddBands => BandChoices.Where(x => x.Band is >= 38 and <= 41).ToArray();

    public RadioPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadPreferencesAsync();
        BandAny.Checked += (_, _) =>
        {
            if (_updating) return;
            _updating = true;
            foreach (BandChoice choice in BandChoices) choice.IsSelected = false;
            _updating = false;
            UpdateHint();
        };
        foreach (BandChoice choice in BandChoices) choice.PropertyChanged += BandChanged;
    }

    private async Task LoadPreferencesAsync()
    {
        StatusProgress.IsActive = true;
        try
        {
            RadioPreferences preferences = await App.Radio.GetPreferencesAsync();
            _updating = true;
            NetworkModeSelector.SelectedIndex = preferences.Mode switch { NetworkMode.ThreeGOnly => 0, NetworkMode.FourGOnly => 1, _ => 2 };
            foreach (BandChoice choice in BandChoices) choice.IsSelected = preferences.Bands.Contains(choice.Band);
            BandAny.IsChecked = preferences.Bands.Count == 0;
            _updating = false;
            UpdateHint();
            CurrentModeText.Text = preferences.Mode switch { NetworkMode.ThreeGOnly => "3G only", NetworkMode.FourGOnly => "4G only", _ => "3G + 4G automatic" };
            CurrentBandsText.Text = preferences.Bands.Count == 0 ? "All 24 supported LTE bands" : string.Join(" · ", preferences.Bands.Select(x => $"B{x}"));
        }
        catch (Exception ex) { CurrentModeText.Text = "Configuration unavailable"; CurrentBandsText.Text = ex.Message; }
        finally { StatusProgress.IsActive = false; }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadPreferencesAsync();

    private void BandChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_updating || e.PropertyName != nameof(BandChoice.IsSelected)) return;
        _updating = true;
        BandAny.IsChecked = !BandChoices.Any(x => x.IsSelected);
        _updating = false;
        UpdateHint();
    }

    private void UpdateHint()
    {
        string[] names = BandChoices.Where(x => x.IsSelected).Select(x => $"B{x.Band}").ToArray();
        BandSelectionHint.Text = names.Length == 0 ? "Automatic lets the network choose any supported LTE band." : $"Manual selection · {string.Join(", ", names)}";
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        NetworkMode mode = NetworkModeSelector.SelectedIndex switch { 0 => NetworkMode.ThreeGOnly, 1 => NetworkMode.FourGOnly, _ => NetworkMode.ThreeGAndFourG };
        List<int> selected = BandChoices.Where(x => x.IsSelected).Select(x => x.Band).ToList();
        ContentDialog confirm = new() { XamlRoot=XamlRoot, Title="Apply radio settings?", Content="The modem may briefly lose registration.", PrimaryButtonText="Apply", CloseButtonText="Cancel", DefaultButton=ContentDialogButton.Close };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        RadioOperationResult result = await App.Radio.ApplyModeAndBandsAsync(mode, selected);
        await new ContentDialog { XamlRoot=XamlRoot, Title=result.Success ? "Radio settings applied" : $"Failed at {result.Stage}", Content=result.Message, CloseButtonText="Close" }.ShowAsync();
        if (result.Success) await LoadPreferencesAsync();
    }

    private static string LabelFor(int band) => band switch
    {
        1 => "B1 · 2100 MHz", 2 => "B2 · 1900 MHz", 3 => "B3 · 1800 MHz", 4 => "B4 · AWS",
        5 => "B5 · 850 MHz", 7 => "B7 · 2600 MHz", 8 => "B8 · 900 MHz", 11 => "B11 · 1500 MHz",
        12 => "B12 · 700 MHz", 13 => "B13 · 700 MHz", 17 => "B17 · 700 MHz", 18 => "B18 · 850 MHz",
        19 => "B19 · 850 MHz", 20 => "B20 · 800 MHz", 21 => "B21 · 1500 MHz", 26 => "B26 · 850 MHz",
        28 => "B28 · 700 MHz", 29 => "B29 · 700 SDL", 30 => "B30 · 2300 MHz", 38 => "B38 · 2600 TDD",
        39 => "B39 · 1900 TDD", 40 => "B40 · 2300 TDD", 41 => "B41 · 2500 TDD", 66 => "B66 · AWS-3",
        _ => $"B{band}"
    };
}
