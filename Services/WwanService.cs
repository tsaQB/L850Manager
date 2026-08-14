using System.Diagnostics;
using System.Text.RegularExpressions;
using L850Manager.Models;

namespace L850Manager.Services;

public sealed partial class WwanService
{
    public async Task<WwanSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        string output = await RunNetshAsync("mbn show connection interface=\"Cellular\"", cancellationToken);
        bool connected = Value(output, "Interface State").Equals("Connected", StringComparison.OrdinalIgnoreCase);
        return new WwanSnapshot(
            connected,
            Value(output, "Provider Name", "—"),
            Value(output, "Provider Data Class", "—"),
            Value(output, "Access Point Name", "—"));
    }

    private static async Task<string> RunNetshAsync(string arguments, CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("netsh", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return output;
    }

    private static string Value(string input, string label, string fallback = "")
    {
        Match match = Regex.Match(input, $"^\\s*{Regex.Escape(label)}\\s*:\\s*(.+?)\\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : fallback;
    }
}
