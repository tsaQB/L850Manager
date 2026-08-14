using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace L850Manager.Pages;

public sealed partial class EsimPage : Page
{
    public EsimPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync(false);
    }

    private async Task RefreshAsync(bool force)
    {
        BusyRing.IsActive = true;
        var result = await App.Esim.DetectAsync(force);
        StatusText.Text = result.Status;
        DetailText.Text = result.Detail;
        BusyRing.IsActive = false;
    }

    private async void OpenManager_Click(object sender, RoutedEventArgs e) =>
        await Services.EsimService.OpenWindowsManagerAsync();

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync(true);
}
