using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
namespace L850Manager.Pages;
public sealed partial class DiagnosticsPage : Page
{
    public DiagnosticsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        var wwan = await App.Wwan.GetSnapshotAsync();
        MbimStateText.Text = wwan.IsConnected ? "Connected" : "Registered";
        MbimDetailText.Text = $"{wwan.Provider} · {wwan.AccessTechnology}";
        string? port = await App.AtCommands.DiscoverAsync();
        AtStateText.Text = port is null ? "Unavailable" : "Available";
        AtDetailText.Text = port ?? "No responsive port";
        try
        {
            var state = await App.Radio.GetCellLockStateAsync();
            LockStateText.Text = state.IsClear ? "Automatic" : "Locked";
            LockDetailText.Text = state.IsClear ? "NVM state clear" : $"EARFCN {state.Earfcn} · PCI {state.Pci?.ToString() ?? "Any"}";
        }
        catch (Exception ex)
        {
            LockStateText.Text = "Unavailable";
            LockDetailText.Text = ex.Message;
        }
        LogTextBox.Text = string.Join(Environment.NewLine, App.Log.Entries.Select(x => x.ToString()));
    }
}
