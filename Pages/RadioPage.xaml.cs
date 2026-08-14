using L850Manager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace L850Manager.Pages;

public sealed partial class RadioPage : Page
{
    private bool _updating;
    private CheckBox[] Bands => [Band1, Band3, Band5, Band7, Band8, Band20, Band28, Band40, Band41];

    public RadioPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadPreferencesAsync();
        BandAny.Checked += (_, _) => { if (_updating) return; _updating = true; foreach (var b in Bands) b.IsChecked = false; _updating = false; UpdateHint(); };
        foreach (var band in Bands) { band.Checked += BandChanged; band.Unchecked += BandChanged; }
    }

    private async Task LoadPreferencesAsync()
    {
        StatusProgress.IsActive = true;
        try
        {
            RadioPreferences preferences = await App.Radio.GetPreferencesAsync();
            _updating = true;
            NetworkModeSelector.SelectedIndex = preferences.Mode switch { NetworkMode.ThreeGOnly => 0, NetworkMode.FourGOnly => 1, _ => 2 };
            int[] values = [1,3,5,7,8,20,28,40,41];
            foreach ((CheckBox box, int i) in Bands.Select((box, i) => (box, i))) box.IsChecked = preferences.Bands.Contains(values[i]);
            BandAny.IsChecked = preferences.Bands.Count == 0;
            _updating = false;
            UpdateHint();
            CurrentModeText.Text = preferences.Mode switch { NetworkMode.ThreeGOnly => "3G only", NetworkMode.FourGOnly => "4G only", _ => "3G + 4G automatic" };
            CurrentBandsText.Text = preferences.Bands.Count == 0 ? "All supported LTE bands" : string.Join(" · ", preferences.Bands.Select(x => $"B{x}"));
        }
        catch (Exception ex) { CurrentModeText.Text = "Configuration unavailable"; CurrentBandsText.Text = ex.Message; }
        finally { StatusProgress.IsActive = false; }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadPreferencesAsync();

    private void BandChanged(object sender, RoutedEventArgs e)
    {
        if (_updating) return;
        _updating = true;
        bool any = Bands.Any(x => x.IsChecked == true);
        BandAny.IsChecked = !any;
        _updating = false;
        UpdateHint();
    }

    private void UpdateHint()
    {
        string[] names = Bands.Where(x => x.IsChecked == true).Select(x => x.Content!.ToString()!.Split('·')[0].Trim()).ToArray();
        BandSelectionHint.Text = names.Length == 0 ? "Automatic lets the network choose any supported LTE band." : $"Manual selection · {string.Join(", ", names)}";
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        NetworkMode mode = NetworkModeSelector.SelectedIndex switch { 0 => NetworkMode.ThreeGOnly, 1 => NetworkMode.FourGOnly, _ => NetworkMode.ThreeGAndFourG };
        int[] values = [1,3,5,7,8,20,28,40,41];
        List<int> selected = Bands.Select((box, i) => (box, i)).Where(x => x.box.IsChecked == true).Select(x => values[x.i]).ToList();
        ContentDialog confirm = new() { XamlRoot=XamlRoot, Title="Apply radio settings?", Content="The modem may briefly lose registration.", PrimaryButtonText="Apply", CloseButtonText="Cancel", DefaultButton=ContentDialogButton.Close };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        RadioOperationResult result = await App.Radio.ApplyModeAndBandsAsync(mode, selected);
        await new ContentDialog { XamlRoot=XamlRoot, Title=result.Success ? "Radio settings applied" : $"Failed at {result.Stage}", Content=result.Message, CloseButtonText="Close" }.ShowAsync();
        if (result.Success) await LoadPreferencesAsync();
    }
}
