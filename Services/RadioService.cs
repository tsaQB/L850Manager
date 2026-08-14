using System.Text.RegularExpressions;
using L850Manager.Models;

namespace L850Manager.Services;

public sealed partial class RadioService(AtCommandService at, WwanService wwan)
{
    private readonly SemaphoreSlim _mutationLock = new(1, 1);
    private static readonly int[] SupportedBands = [1, 2, 3, 4, 5, 7, 8, 11, 12, 13, 17, 18, 19, 20, 21, 26, 28, 29, 30, 38, 39, 40, 41, 66];
    public static IReadOnlyList<int> AllowedBands => SupportedBands;

    public async Task<RadioPreferences> GetPreferencesAsync(CancellationToken cancellationToken = default)
    {
        string response = await at.ExecuteAsync("AT+XACT?", TimeSpan.FromSeconds(6), cancellationToken);
        Match match = Xact().Match(response);
        if (!match.Success) throw new InvalidOperationException("The modem returned an unrecognized radio preference response.");
        NetworkMode mode = match.Groups[1].Value switch { "1" => NetworkMode.ThreeGOnly, "2" => NetworkMode.FourGOnly, _ => NetworkMode.ThreeGAndFourG };
        int[] bands = match.Groups[3].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x.Trim(), out int value) ? value : -1)
            .Where(x => x >= 101).Select(x => x - 100).Distinct().Order().ToArray();
        return new(mode, bands);
    }

    public async Task<IReadOnlyList<CellSnapshot>> ScanCellsAsync(CancellationToken cancellationToken = default)
    {
        string response = await at.ExecuteAsync("AT+XMCI=1", TimeSpan.FromSeconds(20), cancellationToken);
        IReadOnlyList<CellSnapshot> cells = ModemParsers.Xmci(response);
        return cells.Count > 64 ? throw new InvalidOperationException("The modem returned too many cell records.") : cells;
    }

    public async Task<RadioOperationResult> ApplyModeAndBandsAsync(NetworkMode mode, IEnumerable<int> bands, CancellationToken cancellationToken = default)
    {
        int[] selected = bands.Distinct().Order().ToArray();
        if (selected.Any(x => !SupportedBands.Contains(x)))
            return new(false, "validation", "One or more LTE bands are not supported by the L850-GL policy.");

        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            string rat = mode switch { NetworkMode.ThreeGOnly => "1", NetworkMode.FourGOnly => "2", _ => "4" };
            string preferred = mode == NetworkMode.ThreeGAndFourG ? "2" : string.Empty;
            string encodedBands = selected.Length == 0 ? "0" : string.Join(',', selected.Select(x => x + 100));
            string command = $"AT+XACT={rat},{preferred},,{encodedBands}";
            string response = await at.ExecuteAsync(command, TimeSpan.FromSeconds(12), cancellationToken);
            if (!response.Contains("OK", StringComparison.OrdinalIgnoreCase))
                return new(false, "dispatch", "The modem rejected the requested network mode or band list.");

            await Task.Delay(1500, cancellationToken);
            string readback = await at.ExecuteAsync("AT+XACT?", TimeSpan.FromSeconds(6), cancellationToken);
            Match match = Xact().Match(readback);
            if (!match.Success || match.Groups[1].Value != rat)
                return new(false, "readback", "The modem acknowledgement did not match the requested RAT mode.");

            if (mode != NetworkMode.ThreeGOnly && selected.Length > 0)
            {
                int[] returned = match.Groups[3].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => int.TryParse(x.Trim(), out int v) ? v : -1).Where(x => x >= 101).Select(x => x - 100).ToArray();
                if (selected.Except(returned).Any())
                    return new(false, "readback", "The modem did not retain every requested LTE band.");
            }
            return new(true, "verified", "Network mode and LTE bands were applied and verified.");
        }
        catch (Exception ex) { return new(false, "exception", ex.Message); }
        finally { _mutationLock.Release(); }
    }

    public async Task<CellLockState> GetCellLockStateAsync(CancellationToken cancellationToken = default)
    {
        string response = await at.ExecuteAsync("AT@NVM:DYN_CPS.NAS_ASM.FREQ_LOCK_PARAMS.*??", TimeSpan.FromSeconds(6), cancellationToken);
        Match match = Nvm().Match(response.Replace("\r", " ").Replace("\n", " "));
        if (!match.Success) throw new InvalidOperationException("Unrecognized L850-GL cell-lock NVM response.");
        int frequency = int.Parse(match.Groups[1].Value);
        int rat = int.Parse(match.Groups[2].Value);
        int pci = int.Parse(match.Groups[3].Value);
        int bandInfo = int.Parse(match.Groups[4].Value);
        int enabled = int.Parse(match.Groups[5].Value);
        bool clear = rat == 3 && bandInfo == 0 && enabled == 0 && frequency == 65535 && pci == 65535;
        bool configured = rat == 3 && bandInfo == 0 && enabled == 1 && frequency is >= 0 and <= 70545 && pci is >= 0 and <= 65535;
        if (!clear && !configured) throw new InvalidOperationException("The current NVM lock state is outside the validated grammar.");
        return new(clear, clear ? null : frequency, clear || pci == 65535 ? null : pci, response);
    }

    public Task<RadioOperationResult> ClearCellLockAsync(CancellationToken cancellationToken = default) =>
        MutateCellLockAsync(null, null, true, cancellationToken);

    public Task<RadioOperationResult> SetCellLockAsync(int earfcn, int? pci, CancellationToken cancellationToken = default) =>
        MutateCellLockAsync(earfcn, pci, false, cancellationToken);

    private async Task<RadioOperationResult> MutateCellLockAsync(int? earfcn, int? pci, bool clear, CancellationToken cancellationToken)
    {
        if (!clear && (earfcn is null || ModemParsers.BandFromEarfcn(earfcn.Value) == 0)) return new(false, "validation", "EARFCN is not mapped to a supported LTE band.");
        if (pci is < 0 or > 503) return new(false, "validation", "PCI must be between 0 and 503.");

        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            _ = await GetCellLockStateAsync(cancellationToken); // Runtime protocol admission probe.
            int band = clear ? 255 : ModemParsers.BandFromEarfcn(earfcn!.Value);
            string command = clear
                ? "AT@SIC:FREQ_LOCK(0,3,255,0,65535,65535)"
                : $"AT@SIC:FREQ_LOCK(0,3,{band},1,{earfcn},{pci ?? 65535})";
            string acknowledgement = await at.ExecuteAsync(command, TimeSpan.FromSeconds(10), cancellationToken);
            const string expected = "Frequency Lock Configuration Success CPS_MSG_TYPE_ASM_EM_CTRL_CNF";
            if (!acknowledgement.Contains(expected, StringComparison.Ordinal))
                return new(false, "write_ack", "The modem did not return the exact validated lock acknowledgement.");

            int stableReads = 0;
            for (int i = 0; i < 10 && stableReads < 2; i++)
            {
                await Task.Delay(1000, cancellationToken);
                CellLockState observed = await GetCellLockStateAsync(cancellationToken);
                bool matches = clear ? observed.IsClear : !observed.IsClear && observed.Earfcn == earfcn && observed.Pci == pci;
                stableReads = matches ? stableReads + 1 : 0;
            }
            if (stableReads < 2) return new(false, "pre_reset_nvm", "NVM did not converge twice before reset; reset was not sent.");

            try { _ = await at.ExecuteAsync("AT+CFUN=15", TimeSpan.FromSeconds(8), cancellationToken); }
            catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException) { }
            at.ResetDiscovery();

            string? replacementPort = null;
            for (int i = 0; i < 18 && replacementPort is null; i++)
            {
                await Task.Delay(5000, cancellationToken);
                replacementPort = await at.DiscoverAsync(cancellationToken);
            }
            if (replacementPort is null) return new(false, "reprobe", "The modem did not reappear after reset.");

            bool registered = false;
            for (int i = 0; i < 18 && !registered; i++)
            {
                WwanSnapshot state = await wwan.GetSnapshotAsync(cancellationToken);
                registered = state.Provider != "—";
                if (!registered) await Task.Delay(5000, cancellationToken);
            }
            if (!registered) return new(false, "registration", "The modem reappeared but did not register in time.");

            CellLockState post = await GetCellLockStateAsync(cancellationToken);
            if (clear && !post.IsClear) return new(false, "post_reset_nvm", "Clear was not retained after reset.");
            if (!clear && (post.IsClear || post.Earfcn != earfcn || post.Pci != pci)) return new(false, "post_reset_nvm", "Lock state did not match after reset.");

            if (!clear)
            {
                IReadOnlyList<CellSnapshot> cells = await ScanCellsAsync(cancellationToken);
                CellSnapshot? serving = cells.FirstOrDefault(x => x.IsServing);
                if (serving?.Earfcn != earfcn || (pci is not null && serving?.Pci != pci))
                    return new(false, "serving_cell", "NVM is configured, but the serving cell does not match yet.");
            }
            return new(true, "verified", clear ? "Cell lock was cleared and verified." : "Cell lock was applied and verified.");
        }
        catch (Exception ex) { return new(false, "exception", ex.Message); }
        finally { _mutationLock.Release(); }
    }

    [GeneratedRegex(@"\+XACT:\s*(\d+)\s*,\s*(\d*)\s*,?\s*(.*?)(?:\r?\n|\||OK)", RegexOptions.IgnoreCase)]
    private static partial Regex Xact();

    [GeneratedRegex(@"frequency=(\d+).*?rat=(\d+).*?psc_or_pci=(\d+).*?band_info=(\d+).*?inter_freq_lock_support=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex Nvm();
}
