// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace L850Manager.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        VersionText.Text = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "—";
        OverviewIntervalBox.Value = App.OverviewPollingSeconds;
        SmsIntervalBox.Value = App.SmsPollingSeconds;
        SizeChanged += (_, args) => UpdateResponsiveLayout(args.NewSize.Width);
        Loaded += (_, _) => UpdateResponsiveLayout(ActualWidth);
    }

    private void UpdateResponsiveLayout(double width)
    {
        bool wide = width >= 900;
        SettingsPrimaryColumn.Width = new GridLength(wide ? 3 : 1, GridUnitType.Star);
        SettingsSecondaryColumn.Width = wide ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
        SettingsStackRow.Height = wide ? new GridLength(0) : GridLength.Auto;
        Grid.SetRow(AboutPanel, wide ? 0 : 1);
        Grid.SetColumn(AboutPanel, wide ? 1 : 0);
    }

    private void OverviewInterval_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!double.IsNaN(args.NewValue)) App.OverviewPollingSeconds = Math.Clamp((int)args.NewValue, 2, 60);
    }

    private void SmsInterval_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!double.IsNaN(args.NewValue)) App.SmsPollingSeconds = Math.Clamp((int)args.NewValue, 2, 60);
    }
}
