using L850Manager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace L850Manager.Pages;

public sealed partial class CellsPage : Page
{
    public CellsPage() => InitializeComponent();
    private async void Scan_Click(object sender, RoutedEventArgs e) { try { ScanStateText.Text="Scanning…"; var cells=await App.Radio.ScanCellsAsync(); CellList.ItemsSource=cells.Select(x=>new CellRow(x)).ToArray(); ScanStateText.Text=$"{cells.Count} cells · {DateTime.Now:HH:mm:ss}"; } catch(Exception ex){ScanStateText.Text="Scan failed";await Show("Cell scan failed",ex.Message);} }
    private void CellList_SelectionChanged(object sender, SelectionChangedEventArgs e) { if(CellList.SelectedItem is CellRow row){EarfcnBox.Value=row.Value.Earfcn;PciBox.Value=row.Value.Pci;} }
    private async void Apply_Click(object sender, RoutedEventArgs e) { if(double.IsNaN(EarfcnBox.Value)){await Show("Invalid EARFCN","Select a cell or enter an EARFCN.");return;} int earfcn=(int)EarfcnBox.Value;int? pci=double.IsNaN(PciBox.Value)?null:(int)PciBox.Value;if(!await Confirm("Apply cell lock?",$"The modem will restart.\n\nEARFCN {earfcn} · PCI {pci?.ToString()??"Any"}"))return;var r=await App.Radio.SetCellLockAsync(earfcn,pci);await Show(r.Success?"Cell lock verified":$"Failed at {r.Stage}",r.Message); }
    private async void Clear_Click(object sender, RoutedEventArgs e) { if(!await Confirm("Clear cell lock?","The modem will restart and return to automatic cell selection."))return;var r=await App.Radio.ClearCellLockAsync();await Show(r.Success?"Cell lock cleared":$"Failed at {r.Stage}",r.Message); }
    private async Task<bool> Confirm(string title,string content)=>await new ContentDialog{XamlRoot=XamlRoot,Title=title,Content=content,PrimaryButtonText="Continue",CloseButtonText="Cancel",DefaultButton=ContentDialogButton.Close}.ShowAsync()==ContentDialogResult.Primary;
    private async Task Show(string title,string content)=>await new ContentDialog{XamlRoot=XamlRoot,Title=title,Content=content,CloseButtonText="Close"}.ShowAsync();
    private sealed record CellRow(CellSnapshot Value){public string Role=>Value.IsServing?"Serving":"Neighbor";public string Band=>$"B{Value.Band}";public int Earfcn=>Value.Earfcn;public int Pci=>Value.Pci;public string Rsrp=>Value.Rsrp is double v?$"{v:0.#} dBm":"—";public string Rsrq=>Value.Rsrq is double v?$"{v:0.#} dB":"—";}
}
