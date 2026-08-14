using System.Text.Json;
using L850Manager.Models;

namespace L850Manager.Services;

public sealed partial class SmsService
{
    private readonly AtCommandService _at;
    private readonly string _historyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "L850 Manager", "sent-sms.json");
    private readonly List<SmsMessage> _sent = [];
    private readonly SemaphoreSlim _historyLock = new(1, 1);
    private int _nextLocalIndex = -1;
    private bool _receiveConfigured;

    public SmsService(AtCommandService at)
    {
        _at = at;
        LoadHistory();
    }

    public async Task<IReadOnlyList<SmsMessage>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!_receiveConfigured)
        {
            string storage = await _at.ExecuteAsync("AT+CPMS=\"SM\",\"SM\",\"SM\"", TimeSpan.FromSeconds(5), cancellationToken);
            string notification = await _at.ExecuteAsync("AT+CNMI=1,1,0,0,0", TimeSpan.FromSeconds(5), cancellationToken);
            if (!storage.Contains("OK", StringComparison.OrdinalIgnoreCase) || !notification.Contains("OK", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The modem did not accept the SMS receive configuration.");
            _receiveConfigured = true;
        }
        string response = await _at.ExecuteTextModeAsync("AT+CMGL=\"ALL\"", TimeSpan.FromSeconds(8), cancellationToken);
        IReadOnlyList<SmsMessage> stored = SmsParser.ParseTextModeList(response);
        SmsMessage[] result = stored.Concat(_sent).OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Index).ToArray();
        App.Log.Info("sms", $"Loaded {stored.Count} SIM message(s) and {_sent.Count} local sent message(s).");
        return result;
    }

    public async Task<SmsMessage> SendAsync(string recipient, string body, CancellationToken cancellationToken = default)
    {
        await _at.SendTextMessageAsync(recipient, body, cancellationToken);
        SmsMessage sent = new(Interlocked.Decrement(ref _nextLocalIndex), "STO SENT", recipient, DateTimeOffset.Now, body);
        await _historyLock.WaitAsync(cancellationToken);
        try
        {
            _sent.Add(sent);
            await SaveHistoryAsync(cancellationToken);
        }
        finally { _historyLock.Release(); }
        App.Log.Info("sms", "SMS accepted for network delivery.");
        return sent;
    }

    public async Task DeleteAsync(int index, CancellationToken cancellationToken = default)
    {
        if (index < 0)
        {
            await _historyLock.WaitAsync(cancellationToken);
            try { _sent.RemoveAll(x => x.Index == index); await SaveHistoryAsync(cancellationToken); }
            finally { _historyLock.Release(); }
            return;
        }
        if (index > 10000) throw new ArgumentOutOfRangeException(nameof(index));
        string response = await _at.ExecuteAsync($"AT+CMGD={index}", TimeSpan.FromSeconds(6), cancellationToken);
        if (!response.Contains("OK", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The modem did not confirm SMS deletion.");
        }
        App.Log.Info("sms", $"Deleted SIM message index {index}.");
    }

    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(_historyPath)) return;
            var items = JsonSerializer.Deserialize(File.ReadAllText(_historyPath), SmsJsonContext.Default.ListSmsMessage);
            if (items is null) return;
            _sent.AddRange(items.Where(x => x.Index < 0));
            if (_sent.Count > 0) _nextLocalIndex = _sent.Min(x => x.Index) - 1;
        }
        catch { /* A corrupt optional history file must never block modem startup. */ }
    }

    private async Task SaveHistoryAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_historyPath)!);
        List<SmsMessage> ordered = _sent.OrderByDescending(x => x.Timestamp).ToList();
        string json = JsonSerializer.Serialize(ordered, SmsJsonContext.Default.ListSmsMessage);
        await File.WriteAllTextAsync(_historyPath, json, cancellationToken);
    }

}
