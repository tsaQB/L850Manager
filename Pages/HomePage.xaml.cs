using L850Manager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace L850Manager.Pages;

public sealed partial class HomePage : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly Queue<double> _signalHistory = new();
    private CancellationTokenSource? _refreshCancellation;

    public HomePage() { InitializeComponent(); Loaded += LoadedPage; Unloaded += UnloadedPage; _timer.Tick += async (_,_) => await RefreshAsync(); }
    private async void LoadedPage(object sender, RoutedEventArgs e) { _timer.Interval = TimeSpan.FromSeconds(App.OverviewPollingSeconds); _timer.Start(); await QuickAsync(); await RefreshAsync(); }
    private void UnloadedPage(object sender, RoutedEventArgs e) { _timer.Stop(); _refreshCancellation?.Cancel(); }
    private async Task QuickAsync() { try { var s=await App.Wwan.GetSnapshotAsync(); ProviderText.Text=s.Provider;TechnologyText.Text=s.AccessTechnology;ConnectionStateText.Text=s.IsConnected?"Connected":"Registered";ApnText.Text=s.Apn; } catch { } }
    private async Task RefreshAsync() { if(_refreshCancellation is not null)return;_refreshCancellation=new(TimeSpan.FromSeconds(16));try{Render(await App.Modem.RefreshAsync(_refreshCancellation.Token));}finally{_refreshCancellation.Dispose();_refreshCancellation=null;} }
    private void Render(ModemSnapshot s)
    {
        ProviderText.Text=s.Error is null?s.Provider:"Modem unavailable";ConnectionStateText.Text=s.Error is null?(s.IsConnected?"Connected":"Registered"):s.Error;TechnologyText.Text=s.AccessTechnology;HeroBandText.Text=s.Band is int b?$"Band {b}":"—";HeroRsrpText.Text=Metric(s.Rsrp,"dBm");CurrentRsrpText.Text=Metric(s.Rsrp,"dBm");RsrpText.Text=Metric(s.Rsrp,"dBm");RsrqText.Text=Metric(s.Rsrq,"dB");SinrText.Text=Metric(s.Sinr,"dB");EarfcnText.Text=s.Earfcn?.ToString()??"—";PciText.Text=s.Pci?.ToString()??"—";ApnText.Text=s.Apn;FirmwareText.Text=$"Firmware {s.Firmware}";UpdatedText.Text=$"Live · {s.UpdatedAt:HH:mm:ss}";
        if(s.Rsrp is double value){_signalHistory.Enqueue(value);while(_signalHistory.Count>60)_signalHistory.Dequeue();RenderChart();}
        var rows=App.Modem.LastCells.Select(x=>new CellRow(x)).ToArray();CellList.ItemsSource=rows;CellCountText.Text=rows.Length==0?"Waiting for scan":$"{rows.Length} detected";
    }
    private void RenderChart(){double[] values=_signalHistory.ToArray();PointCollection points=[];for(int i=0;i<values.Length;i++){double x=42+(648d*Math.Max(i,59-values.Length+1)/59d);double y=20+(Math.Clamp(-60-values[i],0,60)/60d*194);points.Add(new Point(x,y));}SignalLine.Points=points;}
    private static string Metric(double? value,string unit)=>value is double v?$"{v:0.#} {unit}":"—";
    private sealed record CellRow(CellSnapshot V){public string Role=>V.IsServing?"Serving":"Neighbor";public string Band=>$"B{V.Band}";public int Earfcn=>V.Earfcn;public int Pci=>V.Pci;public string Rsrp=>Metric(V.Rsrp,"dBm");public string Rsrq=>Metric(V.Rsrq,"dB");public string Sinr=>Metric(V.Sinr,"dB");}
}
