using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;

namespace L850Manager.Services;

public sealed partial class AtCommandService : IDisposable
{
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private string? _portName;

    public string? PortName => _portName;

    public void ResetDiscovery() => _portName = null;

    public async Task<string?> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        if (_portName is not null)
        {
            return _portName;
        }

        foreach (string candidate in SerialPort.GetPortNames().OrderBy(PortNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string response = await ExecuteOnPortAsync(candidate, "ATI", TimeSpan.FromSeconds(2), cancellationToken);
                if (response.Contains("L850", StringComparison.OrdinalIgnoreCase) ||
                    response.Contains("Fibocom", StringComparison.OrdinalIgnoreCase) ||
                    response.Contains(".Built@", StringComparison.OrdinalIgnoreCase))
                {
                    _portName = candidate;
                    return candidate;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException)
            {
                // A non-AT or occupied serial interface is expected during discovery.
            }
        }

        return null;
    }

    public async Task<string> ExecuteAsync(string command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        string port = await DiscoverAsync(cancellationToken)
            ?? throw new InvalidOperationException("No responsive Fibocom L850-GL AT port was found.");

        await _commandLock.WaitAsync(cancellationToken);
        try
        {
            return await ExecuteOnPortAsync(port, command, timeout ?? TimeSpan.FromSeconds(4), cancellationToken);
        }
        finally
        {
            _commandLock.Release();
        }
    }

    public async Task<string> ExecuteTextModeAsync(string command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        string port = await DiscoverAsync(cancellationToken)
            ?? throw new InvalidOperationException("No responsive Fibocom L850-GL AT port was found.");
        await _commandLock.WaitAsync(cancellationToken);
        try
        {
            string mode = await ExecuteOnPortAsync(port, "AT+CMGF=1", TimeSpan.FromSeconds(3), cancellationToken);
            if (!mode.Contains("OK", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The modem rejected SMS text mode.");
            }
            return await ExecuteOnPortAsync(port, command, timeout ?? TimeSpan.FromSeconds(8), cancellationToken);
        }
        finally
        {
            try { await ExecuteOnPortAsync(port, "AT+CMGF=0", TimeSpan.FromSeconds(3), CancellationToken.None); }
            catch { /* Best-effort restoration; the next SMS session sets its required mode explicitly. */ }
            _commandLock.Release();
        }
    }

    public async Task SendTextMessageAsync(string recipient, string body, CancellationToken cancellationToken = default)
    {
        if (!Regex.IsMatch(recipient, @"^\+?[0-9]{5,20}$"))
        {
            throw new ArgumentException("Recipient must be a valid international or local number.", nameof(recipient));
        }
        if (string.IsNullOrWhiteSpace(body) || body.Length > 160 || body.Any(c => c > 127))
        {
            throw new ArgumentException("This build supports 1-160 ASCII characters per SMS.", nameof(body));
        }

        string portName = await DiscoverAsync(cancellationToken)
            ?? throw new InvalidOperationException("No responsive Fibocom L850-GL AT port was found.");
        await _commandLock.WaitAsync(cancellationToken);
        try
        {
            await Task.Run(() =>
            {
                using SerialPort port = new(portName, 115200, Parity.None, 8, StopBits.One)
                {
                    DtrEnable = true,
                    RtsEnable = true,
                    ReadTimeout = 500,
                    WriteTimeout = 1000
                };
                port.Open();
                Thread.Sleep(120);
                WriteAndWait(port, "AT+CMGF=1\r", "OK", TimeSpan.FromSeconds(3), cancellationToken);
                WriteAndWait(port, $"AT+CMGS=\"{recipient}\"\r", ">", TimeSpan.FromSeconds(5), cancellationToken);
                string result = WriteAndWait(port, body + (char)26, "+CMGS:", TimeSpan.FromSeconds(45), cancellationToken);
                if (!result.Contains("OK", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("The modem did not confirm SMS delivery to the network.");
                }
                try { WriteAndWait(port, "AT+CMGF=0\r", "OK", TimeSpan.FromSeconds(3), CancellationToken.None); } catch { }
            }, cancellationToken);
        }
        finally
        {
            _commandLock.Release();
        }
    }

    private static string WriteAndWait(SerialPort port, string payload, string marker, TimeSpan timeout, CancellationToken cancellationToken)
    {
        port.DiscardInBuffer();
        port.Write(payload);
        Stopwatch clock = Stopwatch.StartNew();
        StringBuilder response = new();
        while (clock.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (port.BytesToRead > 0)
            {
                response.Append(port.ReadExisting());
                string current = response.ToString();
                if (current.Contains(marker, StringComparison.OrdinalIgnoreCase) || current.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    if (marker == "+CMGS:" && !current.Contains("OK", StringComparison.OrdinalIgnoreCase))
                    {
                        Thread.Sleep(400);
                        if (port.BytesToRead > 0) response.Append(port.ReadExisting());
                    }
                    break;
                }
            }
            Thread.Sleep(50);
        }
        string result = response.ToString();
        if (!result.Contains(marker, StringComparison.OrdinalIgnoreCase))
        {
            throw new TimeoutException($"Timed out waiting for modem response: {marker}");
        }
        return result;
    }

    private static Task<string> ExecuteOnPortAsync(string portName, string command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            using SerialPort port = new(portName, 115200, Parity.None, 8, StopBits.One)
            {
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = 250,
                WriteTimeout = 1000,
                Handshake = Handshake.None,
                NewLine = "\r"
            };

            port.Open();
            Thread.Sleep(120);
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
            port.Write(command + "\r");

            Stopwatch clock = Stopwatch.StartNew();
            Stopwatch quiet = Stopwatch.StartNew();
            StringBuilder response = new();
            while (clock.Elapsed < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (port.BytesToRead > 0)
                {
                    response.Append(port.ReadExisting());
                    quiet.Restart();
                }
                else if (response.Length > 0 && quiet.ElapsedMilliseconds >= 180)
                {
                    break;
                }
                Thread.Sleep(40);
            }

            string normalized = ControlCharacters().Replace(response.ToString().Replace('\r', '\n'), string.Empty).Trim();
            if (normalized.Length == 0)
            {
                throw new TimeoutException($"No response to {command} on {portName}.");
            }
            return normalized;
        }, cancellationToken);
    }

    private static int PortNumber(string name) =>
        int.TryParse(NonDigits().Replace(name, string.Empty), out int value) ? value : int.MaxValue;

    public void Dispose() => _commandLock.Dispose();

    [GeneratedRegex("[\\x00-\\x08\\x0B\\x0C\\x0E-\\x1F]")]
    private static partial Regex ControlCharacters();

    [GeneratedRegex("\\D")]
    private static partial Regex NonDigits();
}
