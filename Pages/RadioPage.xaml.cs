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
        BandAny.Checked += (_, _) => { if (_updating) return; _updating = true; foreach (var b in Bands) b.IsChecked = false; _updating = false; UpdateHint(); };
        foreach (var band in Bands) { band.Checked += BandChanged; band.Unchecked += BandChanged; }
    }

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
    }
}
