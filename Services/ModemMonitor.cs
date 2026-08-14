using L850Manager.Models;

namespace L850Manager.Services;

public sealed class ModemMonitor(AtCommandService at, WwanService wwan)
{
    private CellSnapshot? _lastCell;
    private DateTimeOffset _lastCellScan = DateTimeOffset.MinValue;

    public async Task<ModemSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string? port = await at.DiscoverAsync(cancellationToken);
            if (port is null)
            {
                return new ModemSnapshot { Error = "L850-GL AT port was not found." };
            }

            Task<WwanSnapshot> wwanTask = wwan.GetSnapshotAsync(cancellationToken);
            string manufacturer = ModemParsers.QuotedValue(await at.ExecuteAsync("AT+CGMI", cancellationToken: cancellationToken), "+CGMI");
            string model = ModemParsers.QuotedValue(await at.ExecuteAsync("AT+CGMM", cancellationToken: cancellationToken), "+CGMM").Split(',')[0];
            string firmware = ModemParsers.QuotedValue(await at.ExecuteAsync("AT+CGMR", cancellationToken: cancellationToken), "+CGMR");
            int? voltage = ModemParsers.Voltage(await at.ExecuteAsync("AT+CBC", cancellationToken: cancellationToken));
            int carriers = ModemParsers.ActiveCarrierCount(await at.ExecuteAsync("AT+GTCAINFO?", TimeSpan.FromSeconds(5), cancellationToken));

            if (_lastCell is null || DateTimeOffset.Now - _lastCellScan > TimeSpan.FromSeconds(20))
            {
                string scan = await at.ExecuteAsync("AT+XMCI=1", TimeSpan.FromSeconds(15), cancellationToken);
                _lastCell = ModemParsers.Xmci(scan).FirstOrDefault(x => x.IsServing);
                _lastCellScan = DateTimeOffset.Now;
            }

            WwanSnapshot wwanState = await wwanTask;
            ModemSnapshot snapshot = new()
            {
                IsAvailable = true,
                IsConnected = wwanState.IsConnected,
                PortName = port,
                Manufacturer = manufacturer,
                Model = model,
                Firmware = firmware,
                Provider = wwanState.Provider,
                AccessTechnology = wwanState.AccessTechnology,
                Apn = wwanState.Apn,
                VoltageMv = voltage,
                ActiveCarriers = carriers,
                Band = _lastCell?.Band,
                Earfcn = _lastCell?.Earfcn,
                Pci = _lastCell?.Pci,
                Rsrp = _lastCell?.Rsrp,
                Rsrq = _lastCell?.Rsrq,
                Sinr = _lastCell?.Sinr,
                UpdatedAt = DateTimeOffset.Now
            };
            App.Log.Info("modem", $"{snapshot.Model} on {snapshot.PortName}; {snapshot.Provider} {snapshot.AccessTechnology}; B{snapshot.Band?.ToString() ?? "—"} EARFCN {snapshot.Earfcn?.ToString() ?? "—"} PCI {snapshot.Pci?.ToString() ?? "—"}");
            return snapshot;
        }
        catch (Exception ex)
        {
            App.Log.Info("error", ex.Message);
            return new ModemSnapshot { Error = ex.Message, UpdatedAt = DateTimeOffset.Now };
        }
    }
}
