// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace L850Manager.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        OverviewIntervalBox.Value = App.OverviewPollingSeconds;
        SmsIntervalBox.Value = App.SmsPollingSeconds;
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
