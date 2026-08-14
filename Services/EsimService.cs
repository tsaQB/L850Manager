using L850Manager.Models;
using Windows.Networking.NetworkOperators;
using Windows.System;
using Windows.Foundation.Metadata;
using System.Text.RegularExpressions;

namespace L850Manager.Services;

public sealed class EsimService
{
    private const string IsdRAid = "A0000005591010FFFFFFFF8900000100";
    private readonly AtCommandService _at;
    private EsimAvailability? _cached;

    public EsimService(AtCommandService at) => _at = at;

    public async Task<EsimAvailability> DetectAsync(bool refresh = false)
    {
        if (!refresh && _cached is not null)
            return _cached;

        if (!await HasRemovableEuiccAsync())
            return _cached = new(false, false, "Not detected", "The inserted removable card is not an eUICC.");

        if (!ApiInformation.IsTypePresent("Windows.Networking.NetworkOperators.ESimManager"))
            return _cached = new(false, false, "Not detected", "Windows does not report eSIM support on this device.");

        ESimWatcher? watcher = null;
        try
        {
            watcher = ESimManager.TryCreateESimWatcher();
            if (watcher is null)
                return _cached = new(true, false, "Removable eSIM detected", "The eUICC is present; profile management is available through Windows Settings.");

            var found = new List<ESim>();
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.Added += (_, args) => found.Add(args.ESim);
            watcher.EnumerationCompleted += (_, _) => completed.TrySetResult();
            watcher.Stopped += (_, _) => completed.TrySetResult();
            watcher.Start();

            await Task.WhenAny(completed.Task, Task.Delay(TimeSpan.FromSeconds(4)));
            watcher.Stop();

            if (found.Count == 0)
                return _cached = new(true, true, "Removable eSIM detected", "The eUICC is present and ready for profile management.");

            var profileCount = 0;
            foreach (var esim in found)
                profileCount += esim.GetProfiles().Count;

            return _cached = new(true, true, "Removable eSIM detected", $"Windows reports {profileCount} installed profile(s).", profileCount);
        }
        catch (UnauthorizedAccessException)
        {
            // Profile management is a Microsoft/operator-restricted capability. Do not fake it.
            return _cached = new(true, false, "eSIM managed by Windows", "The eSIM subsystem is present, but profile access is restricted to approved operator apps.");
        }
        catch (Exception ex) when (ex.HResult == unchecked((int)0x80070005))
        {
            return _cached = new(true, false, "eSIM managed by Windows", "The eSIM subsystem is present, but profile access is restricted to approved operator apps.");
        }
        finally
        {
            try { watcher?.Stop(); } catch { }
        }
    }

    private async Task<bool> HasRemovableEuiccAsync()
    {
        int? channel = null;
        try
        {
            var response = await _at.ExecuteAsync($"AT+CCHO=\"{IsdRAid}\"", TimeSpan.FromSeconds(4));
            var match = Regex.Match(response, @"\+CCHO:\s*(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var parsed))
                return false;

            channel = parsed;
            return response.Contains("OK", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (channel is not null)
            {
                try { await _at.ExecuteAsync($"AT+CCHC={channel}", TimeSpan.FromSeconds(3)); }
                catch { /* Never leave detection blocked by best-effort channel cleanup. */ }
            }
        }
    }

    public static Task<bool> OpenWindowsManagerAsync() =>
        Launcher.LaunchUriAsync(new Uri("ms-settings:network-cellular")).AsTask();
}
