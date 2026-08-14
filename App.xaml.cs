using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using L850Manager.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace L850Manager;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    public static AtCommandService AtCommands { get; } = new();
    public static WwanService Wwan { get; } = new();
    public static ModemMonitor Modem { get; } = new(AtCommands, Wwan);
    public static SmsService Sms { get; } = new(AtCommands);
    public static RadioService Radio { get; } = new(AtCommands, Wwan);
    public static EsimService Esim { get; } = new(AtCommands);
    public static AppLog Log { get; } = new();
    public static int OverviewPollingSeconds { get; set; } = 5;
    public static int SmsPollingSeconds { get; set; } = 3;
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
