using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using L850Manager.Pages;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace L850Manager;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1440, 900));
        NavFrame.Navigate(typeof(HomePage));
        Activated += MainWindow_Activated;
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;
        await Task.Delay(TimeSpan.FromSeconds(5));
        var availability = await App.Esim.DetectAsync();
        EsimNavItem.Visibility = availability.IsDetected ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        NavFrame.GoBack();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateOnce(typeof(SettingsPage));
        }
        else if (args.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag)
            {
                case "overview":
                    NavigateOnce(typeof(HomePage));
                    break;
                case "lock":
                    NavigateOnce(typeof(LockPage));
                    break;
                case "sms":
                    NavigateOnce(typeof(SmsPage));
                    break;
                case "esim":
                    NavigateOnce(typeof(EsimPage));
                    break;
                case "diagnostics":
                    NavigateOnce(typeof(DiagnosticsPage));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown navigation item tag: {item.Tag}");
            }
        }
    }

    private void WindowRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool compact = e.NewSize.Width < 920;
        NavView.PaneDisplayMode = compact ? NavigationViewPaneDisplayMode.LeftCompact : NavigationViewPaneDisplayMode.Left;
        NavView.IsPaneOpen = !compact;
        AppTitleBar.IsPaneToggleButtonVisible = compact;
    }

    private void NavigateOnce(Type pageType)
    {
        if (NavFrame.CurrentSourcePageType == pageType) return;
        NavFrame.Navigate(pageType);
        NavFrame.BackStack.Clear();
    }
}
