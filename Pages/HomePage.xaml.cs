using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using L850Manager.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace L850Manager.Pages;

public sealed partial class HomePage : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private CancellationTokenSource? _refreshCancellation;

    public HomePage()
    {
        InitializeComponent();
        Loaded += HomePage_Loaded;
        Unloaded += HomePage_Unloaded;
        _timer.Tick += Timer_Tick;
    }

    private async void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        _timer.Start();
        await RefreshAsync();
    }

    private void HomePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _refreshCancellation?.Cancel();
    }

    private async void Timer_Tick(object? sender, object e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_refreshCancellation is not null)
        {
            return;
        }
        _refreshCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            ModemSnapshot snapshot = await App.Modem.RefreshAsync(_refreshCancellation.Token);
            Render(snapshot);
        }
        finally
        {
            _refreshCancellation.Dispose();
            _refreshCancellation = null;
        }
    }

    private void Render(ModemSnapshot snapshot)
    {
        ConnectionStateText.Text = snapshot.Error is not null ? "Unavailable" : snapshot.IsConnected ? "Connected" : "Registered";
        ProviderText.Text = snapshot.Error is not null ? $"·  {snapshot.Error}" : $"·  {snapshot.Provider}  ·  {snapshot.AccessTechnology}";
        ModelText.Text = snapshot.Model;
        FirmwareText.Text = snapshot.Firmware;
        RsrpText.Text = Metric(snapshot.Rsrp, "dBm");
        RsrqText.Text = Metric(snapshot.Rsrq, "dB");
        SinrText.Text = Metric(snapshot.Sinr, "dB");
        BandText.Text = snapshot.Band is int band ? $"Band {band}" : "—";
        EarfcnText.Text = snapshot.Earfcn?.ToString() ?? "—";
        PciText.Text = snapshot.Pci?.ToString() ?? "—";
        CarrierText.Text = snapshot.ActiveCarriers > 0 ? $"{snapshot.ActiveCarriers} active carrier{(snapshot.ActiveCarriers == 1 ? "" : "s")}" : "—";
        ApnText.Text = snapshot.Apn;
        CurrentRsrpText.Text = $"Current  {Metric(snapshot.Rsrp, "dBm")}";
    }

    private static string Metric(double? value, string unit) => value is double metric ? $"{metric:0.#} {unit}" : "—";
}
